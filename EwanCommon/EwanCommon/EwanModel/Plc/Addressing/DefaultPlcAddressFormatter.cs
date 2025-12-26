using EwanModel.Const;
using System;

namespace EwanModel.Plc
{
    /// <summary>
    /// 默认地址格式化实现：ZR 走十六进制，其它按十进制；bitIndex 非 0 时追加 ".bit"。
    /// </summary>
    public sealed class DefaultPlcAddressFormatter : IPlcAddressFormatter
    {
        /// <inheritdoc />
        public string Format(int addr, string prefix, string bitIndex = null)
        {
            prefix ??= string.Empty;

            var address = prefix.StartsWith(CommonConst.ZRSection, StringComparison.OrdinalIgnoreCase)
                ? prefix + addr.ToString("X2")
                : prefix + addr.ToString();

            if (!string.IsNullOrWhiteSpace(bitIndex) && bitIndex != "0")
            {
                address += "." + bitIndex;
            }

            return address;
        }
    }
}
