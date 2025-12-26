using System;
using System.Collections.Generic;

namespace EwanCore.AlarmSystem
{
    /// <summary>
    /// 报警服务抽象：用于 DI 注入（监控/主控/状态机都可以依赖它，而不是依赖静态全局）。
    /// </summary>
    public interface IAlarmService
    {
        /// <summary>
        /// 报警列表变化（任意增删改清都会触发）。
        /// </summary>
        event EventHandler AlarmListChanged;

        /// <summary>
        /// 报警变化（带增删改清语义）。
        /// </summary>
        event EventHandler<AlarmChangedEventArgs> AlarmChanged;

        /// <summary>
        /// 当前报警快照（线程安全）。
        /// </summary>
        IReadOnlyList<Alarm> Snapshot { get; }

        /// <summary>
        /// 当前报警数量。
        /// </summary>
        int AlarmCount { get; }

        /// <summary>
        /// 是否存在报警。
        /// </summary>
        bool HasAlarm { get; }

        /// <summary>
        /// 是否存在 NeedReset=true 的报警。
        /// </summary>
        bool HasNeedResetAlarm { get; }

        /// <summary>
        /// 新增报警（最简签名，兼容 Byron 用法）。
        /// </summary>
        void AddAlarm(string content, bool needReset = false, object owner = null);

        /// <summary>
        /// 新增报警（可指定来源/级别/去重 Key）。
        /// </summary>
        void AddAlarm(string content, AlarmLevel level, string unit = null, bool needReset = false, object owner = null, string key = null);

        /// <summary>
        /// 新增报警对象。
        /// </summary>
        void AddAlarm(Alarm alarm);

        /// <summary>
        /// 清空报警。
        /// </summary>
        void Clear();

        /// <summary>
        /// 移除指定 Key 的报警。
        /// </summary>
        bool RemoveByKey(string key);

        /// <summary>
        /// 判断 Key 是否存在。
        /// </summary>
        bool ExistsByKey(string key);
    }
}

