namespace EwanModel.Plc
{
    /// <summary>
    /// 通讯协议/默认解析方式（项目级默认）
    /// </summary>
    public enum PlcProtocol
    {
        /// <summary>
        /// 三菱 MC 协议（默认小端）
        /// </summary>
        Mc = 0,

        /// <summary>
        /// Modbus（默认大端）
        /// </summary>
        Modbus = 1
    }
}

