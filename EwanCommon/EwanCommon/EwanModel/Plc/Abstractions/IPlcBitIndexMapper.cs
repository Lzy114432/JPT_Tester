namespace EwanModel.Plc
{
    /// <summary>
    /// PLC bit 索引映射策略（例如 X/Y 地址的特殊映射）。
    /// </summary>
    public interface IPlcBitIndexMapper
    {
        /// <summary>
        /// 将 PLC 地址映射为 bool 数组索引。
        /// </summary>
        /// <param name="prefix">区块前缀（如 X/Y）。</param>
        /// <param name="addr">地址。</param>
        /// <returns>bool 数组索引。</returns>
        int MapIndex(string prefix, int addr);
    }
}
