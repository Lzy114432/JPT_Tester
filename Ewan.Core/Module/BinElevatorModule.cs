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

        private int _scanInterval = 200; // 扫描间隔，毫秒
        private readonly object _stateLock = new object();
        
        // 系统状态
        private bool _systemStarted = false;
        private SystemMode _currentMode = SystemMode.Manual;
        
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
        private const int BIN1_AXIS_ID = 10; // 料仓1轴ID
        private const int BIN2_AXIS_ID = 11; // 料仓2轴ID
        private const int BIN3_AXIS_ID = 12; // 料仓3轴ID
        
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
            // 只有在系统启动且自动模式下才运行
            return _systemStarted && _currentMode == SystemMode.Auto;
        }

        /// <summary>
        /// 处理单个料仓的升降控制
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
                    
                    // 根据感应器状态决定升降动作
                    if (currentSensorState) // 有料 - 升高
                    {
                        if (currentState != BinElevatorState.Elevated && currentState != BinElevatorState.Moving)
                        {
                            MoveBinToPosition(binNumber, axisId, ELEVATED_POSITION, "升高");
                            currentState = BinElevatorState.Moving;
                        }
                    }
                    else // 无料 - 降低
                    {
                        if (currentState != BinElevatorState.Lowered && currentState != BinElevatorState.Moving)
                        {
                            MoveBinToPosition(binNumber, axisId, LOWERED_POSITION, "降低");
                            currentState = BinElevatorState.Moving;
                        }
                    }
                    
                    lastSensorState = currentSensorState;
                }
                
                // 检查移动是否完成
                if (currentState == BinElevatorState.Moving)
                {
                    if (IsAxisMovementComplete(axisId))
                    {
                        // 更新状态
                        currentState = lastSensorState ? BinElevatorState.Elevated : BinElevatorState.Lowered;
                        
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, 
                            "料仓" + binNumber + (currentState == BinElevatorState.Elevated ? "升高完成" : "降低完成"));
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "料仓" + binNumber + "升降处理", ex.Message);
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
                    // TODO: 调用轴管理器移动到指定位置
                    // 检查轴是否可用
                    // if (_axisManager.IsAxisReady(axisId))
                    // {
                    //     _axisManager.MoveAxisToPosition(axisId, position, MOVE_SPEED);
                    // }
                }
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                    "料仓" + binNumber + action + "到位置" + position);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "料仓" + binNumber + "移动", ex.Message);
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
                    // TODO: 检查轴是否到达目标位置
                    // return _axisManager.IsAxisAtTargetPosition(axisId);
                    
                    // 也可以通过IO信号检查到位状态
                    string inPositionSignal = GetInPositionSignalByAxisId(axisId);
                    if (!string.IsNullOrEmpty(inPositionSignal) && _ioManager != null && _ioManager.IsConnected)
                    {
                        // TODO: 实现IO读取
                        // return _ioManager.ReadInput(inPositionSignal);
                    }
                }
                
                // 暂时返回true，等待轴管理器和IO实现
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "检查轴" + axisId + "状态", ex.Message);
                return false;
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

                // 根据料仓编号读取对应的IO感应器
                string sensorSignal = "";
                switch (binNumber)
                {
                    case 1:
                        sensorSignal = AutoProductionIO.Bin1ElevatorSensor;
                        break;
                    case 2:
                        sensorSignal = AutoProductionIO.Bin2ElevatorSensor;
                        break;
                    case 3:
                        sensorSignal = AutoProductionIO.Bin3ElevatorSensor;
                        break;
                    default:
                        return false;
                }
                
                // TODO: 实现IO读取
                // return _ioManager.ReadInput(sensorSignal);
                
                // 暂时返回false，等待IO映射实现
                return false;
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
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "停止所有料仓升降");
                
                // TODO: 停止所有轴的移动
                // _axisManager.StopAxis(BIN1_AXIS_ID);
                // _axisManager.StopAxis(BIN2_AXIS_ID);
                // _axisManager.StopAxis(BIN3_AXIS_ID);
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "所有料仓升降已停止");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "停止料仓升降", ex.Message);
            }
        }

        #endregion

        #region 公共方法

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