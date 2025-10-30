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
        
        // 诊断日志相关
        private long _lastPickingLogTicks = DateTime.Now.Ticks;
        private long _lastIdleLogTicks = DateTime.Now.Ticks; // 最后一次记录Idle状态日志的时间

        // 共享状态（用于与其他模块通信）
        private ProductionLineSharedState _sharedState;

        // 消息队列相关
        private MsgListener _msgManager2;

        private LayeredIOManager _ioManager;

        private ModbusRTUManager _modbusRTUManager;

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

        private bool _ringLineunload = false;
        private bool _lastRingLineunload = false; // 用于边缘检测

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
                _ringLineunload = false;
                _lastRingLineunload = false;
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
                            // 必须同时满足：1.环线要料上升沿(从false变为true)  2.X3=false(无外部料片)  3.能获取流程锁
                            bool ringLineRisingEdge = _ringLineunload && !_lastRingLineunload; // 检测上升沿
                            bool x3Signal = _ioManager.LayeredIO.ReadInBit(MATERIAL_DETECT_SIGNAL);
                            
                            // 诊断日志：定期输出下料条件状态（每5秒记录一次）
                            long currentTicks = DateTime.Now.Ticks;
                            long elapsedSeconds = (currentTicks - _lastIdleLogTicks) / TimeSpan.TicksPerSecond;
                            if (elapsedSeconds >= 5)
                            {
                                var currentProcess = _sharedState?.GetCurrentProcess().ToString() ?? "Unknown";
                                _uiLogger.DebugRaw("[下料诊断-Idle] 环线要料={0}, 上升沿={1}, X3={2}, UnloadingReq={3}, CurrentProcess={4}",
                                    _ringLineunload, ringLineRisingEdge, x3Signal, _unloadingRequested, currentProcess);
                                _lastIdleLogTicks = currentTicks;
                            }
                            
                            if (ringLineRisingEdge &&
                                !_unloadingRequested &&
                                !x3Signal &&  // X3必须为false
                                _sharedState?.TryStartUnloading() == true)
                            {
                                // 禁止后台程序取料，避免与装料流程冲突
                                _ioManager.LayeredIO.WriteOutBit(OUT_ALLOW_PICK, false);

                                // 设置默认料仓为1号（可根据需要修改）
                                RequestUnloading(1);
                                _uiLogger.InfoRaw("处理已开始: {0}", "环线要料上升沿触发且无外部料片(X3=false)，禁止取料(OUT14=false)，开始下料流程");
                                
                                // 成功触发下料，更新边缘检测状态
                                _lastRingLineunload = _ringLineunload;
                            }
                            else if (!_ringLineunload && _lastRingLineunload)
                            {
                                // 检测到下降沿(True→False)，更新状态以便下次能检测到上升沿
                                _lastRingLineunload = false;
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
        /// 处理正在取料状态
        /// </summary>
        private void ProcessPickingMaterial()
        {
            // 检查取料完成状态 - 从SharedState读取（BinElevatorModule通过X10信号更新这个状态）
            bool unloadingCompleted = _sharedState?.GetUnloadingCompleted() == true;
            
            // 诊断日志：定期输出等待状态（每5秒记录一次）
            if (!unloadingCompleted)
            {
                long currentTicks = DateTime.Now.Ticks;
                long elapsedSeconds = (currentTicks - _lastPickingLogTicks) / TimeSpan.TicksPerSecond;
                if (elapsedSeconds >= 5)
                {
                    _uiLogger.DebugRaw("[下料诊断] 等待取料完成: UnloadingCompleted={0}", unloadingCompleted);
                    _lastPickingLogTicks = currentTicks;
                }
            }
            
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

                // 恢复允许取料
                _ioManager.LayeredIO.WriteOutBit(OUT_ALLOW_PICK, true);

                // 释放流程锁
                _sharedState?.FinishProcess();

                // 重置标志并返回空闲状态
                _unloadingRequested = false;
                _lastScannedQrCode = string.Empty; // 清空扫码结果
                _currentState = MaterialUnloadingState.Idle;

                _uiLogger.InfoRaw("处理已完成: {0}", "下料完成，清除扫码完成信号(OUT9)，恢复允许取料(OUT14=true)，释放流程锁");
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
                _lastRingLineunload = _ringLineunload; // 重置边缘检测状态，避免立即重新触发

                _uiLogger.InfoRaw("处理已完成: {0}", "强制停止卸料，所有信号已清除");
            }
        }

        private void CallBackShow1(MessageModel msg)
        {
            var data = msg.GetData<RingLineModel>();
            _ringLineunload = data.IsLoading;
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
