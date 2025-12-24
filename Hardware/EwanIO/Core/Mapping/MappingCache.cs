using System;

namespace EwanIO.Core.Mapping
{
    /// <summary>
    /// 映射缓存 - 数组化的映射信息
    /// 热路径零分配：初始化时一次性分配数组
    /// </summary>
    public class MappingCache
    {
        // 输入映射数组
        private readonly int[] _inputLogicalToPhysical;
        private readonly bool[] _inputInvert;  // true = 常闭（NC），需要反转

        // 输出映射数组
        private readonly int[] _outputLogicalToPhysical;
        private readonly bool[] _outputInvert;
        private readonly int[] _outputPhysicalToLogical; // 物理索引 -> 逻辑索引（用于高效 bulk write）
        private bool _outputIdentityMapping;             // 保守：一旦发现非 1:1，则永久为 false
        private bool _outputHasInversion;                // 保守：一旦发现 NC，则永久为 true

        public int InputCount { get; }
        public int OutputCount { get; }

        public MappingCache(int inputCount, int outputCount)
        {
            InputCount = inputCount;
            OutputCount = outputCount;

            _inputLogicalToPhysical = new int[inputCount];
            _inputInvert = new bool[inputCount];
            _outputLogicalToPhysical = new int[outputCount];
            _outputInvert = new bool[outputCount];
            _outputPhysicalToLogical = new int[outputCount];
            _outputIdentityMapping = true;
            _outputHasInversion = false;

            // 默认 1:1 映射，常开
            for (int i = 0; i < inputCount; i++)
            {
                _inputLogicalToPhysical[i] = i;
                _inputInvert[i] = false;
            }
            for (int i = 0; i < outputCount; i++)
            {
                _outputLogicalToPhysical[i] = i;
                _outputInvert[i] = false;
                _outputPhysicalToLogical[i] = i;
            }
        }

        /// <summary>
        /// 设置输入映射
        /// </summary>
        public void SetInputMapping(int logicalIndex, int physicalIndex, bool isNormallyClosed = false)
        {
            if (logicalIndex < 0 || logicalIndex >= InputCount)
                throw new ArgumentOutOfRangeException(nameof(logicalIndex));
            _inputLogicalToPhysical[logicalIndex] = physicalIndex;
            _inputInvert[logicalIndex] = isNormallyClosed;
        }

        /// <summary>
        /// 设置输出映射
        /// </summary>
        public void SetOutputMapping(int logicalIndex, int physicalIndex, bool isNormallyClosed = false)
        {
            if (logicalIndex < 0 || logicalIndex >= OutputCount)
                throw new ArgumentOutOfRangeException(nameof(logicalIndex));

            if (physicalIndex != logicalIndex)
                _outputIdentityMapping = false;
            if (isNormallyClosed)
                _outputHasInversion = true;

            int oldPhysicalIndex = _outputLogicalToPhysical[logicalIndex];
            _outputLogicalToPhysical[logicalIndex] = physicalIndex;
            _outputInvert[logicalIndex] = isNormallyClosed;

            // best-effort 维护反向映射（假设 1:1；重复映射以最后一次为准）
            if ((uint)oldPhysicalIndex < (uint)_outputPhysicalToLogical.Length &&
                _outputPhysicalToLogical[oldPhysicalIndex] == logicalIndex)
            {
                _outputPhysicalToLogical[oldPhysicalIndex] = -1;
            }

            if ((uint)physicalIndex < (uint)_outputPhysicalToLogical.Length)
            {
                _outputPhysicalToLogical[physicalIndex] = logicalIndex;
            }
        }

        /// <summary>
        /// 获取输入物理索引（零分配）
        /// </summary>
        public int GetInputPhysicalIndex(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= InputCount)
                return logicalIndex;  // 越界时返回原值
            return _inputLogicalToPhysical[logicalIndex];
        }

        /// <summary>
        /// 获取输出物理索引（零分配）
        /// </summary>
        public int GetOutputPhysicalIndex(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= OutputCount)
                return logicalIndex;
            return _outputLogicalToPhysical[logicalIndex];
        }

        /// <summary>
        /// 应用输入逻辑（常开/常闭）（零分配）
        /// </summary>
        public bool ApplyInputLogic(int logicalIndex, bool rawValue)
        {
            if (logicalIndex < 0 || logicalIndex >= InputCount)
                return rawValue;
            return rawValue ^ _inputInvert[logicalIndex];
        }

        /// <summary>
        /// 应用输出逻辑（常开/常闭）（零分配）
        /// </summary>
        public bool ApplyOutputLogic(int logicalIndex, bool logicalValue)
        {
            if (logicalIndex < 0 || logicalIndex >= OutputCount)
                return logicalValue;
            return logicalValue ^ _outputInvert[logicalIndex];
        }

        /// <summary>
        /// 检查输入是否常闭
        /// </summary>
        public bool IsInputNormallyClosed(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= InputCount)
                return false;
            return _inputInvert[logicalIndex];
        }

        /// <summary>
        /// 检查输出是否常闭
        /// </summary>
        public bool IsOutputNormallyClosed(int logicalIndex)
        {
            if (logicalIndex < 0 || logicalIndex >= OutputCount)
                return false;
            return _outputInvert[logicalIndex];
        }

        /// <summary>
        /// 输出映射是否为保守的 1:1 映射（用于 bulk write 快路径）。
        /// 注意：一旦发现非 1:1，此标志会永久变为 false（即使之后改回 1:1）。
        /// </summary>
        public bool IsOutputIdentityMapping => _outputIdentityMapping;

        /// <summary>
        /// 输出是否存在常闭（NC）反转（用于 bulk write 快路径）。
        /// 注意：一旦发现 NC，此标志会永久变为 true（即使之后改回 NO）。
        /// </summary>
        public bool HasOutputInversion => _outputHasInversion;

        /// <summary>
        /// 通过物理索引获取对应的逻辑索引（不存在时返回 -1）
        /// </summary>
        public int GetOutputLogicalIndexFromPhysical(int physicalIndex)
        {
            if ((uint)physicalIndex >= (uint)_outputPhysicalToLogical.Length)
                return -1;
            return _outputPhysicalToLogical[physicalIndex];
        }
    }
}
