using System;
using EwanIO.Core.Interfaces;
using EwanIO.Core.Mapping;

namespace EwanIO.Core.Context
{
    /// <summary>
    /// IoContext 构建器 - Fluent API
    /// </summary>
    public class IoContextBuilder<TLayout> where TLayout : class, new()
    {
        private string _id = "IoContext";
        private IHardwareIO? _hardware;
        private int _defaultConfirmTimeoutMs = 800;
        private string? _mappingFilePath;
        private MappingConfigFile? _mappingConfig;
        private readonly IoContextOptions _options = new IoContextOptions();

        public IoContextBuilder<TLayout> WithId(string id)
        {
            _id = id;
            return this;
        }

        public IoContextBuilder<TLayout> WithHardware(IHardwareIO hardware)
        {
            _hardware = hardware;
            return this;
        }

        public IoContextBuilder<TLayout> WithHardware(Func<HardwareConfigurator, IHardwareIO> configureHardware)
        {
            var configurator = new HardwareConfigurator();
            _hardware = configureHardware(configurator);
            return this;
        }

        public IoContextBuilder<TLayout> WithConfirmTimeout(TimeSpan timeout)
        {
            _defaultConfirmTimeoutMs = (int)timeout.TotalMilliseconds;
            return this;
        }

        public IoContextBuilder<TLayout> WithMapping(string mappingFilePath)
        {
            _mappingFilePath = mappingFilePath;
            return this;
        }

        public IoContextBuilder<TLayout> WithMappingConfig(MappingConfigFile config)
        {
            _mappingConfig = config;
            return this;
        }

        public IoContextBuilder<TLayout> WithOptions(IoContextOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            _options.IndexOutOfRangeBehavior = options.IndexOutOfRangeBehavior;
            return this;
        }

        public IoContextBuilder<TLayout> WithIndexOutOfRangeBehavior(IndexOutOfRangeBehavior behavior)
        {
            _options.IndexOutOfRangeBehavior = behavior;
            return this;
        }

        public IoContextBuilder<TLayout> WithMapping(Action<MappingConfigurator> configureMapping)
        {
            var configurator = new MappingConfigurator();
            configureMapping(configurator);
            _mappingConfig = configurator.Build();
            return this;
        }

        public IoContext<TLayout> Build()
        {
            if (_hardware == null)
                throw new InvalidOperationException("Hardware must be configured before building IoContext");

            var options = new IoContextOptions
            {
                IndexOutOfRangeBehavior = _options.IndexOutOfRangeBehavior
            };
            var context = new IoContext<TLayout>(_id, _hardware, _defaultConfirmTimeoutMs, options);

            // 加载映射配置
            if (!string.IsNullOrEmpty(_mappingFilePath))
            {
                context.Mapping.Load(_mappingFilePath);
            }
            else if (_mappingConfig != null)
            {
                context.Mapping.LoadConfig(_mappingConfig);
            }

            return context;
        }

        public IoContext<TLayout> BuildAndConnect(string connectionString)
        {
            if (_hardware == null)
                throw new InvalidOperationException("Hardware must be configured before building IoContext");

            if (!_hardware.Connect(connectionString))
                throw new InvalidOperationException($"Failed to connect to hardware: {connectionString}");

            // 先连接再 Build，确保 IoContext 构造时能正确记录连接状态（IoHealth）
            return Build();
        }
    }

    /// <summary>
    /// 硬件配置器
    /// </summary>
    public class HardwareConfigurator
    {
        // 占位：后续扩展
    }

    /// <summary>
    /// 映射配置器
    /// </summary>
    public class MappingConfigurator
    {
        private readonly MappingConfigFile _config = new MappingConfigFile();

        public MappingConfigurator SetInput(int logicalIndex, int physicalIndex, bool isNormallyClosed = false, string? comment = null)
        {
            _config.Inputs.Add(new MappingEntry
            {
                LogicalIndex = logicalIndex,
                PhysicalIndex = physicalIndex,
                IsNormallyClosed = isNormallyClosed,
                Comment = comment
            });
            return this;
        }

        public MappingConfigurator SetOutput(int logicalIndex, int physicalIndex, bool isNormallyClosed = false, string? comment = null)
        {
            _config.Outputs.Add(new MappingEntry
            {
                LogicalIndex = logicalIndex,
                PhysicalIndex = physicalIndex,
                IsNormallyClosed = isNormallyClosed,
                Comment = comment
            });
            return this;
        }

        public MappingConfigurator WithDescription(string description)
        {
            _config.Description = description;
            return this;
        }

        internal MappingConfigFile Build() => _config;
    }

    /// <summary>
    /// 静态工厂方法
    /// </summary>
    public static class IoContextBuilder
    {
        public static IoContextBuilder<TLayout> For<TLayout>() where TLayout : class, new()
        {
            return new IoContextBuilder<TLayout>();
        }
    }
}
