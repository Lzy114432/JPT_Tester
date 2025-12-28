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
            set
            {
                lock (_syncRoot)
                {
                    _needHome = value;
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
            set
            {
                lock (_syncRoot)
                {
                    _isHomeing = value;
                }
            }
        }
    }
}

