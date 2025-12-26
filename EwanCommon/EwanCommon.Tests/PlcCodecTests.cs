using System;
using System.Text;
using EwanModel;
using EwanModel.Plc;
using Xunit;

namespace EwanCommon.Tests
{
    /// <summary>
    /// DefaultPlcValueCodec 单元测试
    /// </summary>
    public class PlcCodecTests
    {
        private readonly DefaultPlcValueCodec _codec = new DefaultPlcValueCodec();

        #region TryDecode 数值类型测试

        [Fact]
        public void TryDecode_Short_ShouldDecodeCorrectly()
        {
            // Arrange
            short expected = 12345;
            var buffer = BitConverter.GetBytes(expected);

            // Act
            var result = _codec.TryDecode(typeof(short), null, buffer, 0, buffer.Length, out var value);

            // Assert
            Assert.True(result);
            Assert.Equal(expected, (short)value!);
        }

        [Fact]
        public void TryDecode_UShort_ShouldDecodeCorrectly()
        {
            // Arrange
            ushort expected = 54321;
            var buffer = BitConverter.GetBytes(expected);

            // Act
            var result = _codec.TryDecode(typeof(ushort), null, buffer, 0, buffer.Length, out var value);

            // Assert
            Assert.True(result);
            Assert.Equal(expected, (ushort)value!);
        }

        [Fact]
        public void TryDecode_Int_ShouldDecodeCorrectly()
        {
            // Arrange
            int expected = 123456789;
            var buffer = BitConverter.GetBytes(expected);

            // Act
            var result = _codec.TryDecode(typeof(int), null, buffer, 0, buffer.Length, out var value);

            // Assert
            Assert.True(result);
            Assert.Equal(expected, (int)value!);
        }

        [Fact]
        public void TryDecode_UInt_ShouldDecodeCorrectly()
        {
            // Arrange
            uint expected = 3000000000;
            var buffer = BitConverter.GetBytes(expected);

            // Act
            var result = _codec.TryDecode(typeof(uint), null, buffer, 0, buffer.Length, out var value);

            // Assert
            Assert.True(result);
            Assert.Equal(expected, (uint)value!);
        }

        [Fact]
        public void TryDecode_Long_ShouldDecodeCorrectly()
        {
            // Arrange
            long expected = 1234567890123456789L;
            var buffer = BitConverter.GetBytes(expected);

            // Act
            var result = _codec.TryDecode(typeof(long), null, buffer, 0, buffer.Length, out var value);

            // Assert
            Assert.True(result);
            Assert.Equal(expected, (long)value!);
        }

        [Fact]
        public void TryDecode_ULong_ShouldDecodeCorrectly()
        {
            // Arrange
            ulong expected = 18446744073709551000;
            var buffer = BitConverter.GetBytes(expected);

            // Act
            var result = _codec.TryDecode(typeof(ulong), null, buffer, 0, buffer.Length, out var value);

            // Assert
            Assert.True(result);
            Assert.Equal(expected, (ulong)value!);
        }

        [Fact]
        public void TryDecode_Float_ShouldDecodeCorrectly()
        {
            // Arrange
            float expected = 3.14159f;
            var buffer = BitConverter.GetBytes(expected);

            // Act
            var result = _codec.TryDecode(typeof(float), null, buffer, 0, buffer.Length, out var value);

            // Assert
            Assert.True(result);
            Assert.Equal(expected, (float)value!, 5);
        }

        [Fact]
        public void TryDecode_Double_ShouldDecodeCorrectly()
        {
            // Arrange
            double expected = 3.141592653589793;
            var buffer = BitConverter.GetBytes(expected);

            // Act
            var result = _codec.TryDecode(typeof(double), null, buffer, 0, buffer.Length, out var value);

            // Assert
            Assert.True(result);
            Assert.Equal(expected, (double)value!, 10);
        }

        #endregion

        #region TryDecode 字符串测试

        [Fact]
        public void TryDecode_String_ShouldDecodeCorrectly()
        {
            // Arrange
            var buffer = Encoding.ASCII.GetBytes("Hello");

            // Act
            var result = _codec.TryDecode(typeof(string), null, buffer, 0, buffer.Length, out var value);

            // Assert
            Assert.True(result);
            Assert.Equal("Hello", (string)value!);
        }

        [Fact]
        public void TryDecode_String_WithOffset_ShouldDecodeSubstring()
        {
            // Arrange
            var buffer = Encoding.ASCII.GetBytes("XXXHelloXXX");

            // Act
            var result = _codec.TryDecode(typeof(string), null, buffer, 3, 5, out var value);

            // Assert
            Assert.True(result);
            Assert.Equal("Hello", (string)value!);
        }

        #endregion

        #region TryDecode 边界条件测试

        [Fact]
        public void TryDecode_NullType_ShouldReturnFalse()
        {
            // Arrange
            var buffer = new byte[] { 0x01, 0x02 };

            // Act
            var result = _codec.TryDecode(null!, null, buffer, 0, buffer.Length, out var value);

            // Assert
            Assert.False(result);
            Assert.Null(value);
        }

        [Fact]
        public void TryDecode_NullBuffer_ShouldReturnFalse()
        {
            // Act
            var result = _codec.TryDecode(typeof(short), null, null!, 0, 2, out var value);

            // Assert
            Assert.False(result);
            Assert.Null(value);
        }

        [Fact]
        public void TryDecode_InvalidOffset_ShouldReturnFalse()
        {
            // Arrange
            var buffer = new byte[] { 0x01, 0x02 };

            // Act
            var result = _codec.TryDecode(typeof(short), null, buffer, -1, 2, out var value);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void TryDecode_InvalidLength_ShouldReturnFalse()
        {
            // Arrange
            var buffer = new byte[] { 0x01, 0x02 };

            // Act
            var result1 = _codec.TryDecode(typeof(short), null, buffer, 0, 0, out _);
            var result2 = _codec.TryDecode(typeof(short), null, buffer, 0, -1, out _);

            // Assert
            Assert.False(result1);
            Assert.False(result2);
        }

        [Fact]
        public void TryDecode_OffsetPlusLengthExceedsBuffer_ShouldReturnFalse()
        {
            // Arrange
            var buffer = new byte[] { 0x01, 0x02 };

            // Act
            var result = _codec.TryDecode(typeof(short), null, buffer, 1, 2, out var value);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void TryDecode_UnsupportedType_ShouldReturnFalse()
        {
            // Arrange
            var buffer = new byte[] { 0x01 };

            // Act
            var result = _codec.TryDecode(typeof(bool), null, buffer, 0, 1, out var value);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region TryEncode 数值类型测试

        [Fact]
        public void TryEncode_Short_ShouldEncodeCorrectly()
        {
            // Arrange
            short value = 12345;

            // Act
            var result = _codec.TryEncode(typeof(short), null, value, out var bytes);

            // Assert
            Assert.True(result);
            Assert.NotNull(bytes);
            Assert.Equal(value, BitConverter.ToInt16(bytes!, 0));
        }

        [Fact]
        public void TryEncode_UShort_ShouldEncodeCorrectly()
        {
            // Arrange
            ushort value = 54321;

            // Act
            var result = _codec.TryEncode(typeof(ushort), null, value, out var bytes);

            // Assert
            Assert.True(result);
            Assert.NotNull(bytes);
            Assert.Equal(value, BitConverter.ToUInt16(bytes!, 0));
        }

        [Fact]
        public void TryEncode_Int_ShouldEncodeCorrectly()
        {
            // Arrange
            int value = 123456789;

            // Act
            var result = _codec.TryEncode(typeof(int), null, value, out var bytes);

            // Assert
            Assert.True(result);
            Assert.NotNull(bytes);
            Assert.Equal(value, BitConverter.ToInt32(bytes!, 0));
        }

        [Fact]
        public void TryEncode_UInt_ShouldEncodeCorrectly()
        {
            // Arrange
            uint value = 3000000000;

            // Act
            var result = _codec.TryEncode(typeof(uint), null, value, out var bytes);

            // Assert
            Assert.True(result);
            Assert.NotNull(bytes);
            Assert.Equal(value, BitConverter.ToUInt32(bytes!, 0));
        }

        [Fact]
        public void TryEncode_Long_ShouldEncodeCorrectly()
        {
            // Arrange
            long value = 1234567890123456789L;

            // Act
            var result = _codec.TryEncode(typeof(long), null, value, out var bytes);

            // Assert
            Assert.True(result);
            Assert.NotNull(bytes);
            Assert.Equal(value, BitConverter.ToInt64(bytes!, 0));
        }

        [Fact]
        public void TryEncode_Float_ShouldEncodeCorrectly()
        {
            // Arrange
            float value = 3.14159f;

            // Act
            var result = _codec.TryEncode(typeof(float), null, value, out var bytes);

            // Assert
            Assert.True(result);
            Assert.NotNull(bytes);
            Assert.Equal(value, BitConverter.ToSingle(bytes!, 0), 5);
        }

        [Fact]
        public void TryEncode_Double_ShouldEncodeCorrectly()
        {
            // Arrange
            double value = 3.141592653589793;

            // Act
            var result = _codec.TryEncode(typeof(double), null, value, out var bytes);

            // Assert
            Assert.True(result);
            Assert.NotNull(bytes);
            Assert.Equal(value, BitConverter.ToDouble(bytes!, 0), 10);
        }

        #endregion

        #region TryEncode 字符串测试

        [Fact]
        public void TryEncode_String_ShouldEncodeCorrectly()
        {
            // Arrange
            string value = "Hello";

            // Act
            var result = _codec.TryEncode(typeof(string), null, value, out var bytes);

            // Assert
            Assert.True(result);
            Assert.NotNull(bytes);
            // 字符串长度为奇数时会补齐到偶数
            Assert.True(bytes!.Length % 2 == 0);
        }

        [Fact]
        public void TryEncode_String_NullValue_ShouldEncodeEmptyString()
        {
            // Act
            var result = _codec.TryEncode(typeof(string), null, null!, out var bytes);

            // Assert
            Assert.True(result);
            Assert.NotNull(bytes);
        }

        [Fact]
        public void TryEncode_String_WithLenAttribute_ShouldTruncateOrPad()
        {
            // Arrange
            var attr = new PlcAttribute { Len = 10 };

            // Act
            var result = _codec.TryEncode(typeof(string), attr, "Hi", out var bytes);

            // Assert
            Assert.True(result);
            Assert.Equal(10, bytes!.Length);
        }

        #endregion

        #region TryEncode 边界条件测试

        [Fact]
        public void TryEncode_NullType_ShouldReturnFalse()
        {
            // Act
            var result = _codec.TryEncode(null!, null, 123, out var bytes);

            // Assert
            Assert.False(result);
            Assert.Null(bytes);
        }

        [Fact]
        public void TryEncode_UnsupportedType_ShouldReturnFalse()
        {
            // Act
            var result = _codec.TryEncode(typeof(bool), null, true, out var bytes);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region 编解码往返测试

        [Theory]
        [InlineData(typeof(short), (short)12345)]
        [InlineData(typeof(short), (short)-12345)]
        [InlineData(typeof(ushort), (ushort)54321)]
        [InlineData(typeof(int), 123456789)]
        [InlineData(typeof(int), -123456789)]
        [InlineData(typeof(uint), 3000000000U)]
        public void EncodeDecodeRoundTrip_IntegerTypes_ShouldPreserveValue(Type type, object originalValue)
        {
            // Encode
            var encodeResult = _codec.TryEncode(type, null, originalValue, out var bytes);
            Assert.True(encodeResult);

            // Decode
            var decodeResult = _codec.TryDecode(type, null, bytes!, 0, bytes!.Length, out var decodedValue);
            Assert.True(decodeResult);

            // Compare
            Assert.Equal(originalValue, decodedValue);
        }

        [Fact]
        public void EncodeDecodeRoundTrip_Long_ShouldPreserveValue()
        {
            // Arrange
            long original = 1234567890123456789L;

            // Encode
            var encodeResult = _codec.TryEncode(typeof(long), null, original, out var bytes);
            Assert.True(encodeResult);

            // Decode
            var decodeResult = _codec.TryDecode(typeof(long), null, bytes!, 0, bytes!.Length, out var decoded);
            Assert.True(decodeResult);

            // Compare
            Assert.Equal(original, (long)decoded!);
        }

        [Fact]
        public void EncodeDecodeRoundTrip_Float_ShouldPreserveValue()
        {
            // Arrange
            float original = 3.14159f;

            // Encode
            var encodeResult = _codec.TryEncode(typeof(float), null, original, out var bytes);
            Assert.True(encodeResult);

            // Decode
            var decodeResult = _codec.TryDecode(typeof(float), null, bytes!, 0, bytes!.Length, out var decoded);
            Assert.True(decodeResult);

            // Compare
            Assert.Equal(original, (float)decoded!, 5);
        }

        [Fact]
        public void EncodeDecodeRoundTrip_Double_ShouldPreserveValue()
        {
            // Arrange
            double original = 3.141592653589793;

            // Encode
            var encodeResult = _codec.TryEncode(typeof(double), null, original, out var bytes);
            Assert.True(encodeResult);

            // Decode
            var decodeResult = _codec.TryDecode(typeof(double), null, bytes!, 0, bytes!.Length, out var decoded);
            Assert.True(decodeResult);

            // Compare
            Assert.Equal(original, (double)decoded!, 10);
        }

        #endregion

        #region 字节序测试

        [Fact]
        public void TryEncode_WithSwapBytesInWord_ShouldSwapBytes()
        {
            // Arrange
            var attr = new PlcAttribute { ByteOrder = PlcByteOrder.SwapBytesInWord };
            short value = 0x1234;

            // Act
            var result = _codec.TryEncode(typeof(short), attr, value, out var bytes);

            // Assert
            Assert.True(result);
            // 字节序交换后，原本的 34 12（小端）变成 12 34
            Assert.Equal(0x12, bytes![0]);
            Assert.Equal(0x34, bytes[1]);
        }

        [Fact]
        public void TryDecode_WithSwapBytesInWord_ShouldSwapBytes()
        {
            // Arrange
            var attr = new PlcAttribute { ByteOrder = PlcByteOrder.SwapBytesInWord };
            // PLC 发来的数据（大端格式）：0x12 0x34
            var buffer = new byte[] { 0x12, 0x34 };

            // Act
            var result = _codec.TryDecode(typeof(short), attr, buffer, 0, 2, out var value);

            // Assert
            Assert.True(result);
            Assert.Equal((short)0x1234, (short)value!);
        }

        [Fact]
        public void TryEncode_WithIsBigEndian_ShouldUseSwapBytesInWord()
        {
            // Arrange
            var attr = new PlcAttribute { IsBigEndian = true };
            short value = 0x1234;

            // Act
            var result = _codec.TryEncode(typeof(short), attr, value, out var bytes);

            // Assert
            Assert.True(result);
            Assert.Equal(0x12, bytes![0]);
            Assert.Equal(0x34, bytes[1]);
        }

        #endregion

        #region PlcAttribute 测试

        [Fact]
        public void PlcAttribute_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var attr = new PlcAttribute();

            // Assert
            Assert.Null(attr.Prefix);
            Assert.Equal(0, attr.Addr);
            Assert.Equal(0, attr.Len);
            Assert.Equal("0", attr.BitIndex);
            Assert.Null(attr.Multiple);
            Assert.False(attr.IsBigEndian);
            Assert.Equal(PlcByteOrder.Auto, attr.ByteOrder);
            Assert.False(attr.IsAlarmProperty);
            Assert.Null(attr.AlarmDesc);
            Assert.Null(attr.NeedReset);
        }

        [Fact]
        public void PlcAttribute_WithAllProperties_ShouldStoreCorrectly()
        {
            // Arrange & Act
            var attr = new PlcAttribute
            {
                Prefix = "D",
                Addr = 100,
                Len = 20,
                BitIndex = "5",
                Multiple = "0.1",
                IsBigEndian = true,
                ByteOrder = PlcByteOrder.SwapBytesAndWords,
                IsAlarmProperty = true,
                AlarmDesc = "温度过高",
                NeedReset = true
            };

            // Assert
            Assert.Equal("D", attr.Prefix);
            Assert.Equal(100, attr.Addr);
            Assert.Equal(20, attr.Len);
            Assert.Equal("5", attr.BitIndex);
            Assert.Equal("0.1", attr.Multiple);
            Assert.True(attr.IsBigEndian);
            Assert.Equal(PlcByteOrder.SwapBytesAndWords, attr.ByteOrder);
            Assert.True(attr.IsAlarmProperty);
            Assert.Equal("温度过高", attr.AlarmDesc);
            Assert.True(attr.NeedReset);
        }

        #endregion

        #region 边界值测试

        [Fact]
        public void TryEncode_ShortMinMax_ShouldEncodeCorrectly()
        {
            // Min
            var result1 = _codec.TryEncode(typeof(short), null, short.MinValue, out var bytes1);
            Assert.True(result1);
            Assert.Equal(short.MinValue, BitConverter.ToInt16(bytes1!, 0));

            // Max
            var result2 = _codec.TryEncode(typeof(short), null, short.MaxValue, out var bytes2);
            Assert.True(result2);
            Assert.Equal(short.MaxValue, BitConverter.ToInt16(bytes2!, 0));
        }

        [Fact]
        public void TryEncode_IntMinMax_ShouldEncodeCorrectly()
        {
            // Min
            var result1 = _codec.TryEncode(typeof(int), null, int.MinValue, out var bytes1);
            Assert.True(result1);
            Assert.Equal(int.MinValue, BitConverter.ToInt32(bytes1!, 0));

            // Max
            var result2 = _codec.TryEncode(typeof(int), null, int.MaxValue, out var bytes2);
            Assert.True(result2);
            Assert.Equal(int.MaxValue, BitConverter.ToInt32(bytes2!, 0));
        }

        [Fact]
        public void TryEncode_FloatSpecialValues_ShouldEncodeCorrectly()
        {
            // Zero
            var result1 = _codec.TryEncode(typeof(float), null, 0f, out var bytes1);
            Assert.True(result1);
            Assert.Equal(0f, BitConverter.ToSingle(bytes1!, 0));

            // Negative
            var result2 = _codec.TryEncode(typeof(float), null, -123.456f, out var bytes2);
            Assert.True(result2);
            Assert.Equal(-123.456f, BitConverter.ToSingle(bytes2!, 0), 3);
        }

        #endregion
    }
}
