using EwanCore.AlarmSystem;
using System;
using System.Collections.Generic;
using System.Threading;

namespace EwanCore.StateMachine
{
    /// <summary>
    /// 逻辑线程队列（参考 Byron.Commond.Logic.LogicThread）：维护一组 <see cref="LogicBase"/> 并按 Run/Step/Stop 驱动。
    /// </summary>
    public sealed class LogicRunner : IDisposable
    {
        private readonly object _listSync = new object();
        private readonly List<LogicBase> _logicGather = new List<LogicBase>();
        private readonly List<LogicBase> _finishLogicGather = new List<LogicBase>();

        private Thread _runThread;
        private volatile bool _alive;

        /// <summary>
        /// 当前状态字符串（用于监控显示）。
        /// </summary>
        public string CurLogicStateStr { get; private set; } = string.Empty;

        /// <summary>
        /// 运行标记。
        /// </summary>
        public RunTimeTag RunTag { get; private set; } = RunTimeTag.Stop;

        /// <summary>
        /// 逻辑执行异常（发生异常时会自动切到 Stop，避免线程直接崩溃）。
        /// </summary>
        public event EventHandler<LogicExceptionEventArgs> LogicException;

        /// <summary>
        /// 队列中的逻辑数量。
        /// </summary>
        public int Count
        {
            get
            {
                lock (_listSync)
                {
                    return _logicGather.Count;
                }
            }
        }

        /// <summary>
        /// 追加一个逻辑到队列。
        /// </summary>
        public void AddAction(LogicBase logic)
        {
            if (logic == null) throw new ArgumentNullException(nameof(logic));

            lock (_listSync)
            {
                logic.Rset();
                _logicGather.Add(logic);
            }
        }

        /// <summary>
        /// 清空队列（仅建议在 Stop/Pause 状态下调用）。
        /// </summary>
        public void ClearAction()
        {
            lock (_listSync)
            {
                _logicGather.Clear();
                _finishLogicGather.Clear();
                CurLogicStateStr = string.Empty;
            }
        }

        /// <summary>
        /// 队列中是否存在指定类型的逻辑。
        /// </summary>
        public bool ExistAction(Type baseLogicType)
        {
            if (baseLogicType == null) throw new ArgumentNullException(nameof(baseLogicType));

            lock (_listSync)
            {
                for (var i = 0; i < _logicGather.Count; i++)
                {
                    if (_logicGather[i]?.GetType() == baseLogicType)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// 启动：进入 Run 模式。
        /// </summary>
        public void Start()
        {
            EnsureThread();
            RunTag = RunTimeTag.Run;
        }

        /// <summary>
        /// 停止：进入 Stop 模式（不会清空队列）。
        /// </summary>
        public void Stop()
        {
            RunTag = RunTimeTag.Stop;
        }

        /// <summary>
        /// 暂停：进入 Pause 模式（不会清空队列）。
        /// </summary>
        public void Pause()
        {
            RunTag = RunTimeTag.Pause;
        }

        /// <summary>
        /// 单步：执行一轮后自动切到 Stop。
        /// </summary>
        public void Step()
        {
            EnsureThread();
            RunTag = RunTimeTag.Step;
        }

        private void EnsureThread()
        {
            if (_runThread != null && _runThread.IsAlive)
            {
                return;
            }

            _alive = true;
            _runThread = new Thread(MainHandler)
            {
                IsBackground = true,
                Name = $"{nameof(LogicRunner)}-{GetHashCode():X8}"
            };
            _runThread.Start();
        }

        private void MainHandler()
        {
            while (_alive)
            {
                UpdateCurLogicState();

                var tag = RunTag;
                if (tag == RunTimeTag.Stop || tag == RunTimeTag.Pause)
                {
                    Thread.Sleep(100);
                    continue;
                }

                Thread.Sleep(50);

                if (tag != RunTimeTag.Run && tag != RunTimeTag.Step)
                {
                    continue;
                }

                LogicExceptionEventArgs errorArgs = null;
                lock (_listSync)
                {
                    _finishLogicGather.Clear();

                    for (var i = 0; i < _logicGather.Count; i++)
                    {
                        var logic = _logicGather[i];
                        if (logic == null)
                        {
                            continue;
                        }

                        try
                        {
                            logic.Handler();
                        }
                        catch (Exception ex)
                        {
                            // 不让异常打爆线程；停机等待上层处理（也可以由上层订阅 LogicException 后转为报警）。
                            RunTag = RunTimeTag.Stop;

                            var logicName = logic?.GetType().Name ?? "Unknown";
                            var logicStep = logic?.SwitchIndex ?? string.Empty;
                            LogicBase.TryPostAlarmMessage(
                                key: $"Logic.Exception.{logicName}",
                                content: $"流程异常: [{logicName}] {ex.Message}",
                                level: AlarmLevel.H,
                                needReset: true);

                            errorArgs = new LogicExceptionEventArgs(logicName, logicStep, ex);
                        }
                        if (logic.IsFinish)
                        {
                            _finishLogicGather.Add(logic);
                        }
                    }

                    if (_finishLogicGather.Count > 0)
                    {
                        for (var i = 0; i < _finishLogicGather.Count; i++)
                        {
                            _logicGather.Remove(_finishLogicGather[i]);
                        }
                        _finishLogicGather.Clear();
                    }

                    if (_logicGather.Count == 0)
                    {
                        RunTag = RunTimeTag.Stop;
                    }
                }

                if (errorArgs != null)
                {
                    LogicException?.Invoke(this, errorArgs);
                }

                // Step 模式跑完一轮后自动停止
                if (RunTag == RunTimeTag.Step)
                {
                    RunTag = RunTimeTag.Stop;
                }
            }
        }

        private void UpdateCurLogicState()
        {
            lock (_listSync)
            {
                CurLogicStateStr = string.Empty;
                for (var i = 0; i < _logicGather.Count; i++)
                {
                    CurLogicStateStr += _logicGather[i]?.GetLogicState() ?? string.Empty;
                }
            }
        }

        public void Dispose()
        {
            _alive = false;
            RunTag = RunTimeTag.Stop;
        }
    }
}
