using System;
using System.Threading;
using Ewan.Model.Production;
using Ewan.Model.System;
using Ewan.Core.Axis;
using Ewan.Core.IO;
using Ewan.Core.Msg;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 简化的料仓升降控制模块
    /// 通过外部指令控制，移除复杂的自动状态机
    /// </summary>
    public class SimplifiedBinElevatorModule : BaseModule<SimplifiedBinElevatorModule>
    {
        #region 私有字段

        private int _scanInterval = 20; // 扫描间隔，毫秒
        private readonly object _stateLock = new object();
        
        // 系统状态
        private bool _systemStarted = false;
        private SystemMode _currentMode = SystemMode.Manual;

        // 料仓状态（每个料仓独立）
        private BinExecuteState _bin1State = BinExecuteState.Idle;
        private BinExecuteState _bin2State = BinExecuteState.Idle;
        private BinExecuteState _bin3State = BinExecuteState.Idle;
        
        // 当前执行的指令
        private BinCommand _bin1Command = BinCommand.Stop;
        private BinCommand _bin2Command = BinCommand.Stop;
        private BinCommand _bin3Command = BinCommand.Stop;
        
        // 指令ID跟踪
        private string _bin1CommandId = "";
        private string _bin2CommandId = "";
        private string _bin3CommandId = "";
        
        // 感应器状态缓存
        private bool _bin1SensorLast = false;
        private bool _bin2SensorLast = false;
        private bool _bin3SensorLast = false;
        
        // 上料位置序列状态（仅用于FeedPosition指令）
        private enum FeedSequenceState
        {
            UpToSensor,      // 上升到感应位置
            DownToNoSensor   // 下降到无感应位置
        }
        
        private FeedSequenceState _bin1FeedState = FeedSequenceState.UpToSensor;
        private FeedSequenceState _bin2FeedState = FeedSequenceState.UpToSensor;
        private FeedSequenceState _bin3FeedState = FeedSequenceState.UpToSensor;
        
        // 轴控制器、IO管理器和消息队列
        private AxisManager _axisManager;
        private LayeredIOManager _ioManager;
        private MsgManager _msgManager;
        private MsgListener _systemStatusListener;
        private MsgListener _commandListener;
        
        // 料仓轴配置（需要在配置中定义）
        private const int BIN1_AXIS_ID = 0; // 料仓1轴ID
        private const int BIN2_AXIS_ID = 1; // 料仓2轴ID
        private const int BIN3_AXIS_ID = 2; // 料仓3轴ID

        #endregion

        #region BaseModule 实现

        protected override void OnInit()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "SimplifiedBinElevatorModule");
                
                // 初始化轴管理器、IO管理器和消息队列
                _axisManager = AxisManager.Instance();
                _ioManager = LayeredIOManager.Instance();
                _msgManager = MsgManager.Instance();
                
                // 注册系统状态消息监听
                _systemStatusListener = new MsgListener(MsgSubject.SystemStatus, OnSystemStatusChanged);
                _msgManager.RegisterListener(_systemStatusListener);
                
                // 注册料仓控制指令监听
                _commandListener = new MsgListener(MsgSubject.BinElevatorCommand, OnBinElevatorCommand);
                _msgManager.RegisterListener(_commandListener);
                
                // 初始化料仓状态
                InitializeBinStates();
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.InitializationCompleted, "简化料仓升降控制系统");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleInitializationFailed, "SimplifiedBinElevatorModule", ex.Message);
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
                    // 处理三个料仓的指令执行
                    ProcessBinExecution(1, BIN1_AXIS_ID, ref _bin1State, ref _bin1Command, ref _bin1CommandId, ref _bin1SensorLast, ref _bin1FeedState);
                    ProcessBinExecution(2, BIN2_AXIS_ID, ref _bin2State, ref _bin2Command, ref _bin2CommandId, ref _bin2SensorLast, ref _bin2FeedState);
                    ProcessBinExecution(3, BIN3_AXIS_ID, ref _bin3State, ref _bin3Command, ref _bin3CommandId, ref _bin3SensorLast, ref _bin3FeedState);
                }
                
                Thread.Sleep(_scanInterval);
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "SimplifiedBinElevatorModule", ex.Message);
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
                
                // 注销消息监听器
                if (_systemStatusListener != null)
                {
                    _msgManager.UnregisterListener(_systemStatusListener);
                }
                if (_commandListener != null)
                {
                    _msgManager.UnregisterListener(_commandListener);
                }
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "SimplifiedBinElevatorModule");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "SimplifiedBinElevatorModule销毁", ex.Message);
            }
        }

        #endregion

        #region 核心控制逻辑

        /// <summary>
        /// 检查是否应该运行升降控制
        /// </summary>
        /// <returns>是否应该运行</returns>
        private bool ShouldRunElevatorControl()
        {
            // 在系统启动且为自动模式下运行
            return _systemStarted && _currentMode == SystemMode.Auto;
        }

        /// <summary>
        /// 处理单个料仓的指令执行
        /// </summary>
        private void ProcessBinExecution(int binNumber, int axisId, ref BinExecuteState currentState, 
            ref BinCommand currentCommand, ref string commandId, ref bool lastSensorState, ref FeedSequenceState feedState)
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
                
                // 如果状态不是执行中，则不需要处理
                if (currentState != BinExecuteState.Executing)
                {
                    return;
                }
                
                // 根据当前指令执行相应逻辑
                switch (currentCommand)
                {
                    case BinCommand.FeedPosition:
                        ExecuteFeedPositionCommand(binNumber, axisId, ref currentState, ref commandId, currentSensorState, ref feedState);
                        break;
                        
                    case BinCommand.DownPosition:
                        ExecuteDownPositionCommand(binNumber, axisId, ref currentState, ref commandId);
                        break;
                        
                    case BinCommand.Stop:
                        ExecuteStopCommand(binNumber, axisId, ref currentState, ref commandId);
                        break;
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "料仓" + binNumber + "指令执行", ex.Message);
                
                // 出错时设置状态并发送反馈
                currentState = BinExecuteState.Error;
                SendStatusFeedback(binNumber, currentState, currentCommand, commandId, "执行出错: " + ex.Message);
            }
        }

        /// <summary>
        /// 执行上料位置指令：上升到感应位置 → 下降到无感应位置
        /// </summary>
        private void ExecuteFeedPositionCommand(int binNumber, int axisId, ref BinExecuteState currentState, 
            ref string commandId, bool currentSensorState, ref FeedSequenceState feedState)
        {
            switch (feedState)
            {
                case FeedSequenceState.UpToSensor:
                    if (!currentSensorState) // 无料状态，继续上升
                    {
                        if (!IsAxisMoving(axisId))
                        {
                            StartBinJogUp(binNumber, axisId);
                        }
                    }
                    else // 有料状态，到达感应位置，切换到下降阶段
                    {
                        StopBinAxis(binNumber, axisId);
                        feedState = FeedSequenceState.DownToNoSensor;
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                            "料仓" + binNumber + "到达感应位置，开始下降到无感应位置");
                    }
                    break;
                    
                case FeedSequenceState.DownToNoSensor:
                    if (currentSensorState) // 有料状态，继续下降
                    {
                        if (!IsAxisMoving(axisId))
                        {
                            StartBinJogDown(binNumber, axisId);
                        }
                    }
                    else // 无料状态，到达目标位置，完成指令
                    {
                        StopBinAxis(binNumber, axisId);
                        currentState = BinExecuteState.Completed;
                        feedState = FeedSequenceState.UpToSensor; // 重置为下次使用
                        
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, 
                            "料仓" + binNumber + "上料位置指令完成");
                        
                        SendStatusFeedback(binNumber, currentState, BinCommand.FeedPosition, commandId, "上料位置指令完成");
                    }
                    break;
            }
        }

        /// <summary>
        /// 执行下料位置指令：直接下降到底部
        /// </summary>
        private void ExecuteDownPositionCommand(int binNumber, int axisId, ref BinExecuteState currentState, ref string commandId)
        {
            // 简单实现：启动下降，一段时间后停止（实际应根据下限位开关判断）
            if (!IsAxisMoving(axisId))
            {
                StartBinJogDown(binNumber, axisId);
            }
            
            // TODO: 这里应该检查下限位开关来确定是否到达底部
            // 暂时使用定时方式模拟
            System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ =>
            {
                lock (_stateLock)
                {
                    if (currentState == BinExecuteState.Executing)
                    {
                        StopBinAxis(binNumber, axisId);
                        currentState = BinExecuteState.Completed;
                        
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, 
                            "料仓" + binNumber + "下料位置指令完成");
                        
                        SendStatusFeedback(binNumber, currentState, BinCommand.DownPosition, commandId, "下料位置指令完成");
                    }
                }
            });
        }

        /// <summary>
        /// 执行停止指令：立即停止
        /// </summary>
        private void ExecuteStopCommand(int binNumber, int axisId, ref BinExecuteState currentState, ref string commandId)
        {
            StopBinAxis(binNumber, axisId);
            currentState = BinExecuteState.Completed;
            
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, 
                "料仓" + binNumber + "停止指令完成");
            
            SendStatusFeedback(binNumber, currentState, BinCommand.Stop, commandId, "停止指令完成");
        }

        #endregion

        #region 轴控制辅助方法

        /// <summary>
        /// 开始料仓Jog上升运动
        /// </summary>
        private void StartBinJogUp(int binNumber, int axisId)
        {
            try
            {
                if (_axisManager != null)
                {
                    var axisConfig = _axisManager.GetAxisConfig(axisId);
                    if (axisConfig != null)
                    {
                        _axisManager.JogUp(axisConfig);
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                            "料仓" + binNumber + "开始Jog上升");
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "料仓" + binNumber + "Jog上升", ex.Message);
            }
        }

        /// <summary>
        /// 开始料仓Jog下降运动
        /// </summary>
        private void StartBinJogDown(int binNumber, int axisId)
        {
            try
            {
                if (_axisManager != null)
                {
                    var axisConfig = _axisManager.GetAxisConfig(axisId);
                    if (axisConfig != null)
                    {
                        _axisManager.JogDown(axisConfig);
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                            "料仓" + binNumber + "开始Jog下降");
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "料仓" + binNumber + "Jog下降", ex.Message);
            }
        }

        /// <summary>
        /// 停止料仓轴运动
        /// </summary>
        private void StopBinAxis(int binNumber, int axisId)
        {
            try
            {
                if (_axisManager != null)
                {
                    var axisConfig = _axisManager.GetAxisConfig(axisId);
                    if (axisConfig != null)
                    {
                        _axisManager.JogStop(axisConfig);
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                            "料仓" + binNumber + "Jog停止");
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "料仓" + binNumber + "停止", ex.Message);
            }
        }

        /// <summary>
        /// 检查轴是否正在运动
        /// </summary>
        private bool IsAxisMoving(int axisId)
        {
            try
            {
                if (_axisManager != null)
                {
                    var axisConfig = _axisManager.GetAxisConfig(axisId);
                    if (axisConfig != null)
                    {
                        return _axisManager.IsBusy(axisConfig);
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "检查轴" + axisId + "运动状态", ex.Message);
                return false;
            }
        }

        #endregion

        #region 感应器检测

        /// <summary>
        /// 读取料仓感应器状态
        /// </summary>
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
                        sensorIndex = 27; // 料仓1有料感应 LogicalIndex (IN27)
                        break;
                    case 2:
                        sensorIndex = 28; // 料仓2有料感应 LogicalIndex (IN28)
                        break;
                    case 3:
                        sensorIndex = 29; // 料仓3有料感应 LogicalIndex (IN29)
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

        #region 消息处理

        /// <summary>
        /// 处理系统状态变化消息
        /// </summary>
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
                                    "简化料仓升降系统" + (statusMsg.IsStarted ? "启动" : "停止"));
                                
                                if (!statusMsg.IsStarted)
                                {
                                    StopAllBinMovements();
                                }
                                break;
                                
                            case SystemStatusChangeType.SystemStopped:
                                _systemStarted = false;
                                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                                    "简化料仓升降系统停止");
                                StopAllBinMovements();
                                break;
                                
                            case SystemStatusChangeType.SystemModeChanged:
                                _currentMode = statusMsg.SystemMode;
                                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                                    "简化料仓升降模式切换到" + (statusMsg.SystemMode == SystemMode.Auto ? "自动" : "手动"));
                                
                                if (statusMsg.SystemMode != SystemMode.Auto)
                                {
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

        /// <summary>
        /// 处理料仓升降控制指令
        /// </summary>
        private void OnBinElevatorCommand(MessageModel msg)
        {
            try
            {
                if (msg.Data is BinElevatorCommandMessage commandMsg)
                {
                    lock (_stateLock)
                    {
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                            "收到料仓" + commandMsg.BinNumber + "控制指令: " + commandMsg.Command + 
                            " (ID: " + commandMsg.CommandId + ")");
                        
                        // 根据料仓编号分配指令
                        switch (commandMsg.BinNumber)
                        {
                            case 1:
                                _bin1Command = commandMsg.Command;
                                _bin1CommandId = commandMsg.CommandId;
                                _bin1State = BinExecuteState.Executing;
                                if (commandMsg.Command == BinCommand.FeedPosition)
                                {
                                    _bin1FeedState = FeedSequenceState.UpToSensor;
                                }
                                break;
                                
                            case 2:
                                _bin2Command = commandMsg.Command;
                                _bin2CommandId = commandMsg.CommandId;
                                _bin2State = BinExecuteState.Executing;
                                if (commandMsg.Command == BinCommand.FeedPosition)
                                {
                                    _bin2FeedState = FeedSequenceState.UpToSensor;
                                }
                                break;
                                
                            case 3:
                                _bin3Command = commandMsg.Command;
                                _bin3CommandId = commandMsg.CommandId;
                                _bin3State = BinExecuteState.Executing;
                                if (commandMsg.Command == BinCommand.FeedPosition)
                                {
                                    _bin3FeedState = FeedSequenceState.UpToSensor;
                                }
                                break;
                                
                            default:
                                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                                    "无效的料仓编号: " + commandMsg.BinNumber);
                                return;
                        }
                        
                        // 发送指令接收确认
                        SendStatusFeedback(commandMsg.BinNumber, BinExecuteState.Executing, commandMsg.Command, 
                            commandMsg.CommandId, "指令已接收，开始执行");
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "处理料仓控制指令", ex.Message);
            }
        }

        /// <summary>
        /// 发送料仓状态反馈消息
        /// </summary>
        private void SendStatusFeedback(int binNumber, BinExecuteState state, BinCommand command, 
            string commandId, string description = "")
        {
            try
            {
                bool hasMatchedData = ReadBinSensor(binNumber);
                
                var statusMsg = new BinElevatorStatusMessage(binNumber, state, command, commandId, description)
                {
                    HasMaterial = hasMatchedData
                };
                
                var message = new MessageModel(MsgSubject.BinElevatorStatus, statusMsg);
                _msgManager.PushMsg(message);
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                    "发送料仓" + binNumber + "状态反馈: " + state + " - " + description);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "发送料仓状态反馈", ex.Message);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 初始化料仓状态
        /// </summary>
        private void InitializeBinStates()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "初始化简化料仓状态");
                
                // 读取当前感应器状态
                _bin1SensorLast = ReadBinSensor(1);
                _bin2SensorLast = ReadBinSensor(2);
                _bin3SensorLast = ReadBinSensor(3);
                
                // 设置初始状态为空闲
                _bin1State = BinExecuteState.Idle;
                _bin2State = BinExecuteState.Idle;
                _bin3State = BinExecuteState.Idle;
                
                // 初始化指令为停止
                _bin1Command = BinCommand.Stop;
                _bin2Command = BinCommand.Stop;
                _bin3Command = BinCommand.Stop;
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, 
                    "简化料仓状态初始化完成 - 料仓1:" + (_bin1SensorLast ? "有料" : "无料") + 
                    " 料仓2:" + (_bin2SensorLast ? "有料" : "无料") + 
                    " 料仓3:" + (_bin3SensorLast ? "有料" : "无料"));
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "简化料仓状态初始化", ex.Message);
            }
        }

        /// <summary>
        /// 停止所有料仓移动
        /// </summary>
        private void StopAllBinMovements()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "停止所有料仓运动");
                
                // 停止所有轴的移动
                StopBinAxis(1, BIN1_AXIS_ID);
                StopBinAxis(2, BIN2_AXIS_ID);
                StopBinAxis(3, BIN3_AXIS_ID);
                
                // 重置状态为空闲
                lock (_stateLock)
                {
                    _bin1State = BinExecuteState.Idle;
                    _bin2State = BinExecuteState.Idle;
                    _bin3State = BinExecuteState.Idle;
                    
                    _bin1Command = BinCommand.Stop;
                    _bin2Command = BinCommand.Stop;
                    _bin3Command = BinCommand.Stop;
                }
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "所有料仓运动已停止");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "停止料仓运动", ex.Message);
            }
        }

        #endregion

        #region 公共接口方法

        /// <summary>
        /// 发送料仓控制指令（外部调用接口）
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        /// <param name="command">控制指令</param>
        /// <param name="description">指令描述</param>
        /// <returns>是否成功发送指令</returns>
        public bool SendBinCommand(int binNumber, BinCommand command, string description = "")
        {
            try
            {
                var commandMsg = new BinElevatorCommandMessage(binNumber, command, description);
                var message = new MessageModel(MsgSubject.BinElevatorCommand, commandMsg);
                _msgManager.PushMsg(message);
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                    "发送料仓" + binNumber + "控制指令: " + command);
                
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "发送料仓" + binNumber + "控制指令", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 获取料仓当前状态
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        /// <returns>料仓状态信息</returns>
        public string GetBinStatus(int binNumber)
        {
            lock (_stateLock)
            {
                switch (binNumber)
                {
                    case 1:
                        return "状态:" + _bin1State + " 指令:" + _bin1Command + " 感应:" + (_bin1SensorLast ? "有料" : "无料");
                    case 2:
                        return "状态:" + _bin2State + " 指令:" + _bin2Command + " 感应:" + (_bin2SensorLast ? "有料" : "无料");
                    case 3:
                        return "状态:" + _bin3State + " 指令:" + _bin3Command + " 感应:" + (_bin3SensorLast ? "有料" : "无料");
                    default:
                        return "无效的料仓编号";
                }
            }
        }

        #endregion
    }
}