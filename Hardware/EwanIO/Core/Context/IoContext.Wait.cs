using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using EwanIO.Core.Attributes;
using EwanIO.Core.Data;
using EwanIO.Core.Mapping;

namespace EwanIO.Core.Context
{
    /// <summary>
    /// IoContext - Wait/Confirm 等待和确认操作
    /// </summary>
    public partial class IoContext<TLayout> where TLayout : class, new()
    {
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
    }
}
