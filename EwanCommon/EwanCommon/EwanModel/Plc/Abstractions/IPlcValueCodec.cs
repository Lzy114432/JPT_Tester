using System;

namespace EwanModel.Plc
{
    /// <summary>
    /// PLC 值编解码策略（字节序/字节交换/字符串编码等）。
    /// </summary>
    public interface IPlcValueCodec
    {
        /// <summary>
        /// 从 PLC 字节缓冲区解码为目标类型。
        /// </summary>
        /// <param name="targetType">目标类型。</param>
        /// <param name="attr">PLC 标签属性。</param>
        /// <param name="buffer">字节缓冲区。</param>
        /// <param name="offset">起始偏移。</param>
        /// <param name="length">长度。</param>
        /// <param name="value">解码后的值。</param>
        /// <returns>是否解码成功。</returns>
        bool TryDecode(Type targetType, PlcAttribute attr, byte[] buffer, int offset, int length, out object value);

        /// <summary>
        /// 将值编码为 PLC 可写入的字节数组。
        /// </summary>
        /// <param name="sourceType">源类型。</param>
        /// <param name="attr">PLC 标签属性。</param>
        /// <param name="value">值。</param>
        /// <param name="bytes">编码后的字节数组。</param>
        /// <returns>是否编码成功。</returns>
        bool TryEncode(Type sourceType, PlcAttribute attr, object value, out byte[] bytes);
    }
}
