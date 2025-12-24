using System;
using System.Threading;

namespace EwanIO.Core.Simulation
{
    /// <summary>
    /// 模拟模式
    /// </summary>
    public enum SimMode : byte
    {
        /// <summary>
        /// 无模拟，读硬件
        /// </summary>
        None = 0,
        /// <summary>
        /// 强制 ON
        /// </summary>
        ForceOn = 1,
        /// <summary>
        /// 强制 OFF
        /// </summary>
        ForceOff = 2
    }

    /// <summary>
    /// 模拟管理器 - 线程安全实现
    /// 职责单一：只负责模拟覆盖（ForceOn/ForceOff/None）
    /// 边缘检测由 EdgeManager 统一处理
    /// 线程安全：使用 Interlocked 实现无锁原子操作
    /// </summary>
    public class SimManager
    {
        private readonly int[] _modes; // 使用 int 以支持 Interlocked
        private int _simulatedCount;

        public int Count { get; }
        public int SimulatedCount => Volatile.Read(ref _simulatedCount);

        public SimManager(int inputCount)
        {
            Count = inputCount;
            _modes = new int[inputCount];
            _simulatedCount = 0;
        }

        /// <summary>
        /// 设置模拟模式（线程安全）
        /// </summary>
        public void SetSimulate(int logicalIndex, SimMode mode)
        {
            if ((uint)logicalIndex >= (uint)Count) return;

            int oldMode = Interlocked.Exchange(ref _modes[logicalIndex], (int)mode);

            // 原子更新计数
            if (oldMode == (int)SimMode.None && mode != SimMode.None)
                Interlocked.Increment(ref _simulatedCount);
            else if (oldMode != (int)SimMode.None && mode == SimMode.None)
                Interlocked.Decrement(ref _simulatedCount);
        }

        /// <summary>
        /// 强制 ON
        /// </summary>
        public void ForceOn(int logicalIndex)
        {
            SetSimulate(logicalIndex, SimMode.ForceOn);
        }

        /// <summary>
        /// 强制 OFF
        /// </summary>
        public void ForceOff(int logicalIndex)
        {
            SetSimulate(logicalIndex, SimMode.ForceOff);
        }

        /// <summary>
        /// 清除模拟
        /// </summary>
        public void ClearSimulate(int logicalIndex)
        {
            SetSimulate(logicalIndex, SimMode.None);
        }

        /// <summary>
        /// 清除所有模拟
        /// </summary>
        public void ClearAll()
        {
            for (int i = 0; i < Count; i++)
            {
                Volatile.Write(ref _modes[i], (int)SimMode.None);
            }
            Volatile.Write(ref _simulatedCount, 0);
        }

        /// <summary>
        /// 获取模拟模式（线程安全）
        /// </summary>
        public SimMode GetMode(int logicalIndex)
        {
            if ((uint)logicalIndex >= (uint)Count)
                return SimMode.None;
            return (SimMode)Volatile.Read(ref _modes[logicalIndex]);
        }

        /// <summary>
        /// 检查是否被模拟
        /// </summary>
        public bool IsSimulated(int logicalIndex)
        {
            if ((uint)logicalIndex >= (uint)Count)
                return false;
            return Volatile.Read(ref _modes[logicalIndex]) != (int)SimMode.None;
        }

        /// <summary>
        /// 尝试获取模拟值
        /// </summary>
        public bool TryGetSimulatedValue(int logicalIndex, out bool value)
        {
            if ((uint)logicalIndex >= (uint)Count)
            {
                value = false;
                return false;
            }

            var mode = (SimMode)Volatile.Read(ref _modes[logicalIndex]);
            if (mode == SimMode.None)
            {
                value = false;
                return false;
            }

            value = (mode == SimMode.ForceOn);
            return true;
        }

        /// <summary>
        /// 应用模拟到输入值（热路径优化）
        /// </summary>
        public bool ApplySimulate(int logicalIndex, bool hardwareValue)
        {
            // 快路径：无模拟时直接返回
            if (Volatile.Read(ref _simulatedCount) == 0)
                return hardwareValue;

            if ((uint)logicalIndex >= (uint)Count)
                return hardwareValue;

            var mode = (SimMode)Volatile.Read(ref _modes[logicalIndex]);
            return mode switch
            {
                SimMode.ForceOn => true,
                SimMode.ForceOff => false,
                _ => hardwareValue
            };
        }
    }
}
