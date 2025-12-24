using EwanIO.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EwanIO.Hardware.Composite
{
    /// <summary>
    /// 组合硬件IO
    /// 将多个硬件IO合并为一个逻辑硬件，统一管理输入输出点
    ///
    /// 示例：
    /// IOC0640: 输入0-63, 输出0-63
    /// SMC606:  输入64-103, 输出64-97
    /// 合并后:  输入0-103, 输出0-97
    /// </summary>
    public class CompositeHardwareIO : IHardwareIO, IDisposable
    {
        #region 内部类

        /// <summary>
        /// 硬件条目，包含硬件实例和偏移信息
        /// </summary>
        private class HardwareEntry
        {
            public IHardwareIO Hardware { get; set; }
            public string ConnectionString { get; set; }
            public int InputOffset { get; set; }
            public int OutputOffset { get; set; }

            public HardwareEntry(IHardwareIO hardware, string connectionString)
            {
                Hardware = hardware;
                ConnectionString = connectionString;
            }
        }

        #endregion

        #region 私有字段

        private readonly List<HardwareEntry> _hardwareEntries = new();
        private bool _isConnected = false;
        private bool _disposed = false;
        private int _totalInputCount = 0;
        private int _totalOutputCount = 0;

        // 性能优化：查找表，实现O(1)位索引查找
        private int[] _inputBitToHardwareIndex;   // 输入位 -> 硬件索引
        private int[] _inputBitToLocalBit;        // 输入位 -> 本地位索引
        private int[] _outputBitToHardwareIndex;  // 输出位 -> 硬件索引
        private int[] _outputBitToLocalBit;       // 输出位 -> 本地位索引

        #endregion

        #region IHardwareIO 属性

        public string HardwareType => "Composite";

        public string ConnectionInfo
        {
            get
            {
                var types = _hardwareEntries.Select(e => e.Hardware.HardwareType);
                return $"[{string.Join("+", types)}]";
            }
        }

        public bool IsConnected => _isConnected;

        public int InputCount => _totalInputCount;

        public int OutputCount => _totalOutputCount;

        #endregion

        #region 配置方法

        /// <summary>
        /// 添加硬件
        /// </summary>
        /// <param name="hardware">硬件IO实例</param>
        /// <param name="connectionString">连接字符串</param>
        /// <returns>返回自身，支持链式调用</returns>
        public CompositeHardwareIO AddHardware(IHardwareIO hardware, string connectionString)
        {
            ThrowIfDisposed();

            if (hardware == null)
                throw new ArgumentNullException(nameof(hardware));

            if (_isConnected)
                throw new InvalidOperationException("Cannot add hardware after connection is established");

            _hardwareEntries.Add(new HardwareEntry(hardware, connectionString));
            return this;
        }

        #endregion

        #region IHardwareIO 实现

        /// <summary>
        /// 连接所有硬件
        /// </summary>
        /// <param name="connectionString">此参数被忽略，使用AddHardware时指定的连接字符串</param>
        public bool Connect(string connectionString)
        {
            ThrowIfDisposed();

            if (_hardwareEntries.Count == 0)
            {
                return false;
            }

            try
            {
                // 计算偏移量并连接每个硬件
                int inputOffset = 0;
                int outputOffset = 0;

                foreach (var entry in _hardwareEntries)
                {
                    // 连接硬件
                    if (!entry.Hardware.Connect(entry.ConnectionString))
                    {
                        // 断开已连接的硬件
                        DisconnectAll();
                        return false;
                    }

                    // 记录偏移量
                    entry.InputOffset = inputOffset;
                    entry.OutputOffset = outputOffset;

                    // 累加计数
                    inputOffset += entry.Hardware.InputCount;
                    outputOffset += entry.Hardware.OutputCount;
                }

                _totalInputCount = inputOffset;
                _totalOutputCount = outputOffset;

                // 构建查找表，实现O(1)查找
                BuildLookupTables();

                _isConnected = true;

                return true;
            }
            catch (Exception ex)
            {
                DisconnectAll();
                return false;
            }
        }

        /// <summary>
        /// 断开所有硬件
        /// </summary>
        public bool Disconnect()
        {
            DisconnectAll();
            _isConnected = false;
            _totalInputCount = 0;
            _totalOutputCount = 0;

            // 清空查找表
            _inputBitToHardwareIndex = null;
            _inputBitToLocalBit = null;
            _outputBitToHardwareIndex = null;
            _outputBitToLocalBit = null;

            return true;
        }

        /// <summary>
        /// 同步所有硬件数据
        /// </summary>
        public void DataSync()
        {
            ThrowIfDisposed();

            if (!_isConnected) return;

            foreach (var entry in _hardwareEntries)
            {
                if (entry.Hardware.IsConnected)
                {
                    entry.Hardware.DataSync();
                }
            }
        }

        /// <summary>
        /// 读取输入位
        /// </summary>
        public bool ReadInBit(int bit)
        {
            ThrowIfDisposed();

            if (!_isConnected || bit < 0 || bit >= _totalInputCount)
                return false;

            var (entry, localBit) = FindInputHardware(bit);
            if (entry == null)
                return false;

            return entry.Hardware.ReadInBit(localBit);
        }

        /// <summary>
        /// 读取输出位
        /// </summary>
        public bool ReadOutBit(int bit)
        {
            ThrowIfDisposed();

            if (!_isConnected || bit < 0 || bit >= _totalOutputCount)
                return false;

            var (entry, localBit) = FindOutputHardware(bit);
            if (entry == null)
                return false;

            return entry.Hardware.ReadOutBit(localBit);
        }

        /// <summary>
        /// 写入输出位
        /// </summary>
        public bool WriteOutBit(int bit, bool value)
        {
            ThrowIfDisposed();

            if (!_isConnected || bit < 0 || bit >= _totalOutputCount)
                return false;

            var (entry, localBit) = FindOutputHardware(bit);
            if (entry == null)
                return false;

            return entry.Hardware.WriteOutBit(localBit, value);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 构建查找表，实现O(1)位索引查找
        /// 只在Connect时调用一次，后续查找无需遍历
        /// </summary>
        private void BuildLookupTables()
        {
            // 初始化查找表
            _inputBitToHardwareIndex = new int[_totalInputCount];
            _inputBitToLocalBit = new int[_totalInputCount];
            _outputBitToHardwareIndex = new int[_totalOutputCount];
            _outputBitToLocalBit = new int[_totalOutputCount];

            // 填充输入查找表
            for (int hwIndex = 0; hwIndex < _hardwareEntries.Count; hwIndex++)
            {
                var entry = _hardwareEntries[hwIndex];

                // 输入位映射
                for (int localBit = 0; localBit < entry.Hardware.InputCount; localBit++)
                {
                    int globalBit = entry.InputOffset + localBit;
                    if (globalBit < _totalInputCount)
                    {
                        _inputBitToHardwareIndex[globalBit] = hwIndex;
                        _inputBitToLocalBit[globalBit] = localBit;
                    }
                }

                // 输出位映射
                for (int localBit = 0; localBit < entry.Hardware.OutputCount; localBit++)
                {
                    int globalBit = entry.OutputOffset + localBit;
                    if (globalBit < _totalOutputCount)
                    {
                        _outputBitToHardwareIndex[globalBit] = hwIndex;
                        _outputBitToLocalBit[globalBit] = localBit;
                    }
                }
            }
        }

        /// <summary>
        /// 根据全局输入位索引查找对应的硬件和本地位索引 - O(1)复杂度
        /// </summary>
        private (HardwareEntry entry, int localBit) FindInputHardware(int globalBit)
        {
            // 使用查找表，O(1)复杂度
            if (_inputBitToHardwareIndex != null && globalBit >= 0 && globalBit < _inputBitToHardwareIndex.Length)
            {
                int hwIndex = _inputBitToHardwareIndex[globalBit];
                int localBit = _inputBitToLocalBit[globalBit];
                return (_hardwareEntries[hwIndex], localBit);
            }
            return (null, -1);
        }

        /// <summary>
        /// 根据全局输出位索引查找对应的硬件和本地位索引 - O(1)复杂度
        /// </summary>
        private (HardwareEntry entry, int localBit) FindOutputHardware(int globalBit)
        {
            // 使用查找表，O(1)复杂度
            if (_outputBitToHardwareIndex != null && globalBit >= 0 && globalBit < _outputBitToHardwareIndex.Length)
            {
                int hwIndex = _outputBitToHardwareIndex[globalBit];
                int localBit = _outputBitToLocalBit[globalBit];
                return (_hardwareEntries[hwIndex], localBit);
            }
            return (null, -1);
        }

        /// <summary>
        /// 断开所有已连接的硬件
        /// </summary>
        private void DisconnectAll()
        {
            foreach (var entry in _hardwareEntries)
            {
                try
                {
                    if (entry.Hardware.IsConnected)
                    {
                        entry.Hardware.Disconnect();
                    }
                }
                catch (Exception ex)
                {
                    // 断开失败，静默处理
                }
            }
        }

        #endregion

        #region 扩展方法

        /// <summary>
        /// 获取指定索引的硬件
        /// </summary>
        public IHardwareIO GetHardware(int index)
        {
            if (index < 0 || index >= _hardwareEntries.Count)
                return null;

            return _hardwareEntries[index].Hardware;
        }

        /// <summary>
        /// 获取硬件数量
        /// </summary>
        public int HardwareCount => _hardwareEntries.Count;

        /// <summary>
        /// 获取指定硬件的输入偏移量
        /// </summary>
        public int GetInputOffset(int hardwareIndex)
        {
            if (hardwareIndex < 0 || hardwareIndex >= _hardwareEntries.Count)
                return -1;

            return _hardwareEntries[hardwareIndex].InputOffset;
        }

        /// <summary>
        /// 获取指定硬件的输出偏移量
        /// </summary>
        public int GetOutputOffset(int hardwareIndex)
        {
            if (hardwareIndex < 0 || hardwareIndex >= _hardwareEntries.Count)
                return -1;

            return _hardwareEntries[hardwareIndex].OutputOffset;
        }

        #endregion

        #region IDisposable 实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // 断开所有硬件连接
                DisconnectAll();

                // 释放子硬件资源（如果它们实现了 IDisposable）
                foreach (var entry in _hardwareEntries)
                {
                    if (entry.Hardware is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch (Exception ex)
                        {
                            // 释放失败，静默处理
                        }
                    }
                }

                _hardwareEntries.Clear();
            }

            _disposed = true;
        }

        /// <summary>
        /// 检查是否已释放，如果已释放则抛出异常
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CompositeHardwareIO));
        }

        #endregion
    }
}
