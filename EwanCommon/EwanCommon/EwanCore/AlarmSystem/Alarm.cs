using System;

namespace EwanCore.AlarmSystem
{
    /// <summary>
    /// 报警实体（参考 Byron.AlarmSystem.Alarm：Content/AlarmTime/NeedReset/Owner）。
    /// </summary>
    public sealed class Alarm
    {
        /// <summary>
        /// 唯一键：用于去重/定位。为空时会自动使用 <see cref="Content"/>。
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// 报警内容（显示用）。
        /// </summary>
        public string Content { get; private set; }

        /// <summary>
        /// 报警来源（如 PLC 地址、模块名等，可为空）。
        /// </summary>
        public string Unit { get; private set; }

        /// <summary>
        /// 报警级别。
        /// </summary>
        public AlarmLevel Level { get; private set; }

        /// <summary>
        /// 是否需要复位（存在任一 NeedReset=true 时，通常需要先执行“复位流程”再继续）。
        /// </summary>
        public bool NeedReset { get; private set; }

        /// <summary>
        /// 报警所属对象（可为空）。
        /// </summary>
        public object Owner { get; private set; }

        /// <summary>
        /// 最近一次发生时间（同 Key 重复触发时会刷新）。
        /// </summary>
        public DateTime AlarmTime { get; private set; }

        /// <summary>
        /// 累计发生次数（同 Key 重复触发时递增）。
        /// </summary>
        public int Occurrence { get; private set; }

        private Alarm(string key, string content, string unit, AlarmLevel level, bool needReset, object owner, DateTime alarmTime)
        {
            Key = string.IsNullOrWhiteSpace(key) ? (content ?? string.Empty) : key;
            Content = content ?? string.Empty;
            Unit = unit;
            Level = level;
            NeedReset = needReset;
            Owner = owner;
            AlarmTime = alarmTime;
            Occurrence = 1;
        }

        /// <summary>
        /// 创建报警。
        /// </summary>
        public static Alarm Create(string content, bool needReset = false, object owner = null)
        {
            return new Alarm(key: content, content: content, unit: null, level: AlarmLevel.H, needReset: needReset, owner: owner, alarmTime: DateTime.Now);
        }

        /// <summary>
        /// 创建报警（可指定来源/级别/去重 Key）。
        /// </summary>
        public static Alarm Create(string content, AlarmLevel level, string unit = null, bool needReset = false, object owner = null, string key = null)
        {
            return new Alarm(key: key, content: content, unit: unit, level: level, needReset: needReset, owner: owner, alarmTime: DateTime.Now);
        }

        internal void Touch(DateTime now, string content = null, AlarmLevel? level = null, string unit = null, bool? needReset = null, object owner = null)
        {
            AlarmTime = now;
            Occurrence++;

            if (content != null)
            {
                Content = content;
            }
            if (level.HasValue)
            {
                Level = level.Value;
            }
            if (unit != null)
            {
                Unit = unit;
            }
            if (needReset.HasValue)
            {
                NeedReset = needReset.Value;
            }
            if (owner != null)
            {
                Owner = owner;
            }
        }
    }
}

