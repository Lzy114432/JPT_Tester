using Ewan.Core.Axis;
using System;
using System.Threading;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 皮带输送控制模块
    /// 负责控制皮带轴持续反向运行
    /// </summary>
    public class BeltConveyorModule : BaseModule<BeltConveyorModule>
    {
        #region 私有字段

        private int _scanInterval = 100; // 扫描间隔，毫秒
        private readonly object _stateLock = new object();

        // 皮带状态
        private bool _beltRunning = false;

        // 轴控制器
        private AxisManager _axisManager;

        // 皮带轴配置
        private const int BELT_AXIS_ID = 3; // 皮带轴ID

        #endregion

        #region BaseModule 实现

        protected override void OnInit()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "BeltConveyorModule");

                // 初始化轴管理器
                _axisManager = AxisManager.Instance();

                _uiLogger.Info(() => Ewan.Resources.LogMessages.InitializationCompleted, "皮带输送控制系统");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleInitializationFailed, "BeltConveyorModule", ex.Message);
                throw;
            }
        }

        protected override bool OnRun()
        {
            try
            {
                lock (_stateLock)
                {
                    // 皮带始终运行（只要模块在运行状态）
                    // 如果皮带未运行，启动皮带反向运行
                    if (!_beltRunning)
                    {
                        StartBeltReverse();
                    }
                }

                Thread.Sleep(_scanInterval);
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "BeltConveyorModule", ex.Message);
                Thread.Sleep(1000); // 错误时等待更长时间
                return true;
            }
        }

        protected override void OnDestroy()
        {
            try
            {
                // 停止皮带运行
                StopBelt();

                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "BeltConveyorModule");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "BeltConveyorModule销毁", ex.Message);
            }
        }

        #endregion

        #region 皮带控制逻辑

        /// <summary>
        /// 启动皮带反向运行
        /// </summary>
        private void StartBeltReverse()
        {
            try
            {
                if (_axisManager != null)
                {
                    // 获取皮带轴配置
                    var axisConfig = _axisManager.GetAxisConfig(BELT_AXIS_ID);
                    if (axisConfig != null && axisConfig.IsUsing)
                    {
                        // 使用JogDown进行反向运行
                        _axisManager.JogDown(axisConfig);
                        _beltRunning = true;

                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted,
                            "皮带轴开始反向运行，速度:" + axisConfig.Speed);
                    }
                    else
                    {
                        _uiLogger.Warn(() => Ewan.Resources.LogMessages.ProcessingError,
                            "皮带轴配置未找到或未启用", "轴ID:" + BELT_AXIS_ID);
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
                    "皮带轴启动", ex.Message);
            }
        }

        /// <summary>
        /// 停止皮带运行
        /// </summary>
        private void StopBelt()
        {
            try
            {
                if (_axisManager != null)
                {
                    // 获取皮带轴配置
                    var axisConfig = _axisManager.GetAxisConfig(BELT_AXIS_ID);
                    if (axisConfig != null)
                    {
                        // 停止皮带轴
                        _axisManager.JogStop(axisConfig);
                        _beltRunning = false;

                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted,
                            "皮带轴已停止");
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError,
                    "皮带轴停止", ex.Message);
            }
        }

        #endregion
    }
}
