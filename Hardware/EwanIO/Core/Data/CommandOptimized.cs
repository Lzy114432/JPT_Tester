using System;
using System.Collections.Generic;
using System.Threading;

namespace EwanIO.Core.Data
{
    /// <summary>
    /// CommandOptimized - 输出命令缓冲（端口级优化）
    /// 特点：
    /// - 线程安全写入
    /// - 端口级 dirty 标记（32 位/端口）
    /// - 批量下发优化
    /// </summary>
    public class CommandOptimized
    {
        private const int BITS_PER_PORT = 32;

        private readonly bool[] _values;
        private readonly ulong[] _dirtyPorts; // 每个 bit 表示一个端口是否 dirty
        private readonly object _lock = new object();
        private int _dirtyPortCount;

        public int OutputCount { get; }
        public int PortCount { get; }
        public bool HasDirty => _dirtyPortCount > 0;
        public int DirtyPortCount => _dirtyPortCount;

        internal CommandOptimized(int outputCount)
        {
            OutputCount = outputCount;
            PortCount = (outputCount + BITS_PER_PORT - 1) / BITS_PER_PORT;
            _values = new bool[outputCount];

            // 使用 ulong 数组作为位图（每个 ulong 可以标记 64 个端口）
            int ulongCount = (PortCount + 63) / 64;
            _dirtyPorts = new ulong[ulongCount];
            _dirtyPortCount = 0;
        }

        /// <summary>
        /// 设置输出值（线程安全）
        /// </summary>
        public void SetOutput(int logicalIndex, bool value)
        {
            if (logicalIndex < 0 || logicalIndex >= OutputCount)
                return;

            lock (_lock)
            {
                if (_values[logicalIndex] != value)
                {
                    _values[logicalIndex] = value;

                    // 计算所属端口
                    int portIndex = logicalIndex / BITS_PER_PORT;
                    int ulongIndex = portIndex / 64;
                    int bitIndex = portIndex % 64;
                    ulong mask = 1UL << bitIndex;

                    // 如果该端口还未标记为 dirty，则标记并增加计数
                    if ((_dirtyPorts[ulongIndex] & mask) == 0)
                    {
                        _dirtyPorts[ulongIndex] |= mask;
                        Interlocked.Increment(ref _dirtyPortCount);
                    }
                }
            }
        }

        /// <summary>
        /// 获取输出值
        /// </summary>
        public bool GetOutput(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= OutputCount)
                return false;
            return _values[logicalIndex];
        }

        /// <summary>
        /// 检查指定端口是否 dirty
        /// </summary>
        public bool IsPortDirty(int portIndex)
        {
            if (portIndex < 0 || portIndex >= PortCount)
                return false;

            int ulongIndex = portIndex / 64;
            int bitIndex = portIndex % 64;
            ulong mask = 1UL << bitIndex;

            return (_dirtyPorts[ulongIndex] & mask) != 0;
        }

        /// <summary>
        /// 遍历所有 dirty 端口（用于批量 flush）
        /// </summary>
        internal void FlushDirtyPorts(Action<int, uint> writePort)
        {
            if (_dirtyPortCount == 0)
                return;

            lock (_lock)
            {
                // 遍历所有 dirty 端口
                for (int ulongIndex = 0; ulongIndex < _dirtyPorts.Length; ulongIndex++)
                {
                    ulong dirtyMask = _dirtyPorts[ulongIndex];
                    if (dirtyMask == 0)
                        continue;

                    // 检查这个 ulong 中的每个 bit（每个 bit 代表一个端口）
                    for (int bitIndex = 0; bitIndex < 64; bitIndex++)
                    {
                        if ((dirtyMask & (1UL << bitIndex)) != 0)
                        {
                            int portIndex = ulongIndex * 64 + bitIndex;
                            if (portIndex >= PortCount)
                                break;

                            // 打包这个端口的 32 个 bit
                            uint portValue = PackPort(portIndex);
                            writePort(portIndex, portValue);
                        }
                    }

                    // 清除这个 ulong 的 dirty 标记
                    _dirtyPorts[ulongIndex] = 0;
                }

                _dirtyPortCount = 0;
            }
        }

        /// <summary>
        /// 仅消费 dirty 端口索引（不打包端口值），并清除 dirty 标记。
        /// 用于外部需要自定义端口值构建（例如映射/反转/跨端口）的场景，避免重复 PackPort 开销。
        /// </summary>
        internal void ConsumeDirtyPorts(Action<int> onDirtyPort, Action? afterConsume = null)
        {
            if (_dirtyPortCount == 0)
                return;
            if (onDirtyPort == null)
                throw new ArgumentNullException(nameof(onDirtyPort));

            lock (_lock)
            {
                for (int ulongIndex = 0; ulongIndex < _dirtyPorts.Length; ulongIndex++)
                {
                    ulong dirtyMask = _dirtyPorts[ulongIndex];
                    if (dirtyMask == 0)
                        continue;

                    for (int bitIndex = 0; bitIndex < 64; bitIndex++)
                    {
                        if ((dirtyMask & (1UL << bitIndex)) != 0)
                        {
                            int portIndex = ulongIndex * 64 + bitIndex;
                            if (portIndex >= PortCount)
                                break;
                            onDirtyPort(portIndex);
                        }
                    }

                    _dirtyPorts[ulongIndex] = 0;
                }

                _dirtyPortCount = 0;
                afterConsume?.Invoke();
            }
        }

        /// <summary>
        /// 遍历并清除 dirty 输出（逐位，兼容模式）
        /// </summary>
        internal void FlushDirty(Action<int, bool> writeOutput)
        {
            if (_dirtyPortCount == 0)
                return;

            lock (_lock)
            {
                // 遍历所有 dirty 端口
                for (int ulongIndex = 0; ulongIndex < _dirtyPorts.Length; ulongIndex++)
                {
                    ulong dirtyMask = _dirtyPorts[ulongIndex];
                    if (dirtyMask == 0)
                        continue;

                    // 检查这个 ulong 中的每个端口
                    for (int bitIndex = 0; bitIndex < 64; bitIndex++)
                    {
                        if ((dirtyMask & (1UL << bitIndex)) != 0)
                        {
                            int portIndex = ulongIndex * 64 + bitIndex;
                            if (portIndex >= PortCount)
                                break;

                            // 逐位写入这个端口的所有输出
                            int startBit = portIndex * BITS_PER_PORT;
                            int endBit = Math.Min(startBit + BITS_PER_PORT, OutputCount);

                            for (int i = startBit; i < endBit; i++)
                            {
                                writeOutput(i, _values[i]);
                            }
                        }
                    }

                    // 清除这个 ulong 的 dirty 标记
                    _dirtyPorts[ulongIndex] = 0;
                }

                _dirtyPortCount = 0;
            }
        }

        /// <summary>
        /// 获取所有 dirty 端口信息
        /// </summary>
        internal List<DirtyPortInfo> GetDirtyPorts()
        {
            var result = new List<DirtyPortInfo>();

            if (_dirtyPortCount == 0)
                return result;

            lock (_lock)
            {
                for (int ulongIndex = 0; ulongIndex < _dirtyPorts.Length; ulongIndex++)
                {
                    ulong dirtyMask = _dirtyPorts[ulongIndex];
                    if (dirtyMask == 0)
                        continue;

                    for (int bitIndex = 0; bitIndex < 64; bitIndex++)
                    {
                        if ((dirtyMask & (1UL << bitIndex)) != 0)
                        {
                            int portIndex = ulongIndex * 64 + bitIndex;
                            if (portIndex >= PortCount)
                                break;

                            uint portValue = PackPort(portIndex);
                            result.Add(new DirtyPortInfo
                            {
                                PortIndex = portIndex,
                                Value = portValue
                            });
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 打包端口数据（32 个 bool -> uint）
        /// </summary>
        private uint PackPort(int portIndex)
        {
            uint result = 0;
            int startBit = portIndex * BITS_PER_PORT;
            int endBit = Math.Min(startBit + BITS_PER_PORT, OutputCount);

            for (int i = startBit; i < endBit; i++)
            {
                if (_values[i])
                {
                    int bitPos = i - startBit;
                    result |= (1U << bitPos);
                }
            }

            return result;
        }

        /// <summary>
        /// 解包端口数据（uint -> 32 个 bool）
        /// </summary>
        public void UnpackPort(int portIndex, uint portValue)
        {
            lock (_lock)
            {
                int startBit = portIndex * BITS_PER_PORT;
                int endBit = Math.Min(startBit + BITS_PER_PORT, OutputCount);

                for (int i = startBit; i < endBit; i++)
                {
                    int bitPos = i - startBit;
                    _values[i] = ((portValue >> bitPos) & 1) == 1;
                }
            }
        }

        /// <summary>
        /// 清除所有 dirty 标记
        /// </summary>
        internal void ClearAllDirty()
        {
            lock (_lock)
            {
                Array.Clear(_dirtyPorts, 0, _dirtyPorts.Length);
                _dirtyPortCount = 0;
            }
        }

        /// <summary>
        /// 从 Snapshot 同步输出值（初始化时使用）
        /// </summary>
        internal void SyncFromSnapshot(Snapshot snapshot)
        {
            lock (_lock)
            {
                for (int i = 0; i < OutputCount && i < snapshot.OutputCount; i++)
                {
                    _values[i] = snapshot.GetOutput(i);
                }
            }
        }
    }

    /// <summary>
    /// Dirty 端口信息
    /// </summary>
    public struct DirtyPortInfo
    {
        public int PortIndex { get; set; }
        public uint Value { get; set; }
    }
}
