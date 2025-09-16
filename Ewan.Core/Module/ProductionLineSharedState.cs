using System;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 生产线共享状态类
    /// 用于在 MaterialLoadingModule 和 BinElevatorModule 之间共享状态
    /// </summary>
    public class ProductionLineSharedState
    {
        #region 私有字段

        private readonly object _stateLock = new object();
        private bool _loadingCompleted = false;
        private bool _unloadingCompleted = false;
        private bool _systemPaused = false;
        private bool _requireReinit = false;

        #endregion

        #region 公共属性

        /// <summary>
        /// 装载完成状态（线程安全访问）
        /// </summary>
        public bool LoadingCompleted
        {
            get
            {
                lock (_stateLock)
                {
                    return _loadingCompleted;
                }
            }
            set
            {
                lock (_stateLock)
                {
                    _loadingCompleted = value;
                }
            }
        }

        /// <summary>
        /// 卸载完成状态（线程安全访问）
        /// </summary>
        public bool UnloadingCompleted
        {
            get
            {
                lock (_stateLock)
                {
                    return _unloadingCompleted;
                }
            }
            set
            {
                lock (_stateLock)
                {
                    _unloadingCompleted = value;
                }
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置装载完成状态
        /// </summary>
        /// <param name="completed">是否完成</param>
        public void SetLoadingCompleted(bool completed)
        {
            lock (_stateLock)
            {
                _loadingCompleted = completed;
            }
        }

        /// <summary>
        /// 获取装载完成状态
        /// </summary>
        /// <returns>是否完成</returns>
        public bool GetLoadingCompleted()
        {
            lock (_stateLock)
            {
                return _loadingCompleted;
            }
        }

        /// <summary>
        /// 设置卸载完成状态
        /// </summary>
        /// <param name="completed">是否完成</param>
        public void SetUnloadingCompleted(bool completed)
        {
            lock (_stateLock)
            {
                _unloadingCompleted = completed;
            }
        }

        /// <summary>
        /// 获取卸载完成状态
        /// </summary>
        /// <returns>是否完成</returns>
        public bool GetUnloadingCompleted()
        {
            lock (_stateLock)
            {
                return _unloadingCompleted;
            }
        }

        /// <summary>
        /// 获取系统暂停状态
        /// </summary>
        /// <returns>是否暂停</returns>
        public bool IsSystemPaused()
        {
            lock (_stateLock)
            {
                return _systemPaused;
            }
        }

        /// <summary>
        /// 设置系统暂停状态
        /// </summary>
        /// <param name="paused">是否暂停</param>
        public void SetSystemPaused(bool paused)
        {
            lock (_stateLock)
            {
                _systemPaused = paused;
            }
        }

        /// <summary>
        /// 获取是否需要重新初始化
        /// </summary>
        /// <returns>是否需要重新初始化</returns>
        public bool RequireReinit()
        {
            lock (_stateLock)
            {
                return _requireReinit;
            }
        }

        /// <summary>
        /// 设置是否需要重新初始化
        /// </summary>
        /// <param name="require">是否需要重新初始化</param>
        public void SetRequireReinit(bool require)
        {
            lock (_stateLock)
            {
                _requireReinit = require;
            }
        }

        /// <summary>
        /// 重置所有状态
        /// </summary>
        public void ResetAllStates()
        {
            lock (_stateLock)
            {
                _loadingCompleted = false;
                _unloadingCompleted = false;
                _systemPaused = false;
                _requireReinit = false;
            }
        }

        #endregion
    }
}