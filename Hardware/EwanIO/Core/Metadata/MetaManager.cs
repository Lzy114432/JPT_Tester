using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using EwanIO.Core.Attributes;

namespace EwanIO.Core.Metadata
{
    /// <summary>
    /// 元数据名称提供者（用于映射注释等）
    /// </summary>
    public interface IMetaNameProvider
    {
        string GetInputName(int index);
        string GetOutputName(int index);
    }

    /// <summary>
    /// IO 元数据信息
    /// </summary>
    public class IoMeta
    {
        public int Index { get; }
        public string PropertyName { get; }
        public string DisplayName { get; }
        public int ConfirmTimeoutMs { get; }
        public PropertyInfo Property { get; }

        internal IoMeta(int index, string propertyName, string? displayName, int confirmTimeoutMs, PropertyInfo property)
        {
            Index = index;
            PropertyName = propertyName;
            DisplayName = displayName ?? propertyName;
            ConfirmTimeoutMs = confirmTimeoutMs;
            Property = property;
        }
    }

    /// <summary>
    /// 元数据管理器 - 从 Layout 类型扫描 IO 属性信息
    /// 零分配：在初始化时一次性扫描并缓存
    /// </summary>
    public class MetaManager<TLayout> : IMetaNameProvider where TLayout : class, new()
    {
        private readonly IoMeta[] _inputMetas;
        private readonly IoMeta[] _outputMetas;
        private readonly Dictionary<string, IoMeta> _inputByName;
        private readonly Dictionary<string, IoMeta> _outputByName;
        private readonly int _maxInputIndex;
        private readonly int _maxOutputIndex;

        // 预编译的 getter/setter 委托（性能优化）
        private readonly Func<TLayout, bool>[] _inputGetters;
        private readonly Func<TLayout, bool>[] _outputGetters;
        private readonly Action<TLayout, bool>[] _inputSetters;
        private readonly Action<TLayout, bool>[] _outputSetters;

        public int InputCount => _inputMetas.Length;
        public int OutputCount => _outputMetas.Length;
        public int MaxInputIndex => _maxInputIndex;
        public int MaxOutputIndex => _maxOutputIndex;

        public MetaManager()
        {
            var inputList = new List<IoMeta>();
            var outputList = new List<IoMeta>();
            _inputByName = new Dictionary<string, IoMeta>();
            _outputByName = new Dictionary<string, IoMeta>();
            var inputIndexToName = new Dictionary<int, string>();
            var outputIndexToName = new Dictionary<int, string>();

            var type = typeof(TLayout);
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<IOAttribute>();
                if (attr == null) continue;

                var meta = new IoMeta(attr.Index, prop.Name, attr.DisplayName, attr.ConfirmTimeoutMs, prop);

                if (prop.PropertyType == typeof(InputSignal))
                {
                    if (inputIndexToName.TryGetValue(attr.Index, out var existingName))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate input index {attr.Index} detected in layout '{type.Name}': " +
                            $"'{prop.Name}' conflicts with '{existingName}'.");
                    }

                    inputIndexToName[attr.Index] = prop.Name;
                    inputList.Add(meta);
                    _inputByName[prop.Name] = meta;
                }
                else if (prop.PropertyType == typeof(OutputSignal))
                {
                    if (outputIndexToName.TryGetValue(attr.Index, out var existingName))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate output index {attr.Index} detected in layout '{type.Name}': " +
                            $"'{prop.Name}' conflicts with '{existingName}'.");
                    }

                    outputIndexToName[attr.Index] = prop.Name;
                    outputList.Add(meta);
                    _outputByName[prop.Name] = meta;
                }
                else
                {
                    throw new InvalidOperationException($"IO property {prop.Name} must be of type InputSignal or OutputSignal");
                }
            }

            _inputMetas = inputList.ToArray();
            _outputMetas = outputList.ToArray();

            // 计算最大索引
            _maxInputIndex = -1;
            _maxOutputIndex = -1;
            foreach (var m in _inputMetas)
                if (m.Index > _maxInputIndex) _maxInputIndex = m.Index;
            foreach (var m in _outputMetas)
                if (m.Index > _maxOutputIndex) _maxOutputIndex = m.Index;

            // 创建快速访问数组（索引 -> getter/setter）
            _inputGetters = new Func<TLayout, bool>[_maxInputIndex + 1];
            _outputGetters = new Func<TLayout, bool>[_maxOutputIndex + 1];
            _inputSetters = new Action<TLayout, bool>[_maxInputIndex + 1];
            _outputSetters = new Action<TLayout, bool>[_maxOutputIndex + 1];

            foreach (var meta in _inputMetas)
            {
                _inputGetters[meta.Index] = CreateInputGetter(meta.Property);
                _inputSetters[meta.Index] = CreateInputSetter(meta.Property);
            }
            foreach (var meta in _outputMetas)
            {
                _outputGetters[meta.Index] = CreateOutputGetter(meta.Property);
                _outputSetters[meta.Index] = CreateOutputSetter(meta.Property);
            }
        }

        /// <summary>
        /// 获取输入属性名称
        /// </summary>
        public string GetInputName(int index)
        {
            foreach (var m in _inputMetas)
                if (m.Index == index) return m.DisplayName;
            return $"X{index}";
        }

        /// <summary>
        /// 获取输出属性名称
        /// </summary>
        public string GetOutputName(int index)
        {
            foreach (var m in _outputMetas)
                if (m.Index == index) return m.DisplayName;
            return $"Y{index}";
        }

        /// <summary>
        /// 获取输入元数据
        /// </summary>
        public IoMeta? GetInputMeta(int index)
        {
            foreach (var m in _inputMetas)
                if (m.Index == index) return m;
            return null;
        }

        /// <summary>
        /// 获取输出元数据
        /// </summary>
        public IoMeta? GetOutputMeta(int index)
        {
            foreach (var m in _outputMetas)
                if (m.Index == index) return m;
            return null;
        }

        /// <summary>
        /// 获取输入确认超时时间
        /// </summary>
        public int GetInputConfirmTimeout(int index, int defaultTimeout)
        {
            var meta = GetInputMeta(index);
            if (meta != null && meta.ConfirmTimeoutMs > 0)
                return meta.ConfirmTimeoutMs;
            return defaultTimeout;
        }

        /// <summary>
        /// 从表达式获取输入索引
        /// </summary>
        public int GetInputIndex(Expression<Func<TLayout, InputSignal>> expr)
        {
            var memberName = GetMemberName(expr);
            if (_inputByName.TryGetValue(memberName, out var meta))
                return meta.Index;
            throw new ArgumentException($"Property '{memberName}' is not an input property");
        }

        /// <summary>
        /// 从表达式获取输出索引
        /// </summary>
        public int GetOutputIndex(Expression<Func<TLayout, OutputSignal>> expr)
        {
            var memberName = GetMemberName(expr);
            if (_outputByName.TryGetValue(memberName, out var meta))
                return meta.Index;
            throw new ArgumentException($"Property '{memberName}' is not an output property");
        }

        /// <summary>
        /// 读取输入值（从 Layout 实例）
        /// </summary>
        public bool ReadInput(TLayout layout, int index)
        {
            if (index < 0 || index >= _inputGetters.Length || _inputGetters[index] == null)
                return false;
            return _inputGetters[index](layout);
        }

        /// <summary>
        /// 读取输出值（从 Layout 实例）
        /// </summary>
        public bool ReadOutput(TLayout layout, int index)
        {
            if (index < 0 || index >= _outputGetters.Length || _outputGetters[index] == null)
                return false;
            return _outputGetters[index](layout);
        }

        /// <summary>
        /// 写入输出值（到 Layout 实例）
        /// </summary>
        public void WriteOutput(TLayout layout, int index, bool value)
        {
            if (index < 0 || index >= _outputSetters.Length || _outputSetters[index] == null)
                return;
            _outputSetters[index](layout, value);
        }

        /// <summary>
        /// 从硬件同步到 Layout（输入）
        /// </summary>
        public void SyncInputsToLayout(TLayout layout, Func<int, bool> readInput)
        {
            foreach (var meta in _inputMetas)
            {
                bool value = readInput(meta.Index);
                _inputSetters[meta.Index](layout, value);
            }
        }

        /// <summary>
        /// 从硬件同步到 Layout（输出）
        /// </summary>
        public void SyncOutputsToLayout(TLayout layout, Func<int, bool> readOutput)
        {
            foreach (var meta in _outputMetas)
            {
                bool value = readOutput(meta.Index);
                _outputSetters[meta.Index](layout, value);
            }
        }

        /// <summary>
        /// 获取所有输入元数据
        /// </summary>
        public IReadOnlyList<IoMeta> GetAllInputMetas() => _inputMetas;

        /// <summary>
        /// 获取所有输出元数据
        /// </summary>
        public IReadOnlyList<IoMeta> GetAllOutputMetas() => _outputMetas;

        private static string GetMemberName(LambdaExpression expr)
        {
            if (expr.Body is MemberExpression member)
                return member.Member.Name;
            if (expr.Body is UnaryExpression unary && unary.Operand is MemberExpression unaryMember)
                return unaryMember.Member.Name;
            throw new ArgumentException("Expression must be a property access expression");
        }

        private static Func<TLayout, bool> CreateInputGetter(PropertyInfo prop)
        {
            var param = Expression.Parameter(typeof(TLayout), "x");
            var body = Expression.Property(Expression.Property(param, prop), nameof(InputSignal.Value));
            return Expression.Lambda<Func<TLayout, bool>>(body, param).Compile();
        }

        private static Func<TLayout, bool> CreateOutputGetter(PropertyInfo prop)
        {
            var param = Expression.Parameter(typeof(TLayout), "x");
            var body = Expression.Property(Expression.Property(param, prop), nameof(OutputSignal.Value));
            return Expression.Lambda<Func<TLayout, bool>>(body, param).Compile();
        }

        private static Action<TLayout, bool> CreateInputSetter(PropertyInfo prop)
        {
            var param = Expression.Parameter(typeof(TLayout), "x");
            var valueParam = Expression.Parameter(typeof(bool), "value");
            var ctor = typeof(InputSignal).GetConstructor(new[] { typeof(bool) });
            var body = Expression.Assign(Expression.Property(param, prop), Expression.New(ctor!, valueParam));
            return Expression.Lambda<Action<TLayout, bool>>(body, param, valueParam).Compile();
        }

        private static Action<TLayout, bool> CreateOutputSetter(PropertyInfo prop)
        {
            var param = Expression.Parameter(typeof(TLayout), "x");
            var valueParam = Expression.Parameter(typeof(bool), "value");
            var ctor = typeof(OutputSignal).GetConstructor(new[] { typeof(bool) });
            var body = Expression.Assign(Expression.Property(param, prop), Expression.New(ctor!, valueParam));
            return Expression.Lambda<Action<TLayout, bool>>(body, param, valueParam).Compile();
        }
    }
}
