using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using EwanIO.Core.Attributes;
using EwanIO.Core.Data;
using EwanIO.Core.EdgeDetection;
using EwanIO.Core.Interfaces;
using EwanIO.Core.Mapping;
using EwanIO.Core.Metadata;
using EwanIO.Core.Simulation;

namespace EwanIO.Core.Context
{
    /// <summary>
    /// IoContext - V2 核心上下文
    /// 特点：
    /// - 实例化（非静态单例）：同一 Layout 可创建多个上下文
    /// - 双缓冲 Snapshot：帧一致性 + 原子切换
    /// - Command 缓冲：线程安全写入 + dirty 标记
    /// - 零分配 Tick：热路径不分配对象
    /// </summary>
    public class IoContext<TLayout> : IDisposable where TLayout : class, new()
    {
        private const int BitsPerPort = 32;

        // 核心组件
        private readonly IHardwareIO _hardware;
        private readonly MetaManager<TLayout> _meta;
        private readonly MappingCache _mapping;
        private readonly SimManager _physicalSimulator; // 物理层模拟（PreMap：模拟后、映射前）
        private readonly SimManager _logicalSimulator;  // 逻辑层模拟（R：映射后）
        private readonly EdgeManager _edgeManager;
        private readonly EdgeManager _preMapEdgeManager;
        private readonly EdgeManager _noSimEdgeManager;
        private readonly EdgeManager _hwEdgeManager;
        private readonly DoubleBufferedSnapshot _snapshot;
        private readonly object _command; // Command or CommandOptimized
        private readonly bool _useBulkWrite;
        private readonly object _ioLock = new object();
        private readonly uint[]? _bulkPhysicalPortValues;
        private readonly bool[]? _bulkDirtyPhysicalPorts;
        private readonly int[] _pulseRemainingTicks;
        private readonly bool[] _pulseEndValues;

        // 子对象（对外暴露）
        private readonly EdgeAccessor _edgeAccessor;
        private readonly EdgeAccessor _preMapEdgeAccessor;
        private readonly EdgeAccessor _noSimEdgeAccessor;
        private readonly EdgeAccessor _hwEdgeAccessor;
        private readonly SimAccessor _simAccessor;
        private readonly MetaAccessor _metaAccessor;
        private readonly MappingAccessor _mappingAccessor;

        // Wait/Confirm
        private readonly List<WaitOperation> _waitOperations = new List<WaitOperation>();
        private readonly object _waitLock = new object();
        private readonly ManualResetEventSlim _snapshotUpdated = new ManualResetEventSlim(false);

        // 配置
        private readonly string _id;
        private readonly int _defaultConfirmTimeoutMs;
        private readonly IoContextOptions _options;
        private long _tickCounter;
        private bool _disposed;

        // 读取用的 Layout 实例（用于 R 属性直接访问）
        private TLayout _readLayoutFront;
        private TLayout _readLayoutBack;

        // 映射前读取用的 Layout 实例（用于 PreMap 属性直接访问：经过模拟，绕过 NO/NC 映射）
        private TLayout _preMapLayoutFront;
        private TLayout _preMapLayoutBack;

        // 绕过模拟读取用的 Layout 实例（用于 NoSim 属性直接访问：绕过模拟，应用 NO/NC 映射）
        private TLayout _noSimLayoutFront;
        private TLayout _noSimLayoutBack;

        // 硬件读取用的 Layout 实例（用于 Hw 属性直接访问：绕过模拟 + NO/NC 映射）
        private TLayout _hwLayoutFront;
        private TLayout _hwLayoutBack;

        // 健康状态
        private readonly IoHealth _health;
        private System.Diagnostics.Stopwatch? _tickStopwatch;

        // 事件
        public event EventHandler<IoHealthEventArgs>? HealthChanged;

        public string Id => _id;
        public TLayout R => Volatile.Read(ref _readLayoutFront);
        public TLayout PreMap => Volatile.Read(ref _preMapLayoutFront);
        public TLayout NoSim => Volatile.Read(ref _noSimLayoutFront);
        public TLayout Hw => Volatile.Read(ref _hwLayoutFront);
        public EdgeAccessor Edge => _edgeAccessor;
        public EdgeAccessor EdgePreMap => _preMapEdgeAccessor;
        public EdgeAccessor EdgeNoSim => _noSimEdgeAccessor;
        public EdgeAccessor EdgeHw => _hwEdgeAccessor;
        public SimAccessor Sim => _simAccessor;
        public MetaAccessor Meta => _metaAccessor;
        public MappingAccessor Mapping => _mappingAccessor;
        public long TickCounter => Interlocked.Read(ref _tickCounter);
        public IoHealth Health => _health;

        internal IoContext(
            string id,
            IHardwareIO hardware,
            int defaultConfirmTimeoutMs,
            IoContextOptions? options = null)
        {
            _id = id;
            _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
            _defaultConfirmTimeoutMs = defaultConfirmTimeoutMs;
            _options = options ?? new IoContextOptions();

            // 初始化元数据
            _meta = new MetaManager<TLayout>();

            // 初始化映射（默认 1:1）
            int inputCount = Math.Max(_meta.MaxInputIndex + 1, _hardware.InputCount);
            int outputCount = Math.Max(_meta.MaxOutputIndex + 1, _hardware.OutputCount);
            _mapping = new MappingCache(inputCount, outputCount);

            // 初始化模拟、边缘、快照、命令
            _physicalSimulator = new SimManager(inputCount);
            _logicalSimulator = new SimManager(inputCount);
            _edgeManager = new EdgeManager(inputCount);
            _preMapEdgeManager = new EdgeManager(inputCount);
            _noSimEdgeManager = new EdgeManager(inputCount);
            _hwEdgeManager = new EdgeManager(inputCount);
            _snapshot = new DoubleBufferedSnapshot(inputCount, outputCount);

            // 检测硬件是否支持批量写入
            _useBulkWrite = _hardware is IHardwareIOExtended extHw &&
                            extHw.Capabilities.HasBulkOutputWrite();

            if (_useBulkWrite)
            {
                _command = new CommandOptimized(outputCount);
            }
            else
            {
                _command = new Command(outputCount);
            }

            if (_useBulkWrite)
            {
                int physicalPortCount = (_hardware.OutputCount + BitsPerPort - 1) / BitsPerPort;
                _bulkPhysicalPortValues = new uint[physicalPortCount];
                _bulkDirtyPhysicalPorts = new bool[physicalPortCount];
            }

            _pulseRemainingTicks = new int[outputCount];
            _pulseEndValues = new bool[outputCount];

            // Layout 实例
            _readLayoutFront = new TLayout();
            _readLayoutBack = new TLayout();
            _preMapLayoutFront = new TLayout();
            _preMapLayoutBack = new TLayout();
            _noSimLayoutFront = new TLayout();
            _noSimLayoutBack = new TLayout();
            _hwLayoutFront = new TLayout();
            _hwLayoutBack = new TLayout();

            // 子对象
            _edgeAccessor = new EdgeAccessor(this, _edgeManager);
            _preMapEdgeAccessor = new EdgeAccessor(this, _preMapEdgeManager);
            _noSimEdgeAccessor = new EdgeAccessor(this, _noSimEdgeManager);
            _hwEdgeAccessor = new EdgeAccessor(this, _hwEdgeManager);
            _simAccessor = new SimAccessor(this);
            _metaAccessor = new MetaAccessor(this);
            _mappingAccessor = new MappingAccessor(this);

            // 健康状态
            _health = new IoHealth();
            _tickStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 如果硬件已连接，记录连接状态
            if (_hardware.IsConnected)
            {
                _health.RecordConnect();
            }

            _tickCounter = 0;
            _disposed = false;
        }

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

                    UpdatePulseCounters();

                    // 6. 递增 tick 计数器
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

        private void UpdatePulseCounters()
        {
            for (int i = 0; i < _pulseRemainingTicks.Length; i++)
            {
                int remaining = _pulseRemainingTicks[i];
                if (remaining <= 0)
                    continue;

                remaining--;
                if (remaining == 0)
                {
                    SetOutputInternal(i, _pulseEndValues[i]);
                }

                _pulseRemainingTicks[i] = remaining;
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

        #region Output（写输出）

        /// <summary>
        /// 立即下发当前 dirty 输出
        /// </summary>
        public void Flush()
        {
            if (_disposed) return;
            lock (_ioLock)
            {
                if (_disposed) return;
                FlushOutputsIfDirty();
            }
        }

        /// <summary>
        /// 快捷设置输出为 ON
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        public void On(Expression<Func<TLayout, OutputSignal>> expr, bool now = false)
        {
            int index = _meta.GetOutputIndex(expr);
            On(index, now);
        }

        /// <summary>
        /// 快捷设置输出为 ON（索引）
        /// </summary>
        public void On(int index, bool now = false)
        {
            WriteOutputInternal(index, true, now, nameof(On));
        }

        /// <summary>
        /// 快捷设置输出为 OFF
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        public void Off(Expression<Func<TLayout, OutputSignal>> expr, bool now = false)
        {
            int index = _meta.GetOutputIndex(expr);
            Off(index, now);
        }

        /// <summary>
        /// 快捷设置输出为 OFF（索引）
        /// </summary>
        public void Off(int index, bool now = false)
        {
            WriteOutputInternal(index, false, now, nameof(Off));
        }

        /// <summary>
        /// 快捷设置输出物理值为 ON（绕过 NO/NC 映射影响）
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        public void OnPhysical(Expression<Func<TLayout, OutputSignal>> expr, bool now = false)
        {
            int index = _meta.GetOutputIndex(expr);
            OnPhysical(index, now);
        }

        /// <summary>
        /// 快捷设置输出物理值为 ON（索引，绕过 NO/NC 映射影响）
        /// </summary>
        public void OnPhysical(int index, bool now = false)
        {
            WriteOutputPhysicalInternal(index, true, now, nameof(OnPhysical));
        }

        /// <summary>
        /// 快捷设置输出物理值为 OFF（绕过 NO/NC 映射影响）
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        public void OffPhysical(Expression<Func<TLayout, OutputSignal>> expr, bool now = false)
        {
            int index = _meta.GetOutputIndex(expr);
            OffPhysical(index, now);
        }

        /// <summary>
        /// 快捷设置输出物理值为 OFF（索引，绕过 NO/NC 映射影响）
        /// </summary>
        public void OffPhysical(int index, bool now = false)
        {
            WriteOutputPhysicalInternal(index, false, now, nameof(OffPhysical));
        }

        /// <summary>
        /// 输出脉冲（按 Tick 周期计数）
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="durationTicks">脉冲持续 Tick 数</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        /// <param name="value">脉冲值（true=ON->OFF，false=OFF->ON）</param>
        public void Pulse(Expression<Func<TLayout, OutputSignal>> expr, int durationTicks, bool now = false, bool value = true)
        {
            int index = _meta.GetOutputIndex(expr);
            Pulse(index, durationTicks, now, value);
        }

        /// <summary>
        /// 输出脉冲（索引）
        /// </summary>
        public void Pulse(int index, int durationTicks, bool now = false, bool value = true)
        {
            if (durationTicks <= 0)
                return;
            if (!EnsureOutputIndex(index, nameof(Pulse)))
                return;

            lock (_ioLock)
            {
                if (_disposed) return;
                if (_pulseRemainingTicks[index] > 0)
                    return;

                _pulseRemainingTicks[index] = durationTicks;
                _pulseEndValues[index] = !value;

                SetOutputInternal(index, value);

                if (now)
                {
                    FlushOutputsIfDirty();
                }
            }
        }

        /// <summary>
        /// 输出物理值脉冲（按 Tick 周期计数，绕过 NO/NC 映射影响）
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="durationTicks">脉冲持续 Tick 数</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        /// <param name="value">脉冲物理值（true=ON->OFF，false=OFF->ON）</param>
        public void PulsePhysical(Expression<Func<TLayout, OutputSignal>> expr, int durationTicks, bool now = false, bool value = true)
        {
            int index = _meta.GetOutputIndex(expr);
            PulsePhysical(index, durationTicks, now, value);
        }

        /// <summary>
        /// 输出物理值脉冲（索引，绕过 NO/NC 映射影响）
        /// </summary>
        public void PulsePhysical(int index, int durationTicks, bool now = false, bool value = true)
        {
            if (durationTicks <= 0)
                return;
            if (!EnsureOutputIndex(index, nameof(PulsePhysical)))
                return;

            lock (_ioLock)
            {
                if (_disposed) return;
                if (_pulseRemainingTicks[index] > 0)
                    return;

                bool isNc = _mapping.IsOutputNormallyClosed(index);
                bool logicalStart = value ^ isNc;
                bool logicalEnd = (!value) ^ isNc;

                _pulseRemainingTicks[index] = durationTicks;
                _pulseEndValues[index] = logicalEnd;

                SetOutputInternal(index, logicalStart);

                if (now)
                {
                    FlushOutputsIfDirty();
                }
            }
        }

        private void WriteOutputInternal(int index, bool value, bool now, string caller)
        {
            if (!EnsureOutputIndex(index, caller))
                return;

            lock (_ioLock)
            {
                if (_disposed) return;

                _pulseRemainingTicks[index] = 0;
                SetOutputInternal(index, value);

                if (now)
                {
                    FlushOutputsIfDirty();
                }
            }
        }

        private void WriteOutputPhysicalInternal(int index, bool physicalValue, bool now, string caller)
        {
            if (!EnsureOutputIndex(index, caller))
                return;

            bool logicalValue = physicalValue ^ _mapping.IsOutputNormallyClosed(index);

            lock (_ioLock)
            {
                if (_disposed) return;

                _pulseRemainingTicks[index] = 0;
                SetOutputInternal(index, logicalValue);

                if (now)
                {
                    FlushOutputsIfDirty();
                }
            }
        }

        private void SetOutputInternal(int index, bool value)
        {
            if (_useBulkWrite)
            {
                ((CommandOptimized)_command).SetOutput(index, value);
            }
            else
            {
                ((Command)_command).SetOutput(index, value);
            }
        }

        #endregion

        #region 按索引访问

        /// <summary>
        /// 按索引读取输入
        /// </summary>
        public bool GetInput(int index)
        {
            if (!EnsureInputIndex(index, nameof(GetInput)))
                return false;
            return _snapshot.Current.GetInput(index);
        }

        /// <summary>
        /// 按索引读取原始输入（经过模拟，未应用 NO/NC 映射）
        /// </summary>
        public bool GetPreMapInput(int index)
        {
            if (!EnsureInputIndex(index, nameof(GetPreMapInput)))
                return false;
            return _snapshot.Current.GetPreMapInput(index);
        }

        /// <summary>
        /// 按索引读取输入（绕过模拟，应用 NO/NC 映射）
        /// </summary>
        public bool GetNoSimInput(int index)
        {
            if (!EnsureInputIndex(index, nameof(GetNoSimInput)))
                return false;
            return _snapshot.Current.GetNoSimInput(index);
        }

        /// <summary>
        /// 按索引读取硬件输入（绕过模拟 + NO/NC 映射）
        /// </summary>
        public bool GetHardwareInput(int index)
        {
            if (!EnsureInputIndex(index, nameof(GetHardwareInput)))
                return false;
            return _snapshot.Current.GetHardwareInput(index);
        }

        /// <summary>
        /// 按表达式读取输入
        /// </summary>
        /// <param name="expr">输入选择表达式</param>
        /// <returns>输入状态</returns>
        public bool GetInput(Expression<Func<TLayout, InputSignal>> expr)
        {
            int index = _meta.GetInputIndex(expr);
            return _snapshot.Current.GetInput(index);
        }

        /// <summary>
        /// 按表达式读取原始输入（经过模拟，未应用 NO/NC 映射）
        /// </summary>
        /// <param name="expr">输入选择表达式</param>
        /// <returns>输入状态</returns>
        public bool GetPreMapInput(Expression<Func<TLayout, InputSignal>> expr)
        {
            int index = _meta.GetInputIndex(expr);
            return _snapshot.Current.GetPreMapInput(index);
        }

        /// <summary>
        /// 按表达式读取输入（绕过模拟，应用 NO/NC 映射）
        /// </summary>
        /// <param name="expr">输入选择表达式</param>
        /// <returns>输入状态</returns>
        public bool GetNoSimInput(Expression<Func<TLayout, InputSignal>> expr)
        {
            int index = _meta.GetInputIndex(expr);
            return _snapshot.Current.GetNoSimInput(index);
        }

        /// <summary>
        /// 按表达式读取硬件输入（绕过模拟 + NO/NC 映射）
        /// </summary>
        /// <param name="expr">输入选择表达式</param>
        /// <returns>输入状态</returns>
        public bool GetHardwareInput(Expression<Func<TLayout, InputSignal>> expr)
        {
            int index = _meta.GetInputIndex(expr);
            return _snapshot.Current.GetHardwareInput(index);
        }

        /// <summary>
        /// 按索引读取输出
        /// </summary>
        public bool GetOutput(int index)
        {
            if (!EnsureOutputIndex(index, nameof(GetOutput)))
                return false;
            return _snapshot.Current.GetOutput(index);
        }

        /// <summary>
        /// 按索引读取输出物理值（应用 NO/NC 映射）
        /// </summary>
        public bool GetPhysicalOutput(int index)
        {
            if (!EnsureOutputIndex(index, nameof(GetPhysicalOutput)))
                return false;
            bool logicalValue = _snapshot.Current.GetOutput(index);
            return _mapping.ApplyOutputLogic(index, logicalValue);
        }

        /// <summary>
        /// 按表达式读取输出
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <returns>输出状态</returns>
        public bool GetOutput(Expression<Func<TLayout, OutputSignal>> expr)
        {
            int index = _meta.GetOutputIndex(expr);
            return _snapshot.Current.GetOutput(index);
        }

        /// <summary>
        /// 按表达式读取输出物理值（应用 NO/NC 映射）
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <returns>输出物理状态</returns>
        public bool GetPhysicalOutput(Expression<Func<TLayout, OutputSignal>> expr)
        {
            int index = _meta.GetOutputIndex(expr);
            bool logicalValue = _snapshot.Current.GetOutput(index);
            return _mapping.ApplyOutputLogic(index, logicalValue);
        }

        #endregion

        #region Wait/Confirm

        /// <summary>
        /// 等待输入到达期望值
        /// </summary>
        public IoOp<bool> Until(
            Expression<Func<TLayout, InputSignal>> expr,
            bool expected = true,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            int index = _meta.GetInputIndex(expr);
            return UntilByIndex(index, expected, timeout, cancellationToken);
        }

        /// <summary>
        /// 等待输入到达期望值（按索引）
        /// </summary>
        public IoOp<bool> UntilByIndex(
            int index,
            bool expected = true,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return UntilByIndexInternal(index, expected, timeout, cancellationToken, i => GetInput(i));
        }

        /// <summary>
        /// 等待输入到达期望值（绕过 NO/NC 映射，保留模拟）
        /// </summary>
        public IoOp<bool> UntilPreMap(
            Expression<Func<TLayout, InputSignal>> expr,
            bool expected = true,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            int index = _meta.GetInputIndex(expr);
            return UntilPreMapByIndex(index, expected, timeout, cancellationToken);
        }

        /// <summary>
        /// 等待输入到达期望值（按索引，绕过 NO/NC 映射，保留模拟）
        /// </summary>
        public IoOp<bool> UntilPreMapByIndex(
            int index,
            bool expected = true,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return UntilByIndexInternal(index, expected, timeout, cancellationToken, i => GetPreMapInput(i));
        }

        /// <summary>
        /// 等待输入到达期望值（绕过模拟，应用 NO/NC 映射）
        /// </summary>
        public IoOp<bool> UntilNoSim(
            Expression<Func<TLayout, InputSignal>> expr,
            bool expected = true,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            int index = _meta.GetInputIndex(expr);
            return UntilNoSimByIndex(index, expected, timeout, cancellationToken);
        }

        /// <summary>
        /// 等待输入到达期望值（按索引，绕过模拟，应用 NO/NC 映射）
        /// </summary>
        public IoOp<bool> UntilNoSimByIndex(
            int index,
            bool expected = true,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return UntilByIndexInternal(index, expected, timeout, cancellationToken, i => GetNoSimInput(i));
        }

        /// <summary>
        /// 等待输入到达期望值（绕过模拟 + NO/NC 映射）
        /// </summary>
        public IoOp<bool> UntilHw(
            Expression<Func<TLayout, InputSignal>> expr,
            bool expected = true,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            int index = _meta.GetInputIndex(expr);
            return UntilHwByIndex(index, expected, timeout, cancellationToken);
        }

        /// <summary>
        /// 等待输入到达期望值（按索引，绕过模拟 + NO/NC 映射）
        /// </summary>
        public IoOp<bool> UntilHwByIndex(
            int index,
            bool expected = true,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return UntilByIndexInternal(index, expected, timeout, cancellationToken, i => GetHardwareInput(i));
        }

        /// <summary>
        /// 写输出 + 等待输入反馈
        /// </summary>
        public IoOp<bool> Confirm(
            Expression<Func<TLayout, OutputSignal>> output,
            bool value,
            Expression<Func<TLayout, InputSignal>> confirm,
            bool expected = true,
            TimeSpan? timeout = null,
            bool now = false,
            CancellationToken cancellationToken = default)
        {
            if (value)
            {
                On(output, now);
            }
            else
            {
                Off(output, now);
            }

            return Until(confirm, expected, timeout, cancellationToken);
        }

        /// <summary>
        /// 写输出 + 等待输入反馈（绕过 NO/NC 映射，保留模拟）
        /// </summary>
        public IoOp<bool> ConfirmPreMap(
            Expression<Func<TLayout, OutputSignal>> output,
            bool value,
            Expression<Func<TLayout, InputSignal>> confirm,
            bool expected = true,
            TimeSpan? timeout = null,
            bool now = false,
            CancellationToken cancellationToken = default)
        {
            if (value)
            {
                On(output, now);
            }
            else
            {
                Off(output, now);
            }

            return UntilPreMap(confirm, expected, timeout, cancellationToken);
        }

        /// <summary>
        /// 写输出 + 等待输入反馈（绕过模拟，应用 NO/NC 映射）
        /// </summary>
        public IoOp<bool> ConfirmNoSim(
            Expression<Func<TLayout, OutputSignal>> output,
            bool value,
            Expression<Func<TLayout, InputSignal>> confirm,
            bool expected = true,
            TimeSpan? timeout = null,
            bool now = false,
            CancellationToken cancellationToken = default)
        {
            if (value)
            {
                On(output, now);
            }
            else
            {
                Off(output, now);
            }

            return UntilNoSim(confirm, expected, timeout, cancellationToken);
        }

        /// <summary>
        /// 写输出 + 等待输入反馈（绕过模拟 + NO/NC 映射）
        /// </summary>
        public IoOp<bool> ConfirmHw(
            Expression<Func<TLayout, OutputSignal>> output,
            bool value,
            Expression<Func<TLayout, InputSignal>> confirm,
            bool expected = true,
            TimeSpan? timeout = null,
            bool now = false,
            CancellationToken cancellationToken = default)
        {
            if (value)
            {
                On(output, now);
            }
            else
            {
                Off(output, now);
            }

            return UntilHw(confirm, expected, timeout, cancellationToken);
        }

        private IoOp<bool> UntilByIndexInternal(
            int index,
            bool expected,
            TimeSpan? timeout,
            CancellationToken cancellationToken,
            Func<int, bool> readInput)
        {
            var op = new IoOp<bool>();

            // 解析超时时间：参数 > 标签 > 全局默认
            TimeSpan actualTimeout = timeout ?? TimeSpan.FromMilliseconds(
                _meta.GetInputConfirmTimeout(index, _defaultConfirmTimeoutMs));

            var waitOp = new WaitOperation(
                op,
                () => readInput(index),
                expected,
                actualTimeout);

            // 注册取消
            if (cancellationToken.CanBeCanceled)
            {
                waitOp.CancellationRegistration = cancellationToken.Register(() =>
                {
                    lock (_waitLock)
                    {
                        _waitOperations.Remove(waitOp);
                    }
                    waitOp.Cancel();
                });
            }

            lock (_waitLock)
            {
                _waitOperations.Add(waitOp);
            }

            // 立即检查一次
            CheckWaitOperation(waitOp);

            return op;
        }

        /// <summary>
        /// 通知等待操作（在 Tick 结束时调用）
        /// </summary>
        private void NotifyWaitOperations()
        {
            _snapshotUpdated.Set();

            lock (_waitLock)
            {
                if (_waitOperations.Count == 0)
                    return;

                for (int i = _waitOperations.Count - 1; i >= 0; i--)
                {
                    var waitOp = _waitOperations[i];
                    CheckWaitOperation(waitOp);

                    if (waitOp.Op.IsCompleted)
                    {
                        _waitOperations.RemoveAt(i);
                    }
                }
            }

            _snapshotUpdated.Reset();
        }

        /// <summary>
        /// 检查等待操作条件
        /// </summary>
        private void CheckWaitOperation(WaitOperation waitOp)
        {
            if (waitOp.Op.IsCompleted)
                return;

            // 检查超时
            if (waitOp.IsTimedOut)
            {
                _health.RecordTimeout();
                OnHealthChanged("Timeout", "Wait/Confirm operation timed out");
                waitOp.Complete(false);
                return;
            }

            // 检查条件
            if (waitOp.CheckCondition())
            {
                waitOp.Complete(true);
                return;
            }
        }

        private void ApplyMappingConfig(MappingConfigFile config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            bool reportRangeErrors = _options.IndexOutOfRangeBehavior != IndexOutOfRangeBehavior.Ignore;
            int hardwareInputCount = _hardware.InputCount;
            int hardwareOutputCount = _hardware.OutputCount;
            bool hasHardwareInputRange = hardwareInputCount > 0;
            bool hasHardwareOutputRange = hardwareOutputCount > 0;

            var validInputEntries = new List<MappingEntry>();
            foreach (var entry in config.Inputs)
            {
                if ((uint)entry.LogicalIndex >= (uint)_mapping.InputCount)
                {
                    if (reportRangeErrors)
                        HandleIndexOutOfRange("InputLogical", entry.LogicalIndex, _mapping.InputCount, "Mapping.LoadConfig");
                    continue;
                }

                if (entry.PhysicalIndex < 0)
                {
                    if (reportRangeErrors)
                        HandleIndexOutOfRange("InputPhysical", entry.PhysicalIndex, Math.Max(hardwareInputCount, 0), "Mapping.LoadConfig");
                    continue;
                }

                if (hasHardwareInputRange && (uint)entry.PhysicalIndex >= (uint)hardwareInputCount)
                {
                    if (reportRangeErrors)
                        HandleIndexOutOfRange("InputPhysical", entry.PhysicalIndex, hardwareInputCount, "Mapping.LoadConfig");
                    continue;
                }

                validInputEntries.Add(entry);
            }

            var validOutputEntries = new List<MappingEntry>();
            foreach (var entry in config.Outputs)
            {
                if ((uint)entry.LogicalIndex >= (uint)_mapping.OutputCount)
                {
                    if (reportRangeErrors)
                        HandleIndexOutOfRange("OutputLogical", entry.LogicalIndex, _mapping.OutputCount, "Mapping.LoadConfig");
                    continue;
                }

                if (entry.PhysicalIndex < 0)
                {
                    if (reportRangeErrors)
                        HandleIndexOutOfRange("OutputPhysical", entry.PhysicalIndex, Math.Max(hardwareOutputCount, 0), "Mapping.LoadConfig");
                    continue;
                }

                if (hasHardwareOutputRange && (uint)entry.PhysicalIndex >= (uint)hardwareOutputCount)
                {
                    if (reportRangeErrors)
                        HandleIndexOutOfRange("OutputPhysical", entry.PhysicalIndex, hardwareOutputCount, "Mapping.LoadConfig");
                    continue;
                }

                validOutputEntries.Add(entry);
            }

            var resultInputPhysical = new int[_mapping.InputCount];
            for (int i = 0; i < _mapping.InputCount; i++)
            {
                resultInputPhysical[i] = _mapping.GetInputPhysicalIndex(i);
            }
            foreach (var entry in validInputEntries)
            {
                resultInputPhysical[entry.LogicalIndex] = entry.PhysicalIndex;
            }

            var inputPhysicalToLogical = new Dictionary<int, int>();
            for (int logicalIndex = 0; logicalIndex < resultInputPhysical.Length; logicalIndex++)
            {
                int physicalIndex = resultInputPhysical[logicalIndex];
                if (physicalIndex < 0)
                    continue;
                if (hasHardwareInputRange && (uint)physicalIndex >= (uint)hardwareInputCount)
                    continue;

                if (inputPhysicalToLogical.TryGetValue(physicalIndex, out var existingLogical))
                {
                    if (existingLogical != logicalIndex)
                    {
                        HandleMappingConflict("InputPhysical", physicalIndex, logicalIndex, existingLogical, "Mapping.LoadConfig");
                    }
                }
                else
                {
                    inputPhysicalToLogical[physicalIndex] = logicalIndex;
                }
            }

            var resultPhysical = new int[_mapping.OutputCount];
            for (int i = 0; i < _mapping.OutputCount; i++)
            {
                resultPhysical[i] = _mapping.GetOutputPhysicalIndex(i);
            }
            foreach (var entry in validOutputEntries)
            {
                resultPhysical[entry.LogicalIndex] = entry.PhysicalIndex;
            }

            var physicalToLogical = new Dictionary<int, int>();
            for (int logicalIndex = 0; logicalIndex < resultPhysical.Length; logicalIndex++)
            {
                int physicalIndex = resultPhysical[logicalIndex];
                if (physicalIndex < 0)
                    continue;
                if (hasHardwareOutputRange && (uint)physicalIndex >= (uint)hardwareOutputCount)
                    continue;

                if (physicalToLogical.TryGetValue(physicalIndex, out var existingLogical))
                {
                    if (existingLogical != logicalIndex)
                    {
                        HandleMappingConflict("OutputPhysical", physicalIndex, logicalIndex, existingLogical, "Mapping.LoadConfig");
                    }
                }
                else
                {
                    physicalToLogical[physicalIndex] = logicalIndex;
                }
            }

            foreach (var entry in validInputEntries)
            {
                _mapping.SetInputMapping(entry.LogicalIndex, entry.PhysicalIndex, entry.IsNormallyClosed);
            }

            foreach (var entry in validOutputEntries)
            {
                _mapping.SetOutputMapping(entry.LogicalIndex, entry.PhysicalIndex, entry.IsNormallyClosed);
            }
        }

        #endregion

        #region 子对象访问器

        /// <summary>
        /// Edge 边缘检测访问器
        /// </summary>
        public class EdgeAccessor
        {
            private readonly IoContext<TLayout> _ctx;
            private readonly EdgeManager _edge;
            internal EdgeAccessor(IoContext<TLayout> ctx, EdgeManager edge)
            {
                _ctx = ctx;
                _edge = edge;
            }

            public bool R(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                return R(idx);
            }

            public bool F(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                return F(idx);
            }

            public bool PeekR(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                return PeekR(idx);
            }

            public bool PeekF(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                return PeekF(idx);
            }

            public void ClearR(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                ClearR(idx);
            }

            public void ClearF(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                ClearF(idx);
            }

            public bool R(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(R)))
                    return false;
                return _edge.ReadAndClearRising(index);
            }

            public bool F(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(F)))
                    return false;
                return _edge.ReadAndClearFalling(index);
            }

            public bool PeekR(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(PeekR)))
                    return false;
                return _edge.PeekRising(index);
            }

            public bool PeekF(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(PeekF)))
                    return false;
                return _edge.PeekFalling(index);
            }

            public void ClearR(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(ClearR)))
                    return;
                _edge.ClearRising(index);
            }

            public void ClearF(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(ClearF)))
                    return;
                _edge.ClearFalling(index);
            }
            public void ClearAll() => _edge.ClearAll();
        }

        /// <summary>
        /// Sim 模拟访问器
        /// </summary>
        public class SimAccessor
        {
            private readonly IoContext<TLayout> _ctx;
            internal SimAccessor(IoContext<TLayout> ctx) => _ctx = ctx;

            /// <summary>
            /// 强制“物理/映射前(PreMap)”输入为 ON（即模拟后、映射前值为 true）
            /// </summary>
            public void ForcePhysicalOn(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                _ctx._physicalSimulator.ForceOn(idx);
            }

            /// <summary>
            /// 强制“物理/映射前(PreMap)”输入为 OFF（即模拟后、映射前值为 false）
            /// </summary>
            public void ForcePhysicalOff(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                _ctx._physicalSimulator.ForceOff(idx);
            }

            /// <summary>
            /// 强制“物理/映射前(PreMap)”输入为 ON（索引）
            /// </summary>
            public void ForcePhysicalOn(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(ForcePhysicalOn)))
                    return;
                _ctx._physicalSimulator.ForceOn(index);
            }

            /// <summary>
            /// 强制“物理/映射前(PreMap)”输入为 OFF（索引）
            /// </summary>
            public void ForcePhysicalOff(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(ForcePhysicalOff)))
                    return;
                _ctx._physicalSimulator.ForceOff(index);
            }

            /// <summary>
            /// 清除“物理/映射前(PreMap)”模拟（表达式）
            /// </summary>
            public void ClearPhysicalSimulate(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                _ctx._physicalSimulator.ClearSimulate(idx);
            }

            /// <summary>
            /// 清除“物理/映射前(PreMap)”模拟（索引）
            /// </summary>
            public void ClearPhysicalSimulate(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(ClearPhysicalSimulate)))
                    return;
                _ctx._physicalSimulator.ClearSimulate(index);
            }

            /// <summary>
            /// 获取“物理/映射前(PreMap)”模拟模式（索引）
            /// </summary>
            public SimMode GetPhysicalMode(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(GetPhysicalMode)))
                    return SimMode.None;
                return _ctx._physicalSimulator.GetMode(index);
            }

            /// <summary>
            /// 强制“逻辑层(R)”输入为 ON（映射后值为 true；下一次 Tick 生效）
            /// </summary>
            public void ForceOn(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                _ctx._logicalSimulator.ForceOn(idx);
            }

            /// <summary>
            /// 强制“逻辑层(R)”输入为 OFF（映射后值为 false；下一次 Tick 生效）
            /// </summary>
            public void ForceOff(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                _ctx._logicalSimulator.ForceOff(idx);
            }

            /// <summary>
            /// 清除“逻辑层(R)”模拟（表达式）
            /// </summary>
            public void ClearSimulate(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                _ctx._logicalSimulator.ClearSimulate(idx);
            }

            public void ForceOn(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(ForceOn)))
                    return;
                _ctx._logicalSimulator.ForceOn(index);
            }

            public void ForceOff(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(ForceOff)))
                    return;
                _ctx._logicalSimulator.ForceOff(index);
            }

            public void ClearSimulate(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(ClearSimulate)))
                    return;
                _ctx._logicalSimulator.ClearSimulate(index);
            }

            public void ClearAll()
            {
                _ctx._logicalSimulator.ClearAll();
                _ctx._physicalSimulator.ClearAll();
            }

            public SimMode GetMode(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(GetMode)))
                    return SimMode.None;
                return _ctx._logicalSimulator.GetMode(index);
            }
        }

        /// <summary>
        /// Meta 元数据访问器
        /// </summary>
        public class MetaAccessor
        {
            private readonly IoContext<TLayout> _ctx;
            internal MetaAccessor(IoContext<TLayout> ctx) => _ctx = ctx;

            public string GetInputName(int index) => _ctx._meta.GetInputName(index);
            public string GetOutputName(int index) => _ctx._meta.GetOutputName(index);
            public IoMeta? GetInputMeta(int index) => _ctx._meta.GetInputMeta(index);
            public IoMeta? GetOutputMeta(int index) => _ctx._meta.GetOutputMeta(index);
            public int InputCount => _ctx._meta.InputCount;
            public int OutputCount => _ctx._meta.OutputCount;
        }

        /// <summary>
        /// Mapping 映射访问器
        /// </summary>
        public class MappingAccessor
        {
            private readonly IoContext<TLayout> _ctx;
            internal MappingAccessor(IoContext<TLayout> ctx) => _ctx = ctx;

            public void SetInputMapping(int logicalIndex, int physicalIndex, bool isNormallyClosed = false)
            {
                _ctx._mapping.SetInputMapping(logicalIndex, physicalIndex, isNormallyClosed);
            }

            public void SetOutputMapping(int logicalIndex, int physicalIndex, bool isNormallyClosed = false)
            {
                _ctx._mapping.SetOutputMapping(logicalIndex, physicalIndex, isNormallyClosed);
            }

            /// <summary>
            /// 获取输入映射的物理索引
            /// </summary>
            public int GetInputPhysicalIndex(int logicalIndex)
            {
                if (!_ctx.EnsureInputIndex(logicalIndex, nameof(GetInputPhysicalIndex)))
                    return logicalIndex;
                return _ctx._mapping.GetInputPhysicalIndex(logicalIndex);
            }

            /// <summary>
            /// 获取输出映射的物理索引
            /// </summary>
            public int GetOutputPhysicalIndex(int logicalIndex)
            {
                if (!_ctx.EnsureOutputIndex(logicalIndex, nameof(GetOutputPhysicalIndex)))
                    return logicalIndex;
                return _ctx._mapping.GetOutputPhysicalIndex(logicalIndex);
            }

            /// <summary>
            /// 检查输入是否为常闭 (NC)
            /// </summary>
            public bool IsInputNormallyClosed(int logicalIndex)
            {
                if (!_ctx.EnsureInputIndex(logicalIndex, nameof(IsInputNormallyClosed)))
                    return false;
                return _ctx._mapping.IsInputNormallyClosed(logicalIndex);
            }

            /// <summary>
            /// 检查输出是否为常闭 (NC)
            /// </summary>
            public bool IsOutputNormallyClosed(int logicalIndex)
            {
                if (!_ctx.EnsureOutputIndex(logicalIndex, nameof(IsOutputNormallyClosed)))
                    return false;
                return _ctx._mapping.IsOutputNormallyClosed(logicalIndex);
            }

            /// <summary>
            /// 从文件加载映射配置
            /// </summary>
            public void Load(string filePath)
            {
                var config = MappingConfigManager.Load(filePath);
                LoadConfig(config);
            }

            /// <summary>
            /// 保存映射配置到文件
            /// </summary>
            public void Save(string filePath)
            {
                _ctx.EnsureUniqueInputPhysicalMapping("Mapping.Save");
                _ctx.EnsureUniqueOutputPhysicalMapping("Mapping.Save");
                var config = MappingConfigManager.ExportFromCache(_ctx._mapping, _ctx._meta);
                MappingConfigManager.Save(filePath, config);
            }

            /// <summary>
            /// 加载映射配置对象
            /// </summary>
            public void LoadConfig(MappingConfigFile config)
            {
                _ctx.ApplyMappingConfig(config);
            }

            /// <summary>
            /// 生成默认映射配置
            /// </summary>
            public void GenerateDefaultMapping()
            {
                var config = MappingConfigManager.GenerateDefault(
                    _ctx._mapping.InputCount,
                    _ctx._mapping.OutputCount,
                    $"Default mapping for {_ctx._id}",
                    _ctx._meta);
                LoadConfig(config);
            }

            /// <summary>
            /// 生成默认映射配置并保存到文件
            /// </summary>
            public void GenerateDefaultMappingAndSave(string filePath)
            {
                GenerateDefaultMapping();
                Save(filePath);
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            lock (_ioLock)
            {
                if (_disposed) return;

                // 清理等待操作
                lock (_waitLock)
                {
                    foreach (var waitOp in _waitOperations)
                    {
                        waitOp.Cancel();
                    }
                    _waitOperations.Clear();
                }

                _snapshotUpdated.Dispose();
                _hardware.Dispose();

                _disposed = true;
            }
        }

        #endregion
    }
}
