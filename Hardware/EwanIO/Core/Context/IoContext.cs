using System;
using System.Collections.Generic;
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
    ///
    /// 文件结构（partial class）：
    /// - IoContext.cs          : 字段、属性、构造函数、Dispose
    /// - IoContext.Tick.cs     : Tick 核心同步循环、输出下发、索引验证
    /// - IoContext.Output.cs   : On/Off/Pulse 输出操作
    /// - IoContext.Read.cs     : GetInput/GetOutput 读取方法
    /// - IoContext.Wait.cs     : Until/Confirm 等待操作
    /// - IoContext.Accessors.cs: 嵌套访问器类
    /// </summary>
    public partial class IoContext<TLayout> : IDisposable where TLayout : class, new()
    {
        #region 常量

        private const int BitsPerPort = 32;

        #endregion

        #region 核心组件

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
        private readonly PulseManager _pulseManager;

        #endregion

        #region 子对象（对外暴露）

        private readonly EdgeAccessor _edgeAccessor;
        private readonly EdgeAccessor _preMapEdgeAccessor;
        private readonly EdgeAccessor _noSimEdgeAccessor;
        private readonly EdgeAccessor _hwEdgeAccessor;
        private readonly SimAccessor _simAccessor;
        private readonly MetaAccessor _metaAccessor;
        private readonly MappingAccessor _mappingAccessor;

        #endregion

        #region Wait/Confirm

        private readonly List<WaitOperation> _waitOperations = new List<WaitOperation>();
        private readonly object _waitLock = new object();
        private readonly ManualResetEventSlim _snapshotUpdated = new ManualResetEventSlim(false);

        #endregion

        #region 配置

        private readonly string _id;
        private readonly int _defaultConfirmTimeoutMs;
        private readonly IoContextOptions _options;
        private long _tickCounter;
        private bool _disposed;

        #endregion

        #region Layout 实例

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

        #endregion

        #region 健康状态

        private readonly IoHealth _health;
        private System.Diagnostics.Stopwatch? _tickStopwatch;

        #endregion

        #region 事件

        public event EventHandler<IoHealthEventArgs>? HealthChanged;

        #endregion

        #region 属性

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
        public PulseManager PulseState => _pulseManager;

        #endregion

        #region 构造函数

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

            _pulseManager = new PulseManager(outputCount);

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
