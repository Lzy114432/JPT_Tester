namespace EwanModel.Plc
{
    /// <summary>
    /// PLC 标签解析的项目级默认配置（避免每个标签都写 ByteOrder）
    /// </summary>
    public static class PlcCodecDefaults
    {
        /// <summary>
        /// 当前项目默认协议（仅用于表达默认解析习惯）。
        /// </summary>
        public static PlcProtocol Protocol { get; private set; } = PlcProtocol.Mc;

        /// <summary>
        /// 数值类型默认字节序（short/int/float/...）
        /// </summary>
        public static PlcByteOrder NumericByteOrder { get; private set; } = PlcByteOrder.LittleEndian;

        /// <summary>
        /// 字符串默认处理方式：通常只需要字内交换（SwapBytesInWord），不建议做字顺序反转
        /// </summary>
        public static PlcByteOrder StringByteOrder { get; private set; } = PlcByteOrder.LittleEndian;

        /// <summary>
        /// 使用 MC 默认（数值小端）。
        /// </summary>
        public static void UseMc()
        {
            Protocol = PlcProtocol.Mc;
            NumericByteOrder = PlcByteOrder.LittleEndian;
            StringByteOrder = PlcByteOrder.LittleEndian;
        }

        /// <summary>
        /// 使用 Modbus 默认（数值大端）。
        /// </summary>
        public static void UseModbusBigEndian()
        {
            Protocol = PlcProtocol.Modbus;
            NumericByteOrder = PlcByteOrder.BigEndian;
            StringByteOrder = PlcByteOrder.LittleEndian;
        }

        /// <summary>
        /// 手动设置默认字节序。
        /// </summary>
        /// <param name="numericByteOrder">数值类型字节序。</param>
        /// <param name="stringByteOrder">字符串字节序（默认小端，仅用于是否做字内交换）。</param>
        public static void Set(PlcByteOrder numericByteOrder, PlcByteOrder stringByteOrder = PlcByteOrder.LittleEndian)
        {
            NumericByteOrder = numericByteOrder;
            StringByteOrder = stringByteOrder;
        }
    }
}
