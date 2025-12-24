using System;
using System.Collections.Generic;

namespace EwanSMC606
{
    /// <summary>
    /// SMC606 连接池：按 CardNo 共享 board_init/close，解决 Axis/IO 同时使用时的初始化冲突。
    /// </summary>
    public static class Smc606ConnectionPool
    {
        internal static ISmc606NativeApi NativeApi { get; set; } = new DllImportSmc606NativeApi();

        private static readonly object Gate = new object();
        private static readonly Dictionary<ushort, SharedConnection> Connections = new Dictionary<ushort, SharedConnection>();

        public static Smc606Lease Acquire(Smc606ConnectionOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            SharedConnection connection;
            lock (Gate)
            {
                if (!Connections.TryGetValue(options.CardNo, out connection))
                {
                    connection = new SharedConnection(options.CardNo);
                    Connections.Add(options.CardNo, connection);
                }
            }

            return connection.Acquire(options);
        }

        /// <summary>
        /// 仅在板卡已被其他模块连接的情况下获取租约（不触发 board_init）。
        /// </summary>
        public static bool TryAcquireExisting(ushort cardNo, out Smc606Lease? lease)
        {
            lease = null;

            SharedConnection? connection;
            lock (Gate)
            {
                Connections.TryGetValue(cardNo, out connection);
            }

            if (connection == null) return false;
            return connection.TryAcquireExisting(out lease);
        }

        internal sealed class SharedConnection
        {
            private readonly object _stateLock = new object();
            private readonly object _syncRoot = new object();
            private readonly ushort _cardNo;

            private int _refCount;
            private bool _isConnected;

            public SharedConnection(ushort cardNo)
            {
                _cardNo = cardNo;
            }

            public Smc606Lease Acquire(Smc606ConnectionOptions options)
            {
                options = options.Clone();
                options.Validate();

                lock (_stateLock)
                {
                    if (!_isConnected)
                    {
                        short res;
                        lock (_syncRoot)
                        {
                            res = NativeApi.BoardInit(_cardNo, options.ConnectType, options.ConnectString, options.BaudRate);
                        }
                        if (res != 0)
                        {
                            throw new InvalidOperationException($"SMC606 board_init failed (card={_cardNo}, code={res}).");
                        }

                        _isConnected = true;
                    }

                    _refCount++;
                    return new Smc606Lease(_cardNo, _syncRoot, this);
                }
            }

            public bool TryAcquireExisting(out Smc606Lease? lease)
            {
                lease = null;
                lock (_stateLock)
                {
                    if (!_isConnected) return false;

                    _refCount++;
                    lease = new Smc606Lease(_cardNo, _syncRoot, this);
                    return true;
                }
            }

            public void Release()
            {
                lock (_stateLock)
                {
                    if (_refCount <= 0) return;

                    _refCount--;
                    if (_refCount != 0) return;

                    if (_isConnected)
                    {
                        try
                        {
                            lock (_syncRoot)
                            {
                                NativeApi.BoardClose(_cardNo);
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    _isConnected = false;
                }
            }
        }
    }
}
