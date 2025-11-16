using Ewan.Core.Axis;
using Ewan.Core.IO;
using Ewan.Core.Msg;
using Ewan.Model.Production;
using System;
using System.Diagnostics;
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
        private int _activeUnloadingBin = 0;

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

        // 下料物料检测相关
        private readonly ManualResetEventSlim _materialCheckEvent = new ManualResetEventSlim(false);
        private BinMaterialCheckResult _materialCheckResult = BinMaterialCheckResult.CreateFailure(0);
        private bool _materialDetectionInProgress = false;
        private int _materialDetectionBin = 0;
        private DateTime _materialDetectionStartTime = DateTime.MinValue;
        private readonly int _materialDetectionTimeoutMs = 5000;


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
                _uiLogger.InfoRaw("模块初始化成功: {0}", "BinElevatorModule");
                
                // 初始化轴管理器、IO管理器和消息队列
                _axisManager = AxisManager.Instance();
                _ioManager = LayeredIOManager.Instance();
                _msgManager = MsgManager.Instance();

                // 注册系统状态消息监听,外部通过消息控制模式和启动停止
                _systemStatusListener = new MsgListener(MsgSubject.SystemStatus, OnSystemStatusChanged);
                _msgManager.RegisterListener(_systemStatusListener);
                
                           
                _uiLogger.InfoRaw("初始化已完成: {0}", "料仓升降控制系统");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("模块初始化失败: {0} - {1}", "BinElevatorModule", ex.Message);
                throw;
            }
        }

        protected override bool OnRun()
        {
            try
            {
                lock (_stateLock)
                {
                    // 检查暂停状态
                    if (_sharedState?.IsSystemPaused() == true)
                    {
                        // 暂停状态：停止所有轴运动但不退出循环
                        if (_binElevatorMode != BinElevatorMode.Stopped)
                        {
                            StopAllBinMovements();
                            _binElevatorMode = BinElevatorMode.Stopped;
                            _uiLogger.InfoRaw("处理已完成: {0}", "料仓升降模块已暂停");
                        }
                        Thread.Sleep(_scanInterval);
                        return true;
                    }
                    
                    // 检查是否需要重新初始化
                    if (_sharedState?.RequireReinit() == true)
                    {
                        _sharedState.SetRequireReinit(false);
                        
                        // 重置到初始化状态
                        _binElevatorMode = BinElevatorMode.Init;
                        _bin1State = BinElevatorState.Unknown;
                        _bin2State = BinElevatorState.Unknown; 
                        _bin3State = BinElevatorState.Unknown;
                        
                        // 重置初始化标志
                        _bin1ReachedSensor = false;
                        _bin2ReachedSensor = false;
                        _bin3ReachedSensor = false;
                        
                        _uiLogger.InfoRaw("处理已开始: {0}", "料仓重新初始化开始");
                    }
                    
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
                _uiLogger.ErrorRaw("模块运行错误: {0} - {1}", "BinElevatorModule", ex.Message);
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
                _materialCheckEvent?.Dispose();
                
                _uiLogger.InfoRaw("模块已销毁: {0}", "BinElevatorModule");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "BinElevatorModule销毁", ex.Message);
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 执行料仓升降硬件初始化（公共方法，供外部调用）
        /// 将模式设置为Init，触发料仓初始化流程
        /// </summary>
        public void PerformHardwareInitialization()
        {
            lock (_stateLock)
            {
                _binElevatorMode = BinElevatorMode.Init;
                _bin1State = BinElevatorState.Unknown;
                _bin2State = BinElevatorState.Unknown;
                _bin3State = BinElevatorState.Unknown;

                _bin1ReachedSensor = false;
                _bin2ReachedSensor = false;
                _bin3ReachedSensor = false;

                _uiLogger.InfoRaw("处理已开始: {0}", "料仓升降硬件初始化开始");
            }
        }

        /// <summary>
        /// 强制停止所有料仓并将状态机置为停止
        /// </summary>
        public void ForceStopAllBins()
        {
            lock (_stateLock)
            {
                StopAllBinMovements();
                _binElevatorMode = BinElevatorMode.Stopped;
                _bin1State = BinElevatorState.Stopped;
                _bin2State = BinElevatorState.Stopped;
                _bin3State = BinElevatorState.Stopped;

                _bin1ReachedSensor = false;
                _bin2ReachedSensor = false;
                _bin3ReachedSensor = false;
                _activeUnloadingBin = 0;
            }
        }

        /// <summary>
        /// 将指定料仓上升到有料感应位置，并返回物料检测结果
        /// </summary>
        /// <param name="binNumber">料仓编号 (1-3)</param>
        public BinMaterialCheckResult RaiseToSensor(int binNumber)
        {
            if (binNumber < 1 || binNumber > 3)
            {
                _uiLogger.WarnRaw("处理错误: {0} - {1}", "RaiseToSensor", $"无效的料仓编号 {binNumber}");
                return BinMaterialCheckResult.CreateFailure(binNumber);
            }

            if (ReadBinSensor(binNumber))
            {
                _uiLogger.InfoRaw("处理已完成: {0}", $"料仓{binNumber}已检测到物料，无需上升");
                return BinMaterialCheckResult.CreateHasMaterial(binNumber);
            }

            int axisId = GetAxisIdByBin(binNumber);
            if (axisId < 0)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                    $"料仓{binNumber}检测失败", "未找到对应轴配置");
                return BinMaterialCheckResult.CreateFailure(binNumber);
            }

            _uiLogger.InfoRaw("处理已开始: {0}", $"料仓{binNumber}上升至有料感应请求");

            lock (_stateLock)
            {
                _activeUnloadingBin = binNumber;
                _binElevatorMode = BinElevatorMode.Unloading;
            }

            var result = ExecuteSynchronousMaterialCheck(binNumber, axisId);

            lock (_stateLock)
            {
                _activeUnloadingBin = 0;
                _binElevatorMode = BinElevatorMode.Stopped;
            }

            return result;
        }

        private BinMaterialCheckResult ExecuteSynchronousMaterialCheck(int binNumber, int axisId)
        {
            const int pollIntervalMs = 20;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                StartBinJogUp(binNumber, axisId);

                while (stopwatch.ElapsedMilliseconds < _materialDetectionTimeoutMs)
                {
                    if (ReadBinSensor(binNumber))
                    {
                        _uiLogger.InfoRaw("处理已完成: {0}",
                            $"料仓{binNumber}检测到物料，停止上升");
                        return BinMaterialCheckResult.CreateHasMaterial(binNumber);
                    }

                    Thread.Sleep(pollIntervalMs);
                }

                _uiLogger.WarnRaw("处理错误: {0} - {1}",
                    $"料仓{binNumber}无料", "超时判空");
                return BinMaterialCheckResult.CreateEmpty(binNumber, true);
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                    $"料仓{binNumber}检测失败", ex.Message);
                return BinMaterialCheckResult.CreateFailure(binNumber);
            }
            finally
            {
                StopBinAxis(binNumber, axisId);
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
                    if (IsUnloadingActiveOrPending())
                    {
                        _uiLogger.InfoRaw("处理已开始: {0}", "检测到下料请求，跳过料仓下降");
                    }
                    else
                    {
                        ResetSelectedBinStates(BinElevatorMode.Loading);
                    }
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
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "机械手信号检查", ex.Message);
            }
        }

        private bool IsUnloadingActiveOrPending()
        {
            if (_sharedState == null)
            {
                return false;
            }

            return _sharedState.IsUnloading() || _sharedState.HasUnloadingPriorityRequest();
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
                        _uiLogger.WarnRaw("处理错误: {0} - {1}", 
                            "料仓" + binNumber + "未知的料仓状态机模式", _binElevatorMode.ToString());
                        break;
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", 
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
                        //_uiLogger.Info("处理已开始: {0}", 
                        //    "料仓" + binNumber + "初始化：已在感应位置，准备下降");
                    }
                    break;
                    
                case BinElevatorState.Moving:
                    if (ReadBinSensor(binNumber))
                    {
                        StopBinAxis(binNumber, axisId);
                        //currentState = BinElevatorState.Elevated;
                        //_uiLogger.Info("处理已完成: {0}",
                        //    "料仓" + binNumber + "初始化：已到达感应位置，准备下降");
                    }
                    break;
                    
                case BinElevatorState.Elevated:
                    // 第二步：从感应位置下降
                    StartBinJogDown(binNumber, axisId);
                    currentState = BinElevatorState.Lowered; // 标记为正在下降
                    //_uiLogger.Info("处理已开始: {0}", 
                    //    "料仓" + binNumber + "初始化：开始从感应位置下降");
                    break;


                    
                case BinElevatorState.Lowered:
                    // 正在下降中，检查是否脱离感应位置
                    if (!ReadBinSensor(binNumber))
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
                            _uiLogger.InfoRaw("处理已完成: {0}", 
                                "所有料仓初始化完成，切换到停止模式");

                            // 重置标志
                            _bin1ReachedSensor = false;
                            _bin2ReachedSensor = false;
                            _bin3ReachedSensor = false;
                        }

                        currentState = BinElevatorState.Stopped;
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
                    }
                    else
                    {
                        // 感应器已经为false，直接完成
                        currentState = BinElevatorState.Stopped;
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
        /// 状态机4: 下料模式 - 指定料仓上升至有料感应后停止
        /// </summary>
        private void ProcessUnloadingMode(int binNumber, int axisId, ref BinElevatorState currentState)
        {
            bool isActiveBin = _activeUnloadingBin != 0 && _activeUnloadingBin == binNumber;
            if (!isActiveBin)
            {
                if (currentState == BinElevatorState.Moving)
                {
                    StopBinAxis(binNumber, axisId);
                    currentState = BinElevatorState.Stopped;
                }
                return;
            }

            bool detectionActive = _materialDetectionInProgress && _materialDetectionBin == binNumber;
            double elapsedMs = detectionActive
                ? (DateTime.UtcNow - _materialDetectionStartTime).TotalMilliseconds
                : 0;

            switch (currentState)
            {
                case BinElevatorState.Unknown:
                    if (ReadBinSensor(binNumber))
                    {
                        HandleUnloadingReached(binNumber, detectionActive, false);
                        currentState = BinElevatorState.Stopped;
                    }
                    else
                    {
                        StartBinJogUp(binNumber, axisId);
                        currentState = BinElevatorState.Moving;
                    }
                    break;

                case BinElevatorState.Moving:
                    if (ReadBinSensor(binNumber))
                    {
                        StopBinAxis(binNumber, axisId);
                        HandleUnloadingReached(binNumber, detectionActive, false);
                        currentState = BinElevatorState.Stopped;
                    }
                    else if (detectionActive && elapsedMs >= _materialDetectionTimeoutMs)
                    {
                        StopBinAxis(binNumber, axisId);
                        HandleUnloadingReached(binNumber, true, true);
                        currentState = BinElevatorState.Stopped;
                    }
                    break;

                case BinElevatorState.Stopped:
                    if (detectionActive)
                    {
                        if (ReadBinSensor(binNumber))
                        {
                            HandleUnloadingReached(binNumber, true, false);
                        }
                        else if (elapsedMs >= _materialDetectionTimeoutMs)
                        {
                            HandleUnloadingReached(binNumber, true, true);
                        }
                    }
                    break;

                default:
                    currentState = BinElevatorState.Unknown;
                    break;
            }
        }

        private void HandleUnloadingReached(int binNumber, bool detectionActive, bool timedOut)
        {
            if (detectionActive)
            {
                CompleteMaterialCheck(binNumber, !timedOut, timedOut);
            }
            else
            {
                CompleteUnloadingRaise(binNumber);
            }
        }

        private void CompleteUnloadingRaise(int binNumber)
        {
            switch (binNumber)
            {
                case 1:
                    _bin1State = BinElevatorState.Stopped;
                    break;
                case 2:
                    _bin2State = BinElevatorState.Stopped;
                    break;
                case 3:
                    _bin3State = BinElevatorState.Stopped;
                    break;
            }

            _activeUnloadingBin = 0;
            _binElevatorMode = BinElevatorMode.Stopped;

            _uiLogger.InfoRaw("处理已完成: {0}", $"料仓{binNumber}已上升至有料感应位置");
        }

        private void CompleteMaterialCheck(int binNumber, bool hasMaterial, bool timedOut)
        {
            if (!_materialDetectionInProgress || _materialDetectionBin != binNumber)
            {
                return;
            }

            _materialCheckResult = hasMaterial
                ? BinMaterialCheckResult.CreateHasMaterial(binNumber)
                : BinMaterialCheckResult.CreateEmpty(binNumber, timedOut);

            _materialDetectionInProgress = false;
            _materialDetectionBin = 0;
            _materialDetectionStartTime = DateTime.MinValue;
            _activeUnloadingBin = 0;
            _binElevatorMode = BinElevatorMode.Stopped;

            _materialCheckEvent.Set();

            if (hasMaterial)
            {
                _uiLogger.InfoRaw("处理已完成: {0}", $"料仓{binNumber}检测到物料");
            }
            else
            {
                string reason = timedOut ? "超时判空" : "检测判空";
                _uiLogger.WarnRaw("处理错误: {0} - {1}", $"料仓{binNumber}无料", reason);
            }
        }

        private BinMaterialCheckResult CompleteMaterialCheckTimeout(int binNumber)
        {
            int axisId = GetAxisIdByBin(binNumber);
            if (axisId >= 0)
            {
                StopBinAxis(binNumber, axisId);
            }

            CompleteMaterialCheck(binNumber, false, true);
            return _materialCheckResult ?? BinMaterialCheckResult.CreateEmpty(binNumber, true);
        }

        private void CancelMaterialDetection(string reason)
        {
            if (!_materialDetectionInProgress)
            {
                return;
            }

            int binNumber = _materialDetectionBin;
            int axisId = GetAxisIdByBin(binNumber);
            if (axisId >= 0)
            {
                StopBinAxis(binNumber, axisId);
            }

            CompleteMaterialCheck(binNumber, false, false);
            _uiLogger.WarnRaw("处理错误: {0} - {1}", $"料仓{binNumber}物料检测被中断", reason);
        }

        private int GetAxisIdByBin(int binNumber)
        {
            switch (binNumber)
            {
                case 1:
                    return BIN1_AXIS_ID;
                case 2:
                    return BIN2_AXIS_ID;
                case 3:
                    return BIN3_AXIS_ID;
                default:
                    return -1;
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
                        _uiLogger.InfoRaw("处理已开始: {0}",
                            "料仓" + binNumber + "开始Jog下降，速度:" + axisConfig.Speed);
                    }
                    else
                    {
                        _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                            "料仓" + binNumber + "轴配置未找到", "轴ID:" + axisId);
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
                        _uiLogger.InfoRaw("处理已开始: {0}",
                            "料仓" + binNumber + "开始Jog上升，速度:" + axisConfig.Speed);
                    }
                    else
                    {
                        _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                            "料仓" + binNumber + "轴配置未找到", "轴ID:" + axisId);
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
                        //_uiLogger.Info("处理已开始: {0}", 
                        //    "料仓" + binNumber + "Jog停止");
                    }
                    else
                    {
                        _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                            "料仓" + binNumber + "轴配置未找到", "轴ID:" + axisId);
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
            if (mode == BinElevatorMode.Unloading)
            {
                _binElevatorMode = BinElevatorMode.Stopped;
                _activeUnloadingBin = 0;
            }
            else
            {
                _binElevatorMode = mode;
            }
            
            // 根据Y11/Y12/Y13信号选择性重置料仓状态
            if (_ioManager.LayeredIO.ReadOutBit(BIN1_SELECT_SIGNAL))
            {
                _bin1State = mode == BinElevatorMode.Unloading ? BinElevatorState.Stopped : BinElevatorState.Unknown;
            }
            if (_ioManager.LayeredIO.ReadOutBit(BIN2_SELECT_SIGNAL))
            {
                _bin2State = mode == BinElevatorMode.Unloading ? BinElevatorState.Stopped : BinElevatorState.Unknown;
            }
            if (_ioManager.LayeredIO.ReadOutBit(BIN3_SELECT_SIGNAL))
            {
                _bin3State = mode == BinElevatorMode.Unloading ? BinElevatorState.Stopped : BinElevatorState.Unknown;
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
                _uiLogger.ErrorRaw("读取 {0} 错误: {1}", 
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
                _uiLogger.InfoRaw("处理已开始: {0}", "停止所有料仓Jog运动");
                
                // 停止所有轴的Jog移动
                StopBinAxis(1, BIN1_AXIS_ID);
                StopBinAxis(2, BIN2_AXIS_ID);
                StopBinAxis(3, BIN3_AXIS_ID);
                
                // 重置状态
                _bin1State = BinElevatorState.Unknown;
                _bin2State = BinElevatorState.Unknown;
                _bin3State = BinElevatorState.Unknown;
                _activeUnloadingBin = 0;
                CancelMaterialDetection("停止所有料仓运动");
                
                _uiLogger.InfoRaw("处理已完成: {0}", "所有料仓Jog运动已停止");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "停止料仓Jog运动", ex.Message);
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
            //                    _uiLogger.Info("处理已开始: {0}", 
            //                        "料仓升降系统" + (statusMsg.IsStarted ? "启动" : "停止"));

            //                    if (!statusMsg.IsStarted)
            //                    {
            //                        // 系统停止时，停止所有升降动作
            //                        StopAllBinMovements();
            //                    }
            //                    break;

            //                case SystemStatusChangeType.SystemModeChanged:
            //                    _currentMode = statusMsg.SystemMode;
            //                    _uiLogger.Info("处理已开始: {0}", 
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
            //    _uiLogger.Error("处理错误: {0} - {1}", 
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

    /// <summary>
    /// 料仓物料检测结果
    /// </summary>
    public class BinMaterialCheckResult
    {
        public int BinNumber { get; }
        public bool HasMaterial { get; }
        public bool IsBinEmpty { get; }
        public bool TimedOut { get; }

        private BinMaterialCheckResult(int binNumber, bool hasMaterial, bool isBinEmpty, bool timedOut)
        {
            BinNumber = binNumber;
            HasMaterial = hasMaterial;
            IsBinEmpty = isBinEmpty;
            TimedOut = timedOut;
        }

        public static BinMaterialCheckResult CreateHasMaterial(int binNumber)
            => new BinMaterialCheckResult(binNumber, true, false, false);

        public static BinMaterialCheckResult CreateEmpty(int binNumber, bool timedOut)
            => new BinMaterialCheckResult(binNumber, false, true, timedOut);

        public static BinMaterialCheckResult CreateFailure(int binNumber)
            => new BinMaterialCheckResult(binNumber, false, true, false);

        public static BinMaterialCheckResult CreatePending(int binNumber)
            => new BinMaterialCheckResult(binNumber, false, false, false);
    }
}
