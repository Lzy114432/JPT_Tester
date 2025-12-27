using Ewan.Core.Axis;
using Ewan.Model.System;
using Ewan.Model.Messages;
using EwanCore.Messaging;
using System;
using System.Threading;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 皮带输送控制模块
    /// 负责控制皮带轴持续反向运行
    /// 响应系统控制命令（急停、暂停）
    /// </summary>
    public class BeltConveyorModule : BaseModule<BeltConveyorModule>
    {
        #region 私有字段

        private int _scanInterval = 100; // 扫描间隔，毫秒
        private readonly object _stateLock = new object();

        // 皮带状态
        private bool _beltRunning = false;
        private bool _shouldStop = false; // 是否应该停止（急停、暂停、关闭等）
        private bool _moduleEnabled = true;

        // 轴控制器和消息管理器
        private AxisManager _axisManager;
        private IDisposable _systemControlSubscription;
        private IDisposable _beltControlSubscription;

        private readonly SystemParametersManager _parametersManager = SystemParametersManager.Instance;

        // 皮带轴配置
        private const int BELT_AXIS_ID = 3; // 皮带轴ID

        private bool _loadingStopRequested = false;
        private bool _unloadingStopRequested = false;

        #endregion

        #region BaseModule 实现

        protected override void OnInit()
        {
            try
            {
                _uiLogger.InfoRaw("模块初始化成功: {0}", "BeltConveyorModule");

                // 初始化轴管理器
                _axisManager = AxisManager.Instance();

                // 注册系统控制消息监听器（监听急停、暂停等命令）
                _systemControlSubscription = MessageHub.Current.Subscribe<SystemControlMessage>(OnSystemControlMessage);

                _beltControlSubscription = MessageHub.Current.Subscribe<BeltConveyorControlMessage>(OnBeltControlMessage);

                _uiLogger.InfoRaw("初始化已完成: {0}", "皮带输送控制系统");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("模块初始化失败: {0} - {1}", "BeltConveyorModule", ex.Message);
                throw;
            }
        }

        protected override bool OnRun()
        {
            try
            {
                var parameters = _parametersManager.Parameters;
                if (!parameters.EnableLoadingModule)
                {
                    lock (_stateLock)
                    {
                        if (_beltRunning)
                        {
                            StopBelt();
                        }
                        _moduleEnabled = false;
                    }

                    Thread.Sleep(_scanInterval);
                    return true;
                }

                lock (_stateLock)
                {
                    if (!_moduleEnabled)
                    {
                        _moduleEnabled = true;
                    }

                    bool hasProcessStopRequest = _loadingStopRequested || _unloadingStopRequested;

                    // 检查是否应该停止
                    if (_shouldStop || hasProcessStopRequest)
                    {
                        // 停止皮带
                        if (_beltRunning)
                        {
                            StopBelt();
                        }
                    }
                    else
                    {
                        // 正常运行 - 如果皮带未运行，启动皮带反向运行
                        if (!_beltRunning)
                        {
                            StartBeltReverse();
                        }
                    }
                }

                Thread.Sleep(_scanInterval);
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("模块运行错误: {0} - {1}", "BeltConveyorModule", ex.Message);
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

                // 取消注册系统控制消息监听
                _systemControlSubscription?.Dispose();
                _systemControlSubscription = null;

                _beltControlSubscription?.Dispose();
                _beltControlSubscription = null;

                _uiLogger.InfoRaw("模块已销毁: {0}", "BeltConveyorModule");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "BeltConveyorModule销毁", ex.Message);
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
                        // 使用Jog方法进行反向运行，传入负的配置速度
                        _axisManager.Jog(axisConfig, -axisConfig.Speed);
                        _beltRunning = true;

                        _uiLogger.InfoRaw("处理已开始: {0}",
                            "皮带轴开始反向运行，速度:" + axisConfig.Speed);
                    }
                    else
                    {
                        _uiLogger.WarnRaw("处理错误: {0} - {1}",
                            "皮带轴配置未找到或未启用", "轴ID:" + BELT_AXIS_ID);
                    }
                }
                else
                {
                    _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                        "AxisManager未初始化", "");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}",
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

                        _uiLogger.InfoRaw("处理已完成: {0}",
                            "皮带轴已停止");
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                    "皮带轴停止", ex.Message);
            }
        }

        #endregion

        #region 系统控制消息处理

        private void OnBeltControlMessage(BeltConveyorControlMessage message)
        {
            try
            {
                lock (_stateLock)
                {
                    if (message.Source == Ewan.Model.Messages.BeltConveyorControlSource.MaterialLoading)
                    {
                        _loadingStopRequested = message.StopRequested;
                    }
                    else if (message.Source == Ewan.Model.Messages.BeltConveyorControlSource.MaterialUnloading)
                    {
                        _unloadingStopRequested = message.StopRequested;
                    }
                }

                var actionText = message.StopRequested ? "请求停止" : "释放控制";
                _uiLogger.InfoRaw("处理已开始: {0}",
                    $"皮带{actionText} - 来源:{message.Source}, 原因:{message.Reason ?? "无"}");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                    "处理皮带控制消息", ex.Message);
            }
        }

        /// <summary>
        /// 处理系统控制消息（急停、暂停等）
        /// </summary>
        private void OnSystemControlMessage(SystemControlMessage message)
        {
            try
            {
                var command = message.Command;
                lock (_stateLock)
                {
                    switch (command)
                    {
                        case SystemControlCommand.EmergencyStop:
                            // 紧急停止 - 立即停止皮带
                            _shouldStop = true;
                            if (_beltRunning)
                            {
                                StopBelt();
                            }
                            _uiLogger.WarnRaw("处理已完成: {0}",
                                "皮带紧急停止");
                            break;

                        case SystemControlCommand.Pause:
                            // 暂停 - 停止皮带
                            _shouldStop = true;
                            if (_beltRunning)
                            {
                                StopBelt();
                            }
                            _uiLogger.InfoRaw("处理已完成: {0}",
                                "皮带已暂停");
                            break;

                        case SystemControlCommand.Resume:
                            // 恢复运行 - 允许皮带重新启动
                            _shouldStop = false;
                            _uiLogger.InfoRaw("处理已开始: {0}",
                                "皮带恢复运行");
                            break;

                        case SystemControlCommand.Stop:
                            // 停止 - 停止皮带
                            _shouldStop = true;
                            if (_beltRunning)
                            {
                                StopBelt();
                            }
                            _uiLogger.InfoRaw("处理已完成: {0}",
                                "皮带已停止");
                            break;

                        case SystemControlCommand.Initialize:
                            // 初始化/复位 - 允许皮带重新启动
                            _shouldStop = false;
                            _uiLogger.InfoRaw("处理已开始: {0}",
                                "皮带系统已初始化");
                            break;

                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                    "处理系统控制消息", ex.Message);
            }
        }

        #endregion
    }
}
