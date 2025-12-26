using System;
using System.Linq.Expressions;
using EwanIO.Core.Attributes;
using EwanIO.Core.Data;
using EwanIO.Core.EdgeDetection;
using EwanIO.Core.Mapping;
using EwanIO.Core.Metadata;
using EwanIO.Core.Simulation;

namespace EwanIO.Core.Context
{
    /// <summary>
    /// IoContext - 子对象访问器（EdgeAccessor, SimAccessor, MetaAccessor, MappingAccessor）
    /// </summary>
    public partial class IoContext<TLayout> where TLayout : class, new()
    {
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
            /// 强制"物理/映射前(PreMap)"输入为 ON（即模拟后、映射前值为 true）
            /// </summary>
            public void ForcePhysicalOn(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                _ctx._physicalSimulator.ForceOn(idx);
            }

            /// <summary>
            /// 强制"物理/映射前(PreMap)"输入为 OFF（即模拟后、映射前值为 false）
            /// </summary>
            public void ForcePhysicalOff(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                _ctx._physicalSimulator.ForceOff(idx);
            }

            /// <summary>
            /// 强制"物理/映射前(PreMap)"输入为 ON（索引）
            /// </summary>
            public void ForcePhysicalOn(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(ForcePhysicalOn)))
                    return;
                _ctx._physicalSimulator.ForceOn(index);
            }

            /// <summary>
            /// 强制"物理/映射前(PreMap)"输入为 OFF（索引）
            /// </summary>
            public void ForcePhysicalOff(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(ForcePhysicalOff)))
                    return;
                _ctx._physicalSimulator.ForceOff(index);
            }

            /// <summary>
            /// 清除"物理/映射前(PreMap)"模拟（表达式）
            /// </summary>
            public void ClearPhysicalSimulate(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                _ctx._physicalSimulator.ClearSimulate(idx);
            }

            /// <summary>
            /// 清除"物理/映射前(PreMap)"模拟（索引）
            /// </summary>
            public void ClearPhysicalSimulate(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(ClearPhysicalSimulate)))
                    return;
                _ctx._physicalSimulator.ClearSimulate(index);
            }

            /// <summary>
            /// 获取"物理/映射前(PreMap)"模拟模式（索引）
            /// </summary>
            public SimMode GetPhysicalMode(int index)
            {
                if (!_ctx.EnsureInputIndex(index, nameof(GetPhysicalMode)))
                    return SimMode.None;
                return _ctx._physicalSimulator.GetMode(index);
            }

            /// <summary>
            /// 强制"逻辑层(R)"输入为 ON（映射后值为 true；下一次 Tick 生效）
            /// </summary>
            public void ForceOn(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                _ctx._logicalSimulator.ForceOn(idx);
            }

            /// <summary>
            /// 强制"逻辑层(R)"输入为 OFF（映射后值为 false；下一次 Tick 生效）
            /// </summary>
            public void ForceOff(Expression<Func<TLayout, InputSignal>> expr)
            {
                int idx = _ctx._meta.GetInputIndex(expr);
                _ctx._logicalSimulator.ForceOff(idx);
            }

            /// <summary>
            /// 清除"逻辑层(R)"模拟（表达式）
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
    }
}
