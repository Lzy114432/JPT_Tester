using Ewan.Core.IO;
using Ewan.Core.Msg;
using Ewan.LogManager.Logger;
using IOLibrary.Core.Layered;
using System;
using System.Diagnostics;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 安全模块 - 负责同步IO数据
    /// </summary>
    public class SafetyModule : BaseModule<SafetyModule>
    {
        #region 私有字段

        private LayeredIOManager _ioManager;
        private LayeredIO _layeredIO;
        private MsgManager _msgManager;
        
        // 时间间隔设置
        private int _dataSyncInterval = 10;     // IO数据同步间隔(ms) - 保持快速响应
        
        // 性能监控
        private Stopwatch _performanceWatch = new Stopwatch();
        private long _syncCount = 0;
        private double _avgSyncTime = 0;

        #endregion

        #region BaseModule 实现

        /// <summary>
        /// 初始化模块
        /// </summary>
        protected override void OnInit()
        {
            try
            {
                // 获取管理器实例
                _ioManager = LayeredIOManager.Instance();
                _layeredIO = _ioManager.LayeredIO;
                _msgManager = MsgManager.Instance();

                // 如果未连接，则尝试连接
                if (!_ioManager.IsConnected)
                {
                    bool connected = _ioManager.Connect();
                    if (!connected)
                    {
                        _uiLogger.Warn(() => Ewan.Resources.LogMessages.IOConnectionFailed, "SafetyModule初始化");
                    }
                }

                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "SafetyModule");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleInitializationFailed, "SafetyModule", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 运行模块 - 执行DataSync
        /// </summary>
        protected override bool OnRun()
        {
            try
            {
                if (_layeredIO == null || !_ioManager.IsConnected)
                {
                    System.Threading.Thread.Sleep(_dataSyncInterval);
                    return true; // 跳过但继续运行
                }

                // 记录性能
                _performanceWatch.Restart();

                // 执行IO数据同步
                _layeredIO.DataSync();
                
                _performanceWatch.Stop();
                
                // 更新性能统计
                _syncCount++;
                _avgSyncTime = (_avgSyncTime * (_syncCount - 1) + _performanceWatch.ElapsedMilliseconds) / _syncCount;
                
                // 每1000次输出一次性能报告
                if (_syncCount % 100 == 0)
                {
                    IOLogger.Instance.LogRaw(LogLevel.Debug, 
                        $"IO同步性能: 平均{_avgSyncTime:F2}ms, 当前{_performanceWatch.ElapsedMilliseconds}ms");
                }

                // 延时控制循环速度
                System.Threading.Thread.Sleep(_dataSyncInterval);

                return true; // 继续运行
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "SafetyModule", ex.Message);
                return false; // 停止运行
            }
        }

        /// <summary>
        /// 销毁模块
        /// </summary>
        protected override void OnDestroy()
        {
            // 断开连接
            if (_ioManager != null && _ioManager.IsConnected)
            {
                _ioManager.Disconnect();
            }
            
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "SafetyModule");
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置数据同步间隔
        /// </summary>
        /// <param name="milliseconds">间隔时间（毫秒）</param>
        public void SetDataSyncInterval(int milliseconds)
        {
            if (milliseconds >= 10 && milliseconds <= 1000)
            {
                _dataSyncInterval = milliseconds;
                _uiLogger.Info(() => $"IO同步间隔已设置为: {milliseconds}ms");
            }
        }

        /// <summary>
        /// 获取当前同步状态
        /// </summary>
        public bool IsConnected => _ioManager?.IsConnected ?? false;

        /// <summary>
        /// 获取LayeredIO实例（供其他模块使用）
        /// </summary>
        public LayeredIO GetLayeredIO() => _layeredIO;

        #endregion
    }
}