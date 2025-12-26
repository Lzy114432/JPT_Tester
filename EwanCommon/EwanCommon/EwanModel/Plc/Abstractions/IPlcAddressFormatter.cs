namespace EwanModel.Plc
{
    /// <summary>
    /// PLC 地址格式化策略（用于适配不同 PLC 的地址规则）。
    /// </summary>
    public interface IPlcAddressFormatter
    {
        /// <summary>
        /// 格式化地址。
        /// </summary>
        /// <param name="addr">地址数值。</param>
        /// <param name="prefix">区块前缀（如 D/M/ZR...）。</param>
        /// <param name="bitIndex">可选 bit 索引（如 "1"）。</param>
        /// <returns>格式化后的地址字符串。</returns>
        string Format(int addr, string prefix, string bitIndex = null);
    }
}
