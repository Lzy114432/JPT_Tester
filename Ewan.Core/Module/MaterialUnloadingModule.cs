using Ewan.Core.IO;
using Ewan.Core.Msg;
using Ewan.Core.ScanCode;
using Ewan.Model;
using Ewan.Model.Production;
using System;
using System.Threading;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 物料卸载模块
    /// 负责将料仓中的料片卸载到指定位置
    /// </summary>
    public class MaterialUnloadingModule : BaseModule<MaterialUnloadingModule>
    {
        private MaterialUnloadingState _currentState = MaterialUnloadingState.Idle;
        private readonly object _stateLock = new object();
        private int _scanInterval = 100; // 扫描间隔，毫秒
        private bool _emergencyStopTriggered = false;
        private bool _unloadingRequested = false;
        private bool _stopRequested = false;
        
        // 消息队列相关
        private MsgListener _msgManager;

        private MsgListener _msgManager2;

        private LayeredIOManager _ioManager;

        // 扫码位置到达信号X7
        private const int SCAN_POSITION_SIGNAL = 7;

        // 小车脉冲完成信号X11
        private const int CART_PULSE_SIGNAL = 11;

        // 触发取料信号Y15
        private const int TRIGGER_PICKUP_SIGNAL = 15;

        // 放入小车信号Y17
        private const int PUT_TO_CART_SIGNAL = 17;

        // 料仓选择信号IO配置（复用装料时的信号）
        private const int BIN1_SELECT_SIGNAL = 11; // Y11 - 料仓1选择信号
        private const int BIN2_SELECT_SIGNAL = 12; // Y12 - 料仓2选择信号
        private const int BIN3_SELECT_SIGNAL = 13; // Y13 - 料仓3选择信号

        private bool _unloadingcomplete = false;
        private int _selectedBin = 1; // 选择的料仓编号 (1, 2, 3)


        private bool _ringLineunload = false;

        protected override void OnInit()
        {
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "MaterialUnloadingModule");
            
            _ioManager = LayeredIOManager.Instance();

            _msgManager = new MsgListener(MsgSubject.LoadingandunloadingState, CallBackShow);
            MsgManager.Instance().RegisterListener(_msgManager);

            _msgManager2 = new MsgListener(MsgSubject.RingLineData, CallBackShow1);
            MsgManager.Instance().RegisterListener(_msgManager2);

            // 初始化状态
            lock (_stateLock)
            {
                _currentState = MaterialUnloadingState.Idle;
                _emergencyStopTriggered = false;
            }
        }

        protected override bool OnRun()
        {
            try
            {
                lock (_stateLock)
                {
                    switch (_currentState)
                    {
                        case MaterialUnloadingState.Idle:
                            // 监控环线信号，触发取料流程
                            if (_ringLineunload && !_unloadingRequested)
                            {
                                // 设置默认料仓为1号（可根据需要修改）
                                RequestUnloading(1);
                                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "环线信号触发，开始取料流程");
                            }
                            break;
                            
                        case MaterialUnloadingState.PickingMaterial:
                            ProcessPickingMaterial();
                            break;
                            
                        case MaterialUnloadingState.WaitingForScanPosition:
                            ProcessWaitingForScanPosition();
                            break;
                            
                        case MaterialUnloadingState.Scanning:
                            ProcessScanning();
                            break;
                            
                        case MaterialUnloadingState.PuttingToCart:
                            ProcessPuttingToCart();
                            break;
                                               
                        case MaterialUnloadingState.Stopped:
                            // 停止状态，等待重新启动
                            break;
                            
                        default:
                            _currentState = MaterialUnloadingState.Idle;
                            break;
                    }
                }
                
                Thread.Sleep(_scanInterval);
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "MaterialUnloadingModule: " + ex.Message);
                _currentState = MaterialUnloadingState.Idle;
                Thread.Sleep(1000);
                return true;
            }
        }

        protected override void OnDestroy()
        {
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "MaterialUnloadingModule");
            
            // 注销消息监听器
            if (_msgManager != null)
            {
                MsgManager.Instance().UnRegisterListener(_msgManager);
            }
        }

        #region 核心流程处理

        /// <summary>
        /// 处理正在取料状态
        /// </summary>
        private void ProcessPickingMaterial()
        {
            // 检查取料完成状态
            if (_unloadingcomplete)
            {
                // 取料完成，清除信号
                _ioManager.LayeredIO.WriteOutBit(TRIGGER_PICKUP_SIGNAL, false);
                ClearBinSelectSignals();
                
                // 重置标志并转换到等待扫码位置状态
                _unloadingcomplete = false;
                _currentState = MaterialUnloadingState.WaitingForScanPosition;
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "取料完成，等待到达扫码位置");
            }
        }

        /// <summary>
        /// 处理等待到达扫码位置状态
        /// </summary>
        private void ProcessWaitingForScanPosition()
        {
            // 检查是否到达扫码位置X7
            if (_ioManager.LayeredIO.ReadInBit(SCAN_POSITION_SIGNAL))
            {
                _currentState = MaterialUnloadingState.Scanning;
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "已到达扫码位置，开始扫码");
            }
        }

        /// <summary>
        /// 处理扫码状态
        /// </summary>
        private void ProcessScanning()
        {
            if (DLManager.Instance().TriggerScan() != "")
            {
                _currentState = MaterialUnloadingState.PuttingToCart;

                // 发送放入小车信号
                _ioManager.LayeredIO.WriteOutBit(PUT_TO_CART_SIGNAL, true);
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "扫码完成，放入小车");
            }
        }

        /// <summary>
        /// 处理放入小车状态
        /// </summary>
        private void ProcessPuttingToCart()
        {
            // 检查小车脉冲信号X11
            if (_ioManager.LayeredIO.ReadInBit(CART_PULSE_SIGNAL))
            {
                // 清除放入小车信号
                _ioManager.LayeredIO.WriteOutBit(PUT_TO_CART_SIGNAL, false);
                
                // 重置标志并返回空闲状态
                _unloadingRequested = false;
                _currentState = MaterialUnloadingState.Idle;
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "物料已放入小车，流程完成");
            }
        }

        /// <summary>
        /// 清除所有料仓选择信号
        /// </summary>
        private void ClearBinSelectSignals()
        {
            _ioManager.LayeredIO.WriteOutBit(BIN1_SELECT_SIGNAL, false);
            _ioManager.LayeredIO.WriteOutBit(BIN2_SELECT_SIGNAL, false);
            _ioManager.LayeredIO.WriteOutBit(BIN3_SELECT_SIGNAL, false);
        }

        /// <summary>
        /// 根据料仓编号设置选择信号
        /// </summary>
        /// <param name="binNumber">料仓编号 (1, 2, 3)</param>
        private void SetBinSelectSignal(int binNumber)
        {
            // 先清除所有信号
            ClearBinSelectSignals();
            
            // 设置对应的料仓选择信号
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
                default:
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, $"无效的料仓编号: {binNumber}");
                    break;
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 请求卸料操作
        /// </summary>
        /// <param name="binNumber">料仓编号 (1, 2, 3)</param>
        public void RequestUnloading(int binNumber = 1)
        {
            lock (_stateLock)
            {
                if (_currentState != MaterialUnloadingState.Idle)
                {
                    _uiLogger.Warn(() => Ewan.Resources.LogMessages.ProcessingError, $"当前状态为 {_currentState}，无法开始新的卸料流程");
                    return;
                }

                _selectedBin = binNumber;
                _unloadingRequested = true;
                
                // 设置料仓选择信号
                SetBinSelectSignal(binNumber);
                
                // 触发取料信号
                _ioManager.LayeredIO.WriteOutBit(TRIGGER_PICKUP_SIGNAL, true);
                
                // 转换到取料状态
                _currentState = MaterialUnloadingState.PickingMaterial;
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, $"请求从料仓{binNumber}开始取料");
            }
        }

        /// <summary>
        /// 停止卸料流程
        /// </summary>
        public void StopUnloading()
        {
            lock (_stateLock)
            {
                _stopRequested = true;
                _unloadingRequested = false;
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "请求停止卸料");
            }
        }

        /// <summary>
        /// 强制停止卸料流程
        /// </summary>
        public void ForceStopUnloading()
        {
            lock (_stateLock)
            {
                // 清除所有输出信号
                _ioManager.LayeredIO.WriteOutBit(TRIGGER_PICKUP_SIGNAL, false);
                _ioManager.LayeredIO.WriteOutBit(PUT_TO_CART_SIGNAL, false);
                ClearBinSelectSignals();
                
                // 重置状态
                _currentState = MaterialUnloadingState.Idle;
                _stopRequested = false;
                _unloadingRequested = false;
                _unloadingcomplete = false;
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "强制停止卸料，所有信号已清除");
            }
        }

        private void CallBackShow(MessageModel msg)
        {
            var data = msg.GetData<MaterialOperationStatus>();
            _unloadingcomplete = data.LoadingCompleted;
        }

        private void CallBackShow1(MessageModel msg)
        {
            var data = msg.GetData<RingLineModel>();
            _ringLineunload = data.IsLoading;
        }


        

        #endregion
    }

    /// <summary>
    /// 物料卸载状态枚举
    /// </summary>
    public enum MaterialUnloadingState
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
        /// 等待到达扫码位置
        /// </summary>
        WaitingForScanPosition,
        
        /// <summary>
        /// 进行扫码
        /// </summary>
        Scanning,
        
        /// <summary>
        /// 放入小车
        /// </summary>
        PuttingToCart,
        
        /// <summary>
        /// 停止
        /// </summary>
        Stopped
    }
}
