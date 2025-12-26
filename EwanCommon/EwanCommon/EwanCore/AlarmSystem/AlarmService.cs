using System;
using System.Collections.Generic;
using System.Linq;

namespace EwanCore.AlarmSystem
{
    /// <summary>
    /// 默认报警服务实现：线程安全、按 Key 去重、重复触发会刷新时间并递增 Occurrence。
    /// </summary>
    public sealed class AlarmService : IAlarmService
    {
        private readonly object _syncRoot = new object();
        private readonly List<Alarm> _alarms = new List<Alarm>();

        /// <inheritdoc />
        public event EventHandler AlarmListChanged;

        /// <inheritdoc />
        public event EventHandler<AlarmChangedEventArgs> AlarmChanged;

        /// <inheritdoc />
        public IReadOnlyList<Alarm> Snapshot
        {
            get
            {
                lock (_syncRoot)
                {
                    return _alarms.ToArray();
                }
            }
        }

        /// <inheritdoc />
        public int AlarmCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _alarms.Count;
                }
            }
        }

        /// <inheritdoc />
        public bool HasAlarm => AlarmCount > 0;

        /// <inheritdoc />
        public bool HasNeedResetAlarm
        {
            get
            {
                lock (_syncRoot)
                {
                    for (var i = 0; i < _alarms.Count; i++)
                    {
                        if (_alarms[i].NeedReset)
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }
        }

        /// <inheritdoc />
        public void AddAlarm(string content, bool needReset = false, object owner = null)
        {
            AddAlarm(Alarm.Create(content, needReset, owner));
        }

        /// <inheritdoc />
        public void AddAlarm(string content, AlarmLevel level, string unit = null, bool needReset = false, object owner = null, string key = null)
        {
            AddAlarm(Alarm.Create(content, level, unit, needReset, owner, key));
        }

        /// <inheritdoc />
        public void AddAlarm(Alarm alarm)
        {
            if (alarm == null) throw new ArgumentNullException(nameof(alarm));

            AlarmChangeKind kind;
            Alarm changedAlarm;

            lock (_syncRoot)
            {
                var key = alarm.Key ?? string.Empty;
                var existing = _alarms.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.Ordinal));
                if (existing == null)
                {
                    _alarms.Add(alarm);
                    kind = AlarmChangeKind.Added;
                    changedAlarm = alarm;
                }
                else
                {
                    existing.Touch(DateTime.Now, content: alarm.Content, level: alarm.Level, unit: alarm.Unit, needReset: alarm.NeedReset, owner: alarm.Owner);
                    kind = AlarmChangeKind.Updated;
                    changedAlarm = existing;
                }
            }

            OnAlarmChanged(kind, changedAlarm);
        }

        /// <inheritdoc />
        public void Clear()
        {
            lock (_syncRoot)
            {
                _alarms.Clear();
            }

            // 参考 Byron 行为：即使原本为空也允许触发（便于 UI 强制刷新）。
            OnAlarmChanged(AlarmChangeKind.Cleared, alarm: null, raiseListChanged: true);
        }

        /// <inheritdoc />
        public bool RemoveByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            Alarm removed = null;
            lock (_syncRoot)
            {
                for (var i = 0; i < _alarms.Count; i++)
                {
                    if (string.Equals(_alarms[i].Key, key, StringComparison.Ordinal))
                    {
                        removed = _alarms[i];
                        _alarms.RemoveAt(i);
                        break;
                    }
                }
            }

            if (removed == null)
            {
                return false;
            }

            OnAlarmChanged(AlarmChangeKind.Removed, removed);
            return true;
        }

        /// <inheritdoc />
        public bool ExistsByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            lock (_syncRoot)
            {
                for (var i = 0; i < _alarms.Count; i++)
                {
                    if (string.Equals(_alarms[i].Key, key, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private void OnAlarmChanged(AlarmChangeKind kind, Alarm alarm, bool raiseListChanged = true)
        {
            AlarmChanged?.Invoke(this, new AlarmChangedEventArgs(kind, alarm));
            if (raiseListChanged)
            {
                AlarmListChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
