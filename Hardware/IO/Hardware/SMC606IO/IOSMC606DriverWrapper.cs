using Ewan.LogManager.Logger;
using IOLibrary.Core.Interfaces;
using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace IOLibrary.Hardware.SMC606IO
{
    public class IOSMC606DriverWrapper : IHardwareIO
    {
        #region IOSMC606接口实现
        //板卡配置	
        [DllImport("LTSMC.dll")]
        public static extern short smc_board_init(ushort ConnectNo, ushort ConnectType, string pconnectstring, uint baud);
        [DllImport("LTSMC.dll")]
        public static extern short smc_board_close(ushort ConnectNo);

        [DllImport("LTSMC.dll")]
        public static extern short smc_get_total_ionum(ushort ConnectNo, ref ushort TotalIn, ref ushort TotalOut);

        [DllImport("LTSMC.dll")]
        public static extern uint smc_read_inport(ushort ConnectNo, ushort portno);     //读取输入端口的值

        [DllImport("LTSMC.dll")]
        public static extern short smc_write_outport(ushort ConnectNo, ushort portno, uint outport_val);  	//设置输出端口的值

        #endregion

        ushort card = 0;
        ushort ioinputnum = 0, iooutputnum = 0;
        private int boardCount = 0;

        private int inputboardCount = 0,outputboardCount = 0;

        // 缓存输入输出状态
        private BitArray? inputCache;
        private BitArray? outputCache;
        private BitArray? risingEdgeCache;
        private BitArray? fallingEdgeCache;
        private BitArray? lastInputState;

        private bool isConnected = false;

        public string HardwareType => "IOSMC606";

        public string ConnectionInfo => $"Boards:{boardCount}";

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => isConnected;

        /// <summary>
        /// 输入点数
        /// </summary>
        public int InputCount => inputboardCount;

        /// <summary>
        /// 输出点数
        /// </summary>
        public int OutputCount => outputboardCount;


        #region 初始化和销毁代码

        /// <summary>
        /// 初始化缓存
        /// </summary>
        private void InitializeCaches()
        {
            inputCache = new BitArray(ioinputnum);
            outputCache = new BitArray(iooutputnum);
            risingEdgeCache = new BitArray(ioinputnum);
            fallingEdgeCache = new BitArray(ioinputnum);
            lastInputState = new BitArray(ioinputnum);
        }


        /// <summary>
        /// 连接到硬件
        /// </summary>
        /// <param name="connectionString">连接字符串，格式：boards=n 或留空自动检测</param>
        public bool Connect(string connectionString)
        {
            try
            {
                short res = smc_board_init(card, 2, connectionString, 115200);
                if (res != 0)
                {
                    IOLogger.Instance.LogRaw(LogLevel.Error, $"IOC0640 connection failed with error code: {res}");
                    return false;
                }

                smc_get_total_ionum(card, ref ioinputnum, ref iooutputnum);//获取控制器自身的IO数

                // 计算 inputboardCount 和  outputboardCount的数量 
                int board = ioinputnum / 32;
                int leftio = ioinputnum - 32 * board;
                if (leftio > 0)
                {
                    board++;
                }
                inputboardCount = board;

                board = iooutputnum / 32;
                leftio = iooutputnum - 32 * board;
                if (leftio > 0)
                {
                    board++;
                }
                outputboardCount = board;

                InitializeCaches();

                return true;
            }
            catch (Exception ex)
            {
                IOLogger.Instance.LogRaw(LogLevel.Error, $"IOC0640 connection failed: {ex.Message}");
                isConnected = false;
                return false;
            }
        }


        /// <summary>
        /// 断开连接
        /// </summary>
        public bool Disconnect()
        {
            try
            {
                smc_board_close(card);
                boardCount = 0;
                isConnected = false;
                inputboardCount = 0; outputboardCount = 0;
                IOLogger.Instance.LogRaw(LogLevel.Info, "IOC0640 disconnected");
                return true;
            }
            catch (Exception ex)
            {
                IOLogger.Instance.LogRaw(LogLevel.Error, $"IOC0640 disconnect failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 数据同步

        public void DataSync()
        {
            IO_InputDataSync();

            IO_OutputDataSync();
        }

        private void IO_InputDataSync()
        {
            if (!IsConnected) return;

            uint[] datas = new uint[inputboardCount];

            for (ushort i = 0; i < inputboardCount; i++)
            {
                uint currentInput = (uint)~smc_read_inport(card, 0);
                datas[i] = currentInput;
            }

            // 最终结果长度 = 实际输入点数
            BitArray result = new BitArray(ioinputnum);

            int bitIndex = 0;
            for (int i = 0; i < inputboardCount; i++)
            {
                uint value = datas[i];

                for (int b = 0; b < 32 && bitIndex < ioinputnum; b++)
                {
                    // 判断当前 bit
                    bool bit = (value & (1u << b)) != 0;
                    //inputCache[bitIndex] = bit;

                    bool currentState = bit;
                    bool lastState = lastInputState[bitIndex];

                    // 检测上升沿 (false -> true)
                    if (!lastState && currentState)
                    {
                        risingEdgeCache[bitIndex] = true;
                    }

                    // 检测下降沿 (true -> false)
                    if (lastState && !currentState)
                    {
                        fallingEdgeCache[bitIndex] = true;
                    }

                    inputCache[bitIndex] = currentState;
                    lastInputState[bitIndex] = currentState;
                    bitIndex++;
                }
            }

        }



        private void IO_OutputDataSync()
        {
            if (!IsConnected) return;

            try
            {
                for (ushort i = 0; i < outputboardCount; i++)
                {
                    // 1. 计算当前板卡在 outputCache 中的起始索引
                    // 第0个板卡: i=0, offset=0.  处理 outputCache[0] 到 [31]
                    // 第1个板卡: i=1, offset=32. 处理 outputCache[32] 到 [63]
                    // ...以此类推
                    int offset = i * 32;

                    // 2. 调用新的辅助函数，传入整个缓存和本次操作的起始点
                    uint outputValue = ConvertBitArrayToUint(outputCache, offset);

                    // 3. 将生成的 uint 值写入对应的板卡
                    // 这里的 "~" 取反操作被保留，以匹配您原始代码的逻辑
                    smc_write_outport(card, i, outputValue);
                }
            }
            catch (Exception ex)
            {
                //IOLogger.Instance.LogPLCCommunication(yAreaBaseAddress, "OutputSync", ex.Message, false);
            }
        }


        /// <summary>
        /// 将 BitArray 的一个指定片段转换为一个 uint。
        /// </summary>
        /// <param name="datas">包含所有输出状态的源 BitArray。</param>
        /// <param name="startIndex">要开始转换的起始索引。</param>
        /// <returns>代表指定32个位的 uint 值。</returns>
        uint ConvertBitArrayToUint(BitArray datas, int startIndex)
        {
            // 如果 datas 为 null，直接返回0，这是一个安全的操作
            if (datas == null)
            {
                return ~0u; // 根据您原始逻辑，空数据应该返回全1（~0的结果）
            }

            uint ux = 0;
            // 循环32次，因为一个uint是32位
            for (int i = 0; i < 32; i++)
            {
                // 计算在整个 datas 数组中的实际索引
                int currentIndex = startIndex + i;

                // 检查索引是否在数组范围内。
                // 如果 currentIndex 超出范围，就不会进入if语句，
                // 对应的位在 ux 中自然就是 0，实现了“后续补0”的效果。
                if (currentIndex < datas.Length && datas[currentIndex])
                {
                    // 如果位为 true，则将 ux 中对应的位置1
                    ux |= (1u << i);
                }
            }

            // 在函数的最后进行取反，与您原始的逻辑保持一致
            return ~ux;
        }

        #endregion

        #region IO操作

        public bool ReadInBit(int bit)
        {
            if (bit < 0 || bit >= ioinputnum) return false;
            return inputCache[bit];
        }

        public bool ReadOutBit(int bit)
        {
            if (bit < 0 || bit >= iooutputnum) return false;
            return outputCache[bit];
        }

        public bool WriteOutBit(int bit, bool sts)
        {
            if (bit < 0 || bit >= iooutputnum) return false;

            try
            {
                outputCache[bit] = sts;
                return true;
            }
            catch (Exception ex)
            {
                IOLogger.Instance.LogPLCCommunication($"Y{bit}", "WriteOutBit", ex.Message, false);
                return false;
            }
        }

        #endregion

        #region 边沿检测

        public bool ReadRisingBit(int bit)
        {
            if (bit < 0 || bit >= ioinputnum) return false;
            return risingEdgeCache[bit];
        }

        public void ClearRisingBit(int bit)
        {
            if (bit < 0 || bit >= ioinputnum) return;
            risingEdgeCache[bit] = false;
        }

        public bool ReadFallingBit(int bit)
        {
            if (bit < 0 || bit >= ioinputnum) return false;
            return fallingEdgeCache[bit];
        }

        public void ClearFallingBit(int bit)
        {
            if (bit < 0 || bit >= ioinputnum) return;
            fallingEdgeCache[bit] = false;
        }

        #endregion

        #region 批量操作

        /// <summary>
        /// 获取所有输入状态
        /// </summary>
        public BitArray GetInAll()
        {
            return new BitArray(inputCache);
        }

        /// <summary>
        /// 获取所有输出状态
        /// </summary>
        public BitArray GetOutAll()
        {
            return new BitArray(outputCache);
        }
        #endregion

    }
}
