using EwanCore.AlarmSystem;
using EwanCore.Messaging;
using EwanCore.StateMachine;
using EwanModel;
using EwanModel.Common;
using System;
using System.Threading.Tasks;

// 说明：
// - 这是“使用示例文件”，不参与 EwanCommon.csproj 编译。
// - 你可以把这个文件拷贝到任意 WinForms/WPF/Console 项目里（引用 EwanCommon.dll）直接运行。

namespace EwanCommon.Examples
{
    /// <summary>
    /// 报警示例：
    /// - MES 失败/超时：产生“不需要复位”的报警（needReset:false），流程自行停机
    /// - 流程异常：由 LogicRunner 捕获并抛出 LogicException 事件，你可以把它转成“需要复位”的报警并停机
    /// - StepChangedEventArgs：步骤切换事件参数，用于 UI/监控显示（通过 MessageBus 订阅）
    /// </summary>
    public static class AlarmAndStepChangedExample
    {
        public sealed class MesRequest : IMessage, ICorrelatedMessage<Guid>
        {
            public Guid CorrelationId { get; set; }
            public DateTimeOffset Timestamp { get; set; }
            public string Api { get; set; }
            public object Payload { get; set; }
        }

        public sealed class MesResult : IMessage, ICorrelatedMessage<Guid>
        {
            public Guid CorrelationId { get; set; }
            public DateTimeOffset Timestamp { get; set; }
            public bool Success { get; set; }
            public string Error { get; set; }
            public object Data { get; set; }
        }

        /// <summary>
        /// 可选：PLC 监控报警示例（PlcAlarmTracker 会扫描 IsAlarmProperty=true 的 bool 属性）。
        /// </summary>
        public sealed class PlcSnapshot
        {
            [Plc(Prefix = "M", Addr = 100, IsAlarmProperty = true, AlarmDesc = "急停按下", EAlarmLevel = EAlarmLevel.H, NeedReset = true)]
            public bool EmergencyStop { get; set; }
        }

        public static async Task RunAsync()
        {
            using var bus = new MessageBus();
            var prevHub = MessageHub.Current;
            MessageHub.Current = bus;

            try
            {
                // 1) 报警服务（建议在项目里注册为 DI 单例）
                var alarms = new AlarmService();
                alarms.AlarmChanged += (_, e) =>
                {
                    var key = e.Alarm?.Key ?? "(null)";
                    var content = e.Alarm?.Content ?? "(cleared)";
                    Console.WriteLine($"[AlarmChanged] kind={e.Kind}, key={key}, content={content}, needReset={e.Alarm?.NeedReset}");
                };

                // 2) StepChanged：用于 UI/监控显示当前步骤（LogicBase 内部会通过 MessageHub.Current 广播）
                using var stepSub = bus.Subscribe<StepChangedEventArgs>(args =>
                    Console.WriteLine($"[StepChanged] {args.LogicName}: {args.FromStep} -> {args.ToStep} @ {args.Timestamp:HH:mm:ss.fff}"));

                // 3) 模拟 MES 后台：收到 MesRequest 后回一个失败结果（演示 needReset:false 的报警）
                using var backendSub = bus.Respond<MesRequest, MesResult>(req => new MesResult
                {
                    Success = false,
                    Error = "MES 返回 NG（示例）"
                });

                // 6) 逻辑 runner：捕获流程异常并转成报警
                using var runner = new LogicRunner();
                runner.LogicException += (_, exArgs) =>
                {
                    alarms.AddAlarm(
                        content: $"流程异常：{exArgs.LogicName}/{exArgs.Step} - {exArgs.Exception?.Message}",
                        level: AlarmLevel.H,
                        unit: "Logic",
                        needReset: true,
                        key: "Logic.Exception");

                    // 停机（不清队列，让上层决定是否清/是否走 Home）
                    runner.Stop();
                };

                var op = new MachineOperator(alarms, runner);

                // 7) 可选：PLC 报警同步（监控型语义：外部报警位还在，下次轮询会重新加回来）
                var plcTracker = new PlcAlarmTracker<PlcSnapshot>(alarms);
                plcTracker.Process(new PlcSnapshot { EmergencyStop = false });

                // 8) 启动主流程
                op.Start(() => new MainLogic(alarms, bus));

                // 等待一会儿让示例跑完（实际项目由 UI/主线程决定生命周期）
                await Task.Delay(1500).ConfigureAwait(false);
            }
            finally
            {
                MessageHub.Current = prevHub;
            }
        }

        private sealed class MainLogic : LogicBase
        {
            private readonly IAlarmService _alarms;
            private readonly IMessageBus _bus;

            private Task<MesResult> _pending;

            public MainLogic(IAlarmService alarms, IMessageBus bus)
            {
                _alarms = alarms ?? throw new ArgumentNullException(nameof(alarms));
                _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            }

            public override void Handler()
            {
                switch (SwitchIndex)
                {
                    case "初始状态":
                        SwitchIndex = "发送MES请求";
                        break;

                    case "发送MES请求":
                        try
                        {
                            _pending = _bus.RequestAsync<MesRequest, MesResult>(
                                new MesRequest { Api = "Confirm", Payload = new { Sn = "SN001" } },
                                timeoutMs: 800);
                            SwitchIndex = "等待MES返回";
                        }
                        catch (Exception ex)
                        {
                            _alarms.AddAlarm("发送 MES 请求失败：" + ex.Message, AlarmLevel.H, unit: "MES", needReset: false, key: "Mes.EnqueueFail");
                            Complete();
                        }
                        break;

                    case "等待MES返回":
                        if (_pending == null || !_pending.IsCompleted)
                        {
                            break;
                        }

                        if (_pending.IsCanceled || _pending.IsFaulted)
                        {
                            // MES 通讯类错误：不需要复位（清警即可重启/重试）
                            _alarms.AddAlarm("等待 MES 超时/异常", AlarmLevel.H, unit: "MES", needReset: false, key: "Mes.Timeout");
                            Complete();
                            break;
                        }

                        var res = _pending.Result;
                        _pending = null;
                        if (!res.Success)
                        {
                            _alarms.AddAlarm("MES 返回失败：" + (res.Error ?? string.Empty), AlarmLevel.M, unit: "MES", needReset: false, key: "Mes.NG");
                            Complete();
                            break;
                        }

                        SwitchIndex = "故意触发流程异常";
                        break;

                    case "故意触发流程异常":
                        // 流程内部错误：抛异常后会被 LogicRunner 捕获，并触发 runner.LogicException
                        throw new InvalidOperationException("模拟流程错误（示例）");

                    case "结束状态":
                        break;
                }
            }
        }
    }
}
