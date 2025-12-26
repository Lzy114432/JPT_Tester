namespace EwanModel.Plc
{
    /// <summary>
    /// PLC 字节序/字节交换方式（以 16-bit word 为基本单位）
    /// </summary>
    public enum PlcByteOrder
    {
        /// <summary>
        /// 未指定：使用项目级默认配置（避免每个标签都标注）
        /// </summary>
        Auto = -1,

        /// <summary>
        /// 小端：低字在前、字内低字节在前（默认）
        /// </summary>
        LittleEndian = 0,

        /// <summary>
        /// 字内交换：每个 16-bit word 内交换高低字节（AB -> BA）
        /// </summary>
        SwapBytesInWord = 1,

        /// <summary>
        /// 字顺序交换：按 16-bit word 反转顺序（W0 W1 -> W1 W0）
        /// </summary>
        SwapWords = 2,

        /// <summary>
        /// 字内+字顺序都交换（等价于整体大端）
        /// </summary>
        SwapBytesAndWords = 3,

        /// <summary>
        /// 整体大端（与 SwapBytesAndWords 等价，保留用于语义）
        /// </summary>
        BigEndian = 4
    }
}
