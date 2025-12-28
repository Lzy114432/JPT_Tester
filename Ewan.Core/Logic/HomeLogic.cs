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
        private LayeredIOManager _ioManager;

        // 延时配置 (毫秒)
        private const int STOP_ON_DELAY = 500;
        private const int STOP_OFF_DELAY = 500;
        private const int START_DELAY = 500;
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
                    case "初始状态":
                        ProcessInitialState();
                        break;

                    case "停止ON":
                        ProcessStopOn();
                        break;

                    case "停止ON等待":
                        ProcessStopOnWait();
                        break;

                    case "停止OFF":
                        ProcessStopOff();
                        break;

                    case "停止OFF等待":
                        ProcessStopOffWait();
                        break;

                    case "开始ON":
                        ProcessStartOn();
                        break;

                    case "开始ON等待":
                        ProcessStartOnWait();
                        break;

                    case "开始OFF":
                        ProcessStartOff();
                        break;

                    case "清除允许取料":
                        ProcessClearAllowPickup();
                        break;

                    case "料仓初始化":
                        ProcessBinInit();
                        break;

                    case "等待料仓完成":
                        ProcessWaitBinComplete();
                        break;

                    case "结束状态":
                        // 完成
                        break;
                }
            }
            catch (Exception ex)
            {
                AbortHome("复位异常: " + ex.Message, ex);
            }
        }

        #endregion

        #region 状态处理方法

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

            MachineParameters.Instance.NeedHome = true;
            MachineParameters.Instance.IsHomeing = false;
            Complete();
        }

        /// <summary>
        /// 初始状态
        /// </summary>
        private void ProcessInitialState()
        {
            _uiLogger.InfoRaw("状态机启动: {0}", "HomeLogic");
            MachineParameters.Instance.IsHomeing = true;
            _ioManager = LayeredIOManager.Instance();
            SwitchIndex = "停止ON";
        }

        /// <summary>
        /// 发送停止信号 ON
        /// </summary>
        private void ProcessStopOn()
        {
            if (_ioManager?.Ctx != null)
            {
                _ioManager.Ctx.On(x => x.停止输出);
            }
            SwitchIndex = "停止ON等待";
        }

        /// <summary>
        /// 等待停止信号 ON 延时
        /// </summary>
        private void ProcessStopOnWait()
        {
            if (Tw.StartCheckIsTimeout(SwitchIndex, STOP_ON_DELAY))
            {
                SwitchIndex = "停止OFF";
            }
        }

        /// <summary>
        /// 发送停止信号 OFF
        /// </summary>
        private void ProcessStopOff()
        {
            if (_ioManager?.Ctx != null)
            {
                _ioManager.Ctx.Off(x => x.停止输出);
            }
            SwitchIndex = "停止OFF等待";
        }

        /// <summary>
        /// 等待停止信号 OFF 延时
        /// </summary>
        private void ProcessStopOffWait()
        {
            if (Tw.StartCheckIsTimeout(SwitchIndex, STOP_OFF_DELAY))
            {
                SwitchIndex = "开始ON";
            }
        }

        /// <summary>
        /// 发送开始信号 ON
        /// </summary>
        private void ProcessStartOn()
        {
            if (_ioManager?.Ctx != null)
            {
                _ioManager.Ctx.On(x => x.开始);
            }
            SwitchIndex = "开始ON等待";
        }

        /// <summary>
        /// 等待开始信号 ON 延时
        /// </summary>
        private void ProcessStartOnWait()
        {
            if (Tw.StartCheckIsTimeout(SwitchIndex, START_DELAY))
            {
                SwitchIndex = "开始OFF";
            }
        }

        /// <summary>
        /// 发送开始信号 OFF
        /// </summary>
        private void ProcessStartOff()
        {
            if (_ioManager?.Ctx != null)
            {
                _ioManager.Ctx.Off(x => x.开始);
            }
            SwitchIndex = "清除允许取料";
        }

        /// <summary>
        /// 清除允许取料信号
        /// </summary>
        private void ProcessClearAllowPickup()
        {
            if (_ioManager?.Ctx != null)
            {
                _ioManager.Ctx.Off(x => x.触发机械手皮带线允许取料);
            }
            _uiLogger.InfoRaw("处理已完成: {0}", "上料机硬件初始化完成");
            SwitchIndex = "料仓初始化";
        }

        /// <summary>
        /// 启动料仓初始化
        /// </summary>
        private void ProcessBinInit()
        {
            var posted = MessageHub.Current.Post(Ewan.Model.Production.BinElevatorCommandMessage.InitializeAll(nameof(HomeLogic)));
            if (!posted)
            {
                _binElevator?.PerformHardwareInitialization();
            }
            SwitchIndex = "等待料仓完成";
        }

        /// <summary>
        /// 等待料仓初始化完成
        /// </summary>
        private void ProcessWaitBinComplete()
        {
            // 等待料仓初始化完成（简化处理，实际可检测完成信号）
            if (Tw.StartCheckIsTimeout(SwitchIndex, BIN_INIT_TIMEOUT))
            {
                _uiLogger.InfoRaw("处理已完成: {0}", "HomeLogic 复位完成");
                MachineParameters.Instance.NeedHome = false;
                MachineParameters.Instance.IsHomeing = false;
                MessageHub.Current.Post(new StatusIndicatorCommand(SystemStatus.Standby, "复位完成，待机"));
                Complete();
            }
        }

        #endregion
    }
}
