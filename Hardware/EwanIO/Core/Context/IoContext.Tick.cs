using System;
using System.Collections.Generic;
using System.Threading;
using EwanIO.Core.Data;
using EwanIO.Core.Interfaces;

namespace EwanIO.Core.Context
{
    /// <summary>
    /// IoContext - Tick 核心同步循环
    /// </summary>
    public partial class IoContext<TLayout> where TLayout : class, new()
    {
        #region Tick（10ms 热路径）

        /// <summary>
        /// Tick - 核心同步循环（外部 10ms 调用）
        /// 职责：
        /// 1. 硬件输入同步
        /// 2. 应用模拟 + 映射（NO/NC）
        /// 3. 构建 Snapshot
        /// 4. 边缘检测
        /// 5. 下发 dirty 输出
        /// </summary>
        public void Tick()
        {
            if (_disposed) return;

            // 性能监控
            _tickStopwatch?.Restart();

            try
            {
                lock (_ioLock)
                {
                    if (_disposed) return;

                    // 1. 硬件输入同步（优化：如果硬件支持独立输入同步，只同步输入）
                    if (_hardware is IHardwareIOExtended extendedHw && extendedHw.Capabilities.HasInputSync())
                    {
                        extendedHw.InputSync();
                    }
                    else
                    {
                        _hardware.DataSync();
                    }

                    // 2. 构建 back snapshot
                    var back = _snapshot.GetBackBuffer();

                    // 2.1 刷新输入（硬件 → 模拟 → 映射 → 逻辑值）
                    for (int logicalIdx = 0; logicalIdx < _mapping.InputCount; logicalIdx++)
                    {
                        int physicalIdx = _mapping.GetInputPhysicalIndex(logicalIdx);
                        bool hardwareValue = _hardware.ReadInBit(physicalIdx);
                        bool preMapValue = _physicalSimulator.ApplySimulate(logicalIdx, hardwareValue);
                        bool mappedValue = _mapping.ApplyInputLogic(logicalIdx, preMapValue);
                        bool logicalValue = _logicalSimulator.ApplySimulate(logicalIdx, mappedValue);
                        bool noSimLogicalValue = _mapping.ApplyInputLogic(logicalIdx, hardwareValue);

                        back.SetHardwareInput(logicalIdx, hardwareValue);
                        back.SetPreMapInput(logicalIdx, preMapValue);
                        back.SetNoSimInput(logicalIdx, noSimLogicalValue);
                        back.SetInput(logicalIdx, logicalValue);
                    }

                    // 2.2 刷新输出（从 Command 读取当前状态）
                    for (int logicalIdx = 0; logicalIdx < _mapping.OutputCount; logicalIdx++)
                    {
                        bool logicalValue;
                        if (_useBulkWrite)
                        {
                            logicalValue = ((CommandOptimized)_command).GetOutput(logicalIdx);
                        }
                        else
                        {
                            logicalValue = ((Command)_command).GetOutput(logicalIdx);
                        }
                        back.SetOutput(logicalIdx, logicalValue);
                    }

                    // 3. 原子交换 front/back
                    _snapshot.Swap();

                    // 3.1 同步值到 _readLayout（支持 R.PropertyName 访问）
                    _meta.SyncInputsToLayout(_readLayoutBack, _snapshot.Current.GetInput);
                    _meta.SyncOutputsToLayout(_readLayoutBack, _snapshot.Current.GetOutput);
                    var oldFront = Interlocked.Exchange(ref _readLayoutFront, _readLayoutBack);
                    _readLayoutBack = oldFront;

                    // 3.2 同步映射前输入到 _preMapLayout（支持 PreMap.PropertyName 访问）
                    _meta.SyncInputsToLayout(_preMapLayoutBack, _snapshot.Current.GetPreMapInput);
                    _meta.SyncOutputsToLayout(_preMapLayoutBack, _snapshot.Current.GetOutput);
                    var oldPreMapFront = Interlocked.Exchange(ref _preMapLayoutFront, _preMapLayoutBack);
                    _preMapLayoutBack = oldPreMapFront;

                    // 3.3 同步绕过模拟输入到 _noSimLayout（支持 NoSim.PropertyName 访问）
                    _meta.SyncInputsToLayout(_noSimLayoutBack, _snapshot.Current.GetNoSimInput);
                    _meta.SyncOutputsToLayout(_noSimLayoutBack, _snapshot.Current.GetOutput);
                    var oldNoSimFront = Interlocked.Exchange(ref _noSimLayoutFront, _noSimLayoutBack);
                    _noSimLayoutBack = oldNoSimFront;

                    // 3.4 同步硬件输入到 _hwLayout（支持 Hw.PropertyName 访问）
                    _meta.SyncInputsToLayout(_hwLayoutBack, _snapshot.Current.GetHardwareInput);
                    _meta.SyncOutputsToLayout(_hwLayoutBack, _snapshot.Current.GetOutput);
                    var oldHwFront = Interlocked.Exchange(ref _hwLayoutFront, _hwLayoutBack);
                    _hwLayoutBack = oldHwFront;

                    // 4. 更新边缘检测（基于新的 Snapshot，使用数组引用避免委托开销）
                    _edgeManager.Update(_snapshot.Current.GetInputsRef());
                    _preMapEdgeManager.Update(_snapshot.Current.GetPreMapInputsRef());
                    _noSimEdgeManager.Update(_snapshot.Current.GetNoSimInputsRef());
                    _hwEdgeManager.Update(_snapshot.Current.GetHardwareInputsRef());

                    // 5. 下发 dirty 输出
                    FlushOutputsIfDirty();

                    // 6. 更新脉冲状态（使用绝对时间计时）
                    _pulseManager.Update(SetOutputInternal);

                    // 7. 递增 tick 计数器
                    Interlocked.Increment(ref _tickCounter);
                    _snapshot.Current.IncrementTick();

                    // 7. 通知等待操作
                    NotifyWaitOperations();
                }
            }
            catch (Exception ex)
            {
                _health.RecordError(ex.Message);
                OnHealthChanged("Error", ex.Message);
                throw;
            }
            finally
            {
                // 记录性能
                if (_tickStopwatch != null)
                {
                    _health.RecordTick(_tickStopwatch.Elapsed.TotalMilliseconds);
                }
            }
        }

        /// <summary>
        /// 下发 dirty 输出（如果有）
        /// </summary>
        private void FlushOutputsIfDirty()
        {
            if (_useBulkWrite)
            {
                FlushOutputsWithBulkWrite();
            }
            else
            {
                FlushOutputsWithBitWrite();
            }
        }

        /// <summary>
        /// 使用端口批量写入下发输出（优化模式）
        /// </summary>
        private void FlushOutputsWithBulkWrite()
        {
            var cmdOpt = (CommandOptimized)_command;
            if (!cmdOpt.HasDirty)
                return;

            var extHw = (IHardwareIOExtended)_hardware;

            if (_bulkPhysicalPortValues == null || _bulkDirtyPhysicalPorts == null)
                return;

            int physicalPortCount = _bulkPhysicalPortValues.Length;

            // 快路径：输出映射保持 1:1（且不跨端口），可以直接写 logical portValue（可选 XOR 反转掩码）
            if (_mapping.IsOutputIdentityMapping)
            {
                bool wroteAny = false;

                if (!_mapping.HasOutputInversion)
                {
                    cmdOpt.FlushDirtyPorts((logicalPortIndex, logicalPortValue) =>
                    {
                        if ((uint)logicalPortIndex >= (uint)physicalPortCount)
                            return;

                        uint mask = GetValidPortMask(logicalPortIndex, _hardware.OutputCount);
                        if (mask == 0)
                            return;

                        extHw.WriteBulkOutputPort(logicalPortIndex, logicalPortValue & mask);
                        wroteAny = true;
                    });
                }
                else
                {
                    cmdOpt.FlushDirtyPorts((logicalPortIndex, logicalPortValue) =>
                    {
                        if ((uint)logicalPortIndex >= (uint)physicalPortCount)
                            return;

                        int validBitCount = GetValidBitCountInPort(logicalPortIndex, _hardware.OutputCount);
                        if (validBitCount <= 0)
                            return;

                        uint invertMask = BuildIdentityOutputInvertMask(logicalPortIndex, validBitCount);
                        uint mask = validBitCount == BitsPerPort ? 0xFFFF_FFFFu : ((1u << validBitCount) - 1u);
                        extHw.WriteBulkOutputPort(logicalPortIndex, (logicalPortValue ^ invertMask) & mask);
                        wroteAny = true;
                    });
                }

                if (wroteAny && extHw.Capabilities.HasOutputSync())
                {
                    extHw.OutputSync();
                }

                return;
            }

            // 通用路径：支持跨端口/重排映射。只为受影响的物理端口构建端口值（每端口 32bit），避免全量扫描输出。
            Array.Clear(_bulkDirtyPhysicalPorts, 0, _bulkDirtyPhysicalPorts.Length);

            cmdOpt.ConsumeDirtyPorts(
                onDirtyPort: logicalPortIndex =>
                {
                    int startBit = logicalPortIndex * BitsPerPort;
                    int endBit = Math.Min(startBit + BitsPerPort, _mapping.OutputCount);

                    for (int logicalIdx = startBit; logicalIdx < endBit; logicalIdx++)
                    {
                        int physicalIdx = _mapping.GetOutputPhysicalIndex(logicalIdx);
                        if ((uint)physicalIdx >= (uint)_hardware.OutputCount)
                            continue;

                        int physicalPortIndex = physicalIdx / BitsPerPort;
                        if ((uint)physicalPortIndex < (uint)_bulkDirtyPhysicalPorts.Length)
                        {
                            _bulkDirtyPhysicalPorts[physicalPortIndex] = true;
                        }
                    }
                },
                afterConsume: () =>
                {
                    for (int physicalPortIndex = 0; physicalPortIndex < _bulkDirtyPhysicalPorts.Length; physicalPortIndex++)
                    {
                        if (!_bulkDirtyPhysicalPorts[physicalPortIndex])
                            continue;

                        _bulkPhysicalPortValues[physicalPortIndex] = BuildPhysicalPortValueFromCommand(cmdOpt, physicalPortIndex);
                    }
                });

            bool wrote = false;
            for (int physicalPortIndex = 0; physicalPortIndex < _bulkDirtyPhysicalPorts.Length; physicalPortIndex++)
            {
                if (!_bulkDirtyPhysicalPorts[physicalPortIndex])
                    continue;

                extHw.WriteBulkOutputPort(physicalPortIndex, _bulkPhysicalPortValues[physicalPortIndex]);
                wrote = true;
            }

            if (wrote && extHw.Capabilities.HasOutputSync())
            {
                extHw.OutputSync();
            }
        }

        private static int GetValidBitCountInPort(int portIndex, int totalBitCount)
        {
            int startBit = portIndex * BitsPerPort;
            int remaining = totalBitCount - startBit;
            if (remaining <= 0)
                return 0;
            return remaining >= BitsPerPort ? BitsPerPort : remaining;
        }

        private static uint GetValidPortMask(int portIndex, int totalBitCount)
        {
            int validBitCount = GetValidBitCountInPort(portIndex, totalBitCount);
            if (validBitCount <= 0)
                return 0;
            return validBitCount == BitsPerPort ? 0xFFFF_FFFFu : ((1u << validBitCount) - 1u);
        }

        private uint BuildIdentityOutputInvertMask(int logicalPortIndex, int validBitCount)
        {
            uint invertMask = 0;
            int startBit = logicalPortIndex * BitsPerPort;
            int endBit = startBit + validBitCount;

            for (int logicalIdx = startBit; logicalIdx < endBit; logicalIdx++)
            {
                if (_mapping.IsOutputNormallyClosed(logicalIdx))
                {
                    invertMask |= (1u << (logicalIdx - startBit));
                }
            }

            return invertMask;
        }

        private uint BuildPhysicalPortValueFromCommand(CommandOptimized cmdOpt, int physicalPortIndex)
        {
            uint portValue = 0;

            int startPhysicalBit = physicalPortIndex * BitsPerPort;
            int endPhysicalBit = Math.Min(startPhysicalBit + BitsPerPort, _hardware.OutputCount);

            for (int physicalIdx = startPhysicalBit; physicalIdx < endPhysicalBit; physicalIdx++)
            {
                int logicalIdx = _mapping.GetOutputLogicalIndexFromPhysical(physicalIdx);
                if ((uint)logicalIdx >= (uint)_mapping.OutputCount)
                    continue;

                bool logicalValue = cmdOpt.GetOutput(logicalIdx);
                bool physicalValue = _mapping.ApplyOutputLogic(logicalIdx, logicalValue);
                if (physicalValue)
                {
                    portValue |= (1u << (physicalIdx - startPhysicalBit));
                }
            }

            return portValue;
        }

        /// <summary>
        /// 使用逐位写入下发输出（兼容模式）
        /// </summary>
        private void FlushOutputsWithBitWrite()
        {
            var cmd = (Command)_command;
            if (!cmd.HasDirty)
                return;

            cmd.FlushDirty((logicalIdx, logicalValue) =>
            {
                int physicalIdx = _mapping.GetOutputPhysicalIndex(logicalIdx);
                if ((uint)physicalIdx >= (uint)_hardware.OutputCount)
                    return;
                bool physicalValue = _mapping.ApplyOutputLogic(logicalIdx, logicalValue);
                _hardware.WriteOutBit(physicalIdx, physicalValue);
            });

            // 优化：如果硬件支持独立输出同步，调用 OutputSync
            if (_hardware is IHardwareIOExtended extendedHw && extendedHw.Capabilities.HasOutputSync())
            {
                extendedHw.OutputSync();
            }
        }

        /// <summary>
        /// 触发健康状态变化事件
        /// </summary>
        private void OnHealthChanged(string eventType, string? message = null)
        {
            HealthChanged?.Invoke(this, new IoHealthEventArgs(_health, eventType, message));
        }

        #endregion

        #region 索引验证

        private bool EnsureInputIndex(int index, string caller)
        {
            if ((uint)index < (uint)_mapping.InputCount)
                return true;

            HandleIndexOutOfRange("Input", index, _mapping.InputCount, caller);
            return false;
        }

        private bool EnsureOutputIndex(int index, string caller)
        {
            if ((uint)index < (uint)_mapping.OutputCount)
                return true;

            HandleIndexOutOfRange("Output", index, _mapping.OutputCount, caller);
            return false;
        }

        private void HandleIndexOutOfRange(string kind, int index, int count, string caller)
        {
            if (_options.IndexOutOfRangeBehavior == IndexOutOfRangeBehavior.Ignore)
                return;

            string message = $"IoContext({_id}) {caller}: {kind} index {index} is out of range (valid: [0, {count})).";

            if (_options.IndexOutOfRangeBehavior == IndexOutOfRangeBehavior.Throw)
                throw new ArgumentOutOfRangeException(nameof(index), index, message);

            _health.RecordError(message);
            OnHealthChanged("IndexOutOfRange", message);
        }

        private void HandleMappingConflict(string kind, int physicalIndex, int logicalIndex, int existingLogicalIndex, string caller)
        {
            string message = $"IoContext({_id}) {caller}: {kind} physical index {physicalIndex} is mapped by multiple logical indices ({existingLogicalIndex}, {logicalIndex}).";
            _health.RecordError(message);
            OnHealthChanged("MappingConflict", message);
            throw new InvalidOperationException(message);
        }

        private void EnsureUniqueInputPhysicalMapping(string caller)
        {
            var physicalToLogical = new Dictionary<int, int>();
            for (int logicalIndex = 0; logicalIndex < _mapping.InputCount; logicalIndex++)
            {
                int physicalIndex = _mapping.GetInputPhysicalIndex(logicalIndex);
                if ((uint)physicalIndex >= (uint)_hardware.InputCount)
                    continue;

                if (physicalToLogical.TryGetValue(physicalIndex, out var existingLogical) && existingLogical != logicalIndex)
                {
                    string message = $"IoContext({_id}) {caller}: Input physical index {physicalIndex} is mapped by multiple logical indices ({existingLogical}, {logicalIndex}).";
                    _health.RecordError(message);
                    OnHealthChanged("MappingConflict", message);
                    throw new InvalidOperationException(message);
                }

                physicalToLogical[physicalIndex] = logicalIndex;
            }
        }

        private void EnsureUniqueOutputPhysicalMapping(string caller)
        {
            var physicalToLogical = new Dictionary<int, int>();
            for (int logicalIndex = 0; logicalIndex < _mapping.OutputCount; logicalIndex++)
            {
                int physicalIndex = _mapping.GetOutputPhysicalIndex(logicalIndex);
                if ((uint)physicalIndex >= (uint)_hardware.OutputCount)
                    continue;

                if (physicalToLogical.TryGetValue(physicalIndex, out var existingLogical) && existingLogical != logicalIndex)
                {
                    string message = $"IoContext({_id}) {caller}: Output physical index {physicalIndex} is mapped by multiple logical indices ({existingLogical}, {logicalIndex}).";
                    _health.RecordError(message);
                    OnHealthChanged("MappingConflict", message);
                    throw new InvalidOperationException(message);
                }

                physicalToLogical[physicalIndex] = logicalIndex;
            }
        }

        #endregion
    }
}
