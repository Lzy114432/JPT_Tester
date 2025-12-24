using System;
using EwanIO.Core.Interfaces;

namespace EwanIO.Hardware.InMemory
{
    /// <summary>
    /// 内存硬件 IO - 用于测试和仿真
    /// 不依赖真实硬件，所有数据存储在内存中
    /// 实现了 IHardwareIOExtended，支持细粒度同步
    /// </summary>
    public class InMemoryHardwareIO : IHardwareIOExtended
    {
        private readonly bool[] _inputs;
        private readonly bool[] _outputs;
        private bool _isConnected;
        private bool _disposed;

        public string HardwareType => "InMemory";
        public string ConnectionInfo { get; private set; } = "InMemory";
        public bool IsConnected => _isConnected;
        public int InputCount { get; }
        public int OutputCount { get; }

        /// <summary>
        /// 内存硬件支持完全独立的输入输出同步和批量操作
        /// </summary>
        public HardwareCapabilities Capabilities =>
            HardwareCapabilities.FullySeparatedSync | HardwareCapabilities.BulkOutputWrite;

        public InMemoryHardwareIO(int inputCount, int outputCount)
        {
            if (inputCount < 0) throw new ArgumentOutOfRangeException(nameof(inputCount));
            if (outputCount < 0) throw new ArgumentOutOfRangeException(nameof(outputCount));

            InputCount = inputCount;
            OutputCount = outputCount;
            _inputs = new bool[inputCount];
            _outputs = new bool[outputCount];
            _isConnected = false;
        }

        public bool Connect(string connectionString)
        {
            if (_disposed) return false;
            ConnectionInfo = connectionString ?? "InMemory";
            _isConnected = true;
            return true;
        }

        public bool Disconnect()
        {
            _isConnected = false;
            return true;
        }

        public void DataSync()
        {
            // 内存硬件不需要同步
        }

        public void InputSync()
        {
            // 内存硬件不需要同步输入（数据已在内存中）
        }

        public void OutputSync()
        {
            // 内存硬件不需要同步输出（数据直接写入内存）
        }

        public void WriteBulkOutputPort(int portIndex, uint portValue)
        {
            // 批量写入 32 位端口
            const int BITS_PER_PORT = 32;
            int startBit = portIndex * BITS_PER_PORT;
            int endBit = Math.Min(startBit + BITS_PER_PORT, OutputCount);

            for (int i = startBit; i < endBit; i++)
            {
                int bitPos = i - startBit;
                bool value = ((portValue >> bitPos) & 1) == 1;
                _outputs[i] = value;
            }
        }

        public bool ReadInBit(int bit)
        {
            if (bit < 0 || bit >= InputCount)
                return false;
            return _inputs[bit];
        }

        public bool ReadOutBit(int bit)
        {
            if (bit < 0 || bit >= OutputCount)
                return false;
            return _outputs[bit];
        }

        public bool WriteOutBit(int bit, bool value)
        {
            if (bit < 0 || bit >= OutputCount)
                return false;
            _outputs[bit] = value;
            return true;
        }

        #region 测试辅助方法

        /// <summary>
        /// 设置输入值（用于测试）
        /// </summary>
        public void SetInputBit(int bit, bool value)
        {
            if (bit >= 0 && bit < InputCount)
                _inputs[bit] = value;
        }

        /// <summary>
        /// 批量设置输入值（用于测试）
        /// </summary>
        public void SetInputs(params bool[] values)
        {
            int count = Math.Min(values.Length, InputCount);
            Array.Copy(values, _inputs, count);
        }

        /// <summary>
        /// 批量设置输出值（用于测试）
        /// </summary>
        public void SetOutputs(params bool[] values)
        {
            int count = Math.Min(values.Length, OutputCount);
            Array.Copy(values, _outputs, count);
        }

        /// <summary>
        /// 清除所有输入
        /// </summary>
        public void ClearInputs()
        {
            Array.Clear(_inputs, 0, InputCount);
        }

        /// <summary>
        /// 清除所有输出
        /// </summary>
        public void ClearOutputs()
        {
            Array.Clear(_outputs, 0, OutputCount);
        }

        /// <summary>
        /// 获取所有输入值（用于测试验证）
        /// </summary>
        public bool[] GetAllInputs()
        {
            var result = new bool[InputCount];
            Array.Copy(_inputs, result, InputCount);
            return result;
        }

        /// <summary>
        /// 获取所有输出值（用于测试验证）
        /// </summary>
        public bool[] GetAllOutputs()
        {
            var result = new bool[OutputCount];
            Array.Copy(_outputs, result, OutputCount);
            return result;
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            Disconnect();
            _disposed = true;
        }
    }
}
