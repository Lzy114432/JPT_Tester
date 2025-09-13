using System;
using System.Threading;
using Ewan.Model.Production;
using Ewan.Model.System;
using Ewan.Core.Axis;
using Ewan.Core.IO;
using Ewan.Core.Msg;

namespace Ewan.Core.Module
{
    /// <summary>d
    /// 料仓升降控制模块
    /// 负责在自动模式下根据感应器状态自动控制三个料仓的升降
    /// </summary>
    public class BinElevatorModule : BaseModule<BinElevatorModule>
    {
        #region 私有字段

        private int _scanInterval = 20; // 扫描间隔，毫秒
        private readonly object _stateLock = new object();
        
        // 系统状态
        private bool _systemStarted = false;
        private SystemMode _currentMode = SystemMode.Manual;
        
        // 料仓状态机
        private BinElevatorMode _binElevatorMode = BinElevatorMode.Stopped;
        
        // 料仓升降状态
        private BinElevatorState _bin1State = BinElevatorState.Unknown;
        private BinElevatorState _bin2State = BinElevatorState.Unknown;
        private BinElevatorState _bin3State = BinElevatorState.Unknown;
        
        // 感应器状态缓存
        private bool _bin1SensorLast = false;
        private bool _bin2SensorLast = false;
        private bool _bin3SensorLast = false;
        
        // 轴控制器、IO管理器和消息队列
        private AxisManager _axisManager;
        private LayeredIOManager _ioManager;
        private MsgManager _msgManager;
        private MsgListener _systemStatusListener;
        
        // 料仓轴配置（需要在配置中定义）
        private const int BIN1_AXIS_ID = 0; // 料仓1轴ID
        private const int BIN2_AXIS_ID = 1; // 料仓2轴ID
        private const int BIN3_AXIS_ID = 2; // 料仓3轴ID
        
        // 升降位置参数
        private const double ELEVATED_POSITION = 50.0;  // 升高位置
        private const double LOWERED_POSITION = 0.0;    // 降低位置
        private const double MOVE_SPEED = 10.0;         // 移动速度

        #endregion

        #region 枚举定义

        /// <summary>
        /// 料仓升降状态
        /// </summary>
        private enum BinElevatorState
        {
            Unknown,      // 未知状态
            Lowered,      // 已降低
            Elevated,     // 已升高
            Moving        // 正在移动
        }

        #endregion

        #region BaseModule 实现

        protected override void OnInit()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "BinElevatorModule");
                
                // 初始化轴管理器、IO管理器和消息队列
                _axisManager = AxisManager.Instance();
                _ioManager = LayeredIOManager.Instance();
                _msgManager = MsgManager.Instance();
                
                // 注册系统状态消息监听
                _systemStatusListener = new MsgListener(MsgSubject.SystemStatus, OnSystemStatusChanged);
                _msgManager.RegisterListener(_systemStatusListener);
                
                // 检查轴是否配置
                CheckAxisConfiguration();
                
                // 初始化料仓状态
                InitializeBinStates();
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.InitializationCompleted, "料仓升降控制系统");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleInitializationFailed, "BinElevatorModule", ex.Message);
                throw;
            }
        }

        protected override bool OnRun()
        {
            try
            {
                // 检查是否满足运行条件
                if (!ShouldRunElevatorControl())
                {
                    Thread.Sleep(_scanInterval);
                    return true;
                }

                lock (_stateLock)
                {
                    // 检查并处理三个料仓的感应器状态
                    ProcessBinElevator(1, BIN1_AXIS_ID, ref _bin1State, ref _bin1SensorLast);
                    ProcessBinElevator(2, BIN2_AXIS_ID, ref _bin2State, ref _bin2SensorLast);
                    ProcessBinElevator(3, BIN3_AXIS_ID, ref _bin3State, ref _bin3SensorLast);
                }
                
                Thread.Sleep(_scanInterval);
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "BinElevatorModule", ex.Message);
                Thread.Sleep(1000); // 错误时等待更长时间
                return true;
            }
        }

        protected override void OnDestroy()
        {
            try
            {
                // 停止所有料仓升降动作
                StopAllBinMovements();
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "BinElevatorModule");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "BinElevatorModule销毁", ex.Message);
            }
        }

        #endregion

        #region 核心升降控制逻辑

        /// <summary>
        /// 检查是否应该运行升降控制
        /// </summary>
        /// <returns>是否应该运行</returns>
        private bool ShouldRunElevatorControl()
        {
            // 在系统启动且为自动模式下，根据料仓状态机运行
            return _systemStarted && _currentMode == SystemMode.Auto && _binElevatorMode != BinElevatorMode.Stopped;
        }

        /// <summary>
        /// 处理单个料仓的升降控制 - 料仓状态机
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        /// <param name="axisId">轴ID</param>
        /// <param name="currentState">当前状态引用</param>
        /// <param name="lastSensorState">上次感应器状态引用</param>
        private void ProcessBinElevator(int binNumber, int axisId, ref BinElevatorState currentState, ref bool lastSensorState)
        {
            try
            {
                // 读取感应器状态
                bool currentSensorState = ReadBinSensor(binNumber);
                
                // 检查感应器状态变化
                if (currentSensorState != lastSensorState)
                {
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                        "料仓" + binNumber + "感应器状态变化: " + (currentSensorState ? "有料" : "无料"));
                    
                    lastSensorState = currentSensorState;
                }
                
                // 料仓状态机逻辑 - 使用switch优化
                switch (_binElevatorMode)
                {
                    case BinElevatorMode.AutoUp:
                        // 状态机1: 自动上升模式 - 上升到感应位置停止
                        ProcessAutoUpMode(binNumber, axisId, ref currentState, currentSensorState);
                        break;
                        
                    case BinElevatorMode.AutoDown:
                        // 状态机2: 自动下降模式 - 有料感应就下降
                        ProcessAutoDownMode(binNumber, axisId, ref currentState, currentSensorState);
                        break;
                        
                    case BinElevatorMode.Loading:
                        // 状态机3: 上料模式 - 料仓不动，等待机械手上料完成
                        ProcessLoadingMode(binNumber, axisId, ref currentState, currentSensorState);
                        break;
                        
                    case BinElevatorMode.Unloading:
                        // 状态机4: 下料模式 - 料仓不动，等待机械手下料完成
                        ProcessUnloadingMode(binNumber, axisId, ref currentState, currentSensorState);
                        break;
                        
                    case BinElevatorMode.Stopped:
                        // 停止模式 - 不执行任何升降控制
                        break;
                        
                    default:
                        // 未知模式 - 记录警告
                        _uiLogger.Warn(() => Ewan.Resources.LogMessages.ProcessingError, 
                            "料仓" + binNumber + "未知的料仓状态机模式: " + _binElevatorMode);
                        break;
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "料仓" + binNumber + "升降处理", ex.Message);
            }
        }

        /// <summary>
        /// 状态机1: 自动上升模式 - 上升到感应位置停止，然后切换到自动下降
        /// </summary>
        private void ProcessAutoUpMode(int binNumber, int axisId, ref BinElevatorState currentState, bool currentSensorState)
        {
            if (!currentSensorState) // 无料状态 - 开始上升
            {
                // 如果不是正在移动状态，开始上升
                if (currentState != BinElevatorState.Moving)
                {
                    StartBinJogUp(binNumber, axisId);
                    currentState = BinElevatorState.Moving;
                    
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                        "料仓" + binNumber + "自动上升模式：开始上升到感应位置");
                }
            }
            else // 有料状态 - 到达感应位置，停止并切换到自动下降模式
            {
                // 如果正在移动，停止移动
                if (currentState == BinElevatorState.Moving)
                {
                    StopBinAxis(binNumber, axisId);
                    currentState = BinElevatorState.Elevated;
                    
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, 
                        "料仓" + binNumber + "自动上升完成：到达感应位置，切换到自动下降模式");
                    
                    // 只在料仓1时发送模式切换消息，避免重复切换
                    if (binNumber == 1)
                    {
                        // 自动切换到自动下降模式
                        SetBinElevatorMode(BinElevatorMode.AutoDown);
                    }
                }
            }
        }

        /// <summary>
        /// 状态机2: 自动下降模式 - 有料感应就下降
        /// </summary>
        private void ProcessAutoDownMode(int binNumber, int axisId, ref BinElevatorState currentState, bool currentSensorState)
        {
            if (currentSensorState) // 检测到有料
            {
                // 如果不是正在下降状态，开始下降
                if (currentState != BinElevatorState.Moving)
                {
                    StartBinJogDown(binNumber, axisId);
                    currentState = BinElevatorState.Moving;
                    
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                        "料仓" + binNumber + "自动下降模式：检测到有料，开始下降");
                }
            }
            else // 无料状态
            {
                // 如果正在移动，停止移动
                if (currentState == BinElevatorState.Moving)
                {
                    StopBinAxis(binNumber, axisId);
                    currentState = BinElevatorState.Lowered;
                    
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, 
                        "料仓" + binNumber + "自动下降模式：无料，停止下降");
                }
            }
        }

        /// <summary>
        /// 状态机3: 上料模式 - 料仓不动，等待机械手上料完成
        /// </summary>
        private void ProcessLoadingMode(int binNumber, int axisId, ref BinElevatorState currentState, bool currentSensorState)
        {
            // 上料模式下，料仓保持静止，不执行任何升降操作
            // 只在第一个料仓检查机械手上料完成信号，避免重复检查
            
            if (binNumber == 1) // 只在料仓1检查机械手信号
            {
                // 检查机械手上料完成信号
                if (CheckRobotLoadingCompleted())
                {
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, 
                        "机械手上料完成，自动切换到自动下降模式");
                    
                    // 切换到自动下降模式
                    SetBinElevatorMode(BinElevatorMode.AutoDown);
                }
            }
            
            // 在上料模式下，如果轴正在运动，停止运动
            if (currentState == BinElevatorState.Moving)
            {
                StopBinAxis(binNumber, axisId);
                currentState = BinElevatorState.Unknown;
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                    "料仓" + binNumber + "上料模式：停止所有运动，等待机械手上料");
            }
        }

        /// <summary>
        /// 状态机4: 下料模式 - 料仓不动，等待机械手下料完成，然后自动上升
        /// </summary>
        private void ProcessUnloadingMode(int binNumber, int axisId, ref BinElevatorState currentState, bool currentSensorState)
        {
            // 下料模式下，料仓保持静止，不执行任何升降操作
            // 只在第一个料仓检查机械手下料完成信号，避免重复检查
            
            if (binNumber == 1) // 只在料仓1检查机械手信号
            {
                // 检查机械手下料完成信号
                if (CheckRobotUnloadingCompleted())
                {
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, 
                        "机械手下料完成，自动切换到自动上升模式");
                    
                    // 切换到自动上升模式
                    SetBinElevatorMode(BinElevatorMode.AutoUp);
                }
            }
            
            // 在下料模式下，如果轴正在运动，停止运动
            if (currentState == BinElevatorState.Moving)
            {
                StopBinAxis(binNumber, axisId);
                currentState = BinElevatorState.Unknown;
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                    "料仓" + binNumber + "下料模式：停止所有运动，等待机械手下料");
            }
        }

        /// <summary>
        /// 移动料仓到指定位置
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        /// <param name="axisId">轴ID</param>
        /// <param name="position">目标位置</param>
        /// <param name="action">动作描述</param>
        private void MoveBinToPosition(int binNumber, int axisId, double position, string action)
        {
            try
            {
                if (_axisManager != null)
                {
                    // 获取轴配置
                    var axisConfig = _axisManager.GetAxisConfig(axisId);
                    if (axisConfig != null)
                    {
                        // 使用AbsMove进行绝对位置移动
                        _axisManager.AbsMove(axisConfig, position);
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                            "料仓" + binNumber + action + "到位置" + position);
                    }
                    else
                    {
                        _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                            "料仓" + binNumber + "轴配置未找到，轴ID:" + axisId);
                    }
                }
                else
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                        "AxisManager未初始化");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "料仓" + binNumber + "移动", ex.Message);
            }
        }

        /// <summary>
        /// 开始料仓Jog下降运动
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        /// <param name="axisId">轴ID</param>
        private void StartBinJogDown(int binNumber, int axisId)
        {
            try
            {
                if (_axisManager != null)
                {
                    // 获取轴配置
                    var axisConfig = _axisManager.GetAxisConfig(axisId);
                    if (axisConfig != null)
                    {
                        // 使用JogDown进行下降，速度使用配置中的速度
                        _axisManager.JogDown(axisConfig);
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                            "料仓" + binNumber + "开始Jog下降，速度:" + axisConfig.Speed);
                    }
                    else
                    {
                        _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                            "料仓" + binNumber + "轴配置未找到，轴ID:" + axisId);
                    }
                }
                else
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                        "AxisManager未初始化");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "料仓" + binNumber + "Jog下降", ex.Message);
            }
        }

        /// <summary>
        /// 开始料仓Jog上升运动
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        /// <param name="axisId">轴ID</param>
        private void StartBinJogUp(int binNumber, int axisId)
        {
            try
            {
                if (_axisManager != null)
                {
                    // 获取轴配置
                    var axisConfig = _axisManager.GetAxisConfig(axisId);
                    if (axisConfig != null)
                    {
                        // 使用JogUp进行上升，速度使用配置中的速度
                        _axisManager.JogUp(axisConfig);
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                            "料仓" + binNumber + "开始Jog上升，速度:" + axisConfig.Speed);
                    }
                    else
                    {
                        _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                            "料仓" + binNumber + "轴配置未找到，轴ID:" + axisId);
                    }
                }
                else
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                        "AxisManager未初始化");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "料仓" + binNumber + "Jog上升", ex.Message);
            }
        }

        /// <summary>
        /// 停止料仓轴运动
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        /// <param name="axisId">轴ID</param>
        private void StopBinAxis(int binNumber, int axisId)
        {
            try
            {
                if (_axisManager != null)
                {
                    // 获取轴配置
                    var axisConfig = _axisManager.GetAxisConfig(axisId);
                    if (axisConfig != null)
                    {
                        // 使用JogStop停止Jog运动
                        _axisManager.JogStop(axisConfig);
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                            "料仓" + binNumber + "Jog停止");
                    }
                    else
                    {
                        _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                            "料仓" + binNumber + "轴配置未找到，轴ID:" + axisId);
                    }
                }
                else
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                        "AxisManager未初始化");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "料仓" + binNumber + "停止", ex.Message);
            }
        }

        /// <summary>
        /// 检查轴移动是否完成
        /// </summary>
        /// <param name="axisId">轴ID</param>
        /// <returns>是否完成移动</returns>
        private bool IsAxisMovementComplete(int axisId)
        {
            try
            {
                if (_axisManager != null)
                {
                    // 获取轴配置
                    var axisConfig = _axisManager.GetAxisConfig(axisId);
                    if (axisConfig != null)
                    {
                        // 检查轴是否忙碌，如果不忙碌则表示运动完成
                        return !_axisManager.IsBusy(axisConfig);
                    }
                }
                
                // 如果无法获取轴状态，返回true避免卡死
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "检查轴" + axisId + "状态", ex.Message);
                return true; // 出错时返回true避免卡死
            }
        }
        
        /// <summary>
        /// 根据轴ID获取到位信号
        /// </summary>
        /// <param name="axisId">轴ID</param>
        /// <returns>到位信号名称</returns>
        private string GetInPositionSignalByAxisId(int axisId)
        {
            switch (axisId)
            {
                case BIN1_AXIS_ID: return AutoProductionIO.Bin1ElevatorInPosition;
                case BIN2_AXIS_ID: return AutoProductionIO.Bin2ElevatorInPosition;
                case BIN3_AXIS_ID: return AutoProductionIO.Bin3ElevatorInPosition;
                default: return "";
            }
        }

        /// <summary>
        /// 检查机械手上料完成信号
        /// </summary>
        /// <returns>是否完成上料</returns>
        private bool CheckRobotLoadingCompleted()
        {
            try
            {
                if (_ioManager == null || !_ioManager.IsConnected)
                {
                    return false;
                }

                // 读取机械臂取料完成信号 (LogicalIndex 10)
                return _ioManager.LayeredIO.ReadInBit(10, true);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.IOReadError, 
                    "机械手上料完成信号", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 检查机械手下料完成信号
        /// </summary>
        /// <returns>是否完成下料</returns>
        private bool CheckRobotUnloadingCompleted()
        {
            try
            {
                if (_ioManager == null || !_ioManager.IsConnected)
                {
                    return false;
                }

                // 读取机械臂放置完成信号 (LogicalIndex 8)
                return _ioManager.LayeredIO.ReadInBit(8, true);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.IOReadError, 
                    "机械手下料完成信号", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 发送模式切换消息
        /// </summary>
        /// <param name="newMode">新模式</param>
        private void SendModeChangeMessage(SystemMode newMode)
        {
            try
            {
                var statusMsg = new SystemStatusMessage
                {
                    ChangeType = SystemStatusChangeType.SystemModeChanged,
                    SystemMode = newMode,
                    IsStarted = _systemStarted
                };
                
                var message = new MessageModel(MsgSubject.SystemStatus, statusMsg);
                _msgManager.PushMsg(message);
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                    "发送模式切换消息: " + newMode);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "发送模式切换消息", ex.Message);
            }
        }

        #endregion

        #region 感应器检测

        /// <summary>
        /// 读取料仓感应器状态
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        /// <returns>感应器状态（true=有料，false=无料）</returns>
        private bool ReadBinSensor(int binNumber)
        {
            try
            {
                if (_ioManager == null || !_ioManager.IsConnected)
                {
                    return false;
                }

                // 根据料仓编号读取对应的IO感应器 (LogicalIndex)
                int sensorIndex = -1;
                switch (binNumber)
                {
                    case 1:
                        sensorIndex = 27; // 料仓1有料感应 LogicalIndex
                        break;
                    case 2:
                        sensorIndex = 28; // 料仓2有料感应 LogicalIndex
                        break;
                    case 3:
                        sensorIndex = 29; // 料仓3有料感应 LogicalIndex
                        break;
                    default:
                        return false;
                }
                
                // 使用LayeredIO读取感应器状态
                return _ioManager.LayeredIO.ReadInBit(sensorIndex, true);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.IOReadError, 
                    "料仓" + binNumber + "感应器", ex.Message);
                return false;
            }
        }

        #endregion

        #region 初始化和配置

        /// <summary>
        /// 检查轴配置
        /// </summary>
        private void CheckAxisConfiguration()
        {
            try
            {
                // TODO: 检查料仓轴是否已配置
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "检查料仓轴配置");
                
                // 检查三个料仓轴是否存在
                // if (!_axisManager.IsAxisConfigured(BIN1_AXIS_ID))
                // {
                //     _uiLogger.Warning(() => "料仓1轴未配置 (ID: " + BIN1_AXIS_ID + ")");
                // }
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "轴配置检查完成");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "轴配置检查", ex.Message);
            }
        }

        /// <summary>
        /// 初始化料仓状态
        /// </summary>
        private void InitializeBinStates()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "初始化料仓状态");
                
                // 读取当前感应器状态
                _bin1SensorLast = ReadBinSensor(1);
                _bin2SensorLast = ReadBinSensor(2);
                _bin3SensorLast = ReadBinSensor(3);
                
                // 设置初始状态为未知，让系统自动检测
                _bin1State = BinElevatorState.Unknown;
                _bin2State = BinElevatorState.Unknown;
                _bin3State = BinElevatorState.Unknown;
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, 
                    "料仓状态初始化 - 料仓1:" + (_bin1SensorLast ? "有料" : "无料") + 
                    " 料仓2:" + (_bin2SensorLast ? "有料" : "无料") + 
                    " 料仓3:" + (_bin3SensorLast ? "有料" : "无料"));
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "料仓状态初始化", ex.Message);
            }
        }

        /// <summary>
        /// 停止所有料仓移动
        /// </summary>
        private void StopAllBinMovements()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "停止所有料仓Jog运动");
                
                // 停止所有轴的Jog移动
                StopBinAxis(1, BIN1_AXIS_ID);
                StopBinAxis(2, BIN2_AXIS_ID);
                StopBinAxis(3, BIN3_AXIS_ID);
                
                // 重置状态
                _bin1State = BinElevatorState.Unknown;
                _bin2State = BinElevatorState.Unknown;
                _bin3State = BinElevatorState.Unknown;
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "所有料仓Jog运动已停止");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "停止料仓Jog运动", ex.Message);
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置料仓状态机模式
        /// </summary>
        /// <param name="mode">料仓状态机模式</param>
        public void SetBinElevatorMode(BinElevatorMode mode)
        {
            lock (_stateLock)
            {
                if (_binElevatorMode != mode)
                {
                    BinElevatorMode oldMode = _binElevatorMode;
                    _binElevatorMode = mode;
                    
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                        "料仓状态机切换: " + oldMode + " → " + mode);
                    
                    // 如果切换到停止模式，停止所有升降动作
                    if (mode == BinElevatorMode.Stopped)
                    {
                        StopAllBinMovements();
                    }
                }
            }
        }

        /// <summary>
        /// 获取当前料仓状态机模式
        /// </summary>
        /// <returns>当前料仓状态机模式</returns>
        public BinElevatorMode GetBinElevatorMode()
        {
            lock (_stateLock)
            {
                return _binElevatorMode;
            }
        }

        /// <summary>
        /// 设置系统启动状态
        /// </summary>
        /// <param name="started">是否启动</param>
        public void SetSystemStarted(bool started)
        {
            lock (_stateLock)
            {
                if (_systemStarted != started)
                {
                    _systemStarted = started;
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                        "料仓升降系统" + (started ? "启动" : "停止"));
                    
                    if (!started)
                    {
                        // 系统停止时，停止所有升降动作
                        StopAllBinMovements();
                    }
                }
            }
        }

        /// <summary>
        /// 设置系统模式
        /// </summary>
        /// <param name="mode">系统模式</param>
        public void SetSystemMode(SystemMode mode)
        {
            lock (_stateLock)
            {
                if (_currentMode != mode)
                {
                    _currentMode = mode;
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                        "料仓升降模式切换到" + (mode == SystemMode.Auto ? "自动" : "手动"));
                    
                    if (mode != SystemMode.Auto)
                    {
                        // 切换到手动模式时，停止自动升降
                        StopAllBinMovements();
                    }
                }
            }
        }

        /// <summary>
        /// 获取当前系统状态
        /// </summary>
        /// <returns>系统状态信息</returns>
        public string GetSystemStatus()
        {
            lock (_stateLock)
            {
                return "启动:" + _systemStarted + 
                       " 模式:" + _currentMode + 
                       " 料仓1:" + _bin1State + 
                       " 料仓2:" + _bin2State + 
                       " 料仓3:" + _bin3State;
            }
        }

        /// <summary>
        /// 手动控制料仓升降（手动模式下使用）
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        /// <param name="elevate">true=升高，false=降低</param>
        /// <returns>是否成功</returns>
        public bool ManualControlBin(int binNumber, bool elevate)
        {
            if (_currentMode != SystemMode.Manual)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "非手动模式，无法手动控制料仓");
                return false;
            }

            try
            {
                int axisId = GetAxisIdByBinNumber(binNumber);
                if (axisId == -1)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "无效的料仓编号: " + binNumber);
                    return false;
                }

                double position = elevate ? ELEVATED_POSITION : LOWERED_POSITION;
                MoveBinToPosition(binNumber, axisId, position, elevate ? "手动升高" : "手动降低");
                
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "手动控制料仓" + binNumber, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 根据料仓编号获取轴ID
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        /// <returns>轴ID，-1表示无效</returns>
        private int GetAxisIdByBinNumber(int binNumber)
        {
            switch (binNumber)
            {
                case 1: return BIN1_AXIS_ID;
                case 2: return BIN2_AXIS_ID;
                case 3: return BIN3_AXIS_ID;
                default: return -1;
            }
        }

        #endregion

        #region 消息处理

        /// <summary>
        /// 处理系统状态变化消息
        /// </summary>
        /// <param name="msg">系统状态消息</param>
        private void OnSystemStatusChanged(MessageModel msg)
        {
            try
            {
                if (msg.Data is SystemStatusMessage statusMsg)
                {
                    lock (_stateLock)
                    {
                        switch (statusMsg.ChangeType)
                        {
                            case SystemStatusChangeType.SystemStarted:
                                _systemStarted = statusMsg.IsStarted;
                                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                                    "料仓升降系统" + (statusMsg.IsStarted ? "启动" : "停止"));
                                
                                if (!statusMsg.IsStarted)
                                {
                                    // 系统停止时，停止所有升降动作
                                    StopAllBinMovements();
                                }
                                break;
                                
                            case SystemStatusChangeType.SystemModeChanged:
                                _currentMode = statusMsg.SystemMode;
                                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                                    "料仓升降模式切换到" + (statusMsg.SystemMode == SystemMode.Auto ? "自动" : "手动"));
                                
                                if (statusMsg.SystemMode != SystemMode.Auto)
                                {
                                    // 切换到手动模式时，停止自动升降
                                    StopAllBinMovements();
                                }
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