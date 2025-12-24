using EwanIO.Core.Interfaces;
using EwanSMC606;
using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace EwanIO.Hardware.SMC606IO
{
    /// <summary>
    /// SMC606 IO驱动包装器
    /// 实现IHardwareIO接口
    /// 边缘检测由上层 EdgeDetector 统一处理
    /// </summary>
    public class IOSMC606DriverWrapper : IHardwareIO
    {
        #region IOSMC606接口实现
        //板卡配置
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

        private int inputboardCount = 0, outputboardCount = 0;

        // 缓存输入输出状态
        private BitArray inputCache;
        private BitArray outputCache;

        private bool isConnected = false;
        private Smc606Lease? _lease;

        public string HardwareType => "IOSMC606";

        public string ConnectionInfo => $"Boards:{boardCount}";

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => isConnected;

        /// <summary>
        /// 输入点数
        /// </summary>
        public int InputCount => ioinputnum;

        /// <summary>
        /// 输出点数
        /// </summary>
        public int OutputCount => iooutputnum;

        #region 初始化和销毁代码

        /// <summary>
        /// 初始化缓存
        /// </summary>
        private void InitializeCaches()
        {
            inputCache = new BitArray(ioinputnum);
            outputCache = new BitArray(iooutputnum);
        }

        /// <summary>
        /// 解析富地址连接字符串
        /// 格式: IP地址|input=数量|output=数量|inputboards=数量|outputboards=数量
        /// 例如: 192.168.1.100|input=64|output=48|inputboards=2|outputboards=2
        /// 如果没有指定参数，则使用默认值或从硬件读取
        /// </summary>
        private (string actualAddress, ushort? cardNo, ushort? connectType, uint? baudRate, ushort? inputCount, ushort? outputCount, int? inputBoards, int? outputBoards) ParseConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return (connectionString, null, null, null, null, null, null, null);
            }

            string[] parts = connectionString.Split('|');
            string actualAddress = parts[0];
            ushort? cardNo = null;
            ushort? connectType = null;
            uint? baudRate = null;
            ushort? inputCount = null;
            ushort? outputCount = null;
            int? inputBoards = null;
            int? outputBoards = null;

            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (string.IsNullOrEmpty(part)) continue;

                string[] keyValue = part.Split('=');
                if (keyValue.Length != 2) continue;

                string key = keyValue[0].Trim().ToLower();
                string value = keyValue[1].Trim();

                switch (key)
                {
                    case "card":
                    case "cardno":
                        if (ushort.TryParse(value, out ushort cardVal))
                            cardNo = cardVal;
                        break;
                    case "connecttype":
                        if (ushort.TryParse(value, out ushort ctVal))
                            connectType = ctVal;
                        break;
                    case "baud":
                        if (uint.TryParse(value, out uint baudVal))
                            baudRate = baudVal;
                        break;
                    case "input":
                        if (ushort.TryParse(value, out ushort inVal))
                            inputCount = inVal;
                        break;
                    case "output":
                        if (ushort.TryParse(value, out ushort outVal))
                            outputCount = outVal;
                        break;
                    case "inputboards":
                        if (int.TryParse(value, out int inBoards))
                            inputBoards = inBoards;
                        break;
                    case "outputboards":
                        if (int.TryParse(value, out int outBoards))
                            outputBoards = outBoards;
                        break;
                }
            }

            return (actualAddress, cardNo, connectType, baudRate, inputCount, outputCount, inputBoards, outputBoards);
        }

        /// <summary>
        /// 连接到硬件
        /// </summary>
        /// <param name="connectionString">连接字符串，支持富地址格式: IP|input=数量|output=数量|inputboards=数量|outputboards=数量</param>
        public bool Connect(string connectionString)
        {
            if (isConnected) return true;

            try
            {
                // 解析富地址
                var (actualAddress, cardNo, connectType, baudRate, inputCount, outputCount, inputBoards, outputBoards) =
                    ParseConnectionString(connectionString);

                ushort actualCardNo = cardNo ?? 0;
                ushort actualConnectType = connectType ?? 2;

                uint[] baudCandidates = baudRate.HasValue
                    ? new[] { baudRate.Value }
                    : new[] { 115200u, 1000000u };

                foreach (uint baud in baudCandidates)
                {
                    try
                    {
                        var options = new Smc606ConnectionOptions
                        {
                            CardNo = actualCardNo,
                            ConnectType = actualConnectType,
                            ConnectString = actualAddress,
                            BaudRate = baud
                        };

                        _lease = Smc606ConnectionPool.Acquire(options);
                        card = actualCardNo;
                        return SetupIOConfig(inputCount, outputCount, inputBoards, outputBoards);
                    }
                    catch
                    {
                        _lease?.Dispose();
                        _lease = null;
                    }
                }

                isConnected = false;
                return false;
            }
            catch (Exception ex)
            {
                isConnected = false;
                _lease?.Dispose();
                _lease = null;
                return false;
            }
        }

        /// <summary>
        /// 配置IO参数
        /// </summary>
        private bool SetupIOConfig(ushort? inputCount, ushort? outputCount, int? inputBoards, int? outputBoards)
        {
            // 优先使用指定的数量，否则尝试从硬件读取，最后使用默认值
            if (inputCount.HasValue && outputCount.HasValue)
            {
                ioinputnum = inputCount.Value;
                iooutputnum = outputCount.Value;
            }
            else
            {
                // 尝试从硬件读取IO数量
                ushort totalIn = 0, totalOut = 0;
                short getRes;
                var syncRoot = _lease?.SyncRoot;
                if (syncRoot != null)
                {
                    lock (syncRoot)
                    {
                        getRes = smc_get_total_ionum(card, ref totalIn, ref totalOut);
                    }
                }
                else
                {
                    getRes = smc_get_total_ionum(card, ref totalIn, ref totalOut);
                }

                if (getRes == 0 && totalIn > 0 && totalOut > 0)
                {
                    ioinputnum = totalIn;
                    iooutputnum = totalOut;
                }
                else
                {
                    ioinputnum = 40;
                    iooutputnum = 34;
                }
            }

            // 计算板卡数量
            inputboardCount = inputBoards ?? (int)Math.Ceiling(ioinputnum / 32.0);
            outputboardCount = outputBoards ?? (int)Math.Ceiling(iooutputnum / 32.0);

            InitializeCaches();
            isConnected = true;
            return true;
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public bool Disconnect()
        {
            try
            {
                _lease?.Dispose();
                _lease = null;

                boardCount = 0;
                isConnected = false;
                inputboardCount = 0;
                outputboardCount = 0;
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    _lease?.Dispose();
                }
                catch
                {
                    // ignored
                }
                _lease = null;
                isConnected = false;
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
            var syncRoot = _lease?.SyncRoot;
            if (syncRoot != null)
            {
                lock (syncRoot)
                {
                    for (ushort i = 0; i < inputboardCount; i++)
                    {
                        uint currentInput = (uint)~smc_read_inport(card, i);
                        datas[i] = currentInput;
                    }
                }
            }
            else
            {
                for (ushort i = 0; i < inputboardCount; i++)
                {
                    uint currentInput = (uint)~smc_read_inport(card, i);
                    datas[i] = currentInput;
                }
            }

            int bitIndex = 0;
            for (int i = 0; i < inputboardCount; i++)
            {
                uint value = datas[i];

                for (int b = 0; b < 32 && bitIndex < ioinputnum; b++)
                {
                    bool bit = (value & (1u << b)) != 0;
                    inputCache[bitIndex] = bit;
                    bitIndex++;
                }
            }
        }

        private void IO_OutputDataSync()
        {
            if (!IsConnected) return;

            try
            {
                var syncRoot = _lease?.SyncRoot;
                if (syncRoot != null)
                {
                    lock (syncRoot)
                    {
                        for (ushort i = 0; i < outputboardCount; i++)
                        {
                            int offset = i * 32;
                            uint outputValue = ConvertBitArrayToUint(outputCache, offset);
                            smc_write_outport(card, i, outputValue);
                        }
                    }
                }
                else
                {
                    for (ushort i = 0; i < outputboardCount; i++)
                    {
                        int offset = i * 32;
                        uint outputValue = ConvertBitArrayToUint(outputCache, offset);
                        smc_write_outport(card, i, outputValue);
                    }
                }
            }
            catch (Exception ex)
            {
                // OutputSync失败，静默处理
            }
        }

        /// <summary>
        /// 将 BitArray 的一个指定片段转换为一个 uint。
        /// </summary>
        uint ConvertBitArrayToUint(BitArray datas, int startIndex)
        {
            if (datas == null)
            {
                return ~0u;
            }

            uint ux = 0;
            for (int i = 0; i < 32; i++)
            {
                int currentIndex = startIndex + i;

                if (currentIndex < datas.Length && datas[currentIndex])
                {
                    ux |= (1u << i);
                }
            }

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

        public bool WriteOutBit(int bit, bool value)
        {
            if (bit < 0 || bit >= iooutputnum) return false;

            try
            {
                outputCache[bit] = value;
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

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
