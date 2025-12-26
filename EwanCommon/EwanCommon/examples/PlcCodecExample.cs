using EwanModel;
using EwanModel.Plc;
using System;
using System.Reflection;

// 说明：
// - 这是“使用示例文件”，不参与 EwanCommon.csproj 编译。
// - 演示：项目级默认字节序（MC/Modbus）、Auto 默认、以及少量标签覆盖。

namespace EwanCommon.Examples
{
    public static class PlcCodecExample
    {
        private sealed class DemoPlcModel : PlcBaseModel
        {
            public DemoPlcModel()
            {
                // 关键：把需要解析/写入的属性集合填入基类字段（原工程就是这么用的）
                var props = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
                mPropertyInfosOfRead = props;
                mPropertyInfos = props;
            }

            // D0：16-bit
            [Plc(Prefix = "D", Addr = 0)]
            public short S16 { get; set; }

            // D1-D2：32-bit
            [Plc(Prefix = "D", Addr = 1, ByteOrder = PlcByteOrder.Auto)]
            public int I32 { get; set; }

            // 字符串（Len=4 字节=2 word）
            // - 默认用 PlcCodecDefaults.StringByteOrder（可按项目设置）
            [Plc(Prefix = "D", Addr = 10, Len = 4)]
            public string Code { get; set; }

            // 特殊覆盖：只做字内交换（某些 PLC/驱动对字符串/word 存储会这样）
            [Plc(Prefix = "D", Addr = 12, Len = 4, ByteOrder = PlcByteOrder.SwapBytesInWord)]
            public string CodeSwap { get; set; }
        }

        public static void Run()
        {
            // 1) 项目级默认（建议：每个项目启动时设置一次）
            // MC：数值小端
            PlcCodecDefaults.UseMc();

            // Modbus：数值大端（注意：字符串是否需要交换，按项目实际决定）
            // PlcCodecDefaults.UseModbusBigEndian();
            // 如果你的 Modbus 字符串也需要字内交换：
            // PlcCodecDefaults.Set(numericByteOrder: PlcByteOrder.BigEndian, stringByteOrder: PlcByteOrder.SwapBytesInWord);

            var model = new DemoPlcModel();

            // 2) 模拟从 PLC 读到的 D 区字节块（示例只给够用的长度）
            // - MC 小端：short(0x1234) 的原始 bytes 通常是 34 12
            // - Modbus 大端：short(0x1234) 的原始 bytes 通常是 12 34（此时用 UseModbusBigEndian 解码会得到 0x1234）
            var dBlock = new byte[32];
            dBlock[0] = 0x34; dBlock[1] = 0x12; // D0 short = 0x1234

            // D1..D2 int = 0x01020304（MC 小端 raw = 04 03 02 01）
            dBlock[2] = 0x04; dBlock[3] = 0x03; dBlock[4] = 0x02; dBlock[5] = 0x01;

            // D10 string len=4，ASCII："ABCD"
            dBlock[20] = (byte)'A'; dBlock[21] = (byte)'B'; dBlock[22] = (byte)'C'; dBlock[23] = (byte)'D';

            // D12 string len=4，演示“字内交换”：raw 写成 "BADC"，Decode 时 SwapBytesInWord 还原为 "ABCD"
            dBlock[24] = (byte)'B'; dBlock[25] = (byte)'A'; dBlock[26] = (byte)'D'; dBlock[27] = (byte)'C';

            // 3) 解析：prefixName 传 "D"，只会解析 Prefix=D 的属性
            model.ResolveByte(dBlock, "D");

            Console.WriteLine($"S16={model.S16} (0x{(ushort)model.S16:X4})");
            Console.WriteLine($"I32={model.I32} (0x{(uint)model.I32:X8})");
            Console.WriteLine($"Code={model.Code}");
            Console.WriteLine($"CodeSwap={model.CodeSwap}");
        }
    }
}

