using Ewan.Core.Axis;
using Ewan.Core.IO;
using Ewan.Core.Msg;
using Ewan.Model.Production;
using System;
using System.Threading;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 料仓升降控制模块
    /// 负责在自动模式下根据感应器状态自动控制三个料仓的升降
    /// </summary>
    public class BinElevatorModule : BaseModule<BinElevatorModule>
    {
        #region 私有字段

        private int _scanInterval = 1; // 扫描间隔，毫秒
        private readonly object _stateLock = new object();
        
        // 共享状态（用于与其他模块通信）
        private ProductionLineSharedState _sharedState;
        
        // 系统状态
        private bool _systemStarted = false;
        private SystemMode _currentMode = SystemMode.Manual;

        private BinElevatorMode _binElevatorMode = BinElevatorMode.Init;

        // 料仓升降状态
        private BinElevatorState _bin1State = BinElevatorState.Unknown;
        private BinElevatorState _bin2State = BinElevatorState.Unknown;
        private BinElevatorState _bin3State = BinElevatorState.Unknown;
        
        // 料仓到达感应位置标志
        private bool _bin1ReachedSensor = false;
        private bool _bin2ReachedSensor = false;
        private bool _bin3ReachedSensor = false;
        
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
        
        // 机械手信号IO配置
        private const int ROBOT_LOADING_COMPLETE_SIGNAL = 8;  // 机械手装载完成信号（放入料仓）
        private const int ROBOT_UNLOADING_COMPLETE_SIGNAL = 10; // 机械手卸载完成信号（从料仓取出）
        
        // 料仓选择信号IO配置
        private const int BIN1_SELECT_SIGNAL = 11; // Y11 - 料仓1选择信号
        private const int BIN2_SELECT_SIGNAL = 12; // Y12 - 料仓2选择信号
        private const int BIN3_SELECT_SIGNAL = 13; // Y13 - 料仓3选择信号


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
            Moving,        // 正在移动
            Stopped       // 已停止
        }

        #endregion

        #region 构造函数

        
        /// <summary>
        /// 带共享状态的构造函数
        /// </summary>
        /// <param name="sharedState">共享状态对象</param>
        public BinElevatorModule(ProductionLineSharedState sharedState)
        {
            _sharedState = sharedState;
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

                // 注册系统状态消息监听,外部通过消息控制模式和启动停止
                _systemStatusListener = new MsgListener(MsgSubject.SystemStatus, OnSystemStatusChanged);
                _msgManager.RegisterListener(_systemStatusListener);
                
                           
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
                lock (_stateLock)
                {
                    // 先统一检查机械手信号，避免在每个料仓处理中重复检查
                    CheckRobotSignals();
                    
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
        /// 检查机械手信号并处理相应的状态切换
        /// </summary>
        private void CheckRobotSignals()
        {
            try
            {
                // 检查机械手装载完成信号
                if (_ioManager.LayeredIO.ReadFallingBit(ROBOT_LOADING_COMPLETE_SIGNAL))
                {
                    // 设置装载完成状态
                    SetLoadingCompleted(true);

                    _ioManager.LayeredIO.ClearFallingBit(ROBOT_LOADING_COMPLETE_SIGNAL);
                    ResetSelectedBinStates(BinElevatorMode.Loading);
                }

                // 检查机械手卸载完成信号
                if (_ioManager.LayeredIO.ReadFallingBit(ROBOT_UNLOADING_COMPLETE_SIGNAL))
                {
                    // 设置卸载完成状态
                    SetUnloadingCompleted(true);

                    _ioManager.LayeredIO.ClearFallingBit(ROBOT_UNLOADING_COMPLETE_SIGNAL);



                    ResetSelectedBinStates(BinElevatorMode.Unloading);
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "机械手信号检查", ex.Message);
            }
        }

        /// <summary>
        /// 检查是否应该运行升降控制
        /// </summary>
        /// <returns>是否应该运行</returns>
        private bool ShouldRunElevatorControl()
        {
            // Init模式：只要系统启动就可以运行初始化，不受系统模式限制
            if (_binElevatorMode == BinElevatorMode.Init)
            {
                return _systemStarted;
            }
            
            // 其他模式：需要系统启动且为自动模式，且状态机不是停止状态
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
                // 料仓状态机逻辑 - 使用switch优化
                switch (_binElevatorMode)
                {
                    case BinElevatorMode.Init:
                        // 状态机0: 初始化模式 - 先上升到感应位置，再下降到无感应，保持停止
                        ProcessInitMode(binNumber, axisId, ref currentState);
                        break;

                    case BinElevatorMode.Loading:
                        // 状态机1: 上料模式 - 下降到感应器有信号就停止
                        ProcessLoadingMode(binNumber, axisId, ref currentState);
                        break;

                    case BinElevatorMode.Unloading:
                        // 状态机2: 下料模式 - 上升到感应位置后，下降到无感应就停止
                        ProcessUnloadingMode(binNumber, axisId, ref currentState);
                        break;

                    case BinElevatorMode.Stopped:
                        // 停止模式 - 不执行任何升降控制
                        break;
                        
                    default:
                        // 未知模式 - 记录警告
                        _uiLogger.Warn(() => Ewan.Resources.LogMessages.ProcessingError, 
                            "料仓" + binNumber + "未知的料仓状态机模式", _binElevatorMode.ToString());
                        break;
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "料仓" + binNumber + "升降处理", ex.Message);
            }
        }


        #region 状态机实现


        /// <summary>
        /// 状态机0: 初始化模式 - 先上升到感应位置，再下降到无感应，最后保持停止
        /// </summary>
        private void ProcessInitMode(int binNumber, int axisId, ref BinElevatorState currentState)
        {
            // 根据当前状态执行不同的初始化步骤
            switch (currentState)
            {
                case BinElevatorState.Stopped:
                    // 已经初始化完成，保持静止
                    break;
                case BinElevatorState.Unknown:
                    // 第一步：开始上升到感应位置
                    if (!ReadBinSensor(binNumber)) // 料仓感应为false 就是上升
                    {
                        StartBinJogUp(binNumber, axisId);
                        currentState = BinElevatorState.Moving;
                    }
                    else
                    {
                        // 如果已经有感应，直接进入下降阶段
                        currentState = BinElevatorState.Elevated;
                        //_uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                        //    "料仓" + binNumber + "初始化：已在感应位置，准备下降");
                    }
                    break;
                    
                case BinElevatorState.Moving:
                    if (ReadBinSensor(binNumber))
                    {
                        StopBinAxis(binNumber, axisId);
                        //currentState = BinElevatorState.Elevated;
                        //_uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted,
                        //    "料仓" + binNumber + "初始化：已到达感应位置，准备下降");
                    }
                    break;
                    
                case BinElevatorState.Elevated:
                    // 第二步：从感应位置下降
                    StartBinJogDown(binNumber, axisId);
                    currentState = BinElevatorState.Lowered; // 标记为正在下降
                    //_uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                    //    "料仓" + binNumber + "初始化：开始从感应位置下降");
                    break;


                    
                case BinElevatorState.Lowered:
                    // 正在下降中，检查是否到达感应位置
                    if (ReadBinSensor(binNumber))
                    {
                        StopBinAxis(binNumber, axisId);
                        
                        // 标记该料仓初始化完成
                        switch (binNumber)
                        {
                            case 1:
                                _bin1ReachedSensor = true;
                                break;
                            case 2:
                                _bin2ReachedSensor = true;
                                break;
                            case 3:
                                _bin3ReachedSensor = true;
                                break;
                        }
                                          
                        // 检查是否所有料仓都初始化完成
                        if (_bin1ReachedSensor && _bin2ReachedSensor && _bin3ReachedSensor)
                        {
                            _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, 
                                "所有料仓初始化完成，切换到停止模式");
                            
                            // 重置标志
                            _bin1ReachedSensor = false;
                            _bin2ReachedSensor = false;
                            _bin3ReachedSensor = false;
                            currentState = BinElevatorState.Stopped;
                        }
                    }
                    break;
            }
        }


        /// <summary>
        /// 状态机1: 上料模式 - 下降到感应器有信号就停止
        /// </summary>
        private void ProcessLoadingMode(int binNumber, int axisId, ref BinElevatorState currentState)
        {
            switch (currentState)
            {
                case BinElevatorState.Unknown:
                    // 检查当前感应器状态
                    if (ReadBinSensor(binNumber)) 
                    {
                        // 开始下降动作
                        StartBinJogDown(binNumber, axisId);
                        currentState = BinElevatorState.Moving;
                        //_uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted,
                        //    "料仓" + binNumber + "上料模式：开始下降");
                    }
                    else
                    {
                        // 感应器已经为true，直接完成
                        currentState = BinElevatorState.Stopped;
                        //_uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted,
                        //    "料仓" + binNumber + "上料模式：已在感应位置");
                    }
                    break;

                case BinElevatorState.Moving:
                    // 正在下降中，检查是否到达感应位置
                    if (!ReadBinSensor(binNumber))
                    {
                        StopBinAxis(binNumber, axisId);
                        currentState = BinElevatorState.Stopped;
                    }
                    break;
                case BinElevatorState.Stopped:
                    break;
            }
        }

        /// <summary>
        /// 状态机4: 下料模式 - 先上升后下降到没有感应就停止
        /// </summary>
        private void ProcessUnloadingMode(int binNumber, int axisId, ref BinElevatorState currentState)
        {
            // 根据当前状态执行不同的初始化步骤
            switch (currentState)
            {
                case BinElevatorState.Unknown:
                    // 第一步：开始上升到感应位置
                    if (!ReadBinSensor(binNumber)) // 料仓感应为false 就是上升
                    {
                        StartBinJogUp(binNumber, axisId);
                        currentState = BinElevatorState.Moving;
                        //_uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted,
                        //    "料仓" + binNumber + "初始化：开始上升到感应位置");
                    }
                    else
                    {
                        // 如果已经有感应，直接进入下降阶段
                        currentState = BinElevatorState.Elevated;
                        //_uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted,
                        //    "料仓" + binNumber + "初始化：已在感应位置，准备下降");
                    }
                    break;

                case BinElevatorState.Moving:
                    if (ReadBinSensor(binNumber))
                    {
                        StopBinAxis(binNumber, axisId);
                        currentState = BinElevatorState.Elevated;
                        //_uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted,
                        //    "料仓" + binNumber + "初始化：已到达感应位置，准备下降");
                    }
                    break;

                case BinElevatorState.Elevated:
                    // 第二步：从感应位置下降
                    StartBinJogDown(binNumber, axisId);
                    currentState = BinElevatorState.Lowered; // 标记为正在下降
                    //_uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted,
                    //    "料仓" + binNumber + "初始化：开始从感应位置下降");
                    break;

                case BinElevatorState.Lowered:
                    // 正在下降中，检查是否脱离感应位置
                    if (ReadBinSensor(binNumber))
                    {
                        StopBinAxis(binNumber, axisId);
                        currentState = BinElevatorState.Stopped;
                    }
                    break;


                case BinElevatorState.Stopped:
                    break;
            }
        }


        #endregion


        #endregion

        #region 轴控制操作

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
                            "料仓" + binNumber + "轴配置未找到", "轴ID:" + axisId);
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
                            "料仓" + binNumber + "轴配置未找到", "轴ID:" + axisId);
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
                        //_uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                        //    "料仓" + binNumber + "Jog停止");
                    }
                    else
                    {
                        _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError,
                            "料仓" + binNumber + "轴配置未找到", "轴ID:" + axisId);
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
                    "料仓" + binNumber + "停止", ex.Message);
            }
        }


        #endregion

        #region 料仓状态重置

        /// <summary>
        /// 根据Y11/Y12/Y13信号重置选中料仓的状态并切换模式
        /// </summary>
        /// <param name="mode">要切换到的料仓模式</param>
        private void ResetSelectedBinStates(BinElevatorMode mode)
        {
            // 设置料仓模式
            _binElevatorMode = mode;
            
            // 根据Y11/Y12/Y13信号选择性重置料仓状态
            if (_ioManager.LayeredIO.ReadOutBit(BIN1_SELECT_SIGNAL))
            {
                _bin1State = BinElevatorState.Unknown;
            }
            if (_ioManager.LayeredIO.ReadOutBit(BIN2_SELECT_SIGNAL))
            {
                _bin2State = BinElevatorState.Unknown;
            }
            if (_ioManager.LayeredIO.ReadOutBit(BIN3_SELECT_SIGNAL))
            {
                _bin3State = BinElevatorState.Unknown;
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


        #region 消息处理

        /// <summary>
        /// 处理系统状态变化消息
        /// </summary>
        /// <param name="msg">系统状态消息</param>
        private void OnSystemStatusChanged(MessageModel msg)
        {
            // 目前不处理任何消息，保留接口以备将来扩展，后续通过msg进行系统启动/停止和模式切换



            //try
            //{
            //    if (msg.Data is SystemStatusMessage statusMsg)
            //    {
            //        lock (_stateLock)
            //        {
            //            switch (statusMsg.ChangeType)
            //            {
            //                case SystemStatusChangeType.SystemStarted:
            //                    _systemStarted = statusMsg.IsStarted;
            //                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
            //                        "料仓升降系统" + (statusMsg.IsStarted ? "启动" : "停止"));

            //                    if (!statusMsg.IsStarted)
            //                    {
            //                        // 系统停止时，停止所有升降动作
            //                        StopAllBinMovements();
            //                    }
            //                    break;

            //                case SystemStatusChangeType.SystemModeChanged:
            //                    _currentMode = statusMsg.SystemMode;
            //                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
            //                        "料仓升降模式切换到" + (statusMsg.SystemMode == SystemMode.Auto ? "自动" : "手动"));

            //                    if (statusMsg.SystemMode != SystemMode.Auto)
            //                    {
            //                        // 切换到手动模式时，停止自动升降
            //                        StopAllBinMovements();
            //                    }
            //                    break;
            //            }
            //        }
            //    }
            //}
            //catch (Exception ex)
            //{
            //    _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
            //        "处理系统状态消息", ex.Message);
            //}
        }

        #endregion


        #region 共享状态访问方法

        /// <summary>
        /// 设置装载完成状态
        /// </summary>
        private void SetLoadingCompleted(bool completed)
        {
            if (_sharedState != null)
            {
                _sharedState.SetLoadingCompleted(completed);
            }
        }

        /// <summary>
        /// 设置卸载完成状态
        /// </summary>
        private void SetUnloadingCompleted(bool completed)
        {
            if (_sharedState != null)
            {
                _sharedState.SetUnloadingCompleted(completed);
            }
        }

        /// <summary>
        /// 获取装载完成状态
        /// </summary>
        private bool GetLoadingCompleted()
        {
            if (_sharedState != null)
            {
                return _sharedState.GetLoadingCompleted();
            }
            return false;
        }

        /// <summary>
        /// 获取卸载完成状态
        /// </summary>
        private bool GetUnloadingCompleted()
        {
            if (_sharedState != null)
            {
                return _sharedState.GetUnloadingCompleted();
            }
            return false;
        }

        #endregion

    }
}