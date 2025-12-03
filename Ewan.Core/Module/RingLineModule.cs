using Ewan.Core.Msg;
using Ewan.Core.Plc;
using Ewan.Core.ScanCode;
using Ewan.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ewan.Core.Module
{
    public class RingLineModule :BaseModule<RingLineModule>
    {
        /// <summary>
        /// 读取PLC的时间间隔
        /// </summary>
        private int _interval = 200;

        private const string isLoadingAddr = "152";
        private const string EmptyCarStartAddress = "x=4;2040";
        private const ushort EmptyCarByteLength = 20;

        /// <summary>
        /// 上一次的IsLoading状态，用于边缘检测
        /// </summary>
        private bool _lastIsLoading = false;

        protected override void OnInit()
        {
            _uiLogger.Info("环线模块已初始化");
            _lastIsLoading = false;
        }

        protected override bool OnRun()
        {
            Task.Delay(_interval).Wait();

            try
            {
                // 读取u16类型需要2个字节
                var data = ModbusRTUManager.Instance().Read(isLoadingAddr, 2);
                if (data != null && data.Length >= 2)
                {
                    // Modbus大端字节序: 高字节在前，低字节在后
                    ushort value = (ushort)((data[0] << 8) | data[1]);
                    bool currentIsLoading = value == 1;
                    int emptyCarCount = ReadEmptyCarCount();
                    
                    // 边缘检测
                    bool risingEdge = currentIsLoading && !_lastIsLoading;   // 上升沿: False → True
                    bool fallingEdge = !currentIsLoading && _lastIsLoading;  // 下降沿: True → False
                    
                    // 推送包含边缘检测结果的数据
                    Push(new RingLineModel 
                    { 
                        IsLoading = currentIsLoading,
                        RisingEdge = risingEdge,
                        FallingEdge = fallingEdge,
                        EmptyCarCount = emptyCarCount
                    });
                    
                    // 更新上一次状态
                    _lastIsLoading = currentIsLoading;
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"环线模块运行异常: {ex.Message}");
            }
            return true;
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

        private void Push(RingLineModel ringLineModel)
        {
            MessageModel msg = new MessageModel(MsgSubject.RingLineData, ringLineModel);
            MsgManager.Instance().PushMsg(msg);
            //s_logger.Debug("Push plc heart data...");
        }


        protected override void OnDestroy()
        {
            //throw new NotImplementedException();
        }
    }
}
