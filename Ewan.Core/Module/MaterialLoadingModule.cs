using Ewan.Core.IO;
using Ewan.Core.Msg;
using Ewan.Core.ScanCode;
using Ewan.Model.Production;
using Ewan.Model.System;
using System;
using System.Threading;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 物料装载模块
    /// 负责将扫码识别后的料片装载到对应的料仓中
    /// </summary>
    public class MaterialLoadingModule : BaseModule<MaterialLoadingModule>
    {
        private MaterialLoadingState _currentState = MaterialLoadingState.Idle;
        private readonly object _stateLock = new object();
        private int _scanInterval = 1; // 扫描间隔，毫秒
        private bool _emergencyStopTriggered = false;
        private bool _loadingRequested = false;
        private bool _stopRequested = false;
        private bool _initialized = false; // 初始化标志
        private bool _loadingEnabled = true;
        private volatile bool _beltStopRequested = false;

        // 共享状态（用于与其他模块通信）
        private ProductionLineSharedState _sharedState;

        private LayeredIOManager _ioManager;
        private readonly SystemParametersManager _parametersManager = SystemParametersManager.Instance;

        // 输入信号常量
        private const int MATERIAL_DETECT_SIGNAL = 3;      // 料片检测信号X3
        private const int SCAN_POSITION_SIGNAL = 7;         // 到达扫码位置信号X7
        private const int ROBOT_BUSY_SIGNAL = 20;           // IN20 - 机械手忙碌

        // 输出信号常量 - 初始化相关
        private const int OUT_START = 5;                   // OUT5 - 开始信号
        private const int OUT_STOP = 6;                    // OUT6 - 停止信号
        private const int OUT_ALLOW_PICK = 14;             // OUT14 - 触发机械手皮带线允许取料
        private const int OUT_SEND_PICK_CMD = 15;          // OUT15 - 发送取料指令
        private const int OUT_HIGH_SPEED = 16;             // OUT16 - 高速运行

        // 输出信号常量 - 流程控制
        private const int OUT_SCAN_COMPLETE = 9;           // OUT9 - 发送扫码完成信号
        private const int TRIGGER_LOADING_SIGNAL = 10;     // OUT10 - 触发放入料仓信号

        // 料仓选择信号IO配置
        private const int BIN1_SELECT_SIGNAL = 11;         // OUT11 - 料仓1选择信号
        private const int BIN2_SELECT_SIGNAL = 12;         // OUT12 - 料仓2选择信号
        private const int BIN3_SELECT_SIGNAL = 13;         // OUT13 - 料仓3选择信号

        
        /// <summary>
        /// 带共享状态的构造函数
        /// </summary>
        /// <param name="sharedState">共享状态对象</param>
        public MaterialLoadingModule(ProductionLineSharedState sharedState)
        {
            _sharedState = sharedState;
        }

        protected override void OnInit()
        {
            _uiLogger.InfoRaw("模块初始化成功: {0}", "MaterialLoadingModule");

            _ioManager = LayeredIOManager.Instance();

            // 初始化状态（不执行硬件初始化，等待外部调用）
            lock (_stateLock)
            {
                _currentState = MaterialLoadingState.Idle;
                _emergencyStopTriggered = false;
            }
        }

        /// <summary>
        /// 执行上料机硬件初始化序列（公共方法，供外部调用）
        /// 按照顺序：OUT6(true)->等0.5s->OUT6(false)->等0.5s->OUT5(true)->等0.5s->OUT5(false)->OUT14(true)
        /// 注意：不再设置OUT16速度，由外部调用者控制速度模式（复位时低速，运行时高速）
        /// 初始化过程中绿灯闪烁，完成后黄灯闪烁
        /// </summary>
        public void PerformHardwareInitialization()
        {
            PerformInitialization();
        }

        /// <summary>
        /// 执行上料机初始化序列（内部实现）
        /// 按照顺序：OUT6(true)->等0.5s->OUT6(false)->等0.5s->OUT5(true)->等0.5s->OUT5(false)->OUT14(true)
        /// 注意：不再设置OUT16速度，由外部调用者控制速度模式（复位时低速，运行时高速）
        /// 初始化过程中绿灯闪烁，完成后黄灯闪烁
        /// </summary>
        private void PerformInitialization()
        {
            try
            {
                _uiLogger.InfoRaw("处理已开始: {0}", "上料机初始化开始");

                // 发送初始化中状态 - 绿灯闪烁
                SendStatusMessage(SystemStatus.Running, "上料机初始化中");

                // 步骤1: OUT_STOP置位true（停止信号）
                _ioManager.LayeredIO.WriteOutBit(OUT_STOP, true);
                _appLogger.Info("OUT_STOP置位true");
                Thread.Sleep(500);

                // 步骤2: OUT_STOP置位false
                _ioManager.LayeredIO.WriteOutBit(OUT_STOP, false);
                _appLogger.Info("OUT_STOP置位false");
                Thread.Sleep(500);

                // 注意：移除了OUT_HIGH_SPEED的设置，由外部调用者控制速度
                // 复位时保持低速，自动运行时切换高速

                // 步骤3: OUT_START置位true（开始信号）
                _ioManager.LayeredIO.WriteOutBit(OUT_START, true);
                _appLogger.Info("OUT_START置位true");
                Thread.Sleep(500);

                // 步骤4: OUT_START置位false
                _ioManager.LayeredIO.WriteOutBit(OUT_START, false);
                _appLogger.Info("OUT_START置位false");

                // 步骤5: OUT_ALLOW_PICK置位false
                _ioManager.LayeredIO.WriteOutBit(OUT_ALLOW_PICK, false);
                _appLogger.Info("OUT_ALLOW_PICK置位true");

                _initialized = true;

                // 发送初始化完成状态 - 黄灯闪烁
                SendStatusMessage(SystemStatus.Warning, "上料机初始化完成");

                _uiLogger.InfoRaw("处理已完成: {0}", "上料机初始化完成");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("模块运行错误: {0} - {1}", "上料机初始化失败: " + ex.Message);
                _initialized = false;

                // 发送初始化失败状态 - 红灯闪烁
                SendStatusMessage(SystemStatus.Alarm, "上料机初始化失败: " + ex.Message);
            }
        }

        protected override bool OnRun()
        {
            try
            {
                var parameters = _parametersManager.Parameters;
                if (!parameters.EnableLoadingModule)
                {
                    if (_loadingEnabled)
                    {
                        ForceStopLoading();
                        _loadingEnabled = false;
                    }

                    Thread.Sleep(_scanInterval);
                    return true;
                }

                if (!_loadingEnabled)
                {
                    _loadingEnabled = true;
                }

                // 暂停时保持当前状态，不执行状态机
                if (_sharedState?.IsSystemPaused() == true)
                {
                    Thread.Sleep(_scanInterval);
                    return true;
                }
                var stateSnapshot = MaterialLoadingState.Idle;
                lock (_stateLock)
                {
                    switch (_currentState)
                    {
                        case MaterialLoadingState.Idle:
                            ProcessIdleState();
                            break;
                            
                        case MaterialLoadingState.PickingMaterial:
                            ProcessPickingMaterial();
                            break;
                            
                        case MaterialLoadingState.AtScanPosition:
                            ProcessAtScanPosition();
                            break;
                            
                        case MaterialLoadingState.MovingToBinByScanInfo:
                            ProcessMovingToBin();
                            break;
                                               
                        case MaterialLoadingState.Stopped:
                            // 停止状态，等待重新启动
                            break;
                            
                        default:
                            _currentState = MaterialLoadingState.Idle;
                            break;
                    }

                    stateSnapshot = _currentState;
                }

                UpdateBeltConveyorControl(stateSnapshot);
                
                Thread.Sleep(_scanInterval);
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("模块运行错误: {0} - {1}", "MaterialLoadingModule: " + ex.Message);
                _currentState = MaterialLoadingState.Idle;
                Thread.Sleep(1000);
                return true;
            }
        }

        protected override void OnDestroy()
        {
            _uiLogger.InfoRaw("模块已销毁: {0}", "MaterialLoadingModule");
            ForceReleaseBeltControl("装料模块销毁");
        }

        #region 核心流程处理

        /// <summary>
        /// 处理Idle状态
        /// </summary>
        private void ProcessIdleState()
        {
            // 检查是否有下料优先级请求
            if (_sharedState?.HasUnloadingPriorityRequest() == true)
            {                
                _uiLogger.InfoRaw("处理已开始: {0}", 
                    "检测到下料优先级请求，禁止自动取料(OUT14=false)，等待下料完成");
                
                // 保持Idle状态，不启动新装料
                return;
            }
            
            // 无下料请求，检查是否有料片检测信号
            if (_ioManager.LayeredIO.ReadInBit(MATERIAL_DETECT_SIGNAL) && 
                _sharedState?.TryStartLoading() == true)
            {
                // 有料片检测信号，允许取料
                _ioManager.LayeredIO.WriteOutBit(OUT_ALLOW_PICK, true);
                
                _sharedState?.MarkLoadingInProgress();
                
                _currentState = MaterialLoadingState.PickingMaterial;
                _uiLogger.InfoRaw("处理已开始: {0}", "检测到料片信号(X3=true)，允许取料(OUT14=true)，开始装料流程");
            }
        }



        /// <summary>
        /// 处理取料状态
        /// </summary>
        private void ProcessPickingMaterial()
        {
            // 在取料过程中检测是否到达扫码位置X7
            if (_ioManager.LayeredIO.ReadInBit(SCAN_POSITION_SIGNAL))
            {
                _currentState = MaterialLoadingState.AtScanPosition;
                _uiLogger.InfoRaw("处理已完成: {0}", "料片已到达扫码位置(X7=true)，开始扫码流程");
            }
        }

        /// <summary>
        /// 处理到达扫码位置状态
        /// </summary>
        private void ProcessAtScanPosition()
        {
            _ioManager.LayeredIO.WriteOutBit(OUT_ALLOW_PICK, false);

            DLManager.Instance().TriggerScan(); // 触发扫码,调试模式不需要结果
                                                //if(DLManager.Instance().TriggerScan() != "")
                                                //{

            _ioManager.LayeredIO.WriteOutBit(OUT_SCAN_COMPLETE, true);  // 扫码完成


            var targetBin = GetConfiguredBinNumber();
            SetBinSelectSignal(targetBin);

                // 触发放入料仓信号
                _ioManager.LayeredIO.WriteOutBit(TRIGGER_LOADING_SIGNAL, true);
                
                // 转换到移动到料仓状态
                _currentState = MaterialLoadingState.MovingToBinByScanInfo;
                _uiLogger.InfoRaw("处理已开始: {0}", "开始移动到料仓位置");
            //}
        }

        /// <summary>
        /// 处理移动到料仓位置状态
        /// 等待下料完成信号
        /// </summary>
        private void ProcessMovingToBin()
        {
            // 检查下料完成状态
            bool loadingCompleted = GetLoadingCompleted();
            
            if (loadingCompleted)
            {

                // 清除装料信号
                _ioManager.LayeredIO.WriteOutBit(TRIGGER_LOADING_SIGNAL, false);
                ClearBinSelectSignals();
                _ioManager.LayeredIO.WriteOutBit(OUT_SCAN_COMPLETE, false);

                // 等待0.3秒确保机械手已离开
                Thread.Sleep(300);
                
                // 装料完成，清除IN20脉冲标志
                _sharedState?.ClearLoadingInProgress();
                
                // 释放流程锁
                _sharedState?.FinishProcess();
                SetLoadingCompleted(false);
                _currentState = MaterialLoadingState.Idle;
                
                _uiLogger.InfoRaw("处理已完成: {0}", "装料完成，清除IN20脉冲标志，释放流程锁，回到Idle状态");
           
            }
        }

#region 料仓选择控制

        private int GetConfiguredBinNumber()
        {
            var selected = _parametersManager.Parameters.LoadingBinSelection;
            switch (selected)
            {
                case BinSelection.Bin2:
                    return 2;
                case BinSelection.Bin3:
                    return 3;
                default:
                    return 1;
            }
        }

        private void SetBinSelectSignal(int binNumber)
        {
            if (_ioManager?.LayeredIO == null)
            {
                return;
            }

            ClearBinSelectSignals();

            switch (binNumber)
            {
                case 1:
                    _ioManager.LayeredIO.WriteOutBit(BIN1_SELECT_SIGNAL, true);
                    break;
                case 2:
                    _ioManager.LayeredIO.WriteOutBit(BIN2_SELECT_SIGNAL, true);
                    break;
                case 3:
                    _ioManager.LayeredIO.WriteOutBit(BIN3_SELECT_SIGNAL, true);
                    break;
            }
        }

        private void ClearBinSelectSignals()
        {
            if (_ioManager?.LayeredIO == null)
            {
                return;
            }

            _ioManager.LayeredIO.WriteOutBit(BIN1_SELECT_SIGNAL, false);
            _ioManager.LayeredIO.WriteOutBit(BIN2_SELECT_SIGNAL, false);
            _ioManager.LayeredIO.WriteOutBit(BIN3_SELECT_SIGNAL, false);
        }

#endregion

        #endregion

        #region 公共方法

        /// <summary>
        /// 发送系统状态消息到状态指示器模块
        /// </summary>
        private void SendStatusMessage(SystemStatus status, string description, bool isCritical = false)
        {
            try
            {
                var command = new StatusIndicatorCommand(status, description, isCritical);
                var message = new MessageModel(MsgSubject.StatusIndicator, command);

                MsgManager.Instance().PushMsg(message);

                _uiLogger.DebugRaw("发送状态消息: {0} - {1}", status, description);
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("模块运行错误: {0} - {1}",
                    "MaterialLoadingModule-SendStatusMessage", ex.Message);
            }
        }

        /// <summary>
        /// 停止装载流程
        /// </summary>
        public void StopLoading()
        {
            lock (_stateLock)
            {
                _stopRequested = true;
                _loadingRequested = false;
                _uiLogger.InfoRaw("处理已开始: {0}", "请求停止装载");
            }
        }

        /// <summary>
        /// 强制停止装载流程
        /// </summary>
        public void ForceStopLoading()
        {
            lock (_stateLock)
            {
                // 清除所有相关输出信号，确保设备回到初始安全状态
                try
                {
                    if (_ioManager?.LayeredIO != null)
                    {
                        _ioManager.LayeredIO.WriteOutBit(OUT_ALLOW_PICK, false);
                        _ioManager.LayeredIO.WriteOutBit(OUT_SEND_PICK_CMD, false);
                        _ioManager.LayeredIO.WriteOutBit(TRIGGER_LOADING_SIGNAL, false);
                        _ioManager.LayeredIO.WriteOutBit(OUT_SCAN_COMPLETE, false);
                        ClearBinSelectSignals();
                        _ioManager.LayeredIO.WriteOutBit(OUT_START, false);
                        _ioManager.LayeredIO.WriteOutBit(OUT_STOP, false);
                        _ioManager.LayeredIO.WriteOutBit(OUT_HIGH_SPEED, false);
                    }
                }
                catch (Exception ex)
                {
                    _uiLogger.ErrorRaw("处理错误: {0} - {1}", "强制停止装载清除输出", ex.Message);
                }

                _currentState = MaterialLoadingState.Idle;
                _stopRequested = false;
                _loadingRequested = false;
                
                _sharedState?.ClearLoadingInProgress();
                _sharedState?.FinishProcess();
                SetLoadingCompleted(false);

                ForceReleaseBeltControl("装料强制停止");

                
                _uiLogger.InfoRaw("处理已完成: {0}", "强制停止装载");
            }
        }




        /// <summary>
        /// 消息回调处理（向后兼容性，仅在无共享状态时使用）
        /// </summary>
        private void CallBackShow(MessageModel msg)
        {
            if (_sharedState == null)
            {
                // 兼容性模式：通过本地变量处理
                var data = msg.GetData<MaterialOperationStatus>();
                // _loadingcomplete = data.LoadingCompleted;
                // 注意：需要添加本地变量支持兼容性模式
            }
        }

        #region 皮带控制

        private void UpdateBeltConveyorControl(MaterialLoadingState stateSnapshot)
        {
            try
            {
                if (_ioManager?.LayeredIO == null)
                {
                    return;
                }

                bool robotBusy = _ioManager.LayeredIO.ReadInBit(ROBOT_BUSY_SIGNAL);
                bool shouldStop = stateSnapshot == MaterialLoadingState.PickingMaterial && robotBusy;

                if (shouldStop == _beltStopRequested)
                {
                    return;
                }

                _beltStopRequested = shouldStop;

                var command = new BeltConveyorControlCommand(
                    BeltConveyorControlSource.MaterialLoading,
                    shouldStop,
                    shouldStop ? "机械手正在取料(IN20=1)" : "装料流程完成，释放皮带");

                MsgManager.Instance().PushMsg(new MessageModel(MsgSubject.BeltConveyorControl, command));
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                    "装料皮带控制", ex.Message);
            }
        }

        private void ForceReleaseBeltControl(string reason)
        {
            if (!_beltStopRequested)
            {
                return;
            }

            _beltStopRequested = false;

            var command = new BeltConveyorControlCommand(
                BeltConveyorControlSource.MaterialLoading,
                false,
                reason);

            MsgManager.Instance().PushMsg(new MessageModel(MsgSubject.BeltConveyorControl, command));
        }

        #endregion

        #region 共享状态访问方法

        /// <summary>
        /// 获取装载完成状态
        /// </summary>
        private bool GetLoadingCompleted()
        {
            if (_sharedState != null)
            {
                return _sharedState.GetLoadingCompleted();
            }
            // 兼容性：如果没有共享状态，返回默认值
            return false;
        }

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

        #endregion






        #endregion

    }

    /// <summary>
    /// 物料装载状态枚举
    /// </summary>
    public enum MaterialLoadingState
    {
        /// <summary>
        /// 空闲状态
        /// </summary>
        Idle,
        
        /// <summary>
        /// 正在取料
        /// </summary>
        PickingMaterial,
        
        /// <summary>
        /// 到达扫码位置
        /// </summary>
        AtScanPosition,
        
        /// <summary>
        /// 根据扫码信息移动到料仓位置
        /// </summary>
        MovingToBinByScanInfo,
        
        /// <summary>
        /// 停止
        /// </summary>
        Stopped
    }
}