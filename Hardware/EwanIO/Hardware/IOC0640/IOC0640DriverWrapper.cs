using System;
using System.Runtime.InteropServices;
using EwanIO.Core.Interfaces;

namespace EwanIO.Hardware.IOC0640
{
    /// <summary>
    /// IOC0640驱动包装器
    /// 实现IHardwareIO接口，提供对IOC0640板卡的访问
    /// 边缘检测由上层 EdgeDetector 统一处理
    /// </summary>
    public class IOC0640DriverWrapper : IHardwareIO
    {
        #region 雷赛接口实现
        [DllImport("IOC0640.dll", EntryPoint = "ioc_board_init", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ioc_board_init();
        [DllImport("IOC0640.dll", EntryPoint = "ioc_board_close", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern void ioc_board_close();

        [DllImport("IOC0640.dll", EntryPoint = "ioc_read_inbit", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ioc_read_inbit(ushort cardno, ushort bitno);
        [DllImport("IOC0640.dll", EntryPoint = "ioc_read_outbit", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ioc_read_outbit(ushort cardno, ushort bitno);
        [DllImport("IOC0640.dll", EntryPoint = "ioc_write_outbit", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern uint ioc_write_outbit(ushort cardno, ushort bitno, int on_off);

        [DllImport("IOC0640.dll", EntryPoint = "ioc_read_inport", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ioc_read_inport(ushort cardno, ushort m_PortNo);
        [DllImport("IOC0640.dll", EntryPoint = "ioc_read_outport", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ioc_read_outport(ushort cardno, ushort m_PortNo);
        [DllImport("IOC0640.dll", EntryPoint = "ioc_write_outport", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern uint ioc_write_outport(ushort cardno, ushort m_PortNo, uint port_value);

        public delegate uint IOC0640_OPERATE(IntPtr operate_data);
        [DllImport("IOC0640.dll", EntryPoint = "ioc_int_enable", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern uint ioc_int_enable(ushort cardno, IOC0640_OPERATE funcIntHandler, IntPtr operate_data);
        [DllImport("IOC0640.dll", EntryPoint = "ioc_int_disable", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern uint ioc_int_disable(ushort cardno);

        [DllImport("IOC0640.dll", EntryPoint = "ioc_config_intbitmode", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern uint ioc_config_intbitmode(ushort cardno, ushort bitno, ushort enable, ushort logic);
        [DllImport("IOC0640.dll", EntryPoint = "ioc_config_intbitmode", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern uint ioc_config_intbitmode(ushort cardno, ushort bitno, ushort[] enable, ushort[] logic);
        [DllImport("IOC0640.dll", EntryPoint = "ioc_read_intbitstatus", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ioc_read_intbitstatus(ushort cardno, ushort bitno);

        [DllImport("IOC0640.dll", EntryPoint = "ioc_config_intporten", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern uint ioc_config_intporten(ushort cardno, ushort m_PortNo, uint port_en);
        [DllImport("IOC0640.dll", EntryPoint = "ioc_config_intportlogic", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern uint ioc_config_intportlogic(ushort cardno, ushort m_PortNo, uint port_logic);

        [DllImport("IOC0640.dll", EntryPoint = "ioc_read_intportmode", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern uint ioc_read_intportmode(ushort cardno, ushort m_PortNo, uint[] enable, uint[] logic);
        [DllImport("IOC0640.dll", EntryPoint = "ioc_read_intportstatus", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ioc_read_intportstatus(ushort cardno, ushort m_PortNo);

        [DllImport("IOC0640.dll", EntryPoint = "ioc_set_filter", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern uint ioc_set_filter(ushort cardno, double filter);

        #endregion

        #region 私有字段
        private int boardCount = 0;
        private uint[] inputBuffer;
        private uint[] outputBuffer;
        private bool isConnected = false;

        // 最大支持的板卡数量
        private const int MAX_BOARDS = 8;
        #endregion

        #region IHardwareIO 接口实现

        /// <summary>
        /// 硬件类型
        /// </summary>
        public string HardwareType => "IOC0640";

        /// <summary>
        /// 连接信息
        /// </summary>
        public string ConnectionInfo => $"Boards:{boardCount}";

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => isConnected;

        /// <summary>
        /// 输入点数
        /// </summary>
        public int InputCount => boardCount * 32;

        /// <summary>
        /// 输出点数
        /// </summary>
        public int OutputCount => boardCount * 32;

        /// <summary>
        /// 连接到硬件
        /// </summary>
        /// <param name="connectionString">连接字符串，格式：boards=n 或留空自动检测</param>
        public bool Connect(string connectionString)
        {
            try
            {
                // 解析连接字符串
                int requestedBoards = 0;
                if (!string.IsNullOrEmpty(connectionString))
                {
                    // 支持格式：boards=2 或直接数字 2
                    if (connectionString.ToLower().StartsWith("boards="))
                    {
                        string boardStr = connectionString.Substring(7);
                        int.TryParse(boardStr, out requestedBoards);
                    }
                    else
                    {
                        int.TryParse(connectionString, out requestedBoards);
                    }
                }

                boardCount = ioc_board_init();

                if (boardCount > 0)
                {
                    // 根据实际板卡数量分配缓冲区
                    if (boardCount > MAX_BOARDS)
                    {
                        boardCount = MAX_BOARDS;
                    }

                    // 如果指定了板卡数量，使用较小的值
                    if (requestedBoards > 0 && requestedBoards < boardCount)
                    {
                        boardCount = requestedBoards;
                    }

                    // 分配缓冲区
                    inputBuffer = new uint[boardCount];
                    outputBuffer = new uint[boardCount];

                    // 初始化缓冲区
                    for (int i = 0; i < boardCount; i++)
                    {
                        inputBuffer[i] = 0;
                        outputBuffer[i] = 0;
                    }

                    isConnected = true;
                    return true;
                }
                else
                {
                    isConnected = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
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
                ioc_board_close();
                boardCount = 0;
                isConnected = false;

                // 清理缓冲区
                inputBuffer = null;
                outputBuffer = null;

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// 数据同步
        /// </summary>
        public void DataSync()
        {
            if (!isConnected || boardCount <= 0 || inputBuffer == null) return;

            try
            {
                // 读取所有输入并写入所有输出
                for (ushort i = 0; i < boardCount; i++)
                {
                    // 读取输入
                    uint currentInput = (uint)~ioc_read_inport(i, 0);
                    inputBuffer[i] = currentInput;

                    // 写入输出
                    uint output = ~outputBuffer[i];
                    ioc_write_outport(i, 0, output);
                }
            }
            catch (Exception ex)
            {
                // DataSync失败，静默处理
            }
        }

        /// <summary>
        /// 读取输入位
        /// </summary>
        public bool ReadInBit(int bit)
        {
            if (!isConnected || bit < 0 || bit >= boardCount * 32) return false;

            int boardIndex = bit / 32;
            int bitIndex = bit % 32;

            return (inputBuffer[boardIndex] & (1u << bitIndex)) != 0;
        }

        /// <summary>
        /// 读取输出位
        /// </summary>
        public bool ReadOutBit(int bit)
        {
            if (!isConnected || bit < 0 || bit >= boardCount * 32) return false;

            int boardIndex = bit / 32;
            int bitIndex = bit % 32;

            return (outputBuffer[boardIndex] & (1u << bitIndex)) != 0;
        }

        /// <summary>
        /// 写入输出位
        /// </summary>
        public bool WriteOutBit(int bit, bool value)
        {
            if (!isConnected || bit < 0 || bit >= boardCount * 32) return false;

            int boardIndex = bit / 32;
            int bitIndex = bit % 32;

            if (value)
                outputBuffer[boardIndex] |= (1u << bitIndex);
            else
                outputBuffer[boardIndex] &= ~(1u << bitIndex);

            return true;
        }

        #endregion

        #region 公共属性
        /// <summary>
        /// 获取检测到的板卡数量
        /// </summary>
        public int BoardCount => boardCount;
        #endregion

        #region IDisposable 实现

        private bool _disposed = false;

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // 断开硬件连接
                Disconnect();
            }

            _disposed = true;
        }

        #endregion
    }
}
