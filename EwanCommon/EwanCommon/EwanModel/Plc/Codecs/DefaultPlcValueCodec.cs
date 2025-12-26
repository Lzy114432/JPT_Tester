using System;
using System.Text;

namespace EwanModel.Plc
{
    /// <summary>
    /// 默认 PLC 值编解码实现：支持常见数值类型与字符串，并按 <see cref="PlcCodecDefaults"/> / <see cref="PlcAttribute"/> 决定字节序。
    /// </summary>
    public sealed class DefaultPlcValueCodec : IPlcValueCodec
    {
        /// <inheritdoc />
        public bool TryDecode(Type targetType, PlcAttribute attr, byte[] buffer, int offset, int length, out object value)
        {
            value = null;
            if (targetType == null || buffer == null)
            {
                return false;
            }

            if (offset < 0 || length <= 0 || offset + length > buffer.Length)
            {
                return false;
            }

            if (targetType == typeof(string))
            {
                var bytes = new byte[length];
                Buffer.BlockCopy(buffer, offset, bytes, 0, length);

                var byteOrder = ResolveStringByteOrder(attr);
                if (byteOrder != PlcByteOrder.LittleEndian)
                {
                    // 字符串默认只做字内交换，避免改变字符顺序
                    PlcByteOrderConverter.ApplyInPlace(bytes, PlcByteOrder.SwapBytesInWord);
                }

                value = Encoding.ASCII.GetString(bytes);
                return true;
            }

            var raw = new byte[length];
            Buffer.BlockCopy(buffer, offset, raw, 0, length);

            var order = ResolveNumericByteOrder(attr);
            PlcByteOrderConverter.ApplyInPlace(raw, order);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(raw);
            }

            if (targetType == typeof(short))
            {
                value = BitConverter.ToInt16(raw, 0);
                return true;
            }
            if (targetType == typeof(ushort))
            {
                value = BitConverter.ToUInt16(raw, 0);
                return true;
            }
            if (targetType == typeof(int))
            {
                value = BitConverter.ToInt32(raw, 0);
                return true;
            }
            if (targetType == typeof(uint))
            {
                value = BitConverter.ToUInt32(raw, 0);
                return true;
            }
            if (targetType == typeof(long))
            {
                value = BitConverter.ToInt64(raw, 0);
                return true;
            }
            if (targetType == typeof(ulong))
            {
                value = BitConverter.ToUInt64(raw, 0);
                return true;
            }
            if (targetType == typeof(float))
            {
                value = BitConverter.ToSingle(raw, 0);
                return true;
            }
            if (targetType == typeof(double))
            {
                value = BitConverter.ToDouble(raw, 0);
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public bool TryEncode(Type sourceType, PlcAttribute attr, object value, out byte[] bytes)
        {
            bytes = null;
            if (sourceType == null)
            {
                return false;
            }

            if (sourceType == typeof(string))
            {
                var str = value as string ?? string.Empty;
                bytes = Encoding.UTF8.GetBytes(str);

                if (bytes.Length % 2 != 0)
                {
                    Array.Resize(ref bytes, bytes.Length + 1);
                    bytes[bytes.Length - 1] = 0;
                }

                if (attr != null && attr.Len > 0)
                {
                    if (bytes.Length < attr.Len)
                    {
                        Array.Resize(ref bytes, attr.Len);
                    }
                    else if (bytes.Length > attr.Len)
                    {
                        Array.Resize(ref bytes, attr.Len);
                    }
                }

                var byteOrder = ResolveStringByteOrder(attr);
                if (byteOrder != PlcByteOrder.LittleEndian)
                {
                    PlcByteOrderConverter.ApplyInPlace(bytes, PlcByteOrder.SwapBytesInWord);
                }

                return true;
            }

            try
            {
                bytes = sourceType switch
                {
                    Type t when t == typeof(short) => BitConverter.GetBytes(Convert.ToInt16(value)),
                    Type t when t == typeof(ushort) => BitConverter.GetBytes(Convert.ToUInt16(value)),
                    Type t when t == typeof(int) => BitConverter.GetBytes(Convert.ToInt32(value)),
                    Type t when t == typeof(uint) => BitConverter.GetBytes(Convert.ToUInt32(value)),
                    Type t when t == typeof(long) => BitConverter.GetBytes(Convert.ToInt64(value)),
                    Type t when t == typeof(ulong) => BitConverter.GetBytes(Convert.ToUInt64(value)),
                    Type t when t == typeof(float) => BitConverter.GetBytes(Convert.ToSingle(value)),
                    Type t when t == typeof(double) => BitConverter.GetBytes(Convert.ToDouble(value)),
                    _ => null
                };
            }
            catch
            {
                bytes = null;
                return false;
            }

            if (bytes == null)
            {
                return false;
            }

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            var order = ResolveNumericByteOrder(attr);
            PlcByteOrderConverter.ApplyInPlace(bytes, order);

            return true;
        }

        private static PlcByteOrder ResolveNumericByteOrder(PlcAttribute attr)
        {
            if (attr == null)
            {
                return PlcCodecDefaults.NumericByteOrder;
            }

            if (attr.ByteOrder != PlcByteOrder.Auto)
            {
                return attr.ByteOrder;
            }

            if (attr.IsBigEndian)
            {
                // 兼容旧字段：无需每个标签都写 ByteOrder，也可临时用 IsBigEndian 覆盖
                return PlcByteOrder.SwapBytesInWord;
            }

            return PlcCodecDefaults.NumericByteOrder;
        }

        private static PlcByteOrder ResolveStringByteOrder(PlcAttribute attr)
        {
            if (attr == null)
            {
                return PlcCodecDefaults.StringByteOrder;
            }

            if (attr.ByteOrder != PlcByteOrder.Auto)
            {
                return attr.ByteOrder;
            }

            if (attr.IsBigEndian)
            {
                return PlcByteOrder.SwapBytesInWord;
            }

            return PlcCodecDefaults.StringByteOrder;
        }
    }
}
