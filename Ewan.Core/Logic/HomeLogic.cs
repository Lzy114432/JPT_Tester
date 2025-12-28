using Ewan.Core;
using Ewan.Core.IO;
using Ewan.Core.Module;
using Ewan.Model.Messages;
using Ewan.Model.System;
using EwanCore.Messaging;
using EwanCore.StateMachine;
using System;

namespace Ewan.Core.Logic
{
    /// <summary>
    /// 复位流程状态机
    /// 执行硬件初始化序列
    /// 使用非阻塞状态机模式（无 Thread.Sleep）
    /// </summary>
    public class HomeLogic : LogicBase
    {
        #region 私有字段

        private readonly IBinElevator _binElevator;
        private readonly LayeredIOManager _ioManager = LayeredIOManager.Instance();

        // 延时配置 (毫秒)
        private const int STOP_PULSE_DURATION = 500;
        private const int STOP_OFF_DELAY = 500;
        private const int START_PULSE_DURATION = 500;
        private const int BIN_INIT_TIMEOUT = 10000;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数（兼容旧签名，sharedState 已不再依赖）
        /// </summary>
        /// <param name="sharedState">共享状态对象</param>
        /// <param name="binElevator">料仓升降模块</param>
        [Obsolete("sharedState 已不再依赖，请使用 HomeLogic(IBinElevator)")]
        public HomeLogic(ProductionLineSharedState sharedState, IBinElevator binElevator) : this(binElevator)
        {
        }

        public HomeLogic(IBinElevator binElevator)
        {
            _binElevator = binElevator;
        }

        #endregion

        #region LogicBase 实现

        /// <summary>
        /// 状态机处理器 - 非阻塞模式
        /// </summary>
        public override void Handler()
        {
            try
            {
                switch (SwitchIndex)
                {
                    #region 初始状态
                    case "初始状态":
                        SwitchIndex = "发送停止脉冲";
                        break;
                    #endregion

                    #region 发送停止脉冲
                    case "发送停止脉冲":
                        _ioManager?.Ctx?.Pulse(x => x.停止输出, STOP_PULSE_DURATION, now: true);
                        SwitchIndex = "等待停止完成";
                        Tw.StartWatch(SwitchIndex);
                        break;
                    #endregion

                    #region 等待停止完成
                    case "等待停止完成":
                        // 等待脉冲完成 + OFF延时
                        if (Tw.StartCheckIsTimeout(SwitchIndex, STOP_PULSE_DURATION + STOP_OFF_DELAY))
                        {
                            SwitchIndex = "发送开始脉冲";
                        }
                        break;
                    #endregion

                    #region 发送开始脉冲
                    case "发送开始脉冲":
                        _ioManager?.Ctx?.Pulse(x => x.开始, START_PULSE_DURATION, now: true);
                        SwitchIndex = "等待开始完成";
                        Tw.StartWatch(SwitchIndex);
                        break;
                    #endregion

                    #region 等待开始完成
                    case "等待开始完成":
                        if (Tw.StartCheckIsTimeout(SwitchIndex, START_PULSE_DURATION))
                        {
                            SwitchIndex = "清除允许取料";
                        }
                        break;
                    #endregion

                    #region 清除允许取料
                    case "清除允许取料":
                        _ioManager?.Ctx?.Off(x => x.触发机械手皮带线允许取料);
                        SwitchIndex = "料仓初始化";
                        break;
                    #endregion

                    #region 料仓初始化
                    case "料仓初始化":
                        {
                            var posted = MessageHub.Current.Post(Ewan.Model.Production.BinElevatorCommandMessage.InitializeAll(nameof(HomeLogic)));
                            if (!posted)
                            {
                                _binElevator?.PerformHardwareInitialization();
                            }
                            SwitchIndex = "等待料仓完成";
                        }
                        break;
                    #endregion

                    #region 等待料仓完成
                    case "等待料仓完成":
                        if (Tw.StartCheckIsTimeout(SwitchIndex, BIN_INIT_TIMEOUT))
                        {
                            MachineParameters.Instance.EndHome(success: true);
                            MessageHub.Current.Post(new StatusIndicatorCommand(SystemStatus.Standby, "复位完成，待机"));
                            Complete();
                        }
                        break;
                    #endregion

                    #region 结束状态
                    case "结束状态":
                        // 完成
                        break;
                    #endregion
                }
            }
            catch (Exception ex)
            {
                AbortHome("复位异常: " + ex.Message, ex);
            }
        }

        #endregion

        #region 辅助方法

        private void AbortHome(string alarmMessage, Exception ex = null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(alarmMessage))
                {
                    _uiLogger.ErrorRaw("处理错误: {0} - {1}", "HomeLogic", alarmMessage);

                    MessageHub.Current.Post(new AlarmMessage(
                        key: "Home.Exception",
                        content: alarmMessage,
                        level: EwanCore.AlarmSystem.AlarmLevel.H,
                        needReset: true,
                        unit: "Home"));
                }
            }
            catch (Exception postEx)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "HomeLogic-发送报警", postEx.Message);
            }

            MachineParameters.Instance.EndHome(success: false);
            Complete();
        }

        #endregion
    }
}
