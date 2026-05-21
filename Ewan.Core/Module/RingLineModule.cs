using Ewan.Core.Plc;
using Ewan.Core.ScanCode;
using Ewan.Model;
using Ewan.Model.Messages;
using Ewan.Model.System;
using EwanCore.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ewan.Core.Module
{
    public class RingLineModule : BaseModule<RingLineModule>
    {
        // ... (保持原有字段不变)
        private int _interval = 20;
        private CancellationTokenSource _mesFeedingCts;
        private Task<MesRingLineFeedback> _mesFeedingTask;
        private const int MES_REQUEST_TIMEOUT_BUFFER_MS = 5000;
        private const string isLoadingAddr = "152";

        // 地址定义
        // 4#站 (中料仓)
        private const string str_中料仓LoadingQrCodeAddr = "800";

        // 6#站 (后料仓)
        private const string str_后料仓LoadingQrCodeAddr = "900";

        // 假设每个二维码地址占用长度足够 cover "站号+...二维码+..." 的总长，或者仅仅是二维码
        // 如果是要读取 PLC 拼好的长串，20可能不够。先保持20或者加大
        private const ushort QrCodeByteLength = 40; // 加大长度以防万一

        private const string EmptyCarStartAddress = "x=4;2040";
        private const ushort EmptyCarByteLength = 20;

        private byte _lastState = 0;

        // 缓存
        private string str_中料仓lastLoadingQrCode = string.Empty;
        private string str_中料仓lastUnloadingQrCode = string.Empty;
        private string str_后料仓lastLoadingQrCode = string.Empty;
        private string str_后料仓lastUnloadingQrCode = string.Empty;

        protected override void OnInit()
        {
            _uiLogger.Info("环线模块已初始化");
            _lastState = 0;
        }
        bool risingEdge = false;
        bool fallingEdge = false;
        protected override bool OnRun()
        {
            Task.Delay(_interval).Wait();

            try
            {
                var data = ModbusRTUManager.Instance().Read(isLoadingAddr, 2);
                if (data != null && data.Length >= 2)
                {
                    ushort value = (ushort)((data[0] << 8) | data[1]);
                    byte currentState = (byte)value;
                    int emptyCarCount = ReadEmptyCarCount();
                    int cuttingBridgeCarCount = ReadCuttingBridgeCarCount();



                    // 边缘检测变量

                    bool risingEdge = currentState == 1 && _lastState != 1;
                    bool fallingEdge = currentState == 2 && _lastState != 2;
                    /*&& _lastState != 1*/
                    //bool fallingEdge = currentState == 2 /*&& _lastState != 2*/;
                    bool isLoading = _lastState == 0 && currentState == 2;

                    Push(new RingLineModel
                    {
                        IsLoading = isLoading,
                        RisingEdge = risingEdge,
                        FallingEdge = fallingEdge,
                        EmptyCarCount = emptyCarCount,
                        CuttingBridgeCarCount = cuttingBridgeCarCount,
                    });
                    _lastState = currentState;
                }

                if (SystemParametersManager.Instance.Parameters.MesEnabled)
                {
                    // 读取二维码信息变量
                    string str_中料仓loadingQrCode = "";
                    string str_中料仓unloadingQrCode = "";  // 暂不需要
                    string str_后料仓loadingQrCode = "";
                    string str_后料仓unloadingQrCode = "";  // 暂不需要
                    // 1. 读取并清空 PLC 地址 (防止重复读)
                    // 注意：如果 PLC 逻辑是"没读走就一直保持"，Write(0) 是必须的
                    // 如果 PLC 自动清零，则不需要 Write(0)

                    // 读取 4# 站 (中料仓) -> 地址 800
                    str_中料仓loadingQrCode = ReadPlateInfo(str_中料仓LoadingQrCodeAddr);
                    //if (!string.IsNullOrEmpty(str_中料仓loadingQrCode))
                    //    ModbusRTUManager.Instance().WriteAny(str_中料仓LoadingQrCodeAddr, (ushort)0);

                    // 读取 6# 站 (后料仓) -> 地址 900
                    str_后料仓loadingQrCode = ReadPlateInfo(str_后料仓LoadingQrCodeAddr);
                    //if (!string.IsNullOrEmpty(str_后料仓loadingQrCode))
                    //    ModbusRTUManager.Instance().WriteAny(str_后料仓LoadingQrCodeAddr, (ushort)0);


                    // 2. 核心逻辑：组装数据并发送 (边缘检测)
                    // 格式：站号 + 料仓号(1/2/3) + 进出(1/2) + 空位(0) + F + QRCODE

                    // ----------------------------------------------------
                    // 4# 站 (中料仓) 上料逻辑
                    // ----------------------------------------------------
                    if (!string.IsNullOrEmpty(str_中料仓loadingQrCode) && str_中料仓loadingQrCode != str_中料仓lastLoadingQrCode)
                    {
                        // 组装格式: 04 + 1 + 1 + 0 + F + CODE
                        // 假设中料仓是"1"号仓，上料是"1"
                        var v_temp = str_中料仓loadingQrCode.Split(',');
                        string station = v_temp[0];
                        string bin = $"BIN{v_temp[1]}"; // 请确认料仓号
                        string type = v_temp[2]; // 进出：1(进/上料),2出
                        string finalCode = v_temp[4].Remove(0, 1);
                        if (type == "1")
                        {
                            _uiLogger.InfoRaw("检测到中段上料(4#): {0} -> 拼装: {1}", str_中料仓loadingQrCode, finalCode);
                            TriggerMesRequest(finalCode, MesRingLineAction.FeedingZhongLiaocang, bin);

                        }
                        else
                        {
                            _uiLogger.InfoRaw("检测到中段上料(4#): {0} -> 拼装: {1}", str_中料仓loadingQrCode, finalCode);
                            TriggerMesRequest(finalCode, MesRingLineAction.UnloadingZhongLiaocang, bin);

                        }
                    }

                    // ----------------------------------------------------
                    // 6# 站 (后料仓) 上料逻辑
                    // ----------------------------------------------------
                    if (!string.IsNullOrEmpty(str_后料仓loadingQrCode) && str_后料仓loadingQrCode != str_后料仓lastLoadingQrCode)
                    {
                        // 组装格式: 06 + 2 + 1 + 0 + F + CODE
                        // 假设后料仓是"2"号仓，上料是"1"
                        var v_temp = str_后料仓loadingQrCode.Split(',');
                        string station = v_temp[0];
                        string bin = $"BIN{v_temp[1]}"; // 请确认料仓号
                        string type = v_temp[2]; // 进出：1(进/上料),2出，3为直接上皮带
                        string finalCode = v_temp[4].Remove(0, 1);
                        var v_temp_last = str_后料仓lastLoadingQrCode.Split(',');
                        if (type == "1")
                        {
                            _uiLogger.InfoRaw("检测到后段上料(6#): {0} -> 拼装: {1}", str_中料仓loadingQrCode, finalCode);
                            TriggerMesRequest(finalCode, MesRingLineAction.FeedingHouLiaocang, bin);

                        }
                        else if (type == "2")
                        {
                            _uiLogger.InfoRaw("检测到后段上料(6#): {0} -> 拼装: {1}", str_中料仓loadingQrCode, finalCode);
                            TriggerMesRequest(finalCode, MesRingLineAction.UnloadingHouLiaocang, bin);

                        }
                        else if (type == "3" && v_temp_last.Length >= 2)//环型线逻辑后段扫码会有2次，从料仓出去一次，下到后段一次，这里用来防止上传两次
                        {

                            if (v_temp_last[2] != "2")
                            {
                                _uiLogger.InfoRaw("检测到后段上料(6#): {0} -> 拼装: {1}", str_中料仓loadingQrCode, finalCode);
                                TriggerMesRequest(finalCode, MesRingLineAction.FeedingQingxihongganji);

                            }


                        }
                    }

                    // 更新历史状态
                    str_中料仓lastLoadingQrCode = str_中料仓loadingQrCode;
                    str_后料仓lastLoadingQrCode = str_后料仓loadingQrCode;
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"环线模块运行异常: {ex.Message}");
            }
            return true;
        }

        // ... (TriggerMesRequest 与 ClearMesFeedingRequest 保持不变) ...

        /// <summary>
        /// 触发 MES 请求 (封装)
        /// </summary>
        private void TriggerMesRequest(string code, MesRingLineAction action, string liaokuangCode = "0")
        {
            ClearMesFeedingRequest();

            var requestTimeoutMs = 30 * 1000;
            MesRingLineRequest request;
            if (liaokuangCode == "0")//直接下料到烘干机不需要料框
            {
                request = new MesRingLineRequest
                {
                    Action = action,
                    PlateCode = code, // 这里发送的是拼装好的完整字符串
                    BillNoWip = string.Empty,
                    TimeoutMs = requestTimeoutMs
                };
            }
            else
            {
                request = new MesRingLineRequest
                {
                    Action = action,
                    PlateCode = code, // 这里发送的是拼装好的完整字符串
                    BillNoWip = string.Empty,
                    FeedingLiaokuangCode = liaokuangCode,
                    TimeoutMs = requestTimeoutMs
                };
            }


            _mesFeedingCts = new CancellationTokenSource();
            _mesFeedingTask = MessageHub.Current.RequestAsync<MesRingLineRequest, MesRingLineFeedback>(
                request,
                timeoutMs: requestTimeoutMs + MES_REQUEST_TIMEOUT_BUFFER_MS,
                cancellationToken: _mesFeedingCts.Token);

            //_ = _mesFeedingTask.ContinueWith(t =>
            //{
            //    if (t.IsFaulted) _uiLogger.ErrorRaw("MES异常[{0}]: {1}", action, t.Exception?.GetBaseException().Message);
            //    else if (t.Result != null && !t.Result.Success) _uiLogger.WarnRaw("MES失败[{0}]: {1}", action, t.Result.Message);
            //    else _uiLogger.InfoRaw("MES成功[{0}]: {1}", action, code);
            //});
        }

        private void ClearMesFeedingRequest()
        {
            if (_mesFeedingCts != null)
            {
                try { _mesFeedingCts.Cancel(); } catch { }
                _mesFeedingCts.Dispose();
                _mesFeedingCts = null;
            }
            _mesFeedingTask = null;
        }

        //private string ReadQrCode(string startAddress)
        //{
        //    try
        //    {
        //        var qrData = ModbusRTUManager.Instance().Read(startAddress, QrCodeByteLength,"main");
        //        if (qrData == null || qrData.Length == 0)
        //        {
        //            return string.Empty;
        //        }

        //        // ---------------------------------------------------------
        //        // 核心修复：处理字节序问题 (ZF2H... -> FZH2...)
        //        // ---------------------------------------------------------
        //        // 现象：相邻两个字符顺序颠倒
        //        // 原因：Modbus寄存器的高低字节顺序与C#解析顺序不一致
        //        // 解决：遍历数组，每两个字节进行一次交换
        //        for (int i = 0; i < qrData.Length - 1; i += 2)
        //        {
        //            byte temp = qrData[i];
        //            qrData[i] = qrData[i + 1];
        //            qrData[i + 1] = temp;
        //        }
        //        // ---------------------------------------------------------

        //        string rawString = Encoding.ASCII.GetString(qrData);

        //        // 后续处理：去除空值、截断换行符、去除非法字符
        //        string cleanString = rawString.Trim('\0');

        //        // 查找换行符位置 (0x0A = \n, 0x0D = \r)
        //        int index = cleanString.IndexOfAny(new char[] { '\r', '\n' });
        //        if (index >= 0)
        //        {
        //            cleanString = cleanString.Substring(0, index);
        //        }

        //        // 过滤非法字符
        //        var validChars = cleanString.Trim()
        //            .Where(c => !char.IsControl(c))
        //            .ToArray();

        //        return new string(validChars);
        //    }
        //    catch (Exception ex)
        //    {
        //        _uiLogger.Error($"读取二维码失败({startAddress}): {ex.Message}");
        //        return string.Empty;
        //    }
        //}

        // ... (ReadEmptyCarCount, ReadCuttingBridgeCarCount, Push, OnDestroy 保持不变)
        /// <summary>
        /// 读取并解析站台板件信息 (站号+料仓号+进出+空位+F+QRCode)
        /// </summary>
        private string ReadPlateInfo(string startAddress)
        {
            try
            {
                var qrData = ModbusRTUManager.Instance().Read(startAddress, QrCodeByteLength, "main");
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

                // 校验数据长度是否足够 (至少包含头部8字节 + F + 数据)
                if (qrData.Length < 10) return string.Empty;

                // 2. 解析头部二进制信息
                // 注意：经过上面的 Swap，原本的 06 00 (LE) 变成了 00 06 (BE)
                // 数组现在是: [00, 06, 00, 01, 00, 01, 00, 00, 46(F), ...]

                // 站号 (Byte 0-1): 00 06 -> 6
                int station = (qrData[0]) | qrData[1];

                // 料仓号 (Byte 2-3): 00 01 -> 1
                int bin = (qrData[2]) | qrData[3];

                // 进出 (Byte 4-5): 00 01 -> 1
                int type = (qrData[4]) | qrData[5];

                // 空位 (Byte 6-7): 00 00 -> 0 
                int empty = (qrData[6] << 8) | qrData[7];

                // 3. 解析字符串部分 (从第8字节开始)
                // 找到 'F' (0x46) 的位置，理论上应该是 Index 8
                // 如果前导全为0，可能读到的是空包
                if (station == 0 && bin == 0) return string.Empty;

                // 提取后续的字符串部分 (FZH...)
                // 跳过前8个字节
                string rawString = Encoding.ASCII.GetString(qrData, 8, qrData.Length - 8);
                string cleanString = rawString.Trim('\0');

                // 截断换行符
                int index = cleanString.IndexOfAny(new char[] { '\r', '\n' });
                if (index >= 0)
                {
                    cleanString = cleanString.Substring(0, index);
                }

                // 4. 最终拼接格式
                // 格式：站号(2位) + 料仓号(1位) + 进出(1位) + 空位(1位) + 字符串(F开头...)
                // "06" + "1" + "1" + "0" + "FZH26010001173"
                return $"{station:D2},{bin},{type},{empty},{cleanString}";
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"读取板件信息失败({startAddress}): {ex.Message}");
                return string.Empty;
            }
        }

        private int ReadEmptyCarCount()
        {
            try
            {
                var emptyData = ModbusRTUManager.Instance().Read(EmptyCarStartAddress, EmptyCarByteLength, "main");
                if (emptyData == null || emptyData.Length < 2)
                {
                    return 0;
                }

                int length = Math.Max(emptyData.Length, EmptyCarByteLength);
                int emptyCount = 0;
                for (int i = 0; i + 1 < length; i += 2)
                {
                    ushort carValue = (ushort)((emptyData[i] << 8) | emptyData[i + 1]);
                    if (carValue == 0)
                    {
                        emptyCount++;
                    }
                }
                return emptyCount - 1;
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"读取空车数量失败: {ex.Message}");
                return 0;
            }
        }

        private int ReadCuttingBridgeCarCount()
        {
            try
            {
                var data = ModbusRTUManager.Instance().Read(EmptyCarStartAddress, EmptyCarByteLength, "main");
                if (data == null || data.Length < 2)
                {
                    return 0;
                }

                int length = Math.Max(data.Length, EmptyCarByteLength);
                int cuttingBridgeCount = 0;
                for (int i = 0; i + 1 < length; i += 2)
                {
                    ushort carValue = (ushort)((data[i] << 8) | data[i + 1]);
                    if (carValue == 1)
                    {
                        cuttingBridgeCount++;
                    }
                }
                return cuttingBridgeCount;
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"读取切栈桥车数量失败: {ex.Message}");
                return 0;
            }
        }

        private void Push(RingLineModel ringLineModel)
        {
            // 使用强类型消息发布
            var message = RingLineDataMessage.FromModel(ringLineModel);
            MessageHub.Current.Post(message);
        }


        protected override void OnDestroy()
        {
            //throw new NotImplementedException();
        }
    }
}