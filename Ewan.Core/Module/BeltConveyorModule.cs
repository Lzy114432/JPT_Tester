using Ewan.Core.Axis;
using Ewan.Core.IO;
using Ewan.Core.Msg;
using Ewan.Model.Production;
using Ewan.Model.System;
using System;
using System.Threading;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 皮带输送控制模块
    /// 负责在自动模式下控制皮带轴持续反向运行
    /// </summary>
    public class BeltConveyorModule : BaseModule<BeltConveyorModule>
    {
        #region 私有字段

        private int _scanInterval = 100; // 扫描间隔，毫秒
        private readonly object _stateLock = new object();

        // 系统状态
        private bool _systemStarted = false;
        private SystemMode _currentMode = SystemMode.Manual;
        private bool _beltRunning = false;

        // 轴控制器和IO管理器
        private AxisManager _axisManager;
        private LayeredIOManager _ioManager;
        private MsgManager _msgManager;
        private MsgListener _systemStatusListener;

        // 皮带轴配置
        private const int BELT_AXIS_ID = 3; // 皮带轴ID

        #endregion

        #region BaseModule 实现

        protected override void OnInit()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "BeltConveyorModule");

                // 初始化轴管理器、IO管理器和消息队列
                _axisManager = AxisManager.Instance();
                _ioManager = LayeredIOManager.Instance();
                _msgManager = MsgManager.Instance();

                // 注册系统状态消息监听
                _systemStatusListener = new MsgListener(MsgSubject.SystemStatus, OnSystemStatusChanged);
                _msgManager.RegisterListener(_systemStatusListener);

                _uiLogger.Info(() => Ewan.Resources.LogMessages.InitializationCompleted, "皮带输送控制系统");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleInitializationFailed, "BeltConveyorModule", ex.Message);
                throw;
            }
        }

        protected override bool OnRun()
        {
            try
            {
                lock (_stateLock)
                {
                    // 检查系统是否启动且为自动模式
                    if (_systemStarted && _currentMode == SystemMode.Auto)
                    {
                        // 如果皮带未运行，启动皮带反向运行
                        if (!_beltRunning)
                        {
                            StartBeltReverse();
                        }
                    }
                    else
                    {
                        // 非自动模式，停止皮带
                        if (_beltRunning)
                        {
                            StopBelt();
                        }
                    }
                }

                Thread.Sleep(_scanInterval);
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "BeltConveyorModule", ex.Message);
                Thread.Sleep(1000); // 错误时等待更长时间
                return true;
            }
        }

        protected override void OnDestroy()
        {
            try
            {
                // 停止皮带运行
                StopBelt();

                // 取消注册系统状态消息监听
                if (_systemStatusListener != null)
                {
                    _msgManager.UnRegisterListener(_systemStatusListener);
                    _systemStatusListener = null;
                }

                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "BeltConveyorModule");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "BeltConveyorModule销毁", ex.Message);
            }
        }

        #endregion

        #region 皮带控制逻辑

        /// <summary>
        /// 启动皮带反向运行
        /// </summary>
        private void StartBeltReverse()
        {
            try
            {
                if (_axisManager != null)
                {
                    // 获取皮带轴配置
                    var axisConfig = _axisManager.GetAxisConfig(BELT_AXIS_ID);
                    if (axisConfig != null && axisConfig.IsUsing)
                    {
                        // 使用JogDown进行反向运行
                        _axisManager.JogDown(axisConfig);
                        _beltRunning = true;

                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted,
                            "皮带轴开始反向运行，速度:" + axisConfig.Speed);
                    }
                    else
                    {
                        _uiLogger.Warn(() => Ewan.Resources.LogMessages.ProcessingError,
                            "皮带轴配置未找到或未启用", "轴ID:" + BELT_AXIS_ID);
                    }
                }
                else
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError,
                        "AxisManager未初始化", "");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError,
                    "皮带轴启动", ex.Message);
            }
        }

        /// <summary>
        /// 停止皮带运行
        /// </summary>
        private void StopBelt()
        {
            try
            {
                if (_axisManager != null)
                {
                    // 获取皮带轴配置
                    var axisConfig = _axisManager.GetAxisConfig(BELT_AXIS_ID);
                    if (axisConfig != null)
                    {
                        // 停止皮带轴
                        _axisManager.JogStop(axisConfig);
                        _beltRunning = false;

                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted,
                            "皮带轴已停止");
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError,
                    "皮带轴停止", ex.Message);
            }
        }

        #endregion

        #region 系统状态消息处理

        /// <summary>
        /// 处理系统状态消息
        /// </summary>
        private void OnSystemStatusChanged(MessageModel message)
        {
            try
            {
                if (message.Data is SystemStatusMessage statusMsg)
                {
                    lock (_stateLock)
                    {
                        switch (statusMsg.ChangeType)
                        {
                            case SystemStatusChangeType.SystemStarted:
                                _systemStarted = true;
                                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted,
                                    "皮带控制系统已启动");
                                break;

                            case SystemStatusChangeType.SystemStopped:
                                _systemStarted = false;
                                StopBelt();
                                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted,
                                    "皮带控制系统已停止");
                                break;

                            case SystemStatusChangeType.SystemModeChanged:
                                _currentMode = statusMsg.SystemMode;
                                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted,
                                    "皮带控制模式切换到" + (statusMsg.SystemMode == SystemMode.Auto ? "自动" : "手动"));

                                if (statusMsg.SystemMode != SystemMode.Auto)
                                {
                                    // 切换到手动模式时，停止皮带
                                    StopBelt();
                                }
                                break;

                            default:
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError,
                    "处理系统状态消息", ex.Message);
            }
        }

        #endregion
    }
}
