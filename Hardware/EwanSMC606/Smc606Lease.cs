using System;

namespace EwanSMC606
{
    /// <summary>
    /// SMC606 连接租约：持有期间保证板卡已初始化；Dispose 释放占用计数。
    /// </summary>
    public sealed class Smc606Lease : IDisposable
    {
        private readonly Smc606ConnectionPool.SharedConnection _owner;
        private bool _disposed;

        internal Smc606Lease(ushort cardNo, object syncRoot, Smc606ConnectionPool.SharedConnection owner)
        {
            CardNo = cardNo;
            SyncRoot = syncRoot;
            _owner = owner;
        }

        public ushort CardNo { get; }

        /// <summary>
        /// 同一板卡上的 Axis/IO 混用时，建议所有 SDK 调用都用此锁串行化。
        /// </summary>
        public object SyncRoot { get; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _owner.Release();
        }
    }
}

