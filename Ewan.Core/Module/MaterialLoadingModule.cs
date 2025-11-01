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
        private int _ringLineTimeoutSeconds = 10; // 环线请求超时阈值(秒)

        // 共享状态（用于与其他模块通信）
        private ProductionLineSharedState _sharedState;

        private LayeredIOManager _ioManager;

        // 输入信号常量
        private const int MATERIAL_DETECT_SIGNAL = 3;      // 料片检测信号X3
        private const int SCAN_POSITION_SIGNAL = 7;         // 到达扫码位置信号X7

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

        // IN20	机械手忙碌状态信号
        private const int ROBOT_BUSY_SIGNAL = 20;          // 机械手忙碌状态信号



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

                // 步骤5: OUT_ALLOW_PICK置位true（触发机械手皮带线允许取料）
                _ioManager.LayeredIO.WriteOutBit(OUT_ALLOW_PICK, true);
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
                // 暂停时保持当前状态，不执行状态机
                if (_sharedState?.IsSystemPaused() == true)
                {
                    Thread.Sleep(_scanInterval);
                    return true;
                }
                
                lock (_stateLock)
                {
                    switch (_currentState)
                    {
                        case MaterialLoadingState.Idle:
                            // 首先检查环线请求是否超时
                           if (_ioManager.LayeredIO.ReadInBit(MATERIAL_DETECT_SIGNAL) &&
                                _sharedState?.TryStartLoading() == true)
                            {
                                _ioManager.LayeredIO.WriteOutBit(OUT_ALLOW_PICK, true);

                                _currentState = MaterialLoadingState.MaterialDetected;
                                _uiLogger.InfoRaw("处理已开始: {0}", "检测到皮带来料(X3=true)，获取流程锁，开始装料流程");
                            }
                            break;
                            
                        case MaterialLoadingState.MaterialDetected:
                            ProcessMaterialDetected();
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
                }
                
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
            
        }

        #region 核心流程处理

        /// <summary>
        /// 处理检测到料片状态
        /// </summary>
        private void ProcessMaterialDetected()
        {
            // 触发取料信号Y14
            //_ioManager.LayeredIO.WriteOutBit(PICK_MATERIAL_SIGNAL, true);
            _currentState = MaterialLoadingState.PickingMaterial;
            //_uiLogger.Info("处理已开始: {0}", "开始取料到扫码区");
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
            _ioManager.LayeredIO.WriteOutBit(OUT_ALLOW_PICK, false); // 先暂停机械手自动从皮带取料

            DLManager.Instance().TriggerScan(); // 触发扫码,调试模式不需要结果
                                                //if(DLManager.Instance().TriggerScan() != "")
                                                //{

            _ioManager.LayeredIO.WriteOutBit(OUT_SCAN_COMPLETE, true);  // 扫码完成


            // 根据扫码信息指定目标料仓 ，现在暂时为1
            _ioManager.LayeredIO.WriteOutBit(BIN1_SELECT_SIGNAL, true);

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
                // 下料完成，清除信号
                _ioManager.LayeredIO.WriteOutBit(TRIGGER_LOADING_SIGNAL, false);
                _ioManager.LayeredIO.WriteOutBit(BIN1_SELECT_SIGNAL, false);
                _ioManager.LayeredIO.WriteOutBit(OUT_SCAN_COMPLETE, false);

             
                Thread.Sleep(300); // 因为取完料，会X3会闪一下，所以等一会再读
                bool x3Signal = _ioManager.LayeredIO.ReadInBit(MATERIAL_DETECT_SIGNAL);

                if (!x3Signal) // X3为false，没有料片到达
                {
                    // 释放流程锁
                    _sharedState?.FinishProcess();

                    // 重置标志并返回空闲状态
                    SetLoadingCompleted(false);
                    _currentState = MaterialLoadingState.Idle;

                    _uiLogger.InfoRaw("处理已完成: {0}", "装料完成且X3=false，机械手已离开，释放流程锁");
                }
                else // X3为true，还有料片
                {
                    // 检查环线是否超时,如果超时则释放锁,优先让下料执行
                    double waitTime = _sharedState?.GetRingLineWaitTime() ?? 0;
                    bool isTimeout = waitTime >= _ringLineTimeoutSeconds;

                    if (isTimeout)
                    {
                        // 环线请求超时,优先下料
                        _ioManager.LayeredIO.WriteOutBit(OUT_ALLOW_PICK, false); // 禁止自动取料
                        _sharedState?.FinishProcess(); // 释放流程锁
                        SetLoadingCompleted(false);
                        _currentState = MaterialLoadingState.Idle;

                        _uiLogger.InfoRaw("处理已完成: {0}",
                            $"装填完成但环线请求超时(等待{waitTime:F1}秒,超时阈值{_ringLineTimeoutSeconds}秒),释放流程锁优先下料,OUT_ALLOW_PICK=false");
                    }
                    else
                    {
                        // 环线未超时,继续装填
                        SetLoadingCompleted(false);
                        _ioManager.LayeredIO.WriteOutBit(OUT_ALLOW_PICK, true); // 允许取下一片料
                        _currentState = MaterialLoadingState.MaterialDetected;

                        _uiLogger.InfoRaw("处理已完成: {0}",
                            $"装填完成且X3=true,环线未超时(已等待{waitTime:F1}秒),继续装填");
                    }
                }
            }
        }

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
                _currentState = MaterialLoadingState.Idle;
                _stopRequested = false;
                _loadingRequested = false;

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
        /// 检测到料片
        /// </summary>
        MaterialDetected,
        
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