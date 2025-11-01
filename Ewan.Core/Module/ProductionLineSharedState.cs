using System;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 生产线共享状态类
    /// 用于在 MaterialLoadingModule 和 BinElevatorModule 之间共享状态
    /// </summary>
    public class ProductionLineSharedState
    {
        #region 流程类型枚举

        /// <summary>
        /// 当前正在执行的流程类型
        /// </summary>
        public enum ActiveProcess
        {
            /// <summary>
            /// 空闲状态，无流程运行
            /// </summary>
            None,

            /// <summary>
            /// 正在执行装料流程 (MaterialLoadingModule)
            /// </summary>
            Loading,

            /// <summary>
            /// 正在执行下料流程 (MaterialUnloadingModule)
            /// </summary>
            Unloading
        }

        #endregion

        #region 私有字段

        private readonly object _stateLock = new object();
        private bool _loadingCompleted = false;
        private bool _unloadingCompleted = false;
        private bool _systemPaused = false;
        private bool _requireReinit = false;

        /// <summary>
        /// 当前正在执行的流程
        /// </summary>
        private ActiveProcess _currentProcess = ActiveProcess.None;

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
                _currentProcess = ActiveProcess.None;
            }
        }

        #endregion

        #region 流程互斥锁方法

        /// <summary>
        /// 尝试开始装料流程
        /// </summary>
        /// <returns>true表示成功获取锁并可以开始装料流程；false表示有其他流程正在运行</returns>
        public bool TryStartLoading()
        {
            lock (_stateLock)
            {
                if (_currentProcess == ActiveProcess.None)
                {
                    _currentProcess = ActiveProcess.Loading;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 尝试开始下料流程
        /// </summary>
        /// <returns>true表示成功获取锁并可以开始下料流程；false表示有其他流程正在运行</returns>
        public bool TryStartUnloading()
        {
            lock (_stateLock)
            {
                if (_currentProcess == ActiveProcess.None)
                {
                    _currentProcess = ActiveProcess.Unloading;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 完成当前流程，释放流程锁
        /// </summary>
        public void FinishProcess()
        {
            lock (_stateLock)
            {
                _currentProcess = ActiveProcess.None;
            }
        }

        /// <summary>
        /// 获取当前正在执行的流程
        /// </summary>
        /// <returns>当前流程类型</returns>
        public ActiveProcess GetCurrentProcess()
        {
            lock (_stateLock)
            {
                return _currentProcess;
            }
        }

        /// <summary>
        /// 检查是否正在执行装料流程
        /// </summary>
        /// <returns>true表示正在装料</returns>
        public bool IsLoading()
        {
            lock (_stateLock)
            {
                return _currentProcess == ActiveProcess.Loading;
            }
        }

        /// <summary>
        /// 检查是否正在执行下料流程
        /// </summary>
        /// <returns>true表示正在下料</returns>
        public bool IsUnloading()
        {
            lock (_stateLock)
            {
                return _currentProcess == ActiveProcess.Unloading;
            }
        }

        #endregion
    }
}