using System;
using System.Threading;

namespace EwanIO.Core.Data
{
    /// <summary>
    /// Snapshot - 只读快照
    /// 特点：帧一致性、多线程安全读
    /// </summary>
    public class Snapshot
    {
        private readonly bool[] _inputs;
        private readonly bool[] _preMapInputs;
        private readonly bool[] _noSimInputs;
        private readonly bool[] _hardwareInputs;
        private readonly bool[] _outputs;
        private long _tickCounter;

        public int InputCount { get; }
        public int OutputCount { get; }
        public long TickCounter => Interlocked.Read(ref _tickCounter);

        internal Snapshot(int inputCount, int outputCount)
        {
            InputCount = inputCount;
            OutputCount = outputCount;
            _inputs = new bool[inputCount];
            _preMapInputs = new bool[inputCount];
            _noSimInputs = new bool[inputCount];
            _hardwareInputs = new bool[inputCount];
            _outputs = new bool[outputCount];
            _tickCounter = 0;
        }

        /// <summary>
        /// 读取输入值
        /// </summary>
        public bool GetInput(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= InputCount)
                return false;
            return _inputs[logicalIndex];
        }

        /// <summary>
        /// 读取输入值（经过模拟，未应用 NO/NC 映射；映射前值）
        /// </summary>
        public bool GetPreMapInput(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= InputCount)
                return false;
            return _preMapInputs[logicalIndex];
        }

        /// <summary>
        /// 读取输入值（绕过模拟，应用 NO/NC 映射）
        /// </summary>
        public bool GetNoSimInput(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= InputCount)
                return false;
            return _noSimInputs[logicalIndex];
        }

        /// <summary>
        /// 读取硬件输入值（绕过模拟 + NO/NC 映射）
        /// </summary>
        public bool GetHardwareInput(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= InputCount)
                return false;
            return _hardwareInputs[logicalIndex];
        }

        /// <summary>
        /// 读取输出值
        /// </summary>
        public bool GetOutput(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= OutputCount)
                return false;
            return _outputs[logicalIndex];
        }

        /// <summary>
        /// 内部设置输入值
        /// </summary>
        internal void SetInput(int logicalIndex, bool value)
        {
            if (logicalIndex >= 0 && logicalIndex < InputCount)
                _inputs[logicalIndex] = value;
        }

        /// <summary>
        /// 内部设置输入值（经过模拟，未应用 NO/NC 映射；映射前值）
        /// </summary>
        internal void SetPreMapInput(int logicalIndex, bool value)
        {
            if (logicalIndex >= 0 && logicalIndex < InputCount)
                _preMapInputs[logicalIndex] = value;
        }

        /// <summary>
        /// 内部设置输入值（绕过模拟，应用 NO/NC 映射）
        /// </summary>
        internal void SetNoSimInput(int logicalIndex, bool value)
        {
            if (logicalIndex >= 0 && logicalIndex < InputCount)
                _noSimInputs[logicalIndex] = value;
        }

        /// <summary>
        /// 内部设置硬件输入值（绕过模拟 + NO/NC 映射）
        /// </summary>
        internal void SetHardwareInput(int logicalIndex, bool value)
        {
            if (logicalIndex >= 0 && logicalIndex < InputCount)
                _hardwareInputs[logicalIndex] = value;
        }

        /// <summary>
        /// 内部设置输出值
        /// </summary>
        internal void SetOutput(int logicalIndex, bool value)
        {
            if (logicalIndex >= 0 && logicalIndex < OutputCount)
                _outputs[logicalIndex] = value;
        }

        /// <summary>
        /// 内部递增 tick 计数器
        /// </summary>
        internal void IncrementTick()
        {
            Interlocked.Increment(ref _tickCounter);
        }

        /// <summary>
        /// 获取输入数组引用（内部使用，用于高效边缘检测）
        /// </summary>
        internal bool[] GetInputsRef() => _inputs;

        /// <summary>
        /// 获取输出数组引用（内部使用）
        /// </summary>
        internal bool[] GetOutputsRef() => _outputs;

        /// <summary>
        /// 获取输入数组引用（经过模拟，未应用 NO/NC 映射；映射前值）（内部使用）
        /// </summary>
        internal bool[] GetPreMapInputsRef() => _preMapInputs;

        /// <summary>
        /// 获取输入数组引用（绕过模拟，应用 NO/NC 映射）（内部使用）
        /// </summary>
        internal bool[] GetNoSimInputsRef() => _noSimInputs;

        /// <summary>
        /// 获取硬件输入数组引用（绕过模拟 + NO/NC 映射）（内部使用）
        /// </summary>
        internal bool[] GetHardwareInputsRef() => _hardwareInputs;
    }

    /// <summary>
    /// 双缓冲快照管理器
    /// 实现原子切换：读线程始终读 front，tick 写 back，完成后原子 swap
    /// </summary>
    public class DoubleBufferedSnapshot
    {
        private Snapshot _front;
        private Snapshot _back;

        public int InputCount { get; }
        public int OutputCount { get; }

        /// <summary>
        /// 当前可读快照（front）
        /// </summary>
        public Snapshot Current => Volatile.Read(ref _front);

        public DoubleBufferedSnapshot(int inputCount, int outputCount)
        {
            InputCount = inputCount;
            OutputCount = outputCount;
            _front = new Snapshot(inputCount, outputCount);
            _back = new Snapshot(inputCount, outputCount);
        }

        /// <summary>
        /// 获取 back buffer 用于写入
        /// </summary>
        internal Snapshot GetBackBuffer() => _back;

        /// <summary>
        /// 原子交换 front/back
        /// </summary>
        internal void Swap()
        {
            // Interlocked.Exchange 提供全栅栏，确保 back buffer 的写入对读线程可见
            var oldFront = Interlocked.Exchange(ref _front, _back);
            _back = oldFront;
        }

        /// <summary>
        /// 将 front 的内容复制到 back（在 swap 之前调用，确保连续性）
        /// </summary>
        internal void CopyFrontToBack()
        {
            var frontInputs = _front.GetInputsRef();
            var frontPreMapInputs = _front.GetPreMapInputsRef();
            var frontNoSimInputs = _front.GetNoSimInputsRef();
            var frontHardwareInputs = _front.GetHardwareInputsRef();
            var frontOutputs = _front.GetOutputsRef();
            var backInputs = _back.GetInputsRef();
            var backPreMapInputs = _back.GetPreMapInputsRef();
            var backNoSimInputs = _back.GetNoSimInputsRef();
            var backHardwareInputs = _back.GetHardwareInputsRef();
            var backOutputs = _back.GetOutputsRef();

            Array.Copy(frontInputs, backInputs, InputCount);
            Array.Copy(frontPreMapInputs, backPreMapInputs, InputCount);
            Array.Copy(frontNoSimInputs, backNoSimInputs, InputCount);
            Array.Copy(frontHardwareInputs, backHardwareInputs, InputCount);
            Array.Copy(frontOutputs, backOutputs, OutputCount);
        }
    }
}
