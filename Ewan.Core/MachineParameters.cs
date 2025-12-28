using System;

namespace Ewan.Core
{
    /// <summary>
    /// 机台运行参数（运行时状态，而非配置参数）：
    /// - NeedHome：是否需要复位后才能启动
    /// - IsHomeing：是否正在复位中
    /// </summary>
    public sealed class MachineParameters
    {
        private static readonly Lazy<MachineParameters> s_instance =
            new Lazy<MachineParameters>(() => new MachineParameters());

        private readonly object _syncRoot = new object();
        private bool _needHome = true;
        private bool _isHomeing;

        private MachineParameters()
        {
        }

        public static MachineParameters Instance => s_instance.Value;

        public bool NeedHome
        {
            get
            {
                lock (_syncRoot)
                {
                    return _needHome;
                }
            }
        }

        public bool IsHomeing
        {
            get
            {
                lock (_syncRoot)
                {
                    return _isHomeing;
                }
            }
        }

        /// <summary>
        /// 开始复位：设置 NeedHome=true, IsHomeing=true
        /// </summary>
        public void BeginHome()
        {
            lock (_syncRoot)
            {
                _needHome = true;
                _isHomeing = true;
            }
        }

        /// <summary>
        /// 结束复位：根据成功与否设置状态
        /// </summary>
        /// <param name="success">复位是否成功</param>
        public void EndHome(bool success)
        {
            lock (_syncRoot)
            {
                _needHome = !success;
                _isHomeing = false;
            }
        }

        /// <summary>
        /// 标记需要复位（停止时调用）
        /// </summary>
        public void MarkNeedHome()
        {
            lock (_syncRoot)
            {
                _needHome = true;
                _isHomeing = false;
            }
        }
    }
}

