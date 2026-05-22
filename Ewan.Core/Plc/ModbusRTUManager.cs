using Ewan.Model.System;
using EwanCommon.Logging;
using EwanCore;
using EwanCore.Attribute;
using HslCommunication;
using HslCommunication.ModBus;
using log4net;
using log4net.Core;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using System.IO.Ports;
using System.Linq;
using System.Text;

namespace Ewan.Core.Plc
{
    /// <summary>
    /// Modbus RTU管理器 - RS485串口通信实现
    /// 支持RS485网络通信，有效距离50米，支持多从站
    /// </summary>
    [Manager(Priority = 1)]
    public class ModbusRTUManager : IManager
    {
        private static readonly ILog s_logger = Log.GetLogger(typeof(ModbusRTUManager));
        private bool _disposed;

        #region 单例支持
        private static readonly Lazy<ModbusRTUManager> s_instance = new Lazy<ModbusRTUManager>(() => new ModbusRTUManager());
        public static ModbusRTUManager Instance() => s_instance.Value;
        #endregion

        private const string DefaultClientKey = "default";
        private readonly ConcurrentDictionary<string, ClientContext> _clientContexts = new ConcurrentDictionary<string, ClientContext>(StringComparer.OrdinalIgnoreCase);

        private class ClientContext
        {
            public string Key { get; set; }
            public byte StationNo { get; set; }
            public string ComPort { get; set; }
            public int BaudRate { get; set; }
            public int DataBits { get; set; }
            public StopBits StopBits { get; set; }
            public Parity Parity { get; set; }
            public ModbusRtu Client { get; set; }
            public bool IsConnected { get; set; }
        }

        public bool Init()
        {
            s_logger.Info("ModbusRTUManager 初始化开始");
            try
            {
                var defaultContext = new ClientContext
                {
                    Key = DefaultClientKey,
                    StationNo = 1,
                    ComPort = "COM10",
                    BaudRate = 115200,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Parity = Parity.None
                };

                s_logger.InfoFormat("ModbusRTUManager 尝试初始化 - 串口:{0}, 波特率:{1}, 数据位:{2}, 停止位:{3}, 校验位:{4}, 从站地址:{5}",
                    defaultContext.ComPort, defaultContext.BaudRate, defaultContext.DataBits, defaultContext.StopBits, defaultContext.Parity, defaultContext.StationNo);

                if (Open(defaultContext))
                {
                    _clientContexts[DefaultClientKey] = defaultContext;
                    s_logger.InfoFormat("ModbusRTUManager 初始化成功 - ID:{0}, RS485连接已建立", this.GetHashCode());

                    // 初始化 main 客户端 - COM10
                    var mainContext = new ClientContext
                    {
                        Key = "main",
                        StationNo = 1,
                        ComPort = "COM9",
                        BaudRate = 115200,
                        DataBits = 8,
                        StopBits = StopBits.One,
                        Parity = Parity.None
                    };

                    if (Open(mainContext))
                    {
                        _clientContexts["main"] = mainContext;
                        s_logger.Info("ModbusRTUManager main客户端初始化成功 - 串口:COM10");
                    }
                    else
                    {
                        s_logger.Warn("ModbusRTUManager main客户端初始化失败 - 串口:COM10");
                    }

                    s_logger.Info("ModbusRTUManager 初始化完成");
                    return true;
                }
                else
                {
                    s_logger.ErrorFormat("ModbusRTUManager 初始化失败 - ID:{0}, 无法建立RS485连接", this.GetHashCode());
                    return false;
                }
            }
            catch (Exception ex)
            {
                s_logger.Error("ModbusRTUManager 初始化异常", ex);
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            s_logger.Info("ModbusRTUManager 开始销毁");
            try
            {
                Close();
            }
            catch (Exception ex)
            {
                s_logger.Error("ModbusRTUManager 销毁异常", ex);
            }
            s_logger.Info("ModbusRTUManager 销毁完成");
        }

        [Obsolete("请使用 Dispose() 方法")]
        public void Destroy() => Dispose();

        /// <summary>
        /// 打开RS485串口连接
        /// </summary>
        /// <returns>是否连接成功</returns>
        private bool Open(ClientContext context)
        {
            var isSuccess = true;

            s_logger.InfoFormat("ModbusRTUManager 尝试打开RS485串口连接 - 客户端:{0}, 串口:{1}", context.Key, context.ComPort);

            try
            {
                if (context.Client == null)
                {
                    context.Client = new ModbusRtu();
                }

                context.Client.SerialPortInni(context.ComPort, context.BaudRate, context.DataBits, context.StopBits, context.Parity);
                context.Client.Station = context.StationNo;
                context.Client.Open();

                context.IsConnected = true;
                s_logger.InfoFormat("ModbusRTUManager RS485连接成功 - 客户端:{0}, 串口:{1}, 波特率:{2}, 从站地址:{3}", context.Key, context.ComPort, context.BaudRate, context.StationNo);
            }
            catch (Exception ex)
            {
                isSuccess = false;
                context.IsConnected = false;
                s_logger.Error("ModbusRTUManager RS485连接异常", ex);
            }
            return isSuccess;
        }

        private ClientContext GetContext(string clientKey)
        {
            var key = string.IsNullOrWhiteSpace(clientKey) ? DefaultClientKey : clientKey;
            if (_clientContexts.TryGetValue(key, out var context))
            {
                return context;
            }

            s_logger.InfoFormat("ModbusRTUManager 未找到客户端:{0}", key);
            return null;
        }

        private void CloseInternal(ClientContext context)
        {
            try
            {
                context.Client?.Close();
            }
            catch (Exception ex)
            {
                s_logger.Error($"ModbusRTUManager 关闭客户端 {context.Key} 时异常", ex);
            }
            finally
            {
                context.IsConnected = false;
            }
        }

        /// <summary>
        /// 注册一个新的Modbus RTU客户端
        /// </summary>
        public bool RegisterClient(string clientKey, string comPort, int baudRate, int dataBits, StopBits stopBits, Parity parity, byte stationNo)
        {
            if (string.IsNullOrWhiteSpace(clientKey))
            {
                throw new ArgumentException("clientKey 不能为空", nameof(clientKey));
            }

            var normalizedKey = clientKey.Trim();
            var context = new ClientContext
            {
                Key = normalizedKey,
                ComPort = comPort,
                BaudRate = baudRate,
                DataBits = dataBits,
                StopBits = stopBits,
                Parity = parity,
                StationNo = stationNo
            };

            if (Open(context))
            {
                if (_clientContexts.TryGetValue(normalizedKey, out var existing))
                {
                    CloseInternal(existing);
                }

                _clientContexts[normalizedKey] = context;
                s_logger.InfoFormat("ModbusRTUManager 新客户端已注册 - Key:{0}, 串口:{1}", normalizedKey, comPort);
                return true;
            }

            s_logger.ErrorFormat("ModbusRTUManager 新客户端注册失败 - Key:{0}", normalizedKey);
            return false;
        }

        /// <summary>
        /// 读数据
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public byte[] Read(string address, ushort length, string clientKey = null)
        {
            try
            {
                var context = GetContext(clientKey);
                if (context == null)
                {
                    return Array.Empty<byte>();
                }

                OperateResult<byte[]> read = context.Client.Read(address, length);
                if (read.IsSuccess)
                {
                    return read.Content;
                }
                else
                {
                    s_logger.InfoFormat("ModbusRTUManager Read failed, {0}", read.ToMessageShowString());
                    return new byte[0];
                }
            }
            catch (Exception ex)
            {
                s_logger.ErrorFormat("ModbusRTUManager Read occur exception, {0}", ex);
                return new byte[0];
            }
        }


        public bool[] ReadCoil(string address, ushort length, string clientKey = null)
        {
            try
            {
                var context = GetContext(clientKey);
                if (context == null)
                {
                    return Array.Empty<bool>();
                }

                bool[] read = context.Client.ReadCoil(address, length).Content;
                return read;
            }
            catch (Exception ex)
            {
                s_logger.ErrorFormat("ModbusRTUManager Read occur exception, {0}", ex);
                return new bool[0];
            }
        }


        private string func_Read(string startAddress, ushort U_Length)
        {
            try
            {
                var qrData = ModbusRTUManager.Instance().Read(startAddress, U_Length, "main");
                if (qrData == null || qrData.Length == 0)
                {
                    return string.Empty;
                }

                // 1. 全局字节交换 (修复 Modbus 高低字节)
                // 原始数据: 06 00 01 00 ... (小端显示) -> 实际上是 00 06 ...
                // 经过交换后: 00 06 00 01 ... (符合大端阅读习惯，且字符串部分正常)
                for (int i = 0; i < qrData.Length - 1; i += 2)
                {
                    byte temp = qrData[i];
                    qrData[i] = qrData[i + 1];
                    qrData[i + 1] = temp;
                }
                string result = string.Concat(qrData.Select(b => (char)(b)));
                //var result = string.Concat(qrData.Where((b, i) => i % 2 == 0)  // 偶数索引
                //                  .Select(b => (char)(b + '0')));

                //string rawString = Encoding.ASCII.GetString(qrData, 0, qrData.Length - 0);
                //string cleanString = rawString.Replace("\0", "");

                //// 截断换行符
                //int index = cleanString.IndexOfAny(new char[] { '\r', '\n' });
                //if (index >= 0)
                //{
                //    cleanString = cleanString.Substring(0, index);
                //}

                // 4. 最终拼接格式
                // 格式：站号(2位) + 料仓号(1位) + 进出(1位) + 空位(1位) + 字符串(F开头...)
                // "06" + "1" + "1" + "0" + "FZH26010001173"
                return $"{result}";
            }
            catch (Exception ex)
            {
                //_uiLogger.Error($"读取板件信息失败({startAddress}): {ex.Message}");
                return string.Empty;
            }
        }

        public bool WriteWorkOrderToFirstAvailable(string workOrder, string clientKey = null)
        {
            const ushort length = 10;
            const string primaryAddress = "701";  // 
            const string primaryAddress1 = "711";  // 
            const string secondaryAddress = "731"; //
            const string secondaryAddress1 = "741"; //

            ushort us_Cur1 = 1;
            ushort us_Cur2 = 1;
            var v_A = func_Read(primaryAddress, length);
            var v_B = func_Read(primaryAddress1, length);
            var v_C = func_Read(secondaryAddress, length);
            var v_D = func_Read(secondaryAddress1, length);
            //var v_D1 = func_Read("700", 1);
            //var v_D2 = func_Read("730", 1);

            if (SystemParametersManager.Instance.Parameters.str_当前工单号 == v_A && v_A != "")
            {

                us_Cur1 = 1;
            }
            else if (SystemParametersManager.Instance.Parameters.str_当前工单号 == v_B && v_B != "")
            {
                us_Cur1 = 2;
            }
            if (SystemParametersManager.Instance.Parameters.str_当前工单号 == v_C && v_C != "")
            {
                us_Cur2 = 1;
            }
            else if (SystemParametersManager.Instance.Parameters.str_当前工单号 == v_D && v_D != "")
            {
                us_Cur2 = 2;
            }
            WriteAny("700", us_Cur1, "main");
            WriteAny("730", us_Cur2, "main");
            //SystemParametersManager.Instance.Parameters.str_当前工单号；
            try
            {    //0 = UNKNOWN // 未知
                 //1 = EMPTY // 空
                 //2 = HAS_ITEM // 有料
                 //3 = FULL // 满
                 //4 = ERROR // 异常

                // 尝试读取主区
                var primaryBytes = func_Read("190", 1);
                var primaryBytes1 = func_Read("191", 1);

                if (!(v_A.Contains(workOrder) || v_B.Contains(workOrder)))
                {
                    if (primaryBytes == "\u0001\0")
                        WriteStringToRegisters(primaryAddress, workOrder, length, clientKey);
                    else if (primaryBytes1 == "\u0001\0")
                        WriteStringToRegisters(primaryAddress1, workOrder, length, clientKey);
                }

                var primaryBytes2 = func_Read("192", 1);
                var primaryBytes3 = func_Read("193", 1);
                if (!(v_C.Contains(workOrder) || v_D.Contains(workOrder)))
                {
                    if (primaryBytes2 == "\u0001\0")
                        WriteStringToRegisters(secondaryAddress, workOrder, length, clientKey);
                    else if (primaryBytes3 == "\u0001\0")
                        WriteStringToRegisters(secondaryAddress1, workOrder, length, clientKey);
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public bool func_清空单号(string str_工单地址, string clientKey = null)
        {

            const ushort length = 10;
            try
            {
                return WriteStringToRegisters(str_工单地址, "", length, clientKey);
            }
            catch (Exception ex)
            {
                return false;
            }
        }


        // 将字符串转换为 length 个 uint16 并写入指定寄存器地址（大端）
        public bool WriteStringToRegisters(string address, string text, ushort length, string clientKey = null)
        {
            if (text == null) text = string.Empty;
            // 最大可存储字符数 = 寄存器数量 * 2
            int maxChars = length * 2;
            if (text.Length > maxChars) text = text.Substring(0, maxChars);

            byte[] payload = new byte[length * 2];  // 每个寄存器 2 字节

            // 遍历每个寄存器（不是每个字符）
            for (int regIdx = 0; regIdx < length; regIdx++)
            {
                // 当前寄存器对应的两个字符在字符串中的索引
                int charIdx1 = regIdx * 2;
                int charIdx2 = regIdx * 2 + 1;

                // 获取字符值（如果索引超出则填 0）
                ushort val1 = (charIdx1 < text.Length) ? (ushort)(text[charIdx1]) : (ushort)0;
                ushort val2 = (charIdx2 < text.Length) ? (ushort)(text[charIdx2]) : (ushort)0;

                // 存储：高字节 = val1，低字节 = val2
                payload[regIdx * 2 + 1] = (byte)val1;   // 高字节
                payload[regIdx * 2] = (byte)val2;   // 低字节
            }

            var result = ModbusRTUManager.Instance().WriteAny(address, payload, clientKey ?? "main");
            return result != null && result.IsSuccess;
        }
        private bool WriteStringToRegisters1(string address, string text, ushort length, string clientKey = null)
        {
            if (text == null) text = string.Empty;
            // 最大可存储字符数 = 寄存器数量 * 2
            int maxChars = length * 2;
            if (text.Length > maxChars) text = text.Substring(0, maxChars);

            byte[] payload = new byte[length * 2];  // 每个寄存器 2 字节

            // 遍历每个寄存器（不是每个字符）
            for (int regIdx = 0; regIdx < length; regIdx++)
            {
                // 当前寄存器对应的两个字符在字符串中的索引
                int charIdx1 = regIdx * 2;
                int charIdx2 = regIdx * 2 + 1;

                // 获取字符值（如果索引超出则填 0）
                ushort val1 = (charIdx1 < text.Length) ? (ushort)(text[charIdx1]) : (ushort)0;
                ushort val2 = (charIdx2 < text.Length) ? (ushort)(text[charIdx2]) : (ushort)0;

                // 存储：高字节 = val1，低字节 = val2
                payload[regIdx * 2] = (byte)val1;   // 高字节
                payload[regIdx * 2 + 1] = (byte)val2;   // 低字节
            }

            var result = ModbusRTUManager.Instance().WriteAny(address, payload, clientKey ?? "main");
            return result != null && result.IsSuccess;
        }
        /// <summary>
        /// 万能写入 - 支持任意数据类型转字节后写入
        /// </summary>
        public OperateResult WriteAny(string address, object value, string clientKey = null)
        {
            try
            {
                var context = GetContext(clientKey);
                if (context == null)
                {
                    return CreateClientNotFoundResult(clientKey);
                }

                byte[] data = ConvertToBytes(value);
                return context.Client.Write(address, data);
            }
            catch (Exception ex)
            {
                s_logger.ErrorFormat("WriteAny failed: {0}", ex);
                return OperateResult.CreateSuccessResult(ex.Message);
            }
        }

        private byte[] ConvertToBytes(object value)
        {
            switch (value)
            {
                case byte[] bytes:
                    return bytes;
                case byte b:
                    // u8: 单字节，Modbus寄存器写入需要2字节（高字节为0）
                    return new byte[] { 0x00, b };
                case ushort us:
                    // u16: 大端字节序（Modbus标准）
                    return new byte[] { (byte)(us >> 8), (byte)(us & 0xFF) };
                case short s:
                    // i16: 大端字节序
                    ushort usValue = (ushort)s;
                    return new byte[] { (byte)(usValue >> 8), (byte)(usValue & 0xFF) };
                case int i:
                    // i32: 大端字节序
                    uint uiValue = (uint)i;
                    return new byte[] {
                        (byte)(uiValue >> 24),
                        (byte)((uiValue >> 16) & 0xFF),
                        (byte)((uiValue >> 8) & 0xFF),
                        (byte)(uiValue & 0xFF)
                    };
                case uint ui:
                    // u32: 大端字节序
                    return new byte[] {
                        (byte)(ui >> 24),
                        (byte)((ui >> 16) & 0xFF),
                        (byte)((ui >> 8) & 0xFF),
                        (byte)(ui & 0xFF)
                    };
                case float f:
                    // float: 转为字节后确保大端
                    byte[] floatBytes = BitConverter.GetBytes(f);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(floatBytes);
                    return floatBytes;
                case double d:
                    // double: 转为字节后确保大端
                    byte[] doubleBytes = BitConverter.GetBytes(d);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(doubleBytes);
                    return doubleBytes;
                default:
                    throw new ArgumentException($"Unsupported data type: {value.GetType()}");
            }
        }







        /// <summary>
        /// 关闭RS485串口连接
        /// </summary>
        /// <returns>是否成功关闭</returns>
        public bool Close(string clientKey = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(clientKey))
                {
                    foreach (var context in _clientContexts.Values)
                    {
                        s_logger.InfoFormat("ModbusRTUManager 开始关闭RS485连接 - 客户端:{0}, 串口:{1}", context.Key, context.ComPort);
                        CloseInternal(context);
                    }
                    return true;
                }

                var target = GetContext(clientKey);
                if (target == null)
                {
                    return false;
                }

                s_logger.InfoFormat("ModbusRTUManager 开始关闭RS485连接 - 客户端:{0}, 串口:{1}", target.Key, target.ComPort);
                CloseInternal(target);
                s_logger.InfoFormat("ModbusRTUManager 客户端 {0} 已关闭", target.Key);
                return true;
            }
            catch (Exception ex)
            {
                s_logger.Error("ModbusRTUManager 关闭RS485连接异常", ex);
                return false;
            }
        }

        /// <summary>
        /// 获取连接状态
        /// </summary>
        /// <returns>是否已连接</returns>
        public bool IsConnected(string clientKey = null)
        {
            var context = GetContext(clientKey);
            return context?.IsConnected == true;
        }

        /// <summary>
        /// 重新连接RS485
        /// </summary>
        /// <returns>是否重连成功</returns>
        public bool Reconnect(string clientKey = null)
        {
            try
            {
                var context = GetContext(clientKey);
                if (context == null)
                {
                    return false;
                }

                s_logger.InfoFormat("ModbusRTUManager 开始重新连接RS485 - 客户端:{0}", context.Key);
                CloseInternal(context);
                context.Client = null;
                return Open(context);
            }
            catch (Exception ex)
            {
                s_logger.Error("ModbusRTUManager RS485重连异常", ex);
                return false;
            }
        }

        /// <summary>
        /// 设置串口参数
        /// </summary>
        /// <param name="comPort">串口号</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="parity">校验位</param>
        /// <param name="stationNo">从站地址</param>
        /// <param name="clientKey">客户端 Key，默认为 default</param>
        public void SetSerialParams(string comPort, int baudRate, int dataBits, StopBits stopBits, Parity parity, byte stationNo, string clientKey = null)
        {
            var context = GetContext(clientKey);
            if (context == null)
            {
                return;
            }

            context.ComPort = comPort;
            context.BaudRate = baudRate;
            context.DataBits = dataBits;
            context.StopBits = stopBits;
            context.Parity = parity;
            context.StationNo = stationNo;

            s_logger.InfoFormat("ModbusRTUManager 串口参数已更新 - 客户端:{0}, 串口:{1}, 波特率:{2}, 数据位:{3}, 停止位:{4}, 校验位:{5}, 从站地址:{6}",
                context.Key, comPort, baudRate, dataBits, stopBits, parity, stationNo);

            if (context.IsConnected)
            {
                CloseInternal(context);
                Open(context);
            }
        }

        /// <summary>
        /// 获取当前串口配置信息
        /// </summary>
        /// <returns>配置信息字符串</returns>
        public string GetConfigInfo(string clientKey = null)
        {
            var context = GetContext(clientKey);
            if (context == null)
            {
                return "客户端不存在";
            }

            return $"客户端:{context.Key}, 串口:{context.ComPort}, 波特率:{context.BaudRate}, 数据位:{context.DataBits}, 停止位:{context.StopBits}, 校验位:{context.Parity}, 从站地址:{context.StationNo}, 连接状态:{(context.IsConnected ? "已连接" : "未连接")}";
        }

        private OperateResult CreateClientNotFoundResult(string clientKey)
        {
            return new OperateResult
            {
                IsSuccess = false,
                Message = $"客户端 {(!string.IsNullOrWhiteSpace(clientKey) ? clientKey : DefaultClientKey)} 不存在"
            };
        }
    }
}
