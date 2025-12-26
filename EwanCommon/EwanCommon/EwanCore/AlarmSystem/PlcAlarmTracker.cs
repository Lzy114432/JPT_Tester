using EwanModel;
using EwanModel.Common;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace EwanCore.AlarmSystem
{
    /// <summary>
    /// PLC 报警追踪器：扫描模型上标记了 <see cref="PlcAttribute.IsAlarmProperty"/> 的 bool 属性，
    /// 将 0→1 作为“发生报警”，1→0 作为“清除报警”，并同步到 <see cref="IAlarmService"/>。
    /// </summary>
    /// <typeparam name="TModel">PLC 模型类型。</typeparam>
    public sealed class PlcAlarmTracker<TModel>
    {
        private readonly IAlarmService _alarmService;
        private readonly PropertyInfo[] _alarmProperties;
        private readonly Dictionary<string, bool> _lastStates = new Dictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>
        /// 创建一个追踪器。
        /// </summary>
        /// <param name="alarmService">报警服务。</param>
        /// <param name="properties">
        /// 可选：指定要扫描的属性集合；为空则扫描 <typeparamref name="TModel"/> 的全部公共实例属性。
        /// </param>
        public PlcAlarmTracker(IAlarmService alarmService, IEnumerable<PropertyInfo> properties = null)
        {
            _alarmService = alarmService ?? throw new ArgumentNullException(nameof(alarmService));

            var props = properties != null ? new List<PropertyInfo>(properties).ToArray() : typeof(TModel).GetProperties(BindingFlags.Instance | BindingFlags.Public);
            _alarmProperties = FilterAlarmProperties(props);
        }

        /// <summary>
        /// 处理一次 PLC 模型快照并同步报警。
        /// </summary>
        public void Process(TModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            for (var i = 0; i < _alarmProperties.Length; i++)
            {
                var p = _alarmProperties[i];
                var attr = p.GetCustomAttribute<PlcAttribute>();
                if (attr == null || !attr.IsAlarmProperty)
                {
                    continue;
                }

                // 约定：报警属性为 bool
                if (p.PropertyType != typeof(bool))
                {
                    continue;
                }

                var key = p.Name;
                var current = (bool)p.GetValue(model);

                if (!_lastStates.TryGetValue(key, out var last))
                {
                    _lastStates[key] = current;
                    if (current)
                    {
                        AddOrUpdateAlarm(key, attr);
                    }
                    continue;
                }

                if (current == last)
                {
                    continue;
                }

                _lastStates[key] = current;

                if (current)
                {
                    AddOrUpdateAlarm(key, attr);
                }
                else
                {
                    _alarmService.RemoveByKey(key);
                }
            }
        }

        private void AddOrUpdateAlarm(string key, PlcAttribute attr)
        {
            var content = string.IsNullOrWhiteSpace(attr.AlarmDesc) ? key : attr.AlarmDesc;
            var unit = attr.Prefix + attr.Addr;
            var level = (AlarmLevel)(EAlarmLevel)attr.EAlarmLevel;
            var needReset = attr.NeedReset ?? (level == AlarmLevel.H);
            _alarmService.AddAlarm(content, level, unit: unit, needReset: needReset, owner: null, key: key);
        }

        private static PropertyInfo[] FilterAlarmProperties(PropertyInfo[] props)
        {
            if (props == null || props.Length == 0)
            {
                return Array.Empty<PropertyInfo>();
            }

            var list = new List<PropertyInfo>(props.Length);
            for (var i = 0; i < props.Length; i++)
            {
                var p = props[i];
                if (p == null || !p.CanRead)
                {
                    continue;
                }

                var attr = p.GetCustomAttribute<PlcAttribute>();
                if (attr != null && attr.IsAlarmProperty)
                {
                    list.Add(p);
                }
            }

            return list.ToArray();
        }
    }
}

