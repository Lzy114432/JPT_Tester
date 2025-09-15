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


        private const string isLoadingAddr = "52";

        protected override void OnInit()
        {
            _uiLogger.Info($"RingLineModule OnInit...");
        }

        protected override bool OnRun()
        {
            Task.Delay(_interval).Wait();

            try
            {
                var data = ModbusRTUManager.Instance().ReadCoil(isLoadingAddr, 1);
                Push(new RingLineModel {  IsLoading= data[0] });
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"RingLineModule run occur an exception{ex}");
            }
            return true;
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
