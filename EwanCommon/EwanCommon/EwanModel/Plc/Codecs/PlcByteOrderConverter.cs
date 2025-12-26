using System;

namespace EwanModel.Plc
{
    /// <summary>
    /// PLC 字节序转换工具（以 16-bit word 为基本单位）。
    /// </summary>
    public static class PlcByteOrderConverter
    {
        /// <summary>
        /// 归一化字节序枚举（<see cref="PlcByteOrder.BigEndian"/> 等价于 <see cref="PlcByteOrder.SwapBytesAndWords"/>）。
        /// </summary>
        public static PlcByteOrder Normalize(PlcByteOrder order)
        {
            return order == PlcByteOrder.BigEndian ? PlcByteOrder.SwapBytesAndWords : order;
        }

        /// <summary>
        /// 在原数组上应用字节序转换。
        /// </summary>
        /// <param name="bytes">字节数组。</param>
        /// <param name="order">字节序/交换方式。</param>
        public static void ApplyInPlace(byte[] bytes, PlcByteOrder order)
        {
            if (bytes == null || bytes.Length < 2)
            {
                return;
            }

            order = Normalize(order);
            switch (order)
            {
                case PlcByteOrder.LittleEndian:
                    return;
                case PlcByteOrder.SwapBytesInWord:
                    SwapBytesInWord(bytes);
                    return;
                case PlcByteOrder.SwapWords:
                    ReverseWords(bytes);
                    return;
                case PlcByteOrder.SwapBytesAndWords:
                    SwapBytesInWord(bytes);
                    ReverseWords(bytes);
                    return;
                default:
                    return;
            }
        }

        private static void SwapBytesInWord(byte[] bs)
        {
            for (var i = 0; i + 1 < bs.Length; i += 2)
            {
                var temp = bs[i];
                bs[i] = bs[i + 1];
                bs[i + 1] = temp;
            }
        }

        private static void ReverseWords(byte[] bs)
        {
            var wordCount = bs.Length / 2;
            for (var i = 0; i < wordCount / 2; i++)
            {
                var j = i * 2;
                var k = (wordCount - 1 - i) * 2;

                var temp0 = bs[j];
                var temp1 = bs[j + 1];
                bs[j] = bs[k];
                bs[j + 1] = bs[k + 1];
                bs[k] = temp0;
                bs[k + 1] = temp1;
            }
        }
    }
}
