using System;
using System.Threading;

namespace EwanIO.Core.EdgeDetection
{
    /// <summary>
    /// 边缘检测管理器 - 基于逻辑值（含模拟和映射）
    /// 零分配热路径：使用预分配数组
    /// 线程安全：支持 Tick 线程更新，其他线程读取并清除
    /// </summary>
    public class EdgeManager
    {
        private readonly int _inputCount;
        private readonly bool[] _previousStates;
        private readonly int[] _risingEdges;   // 使用 int 以支持 Interlocked
        private readonly int[] _fallingEdges;  // 0 = false, 1 = true
        private bool _initialized;

        public int InputCount => _inputCount;

        public EdgeManager(int inputCount)
        {
            _inputCount = inputCount;
            _previousStates = new bool[inputCount];
            _risingEdges = new int[inputCount];
            _fallingEdges = new int[inputCount];
            _initialized = false;
        }

        /// <summary>
        /// 更新边缘检测（在 Snapshot 更新后调用）
        /// </summary>
        /// <param name="getCurrentValue">获取当前逻辑输入值的函数</param>
        public void Update(Func<int, bool> getCurrentValue)
        {
            if (!_initialized)
            {
                // 首次调用，只记录状态，不产生边沿
                for (int i = 0; i < _inputCount; i++)
                {
                    _previousStates[i] = getCurrentValue(i);
                }
                _initialized = true;
                return;
            }

            for (int i = 0; i < _inputCount; i++)
            {
                bool current = getCurrentValue(i);
                bool previous = _previousStates[i];

                // 检测上升沿（使用 Volatile.Write 确保可见性）
                if (current && !previous)
                    Volatile.Write(ref _risingEdges[i], 1);

                // 检测下降沿
                if (!current && previous)
                    Volatile.Write(ref _fallingEdges[i], 1);

                _previousStates[i] = current;
            }
        }

        /// <summary>
        /// 批量更新边缘检测（零分配版本）
        /// </summary>
        public void Update(bool[] currentStates)
        {
            if (currentStates == null || currentStates.Length < _inputCount)
                return;

            if (!_initialized)
            {
                Array.Copy(currentStates, _previousStates, _inputCount);
                _initialized = true;
                return;
            }

            for (int i = 0; i < _inputCount; i++)
            {
                bool current = currentStates[i];
                bool previous = _previousStates[i];

                if (current && !previous)
                    Volatile.Write(ref _risingEdges[i], 1);

                if (!current && previous)
                    Volatile.Write(ref _fallingEdges[i], 1);

                _previousStates[i] = current;
            }
        }

        /// <summary>
        /// 读取上升沿（只读）
        /// </summary>
        public bool PeekRising(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= _inputCount)
                return false;
            return Volatile.Read(ref _risingEdges[logicalIndex]) != 0;
        }

        /// <summary>
        /// 读取下降沿（只读）
        /// </summary>
        public bool PeekFalling(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= _inputCount)
                return false;
            return Volatile.Read(ref _fallingEdges[logicalIndex]) != 0;
        }

        /// <summary>
        /// 读取并清除上升沿（线程安全）
        /// </summary>
        public bool ReadAndClearRising(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= _inputCount)
                return false;
            // 原子操作：读取当前值并设置为 0
            return Interlocked.Exchange(ref _risingEdges[logicalIndex], 0) != 0;
        }

        /// <summary>
        /// 读取并清除下降沿（线程安全）
        /// </summary>
        public bool ReadAndClearFalling(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= _inputCount)
                return false;
            return Interlocked.Exchange(ref _fallingEdges[logicalIndex], 0) != 0;
        }

        /// <summary>
        /// 清除上升沿
        /// </summary>
        public void ClearRising(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= _inputCount) return;
            Volatile.Write(ref _risingEdges[logicalIndex], 0);
        }

        /// <summary>
        /// 清除下降沿
        /// </summary>
        public void ClearFalling(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= _inputCount) return;
            Volatile.Write(ref _fallingEdges[logicalIndex], 0);
        }

        /// <summary>
        /// 清除所有边沿
        /// </summary>
        public void ClearAll()
        {
            Array.Clear(_risingEdges, 0, _inputCount);
            Array.Clear(_fallingEdges, 0, _inputCount);
        }

        /// <summary>
        /// 重置边缘检测器
        /// </summary>
        public void Reset()
        {
            Array.Clear(_previousStates, 0, _inputCount);
            Array.Clear(_risingEdges, 0, _inputCount);
            Array.Clear(_fallingEdges, 0, _inputCount);
            _initialized = false;
        }
    }
}
