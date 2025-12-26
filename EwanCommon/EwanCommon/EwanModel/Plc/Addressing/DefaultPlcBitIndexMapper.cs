using EwanModel.Const;
using System;

namespace EwanModel.Plc
{
    /// <summary>
    /// 默认 bit 索引映射实现：仅对 X/Y 区块做映射，其它按原地址返回。
    /// </summary>
    public sealed class DefaultPlcBitIndexMapper : IPlcBitIndexMapper
    {
        /// <inheritdoc />
        public int MapIndex(string prefix, int addr)
        {
            if (prefix == CommonConst.XSection || prefix == CommonConst.YSection)
            {
                return ConvertXYNumber(addr);
            }

            return addr;
        }

        private static int ConvertXYNumber(int number)
        {
            // 原项目逻辑：对 X/Y 地址做特殊转换
            var hexString = number.ToString("X");
            if (hexString.Length == 1)
            {
                return Convert.ToInt32(hexString, 16);
            }

            var lastChar = hexString.Substring(hexString.Length - 1, 1);
            var remainingChars = hexString.Substring(0, hexString.Length - 1);

            var p = Convert.ToInt32(remainingChars);
            var hexValue = p.ToString("X");
            var result = hexValue + lastChar;

            return Convert.ToInt32(result, 16);
        }
    }
}
