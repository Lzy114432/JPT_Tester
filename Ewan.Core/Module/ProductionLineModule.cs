using System;
using System.Threading;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 生产线统一控制模块
    /// 管理物料装载和料仓升降的协调工作
    /// </summary>
    public class ProductionLineModule : BaseModule<ProductionLineModule>
    {
        #region 私有字段

        private MaterialLoadingModule _materialLoading;
        private BinElevatorModule _binElevator;
        private ProductionLineSharedState _sharedState;
        
        private int _scanInterval = 100; // 扫描间隔，毫秒
        private bool _systemReady = false;

        #endregion

        #region BaseModule 实现

        protected override void OnInit()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "ProductionLineModule");
                
                // 创建共享状态
                _sharedState = new ProductionLineSharedState();
                
                // 创建子模块并传递共享状态
                _materialLoading = new MaterialLoadingModule(_sharedState);
                _binElevator = new BinElevatorModule(_sharedState);
                
                // 初始化子模块
                _materialLoading.Init();
                _binElevator.Init();
                
                _systemReady = true;
                _uiLogger.Info(() => Ewan.Resources.LogMessages.InitializationCompleted, "生产线控制系统");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleInitializationFailed, 
                    "ProductionLineModule", ex.Message);
                throw;
            }
        }

        protected override bool OnRun()
        {
            if (!_systemReady) return true;
            
            try
            {
                Thread.Sleep(_scanInterval);

                // 顺序调用子模块，让它们各自处理状态机
                // 通过共享状态自动协调工作
                _materialLoading.Run();
                _binElevator.Run();
                
                // 添加必要的延时
               
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, 
                    "ProductionLineModule", ex.Message);
                return true;
            }
        }

        protected override void OnDestroy()
        {
            try
            {
                if (_materialLoading != null)
                {
                    _materialLoading.Destroy();
                    _materialLoading = null;
                }
                
                if (_binElevator != null)
                {
                    _binElevator.Destroy();
                    _binElevator = null;
                }
                
                _systemReady = false;
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "ProductionLineModule");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "ProductionLineModule销毁", ex.Message);
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 紧急停止所有流程
        /// </summary>
        public void EmergencyStop()
        {
            try
            {
                _materialLoading?.ForceStopLoading();
                // 如果BinElevatorModule有紧急停止方法，可以在这里调用
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "生产线紧急停止");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "生产线紧急停止", ex.Message);
            }
        }

        #endregion
    }
}
