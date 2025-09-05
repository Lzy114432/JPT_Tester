using MotionSMC304;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

namespace Ewan.Core.Axis
{
    public class AxisManager : BaseManager<AxisManager>
    {
        ushort card = 0;

        public bool IsBusy
        {
            get
            {
                short rcode = LTSMC.smc_check_done(card, 0);
                return rcode != 1;
            }
        }


        public bool IsAlarm
        {
            get
            {
                uint val2 = LTSMC.smc_axis_io_status(card, 0);
                return (val2 & 0x01) > 0;
            }
        }

        public double Position
        {
            get
            {
                double pos = 0;
                LTSMC.smc_get_position_unit(card, 0, ref pos);
                pos = pos * (true ? -1 : 1);
                return pos / 50;
            }
            set => LTSMC.smc_set_position_unit(card, 0, value);
        }





        public override bool Init()
        {
            string ip = "192.168.5.11";

            short res = LTSMC.smc_board_init(card, 2, ip, 115200);//ec则认为只有一个卡
            if (res != 0)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.AxisManagerInitFailed, res);
                return false;
            }

            return base.Init();
        }
       

        public override void Destroy()
        {
            short res = LTSMC.smc_board_close(card);
            if (res != 0)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.AxisManagerDestroyFailed, res);
            }
            base.Destroy();
        }



        // 直接控制指定轴Jog - 带默认值
        public void DirectJog(double speed, double step = 50, double acc = 100, double dec = 100)
        {
            ushort axisNum = 0;
            double vel = Math.Abs(speed);
            int dir = speed > 0 ? 1 : 0;

            LTSMC.smc_set_s_profile(card, axisNum, 0, dec / 3);
            LTSMC.smc_set_profile_unit(card, axisNum, 10000, vel * step, acc, dec, 10000);
            LTSMC.smc_vmove(card, axisNum, (ushort)dir);
        }

        // 停止指定轴 - 带默认值
        public void DirectJogStop(ushort axisNum = 0)
        {
            LTSMC.smc_stop(card, axisNum, 0);
        }



        public void EmgStop(ushort axisNum = 0)
        {
            short val = LTSMC.smc_stop(card, axisNum, 1);
        }

    }
}
