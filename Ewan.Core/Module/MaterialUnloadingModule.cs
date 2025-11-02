using Ewan.Core.IO;
using Ewan.Core.Msg;
using Ewan.Core.Plc;
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

        // 共享状态（用于与其他模块通信）
        private ProductionLineSharedState _sharedState;

        // 消息队列相关
        private MsgListener _msgManager2;

        private LayeredIOManager _ioManager;

        private ModbusRTUManager _modbusRTUManager;
        
        // 环线信号状态（使用电平检测替代上升沿）
        private bool _ringLineSignal = false;      // 当前信号电平（寄存器152的值）
        private bool _requestProcessed = false;    // 是否已处理过此次请求

        // 输入信号常量
        private const int MATERIAL_DETECT_SIGNAL = 3;       // X3 - 料片检测信号
        private const int SCAN_POSITION_SIGNAL = 7;         // X7 - 扫码位置到达信号
        private const int CART_PULSE_SIGNAL = 11;           // X11 - 小车脉冲完成信号

        // 输出信号常量
        private const int OUT_SCAN_COMPLETE = 9;            // OUT9 - 发送扫码完成信号
        private const int OUT_ALLOW_PICK = 14;              // OUT14 - 允许取料信号
        private const int TRIGGER_PICKUP_SIGNAL = 15;       // Y15 - 触发取料信号
        private const int PUT_TO_CART_SIGNAL = 17;          // Y17 - 放入小车信号

        // 料仓选择信号IO配置（复用装料时的信号）
        private const int BIN1_SELECT_SIGNAL = 11; // Y11 - 料仓1选择信号
        private const int BIN2_SELECT_SIGNAL = 12; // Y12 - 料仓2选择信号
        private const int BIN3_SELECT_SIGNAL = 13; // Y13 - 料仓3选择信号

        // Modbus寄存器地址定义
        private const string CART_COMPLETION_REGISTER = "153"; // 放入小车完成寄存器地址
        private const string QR_CODE_START_REGISTER = "180";   // 二维码起始寄存器地址
        private const string QR_CODE_END_REGISTER = "199";     // 二维码结束寄存器地址
        private const string QR_CODE_REGISTER_COUNT = "20";    // 二维码寄存器数量(180-199)

        private int _selectedBin = 1; // 选择的料仓编号 (1, 2, 3)

        // 存储扫码结果
        private string _lastScannedQrCode = string.Empty;

        /// <summary>
        /// 带共享状态的构造函数
        /// </summary>
        /// <param name="sharedState">共享状态对象</param>
        public MaterialUnloadingModule(ProductionLineSharedState sharedState)
        {
            _sharedState = sharedState;
        }

        protected override void OnInit()
        {
            _uiLogger.InfoRaw("模块初始化成功: {0}", "MaterialUnloadingModule");

            _modbusRTUManager = ModbusRTUManager.Instance();

            _ioManager = LayeredIOManager.Instance();

            _msgManager2 = new MsgListener(MsgSubject.RingLineData, CallBackShow1);
            MsgManager.Instance().RegisterListener(_msgManager2);

            // 初始化状态
            lock (_stateLock)
            {
                _currentState = MaterialUnloadingState.Idle;
                _emergencyStopTriggered = false;
                _ringLineSignal = false;
                _requestProcessed = false;
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
                            ProcessIdleState();
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
                _uiLogger.ErrorRaw("模块运行错误: {0} - {1}", "MaterialUnloadingModule: " + ex.Message);
                _currentState = MaterialUnloadingState.Idle;
                Thread.Sleep(1000);
                return true;
            }
        }

        protected override void OnDestroy()
        {
            _uiLogger.InfoRaw("模块已销毁: {0}", "MaterialUnloadingModule");

            // 注销消息监听器
            if (_msgManager2 != null)
            {
                MsgManager.Instance().UnRegisterListener(_msgManager2);
            }
            if (_ioManager != null)
            {
                _ioManager = null;
            }
            if (_modbusRTUManager != null)
            {
                _modbusRTUManager = null;
            }
        }

        #region 核心流程处理

        /// <summary>
        /// 处理Idle状态
        /// </summary>
        private void ProcessIdleState()
        {
            // 使用电平检测替代上升沿检测
            // 信号为1且未处理过时，开始下料流程
            if (_ringLineSignal && !_requestProcessed)
            {
                // 标记已处理，防止重复处理
                _requestProcessed = true;
                
                // 立即记录下料优先级请求
                _sharedState?.RequestUnloadingPriority();
                
                _uiLogger.InfoRaw("处理已开始: {0}", "环线请求下料，设置优先级标志");
                
                // 关键检查：装料流程是否进行中（检查IN20脉冲标志）
                bool loadingInProgress = _sharedState?.IsLoadingInProgress() ?? false;
                
                if (loadingInProgress)
                {
                    // 有IN20脉冲，说明装料流程未完成，不能开始下料
                    _uiLogger.InfoRaw("处理已开始: {0}", 
                        "检测到装料流程进行中(IN20脉冲)，等待装料完成后再下料");
                    // 重置标志，下次循环继续尝试
                    _requestProcessed = false;
                    return;
                }
                
                // 装料流程已完成（无IN20脉冲），尝试获取流程锁并开始下料
                if (_sharedState?.TryStartUnloading() == true)
                {
                    // 成功获取锁，清除优先级请求
                    _sharedState?.ClearUnloadingPriority();
                    
                    // 禁止机械臂自动取料
                    _ioManager.LayeredIO.WriteOutBit(OUT_ALLOW_PICK, false);
                    
                    RequestUnloading(1);
                    _uiLogger.InfoRaw("处理已开始: {0}", "装料流程已完成，开始下料流程");
                }
                else
                {
                    // 获取锁失败，可能是其他流程占用（不太可能发生）
                    _uiLogger.WarnRaw("处理错误: {0} - {1}", 
                        "下料流程", "装料已完成但无法获取流程锁");
                    // 重置标志，稍后重试
                    _requestProcessed = false;
                }
            }
            // 信号变为0时，清除处理标志，准备接收下次请求
            else if (!_ringLineSignal && _requestProcessed)
            {
                _requestProcessed = false;
                _uiLogger.InfoRaw("处理已完成: {0}", "环线信号结束，准备接收下次请求");
            }
        }

        /// <summary>
        /// 处理正在取料状态
        /// </summary>
        private void ProcessPickingMaterial()
        {
            // 检查取料完成状态 - 从SharedState读取（BinElevatorModule通过X10信号更新这个状态）
            bool unloadingCompleted = _sharedState?.GetUnloadingCompleted() == true;
            
            if (unloadingCompleted)
            {
                // 取料完成，清除SharedState标志
                _sharedState.SetUnloadingCompleted(false);

                // 清除信号
                _ioManager.LayeredIO.WriteOutBit(TRIGGER_PICKUP_SIGNAL, false);
                ClearBinSelectSignals();

                // 转换到等待扫码位置状态
                _currentState = MaterialUnloadingState.WaitingForScanPosition;

                _uiLogger.InfoRaw("处理已完成: {0}", "取料完成，等待到达扫码位置");
            }
        }

        /// <summary>
        /// 处理等待到达扫码位置状态
        /// </summary>
        private void ProcessWaitingForScanPosition()
        {
            // 使用边缘检测读取扫码位置脉冲信号X7
            if (_ioManager.LayeredIO.ReadRisingBit(SCAN_POSITION_SIGNAL))
            {
                // 清除当前边缘检测状态
                _ioManager.LayeredIO.ClearRisingBit(SCAN_POSITION_SIGNAL);

                _currentState = MaterialUnloadingState.Scanning;
                _uiLogger.InfoRaw("处理已开始: {0}", "已到达扫码位置，开始扫码");
            }
        }

        /// <summary>
        /// 处理扫码状态
        /// </summary>
        private void ProcessScanning()
        {
            string scannedCode = DLManager.Instance().TriggerScan();
            if (!string.IsNullOrEmpty(scannedCode))
            {
                // 保存扫码结果
                _lastScannedQrCode = scannedCode;

                // 发送扫码完成信号给机械臂
                _ioManager.LayeredIO.WriteOutBit(OUT_SCAN_COMPLETE, true);

                _currentState = MaterialUnloadingState.PuttingToCart;

                // 发送放入小车信号
                _ioManager.LayeredIO.WriteOutBit(PUT_TO_CART_SIGNAL, true);
                _uiLogger.InfoRaw("处理已开始: {0}", $"扫码完成: {scannedCode}，发送扫码完成信号(OUT9)，放入小车");
            }
        }

        /// <summary>
        /// 处理放入小车状态
        /// </summary>
        private void ProcessPuttingToCart()
        {
            // 检查小车脉冲信号X11
            if (_ioManager.LayeredIO.ReadRisingBit(CART_PULSE_SIGNAL))
            {
                // 清除小车脉冲信号X11的边缘检测状态
                _ioManager.LayeredIO.ClearRisingBit(CART_PULSE_SIGNAL);

                // 清除放入小车信号
                _ioManager.LayeredIO.WriteOutBit(PUT_TO_CART_SIGNAL, false);

                // 清除扫码完成信号
                _ioManager.LayeredIO.WriteOutBit(OUT_SCAN_COMPLETE, false);

                // 发送完成信号到Modbus寄存器153
                SendCartCompletionToModbus();

                // 清除下料优先级请求
                _sharedState?.ClearUnloadingPriority();

                // 恢复允许取料（由Idle状态根据X3控制）
                // 这里先恢复为true，让Idle状态接管控制
                _ioManager.LayeredIO.WriteOutBit(OUT_ALLOW_PICK, true);

                // 释放流程锁
                _sharedState?.FinishProcess();

                // 重置标志并返回空闲状态
                _unloadingRequested = false;
                _lastScannedQrCode = string.Empty; // 清空扫码结果
                _currentState = MaterialUnloadingState.Idle;

                _uiLogger.InfoRaw("处理已完成: {0}", 
                    "下料完成，清除优先级请求，恢复允许取料(OUT14=true)，释放流程锁");
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
                    _uiLogger.ErrorRaw("处理错误: {0} - {1}", $"无效的料仓编号: {binNumber}");
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
                    _uiLogger.WarnRaw("处理错误: {0} - {1}", $"当前状态为 {_currentState}，无法开始新的卸料流程");
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
                
                _uiLogger.InfoRaw("处理已开始: {0}", $"请求从料仓{binNumber}开始取料");
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
                _uiLogger.InfoRaw("处理已开始: {0}", "请求停止卸料");
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
                _ioManager.LayeredIO.WriteOutBit(OUT_SCAN_COMPLETE, false);
                _ioManager.LayeredIO.WriteOutBit(TRIGGER_PICKUP_SIGNAL, false);
                _ioManager.LayeredIO.WriteOutBit(PUT_TO_CART_SIGNAL, false);
                ClearBinSelectSignals();

                // 重置状态
                _currentState = MaterialUnloadingState.Idle;
                _stopRequested = false;
                _unloadingRequested = false;
                _ringLineSignal = false;     // 清除信号电平标志
                _requestProcessed = false;   // 清除处理标志

                _uiLogger.InfoRaw("处理已完成: {0}", "强制停止卸料，所有信号已清除");
            }
        }

        private void CallBackShow1(MessageModel msg)
        {
            var data = msg.GetData<RingLineModel>();
            // 使用电平而非边缘检测，避免上升沿丢失
            _ringLineSignal = data.IsLoading;
        }

        /// <summary>
        /// 发送放入小车完成信号到Modbus寄存器153
        /// </summary>
        private void SendCartCompletionToModbus()
        {
            try
            {
                // 发送完成信号值为1到寄存器153 (u16类型)
                _modbusRTUManager.WriteAny(CART_COMPLETION_REGISTER, (ushort)1);
                _uiLogger.InfoRaw("处理已完成: {0}", $"放入小车完成信号已发送到寄存器{CART_COMPLETION_REGISTER}");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", $"发送完成信号到Modbus失败: {ex.Message}");
            }
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
