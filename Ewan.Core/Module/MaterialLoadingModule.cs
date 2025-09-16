using Ewan.Core.IO;
using Ewan.Core.Msg;
using Ewan.Core.ScanCode;
using Ewan.Model.Production;
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
        
        // 共享状态（用于与其他模块通信）
        private ProductionLineSharedState _sharedState;

        private LayeredIOManager _ioManager;
        private const int MATERIAL_DETECT_SIGNAL = 3;      // 料片检测信号X3
        private const int PICK_MATERIAL_SIGNAL = 14;       // 触发取料信号Y14
        private const int SCAN_POSITION_SIGNAL = 7;         // 到达扫码位置信号X7
        private const int TRIGGER_LOADING_SIGNAL = 10;         // 触发放入料仓信号Y10

        // 料仓选择信号IO配置
        private const int BIN1_SELECT_SIGNAL = 11; // Y11 - 料仓1选择信号
        private const int BIN2_SELECT_SIGNAL = 12; // Y12 - 料仓2选择信号
        private const int BIN3_SELECT_SIGNAL = 13; // Y13 - 料仓3选择信号

        
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
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "MaterialLoadingModule");
            
            _ioManager = LayeredIOManager.Instance();

            // 初始化状态
            lock (_stateLock)
            {
                _currentState = MaterialLoadingState.Idle;
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
                        case MaterialLoadingState.Idle:
                            // 在空闲状态检测料片
                            if (_ioManager.LayeredIO.ReadInBit(MATERIAL_DETECT_SIGNAL))
                            {
                                _currentState = MaterialLoadingState.MaterialDetected;
                                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "检测到料片，准备取料");
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
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "MaterialLoadingModule: " + ex.Message);
                _currentState = MaterialLoadingState.Idle;
                Thread.Sleep(1000);
                return true;
            }
        }

        protected override void OnDestroy()
        {
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "MaterialLoadingModule");
            
        }

        #region 核心流程处理

        /// <summary>
        /// 处理检测到料片状态
        /// </summary>
        private void ProcessMaterialDetected()
        {
            // 触发取料信号Y14
            _ioManager.LayeredIO.WriteOutBit(PICK_MATERIAL_SIGNAL, true);
            _currentState = MaterialLoadingState.PickingMaterial;
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "开始取料到扫码区");
        }

        /// <summary>
        /// 处理取料状态
        /// </summary>
        private void ProcessPickingMaterial()
        {
            // 在取料过程中检测是否到达扫码位置X7
            if (_ioManager.LayeredIO.ReadInBit(SCAN_POSITION_SIGNAL))
            {
                // 关闭取料信号
                _ioManager.LayeredIO.WriteOutBit(PICK_MATERIAL_SIGNAL, false);
                _currentState = MaterialLoadingState.AtScanPosition;
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "取料完成，到达扫码位置，开始扫码流程");
            }
        }

        /// <summary>
        /// 处理到达扫码位置状态
        /// </summary>
        private void ProcessAtScanPosition()
        {
            DLManager.Instance().TriggerScan(); // 触发扫码,调试模式不需要结果
            //if(DLManager.Instance().TriggerScan() != "")
            //{
            // 根据扫码信息指定目标料仓 ，现在暂时为1
                _ioManager.LayeredIO.WriteOutBit(BIN1_SELECT_SIGNAL,true);

                // 触发放入料仓信号
                _ioManager.LayeredIO.WriteOutBit(TRIGGER_LOADING_SIGNAL,true);
                
                // 转换到移动到料仓状态
                _currentState = MaterialLoadingState.MovingToBinByScanInfo;
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "开始移动到料仓位置");
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
                
                // 重置标志并返回空闲状态
                SetLoadingCompleted(false);
                _currentState = MaterialLoadingState.Idle;
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "下料完成，返回空闲状态");
            }
        }

        #endregion

        #region 公共方法

      
        /// <summary>
        /// 停止装载流程
        /// </summary>
        public void StopLoading()
        {
            lock (_stateLock)
            {
                _stopRequested = true;
                _loadingRequested = false;
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "请求停止装载");
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
                
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "强制停止装载");
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