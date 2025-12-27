using Ewan.Core.IO;
using Ewan.Core.Plc;
using Ewan.Core.ScanCode;
using Ewan.Model;
using Ewan.Model.Production;
using Ewan.Model.System;
using Ewan.Model.Messages;
using EwanCore.Messaging;
using System;
using System.IO;
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
        private bool _unloadingEnabled = true;
        private volatile bool _beltStopRequested = false;

        // 共享状态（用于与其他模块通信）
        private ProductionLineSharedState _sharedState;

        // 消息队列相关
        private IDisposable _ringLineSubscription;

        private LayeredIOManager _ioManager;
        private readonly SystemParametersManager _parametersManager = SystemParametersManager.Instance;

        private ModbusRTUManager _modbusRTUManager;

        private BinElevatorModule _binElevator;
        
        // 环线信号状态（使用电平检测替代上升沿）
        private bool _ringLineSignal = false;      // 当前信号电平（寄存器152的值）
        private bool _requestProcessed = false;    // 是否已处理过此次请求

        private int _emptyCount = 0;  //空车计数
        private int _cuttingBridgeCarCount = 0;  //切栈桥车计数

        // Modbus寄存器地址定义
        private const string CART_COMPLETION_REGISTER = "153"; // 放入小车完成寄存器地址
        private const string MATERIAL_STATUS_REGISTER = "178"; // 放料状态寄存器：1=放料，0=排空

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

            _ringLineSubscription = MessageHub.Current.Subscribe<RingLineDataMessage>(OnRingLineData);

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
                var parameters = _parametersManager.Parameters;
                if (!parameters.EnableUnloadingModule)
                {
                    if (_unloadingEnabled)
                    {
                        ForceStopUnloading();
                        _unloadingEnabled = false;
                    }

                    Thread.Sleep(_scanInterval);
                    return true;
                }

                if (!_unloadingEnabled)
                {
                    _unloadingEnabled = true;
                }

                lock (_stateLock)
                {
                    // 检查暂停状态
                    if (_sharedState?.IsSystemPaused() == true)
                    {
                        // 暂停期间不执行任何状态处理
                        Thread.Sleep(_scanInterval);
                        return true;
                    }

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

            ForceReleaseBeltControl("下料模块销毁");

            // 注销消息监听器
            _ringLineSubscription?.Dispose();
            _ringLineSubscription = null;

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
            // 检查系统是否处于暂停状态
            if (_sharedState?.IsSystemPaused() == true)
            {
                // 暂停期间不处理新的下料请求
                return;
            }

            // 使用电平检测替代上升沿检测
            // 信号为1且未处理过时，开始处理
            if (_ringLineSignal && !_requestProcessed)
            {
                // 标记已处理，防止重复处理
                _requestProcessed = true;
                
                // 根据检测模式判断是否需要补充空车
                var parameters = _parametersManager?.Parameters;
                var cartCheckMode = parameters?.CartCheckMode ?? CartCheckMode.EmptyCart;
                bool needSendEmptyCar = false;
                
                if (cartCheckMode == CartCheckMode.EmptyCart)
                {
                    // 按空车数量检测
                    var reserveCount = Math.Max(0, parameters?.EmptyCartReserveCount ?? 0);
                    if (_emptyCount <= reserveCount)
                    {
                        _uiLogger.InfoRaw("处理已开始: {0}",
                            $"环线要料信号到达，空车数量 {_emptyCount} ≤ 设定值 {reserveCount}，执行下空车");
                        needSendEmptyCar = true;
                    }
                }
                else
                {
                    // 按切栈桥车数量检测：切栈桥车数量 > 设定值时，释放空车
                    var reserveCount = Math.Max(0, parameters?.CuttingBridgeCarReserveCount ?? 0);
                    if (_cuttingBridgeCarCount > reserveCount)
                    {
                        _uiLogger.InfoRaw("处理已开始: {0}",
                            $"环线要料信号到达，切栈桥车数量 {_cuttingBridgeCarCount} > 设定值 {reserveCount}，执行下空车");
                        needSendEmptyCar = true;
                    }
                }
                
                if (needSendEmptyCar)
                {
                    SendCartCompletionToModbus(false);
                    // 放空车完成后直接返回，等待信号变为0后重置_requestProcessed
                    return;
                }
                
                // 空车数量充足，继续正常下料流程
                // 立即记录下料优先级请求
                _sharedState?.RequestUnloadingPriority();
                
                _uiLogger.InfoRaw("处理已开始: {0}", "环线请求下料，设置优先级标志");
                
                if (_ioManager.Ctx.R.触发机械手皮带线允许取料)
                {
                    // 正在装填等下完成
                    _requestProcessed = false;
                    return;
                }
                
                // 装料流程已完成，尝试获取流程锁并开始下料
                if (_sharedState?.TryStartUnloading() == true)
                {
                    // 成功获取锁，清除优先级请求
                    _sharedState?.ClearUnloadingPriority();

                    int binNumber = GetConfiguredBinNumber();
                    BinMaterialCheckResult materialCheckResult = null;

                    if (_binElevator != null)
                    {
                        try
                        {
                            materialCheckResult = _binElevator.RaiseToSensor(binNumber);
                        }
                        catch (Exception ex)
                        {
                            _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                                $"料仓{binNumber}检测物料失败", ex.Message);
                        }
                    }
                    else
                    {
                        _uiLogger.WarnRaw("处理错误: {0} - {1}",
                            "BinElevatorModule未配置", "跳过有料检测并继续流程");
                    }

                    if (materialCheckResult == null || materialCheckResult.HasMaterial)
                    {
                        RequestUnloading();
                        _uiLogger.InfoRaw("处理已开始: {0}", "装料流程已完成，开始下料流程");
                    }
                    else
                    {
                        HandleEmptyUnloadingResult(materialCheckResult);
                    }
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
                _ioManager.Ctx.Off(x => x.发送取料指令);
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
            if (_ioManager.Ctx.Edge.R(x => x.移至扫码区到位信号))
            {
                _currentState = MaterialUnloadingState.Scanning;
                _uiLogger.InfoRaw("处理已开始: {0}", "已到达扫码位置，开始扫码");
            }
        }

        /// <summary>
        /// 处理扫码状态
        /// </summary>
        private void ProcessScanning()
        {
            var parameters = _parametersManager?.Parameters;
            bool mesEnabled = parameters != null && parameters.MesEnabled;
            int retryCount = parameters != null ? parameters.CodeReaderScanRetryCount : 3;
            if (retryCount <= 0) retryCount = 3;

            string scannedCode = string.Empty;

            if (mesEnabled)
            {
                for (int attempt = 1; attempt <= retryCount; attempt++)
                {
                    scannedCode = (DLManager.Instance().TriggerScan() ?? string.Empty).Trim();

                    if (!string.IsNullOrWhiteSpace(scannedCode))
                    {
                        _uiLogger.InfoRaw("处理已完成: {0}", $"扫码内容: {scannedCode}");
                        break;
                    }

                    _uiLogger.WarnRaw("操作失败: {0}", $"第{attempt}次扫码无结果");
                }

                if (string.IsNullOrWhiteSpace(scannedCode))
                {
                    scannedCode = string.Empty;
                    _uiLogger.WarnRaw("操作失败: {0}", $"连续扫码{retryCount}次无结果，继续流程");
                }
            }
            else
            {
                _uiLogger.InfoRaw("处理已跳过: {0}", "MES未启用，跳过扫码");
            }

            // 保存扫码结果（允许为空）
            _lastScannedQrCode = scannedCode;

            // 发送扫码完成信号给机械臂
            _ioManager.Ctx.On(x => x.发送扫码完成信号);

            _currentState = MaterialUnloadingState.PuttingToCart;

            // 发送放入小车信号
            _ioManager.Ctx.On(x => x.发送放入小车指令);
        }

        /// <summary>
        /// 处理放入小车状态
        /// </summary>
        private void ProcessPuttingToCart()
        {
            // 检查小车脉冲信号X11
            if (_ioManager.Ctx.Edge.R(x => x.放入小车完成信号))
            {
                // 清除放入小车信号
                _ioManager.Ctx.Off(x => x.发送放入小车指令);

                // 清除扫码完成信号
                _ioManager.Ctx.Off(x => x.发送扫码完成信号);

                // 发送完成信号到Modbus寄存器153
                SendCartCompletionToModbus(true);

                // 清除下料优先级请求
                _sharedState?.ClearUnloadingPriority();

                // 释放流程锁
                _sharedState?.FinishProcess();

                // 重置标志并返回空闲状态
                _unloadingRequested = false;
                _lastScannedQrCode = string.Empty; // 清空扫码结果
                _currentState = MaterialUnloadingState.Idle;

                UpdateBeltConveyorStopRequest(false, "下料流程完成");

                _uiLogger.InfoRaw("处理已完成: {0}",
                    "下料完成，清除优先级请求，释放流程锁");
            }
        }

        /// <summary>
        /// 处理料仓无料场景，释放空车
        /// </summary>
        /// <param name="result">检测结果</param>
        private void HandleEmptyUnloadingResult(BinMaterialCheckResult result)
        {
            int binNumber = result?.BinNumber ?? GetConfiguredBinNumber();
            string reason = result?.TimedOut == true
                ? "超时仍未检测到物料"
                : "传感器未检测到物料";

            _uiLogger.WarnRaw("处理错误: {0} - {1}",
                $"料仓{binNumber}无料，释放空车", reason);

            _lastScannedQrCode = string.Empty;
            ClearBinSelectSignals();
            SendCartCompletionToModbus(false);

            _sharedState?.ClearUnloadingPriority();
            _sharedState?.FinishProcess();

            _unloadingRequested = false;
            _currentState = MaterialUnloadingState.Idle;

            UpdateBeltConveyorStopRequest(false, $"料仓{binNumber}无料，释放皮带");
        }

        /// <summary>
        /// 清除所有料仓选择信号
        /// </summary>
        private void ClearBinSelectSignals()
        {
            if (_ioManager?.Ctx == null)
            {
                return;
            }

            _ioManager.Ctx.Off(x => x.料仓1选择信号);
            _ioManager.Ctx.Off(x => x.料仓2选择信号);
            _ioManager.Ctx.Off(x => x.料仓3选择信号);
        }

        /// <summary>
        /// 获取当前配置的料仓编号
        /// </summary>
        /// <returns>料仓编号 (1-3)</returns>
        private int GetConfiguredBinNumber()
        {
            var selected = _parametersManager.Parameters.UnloadingBinSelection;
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
                    _ioManager.Ctx.On(x => x.料仓1选择信号);
                    break;
                case 2:
                    _ioManager.Ctx.On(x => x.料仓2选择信号);
                    break;
                case 3:
                    _ioManager.Ctx.On(x => x.料仓3选择信号);
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
        public void RequestUnloading()
        {
            lock (_stateLock)
            {
                if (_currentState != MaterialUnloadingState.Idle)
                {
                    _uiLogger.WarnRaw("处理错误: {0} - {1}", $"当前状态为 {_currentState}，无法开始新的卸料流程");
                    return;
                }

                int binNumber = GetConfiguredBinNumber();
                _selectedBin = binNumber;
                _unloadingRequested = true;
                
                // 设置料仓选择信号
                SetBinSelectSignal(binNumber);
                
                // 触发取料信号
                _ioManager.Ctx.On(x => x.发送取料指令);
                 
                // 转换到取料状态
                _currentState = MaterialUnloadingState.PickingMaterial;

                UpdateBeltConveyorStopRequest(true, "下料流程请求停止皮带");
                
                _uiLogger.InfoRaw("处理已开始: {0}", $"请求从料仓{binNumber}开始取料");
            }
        }

        public void SetBinElevatorModule(BinElevatorModule binElevator)
        {
            _binElevator = binElevator;
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
                try
                {
                    if (_ioManager?.Ctx != null)
                    {
                        _ioManager.Ctx.Off(x => x.触发机械手皮带线允许取料);
                        _ioManager.Ctx.Off(x => x.发送扫码完成信号);
                        _ioManager.Ctx.Off(x => x.发送取料指令);
                        _ioManager.Ctx.Off(x => x.发送放入小车指令);
                        ClearBinSelectSignals();
                    }
                }
                catch (Exception ex)
                {
                    _uiLogger.ErrorRaw("处理错误: {0} - {1}", "强制停止卸料清除输出", ex.Message);
                }

                // 重置状态
                _currentState = MaterialUnloadingState.Idle;
                _stopRequested = false;
                _unloadingRequested = false;
                _ringLineSignal = false;     // 清除信号电平标志
                _requestProcessed = false;   // 清除处理标志

                _sharedState?.SetUnloadingCompleted(false);
                _sharedState?.ClearUnloadingPriority();
                _sharedState?.FinishProcess();

                _uiLogger.InfoRaw("处理已完成: {0}", "强制停止卸料，所有信号已清除");
            }

            ForceReleaseBeltControl("下料强制停止");
        }

        private void OnRingLineData(RingLineDataMessage msg)
        {
            // 使用电平而非边缘检测，避免上升沿丢失
            _ringLineSignal = msg.IsLoading;

            _emptyCount = msg.EmptyCarCount;
            _cuttingBridgeCarCount = msg.CuttingBridgeCarCount;
        }

        /// <summary>
        /// 发送放入小车完成信号到Modbus寄存器153
        /// </summary>
        /// <param name="hasMaterial">是否有料</param>
        private void SendCartCompletionToModbus(bool hasMaterial)
        {
            try
            {
                // 发送完成信号值为1到寄存器153 (u16类型)
                _modbusRTUManager.WriteAny(CART_COMPLETION_REGISTER, (ushort)1);

                ushort statusValue = hasMaterial ? (ushort)1 : (ushort)0;
                _modbusRTUManager.WriteAny(MATERIAL_STATUS_REGISTER, statusValue);

                _uiLogger.InfoRaw("处理已完成: {0}",
                    $"放入小车完成信号已发送到寄存器{CART_COMPLETION_REGISTER}，放料状态={(hasMaterial ? "放料" : "空车")}");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", $"发送完成信号到Modbus失败: {ex.Message}");
            }
        }

        private void UpdateBeltConveyorStopRequest(bool shouldStop, string reason)
        {
            if (_beltStopRequested == shouldStop)
            {
                return;
            }

            _beltStopRequested = shouldStop;

            // 使用强类型消息发布
            var message = new BeltConveyorControlMessage(
                Ewan.Model.Messages.BeltConveyorControlSource.MaterialUnloading,
                shouldStop,
                reason);

            MessageHub.Current.Post(message);
        }

        private void ForceReleaseBeltControl(string reason)
        {
            if (!_beltStopRequested)
            {
                return;
            }

            _beltStopRequested = false;

            // 使用强类型消息发布
            var message = new BeltConveyorControlMessage(
                Ewan.Model.Messages.BeltConveyorControlSource.MaterialUnloading,
                false,
                reason);

            MessageHub.Current.Post(message);
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
