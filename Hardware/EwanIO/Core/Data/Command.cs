using System;
using System.Threading;

namespace EwanIO.Core.Data
{
    /// <summary>
    /// Command - 输出命令缓冲
    /// 特点：线程安全写入、dirty 标记、延迟下发
    /// </summary>
    public class Command
    {
        private readonly bool[] _values;
        private readonly bool[] _dirty;
        private readonly object _lock = new object();
        private readonly int[] _dirtyIndices;
        private int _dirtyIndicesCount;
        private int _dirtyCount;

        public int OutputCount { get; }
        public bool HasDirty => _dirtyCount > 0;
        public int DirtyCount => _dirtyCount;

        internal Command(int outputCount)
        {
            OutputCount = outputCount;
            _values = new bool[outputCount];
            _dirty = new bool[outputCount];
            _dirtyIndices = new int[outputCount];
            _dirtyIndicesCount = 0;
            _dirtyCount = 0;
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
                    if (!_dirty[logicalIndex])
                    {
                        _dirty[logicalIndex] = true;
                        _dirtyIndices[_dirtyIndicesCount++] = logicalIndex;
                        Interlocked.Increment(ref _dirtyCount);
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
        /// 检查是否 dirty
        /// </summary>
        public bool IsDirty(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= OutputCount)
                return false;
            return _dirty[logicalIndex];
        }

        /// <summary>
        /// 遍历并清除 dirty 输出（用于 flush）
        /// </summary>
        internal void FlushDirty(Action<int, bool> writeOutput)
        {
            if (_dirtyCount == 0)
                return;

            lock (_lock)
            {
                for (int j = 0; j < _dirtyIndicesCount; j++)
                {
                    int i = _dirtyIndices[j];
                    if (!_dirty[i])
                        continue;

                    writeOutput(i, _values[i]);
                    _dirty[i] = false;
                }

                _dirtyIndicesCount = 0;
                _dirtyCount = 0;
            }
        }

        /// <summary>
        /// 获取所有 dirty 输出的索引和值（不清除）
        /// </summary>
        internal void GetDirtyOutputs(Action<int, bool> callback)
        {
            if (_dirtyCount == 0)
                return;

            lock (_lock)
            {
                for (int j = 0; j < _dirtyIndicesCount; j++)
                {
                    int i = _dirtyIndices[j];
                    if (_dirty[i])
                        callback(i, _values[i]);
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
                Array.Clear(_dirty, 0, OutputCount);
                _dirtyIndicesCount = 0;
                _dirtyCount = 0;
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
}
