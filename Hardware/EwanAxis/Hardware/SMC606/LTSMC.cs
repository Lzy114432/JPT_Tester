/*===================================================
 * 类名称: LTSMC
 * 类描述: 雷赛运动控制卡 SDK 封装
 * 来源: 参考 MotionSMC304 项目
 * 版本: V1.0
 =====================================================*/

using System;
using System.Runtime.InteropServices;

namespace EwanAxis.Hardware.SMC606
{
    /// <summary>
    /// 雷赛运动控制卡 SDK
    /// 适用于 SMC304/SMC606 等系列控制卡
    /// </summary>
    public static class LTSMC
    {
        private const string DllName = "LTSMC.dll";

        #region 板卡配置

        [DllImport(DllName)]
        public static extern short smc_board_init(ushort ConnectNo, ushort ConnectType, string pconnectstring, uint baud);

        [DllImport(DllName)]
        public static extern short smc_board_close(ushort ConnectNo);

        [DllImport(DllName)]
        public static extern short smc_get_card_version(ushort ConnectNo, ref uint CardVersion);

        [DllImport(DllName)]
        public static extern short smc_get_total_axes(ushort ConnectNo, ref uint TotalAxis);

        [DllImport(DllName)]
        public static extern short smc_get_total_ionum(ushort ConnectNo, ref ushort TotalIn, ref ushort TotalOut);

        #endregion

        #region 脉冲配置

        [DllImport(DllName)]
        public static extern short smc_set_pulse_outmode(ushort ConnectNo, ushort axis, ushort outmode);

        [DllImport(DllName)]
        public static extern short smc_get_pulse_outmode(ushort ConnectNo, ushort axis, ref ushort outmode);

        [DllImport(DllName)]
        public static extern short smc_set_equiv(ushort ConnectNo, ushort axis, double equiv);

        [DllImport(DllName)]
        public static extern short smc_get_equiv(ushort ConnectNo, ushort axis, ref double equiv);

        #endregion

        #region 编码器

        [DllImport(DllName)]
        public static extern short smc_set_encoder_unit(ushort ConnectNo, ushort axis, double pos);

        [DllImport(DllName)]
        public static extern short smc_get_encoder_unit(ushort ConnectNo, ushort axis, ref double pos);

        [DllImport(DllName)]
        public static extern short smc_set_counter_inmode(ushort ConnectNo, ushort axis, ushort mode);

        [DllImport(DllName)]
        public static extern short smc_get_counter_inmode(ushort ConnectNo, ushort axis, ref ushort mode);

        #endregion

        #region 单轴速度参数

        [DllImport(DllName)]
        public static extern short smc_set_profile_unit(ushort ConnectNo, ushort axis, double Min_Vel, double Max_Vel, double Tacc, double Tdec, double Stop_Vel);

        [DllImport(DllName)]
        public static extern short smc_get_profile_unit(ushort ConnectNo, ushort axis, ref double Min_Vel, ref double Max_Vel, ref double Tacc, ref double Tdec, ref double Stop_Vel);

        [DllImport(DllName)]
        public static extern short smc_set_s_profile(ushort ConnectNo, ushort axis, ushort s_mode, double s_para);

        [DllImport(DllName)]
        public static extern short smc_get_s_profile(ushort ConnectNo, ushort axis, ushort s_mode, ref double s_para);

        #endregion

        #region 单轴运动

        [DllImport(DllName)]
        public static extern short smc_pmove_unit(ushort ConnectNo, ushort axis, double Dist, ushort posi_mode);

        [DllImport(DllName)]
        public static extern short smc_vmove(ushort ConnectNo, ushort axis, ushort dir);

        [DllImport(DllName)]
        public static extern short smc_change_speed(ushort ConnectNo, ushort axis, double Curr_Vel, double Taccdec);

        #endregion

        #region 回零运动

        [DllImport(DllName)]
        public static extern short smc_set_home_pin_logic(ushort ConnectNo, ushort axis, ushort org_logic, double filter);

        [DllImport(DllName)]
        public static extern short smc_get_home_pin_logic(ushort ConnectNo, ushort axis, ref ushort org_logic, ref double filter);

        [DllImport(DllName)]
        public static extern short smc_set_home_profile_unit(ushort ConnectNo, ushort axis, double Low_Vel, double High_Vel, double Tacc, double Tdec);

        [DllImport(DllName)]
        public static extern short smc_get_home_profile_unit(ushort ConnectNo, ushort axis, ref double Low_Vel, ref double High_Vel, ref double Tacc, ref double Tdec);

        [DllImport(DllName)]
        public static extern short smc_set_homemode(ushort ConnectNo, ushort axis, ushort home_dir, double vel_mode, ushort mode, ushort pos_source);

        [DllImport(DllName)]
        public static extern short smc_get_homemode(ushort ConnectNo, ushort axis, ref ushort home_dir, ref double vel_mode, ref ushort home_mode, ref ushort pos_source);

        [DllImport(DllName)]
        public static extern short smc_home_move(ushort ConnectNo, ushort axis);

        [DllImport(DllName)]
        public static extern short smc_get_home_result(ushort ConnectNo, ushort axis, ref ushort state);

        [DllImport(DllName)]
        public static extern short smc_set_home_position_unit(ushort ConnectNo, ushort axis, ushort enable, double position);

        #endregion

        #region 状态监控

        [DllImport(DllName)]
        public static extern short smc_check_done(ushort ConnectNo, ushort axis);

        [DllImport(DllName)]
        public static extern short smc_stop(ushort ConnectNo, ushort axis, ushort stop_mode);

        [DllImport(DllName)]
        public static extern short smc_emg_stop(ushort ConnectNo);

        [DllImport(DllName)]
        public static extern uint smc_axis_io_status(ushort ConnectNo, ushort axis);

        [DllImport(DllName)]
        public static extern short smc_set_position_unit(ushort ConnectNo, ushort axis, double pos);

        [DllImport(DllName)]
        public static extern short smc_get_position_unit(ushort ConnectNo, ushort axis, ref double pos);

        [DllImport(DllName)]
        public static extern short smc_set_dec_stop_time(ushort ConnectNo, ushort axis, double time);

        [DllImport(DllName)]
        public static extern short smc_get_dec_stop_time(ushort ConnectNo, ushort axis, ref double time);

        #endregion

        #region 通用IO

        [DllImport(DllName)]
        public static extern short smc_read_inbit(ushort ConnectNo, ushort bitno);

        [DllImport(DllName)]
        public static extern short smc_write_outbit(ushort ConnectNo, ushort bitno, ushort on_off);

        [DllImport(DllName)]
        public static extern short smc_read_outbit(ushort ConnectNo, ushort bitno);

        [DllImport(DllName)]
        public static extern uint smc_read_inport(ushort ConnectNo, ushort portno);

        [DllImport(DllName)]
        public static extern uint smc_read_outport(ushort ConnectNo, ushort portno);

        [DllImport(DllName)]
        public static extern short smc_write_outport(ushort ConnectNo, ushort portno, uint outport_val);

        #endregion

        #region 专用IO操作

        [DllImport(DllName)]
        public static extern short smc_set_alm_mode(ushort ConnectNo, ushort axis, ushort enable, ushort alm_logic, ushort alm_action);

        [DllImport(DllName)]
        public static extern short smc_get_alm_mode(ushort ConnectNo, ushort axis, ref ushort enable, ref ushort alm_logic, ref ushort alm_action);

        [DllImport(DllName)]
        public static extern short smc_write_sevon_pin(ushort ConnectNo, ushort axis, ushort on_off);

        [DllImport(DllName)]
        public static extern short smc_read_sevon_pin(ushort ConnectNo, ushort axis);

        [DllImport(DllName)]
        public static extern short smc_write_erc_pin(ushort ConnectNo, ushort axis, ushort on_off);

        [DllImport(DllName)]
        public static extern short smc_read_erc_pin(ushort ConnectNo, ushort axis);

        #endregion

        #region 安全机制

        [DllImport(DllName)]
        public static extern short smc_set_el_mode(ushort ConnectNo, ushort axis, ushort enable, ushort el_logic, ushort el_mode);

        [DllImport(DllName)]
        public static extern short smc_get_el_mode(ushort ConnectNo, ushort axis, ref ushort enable, ref ushort el_logic, ref ushort el_mode);

        [DllImport(DllName)]
        public static extern short smc_set_softlimit_unit(ushort ConnectNo, ushort axis, ushort enable, ushort source_sel, ushort SL_action, double N_limit, double P_limit);

        [DllImport(DllName)]
        public static extern short smc_get_softlimit_unit(ushort ConnectNo, ushort axis, ref ushort enable, ref ushort source_sel, ref ushort SL_action, ref double N_limit, ref double P_limit);

        #endregion

        #region 螺距补偿

        [DllImport(DllName)]
        public static extern short smc_enable_leadscrew_comp(ushort ConnectNo, ushort axis, ushort enable);

        [DllImport(DllName)]
        public static extern short smc_set_leadscrew_comp_config_unit(ushort ConnectNo, ushort axis, ushort n, double startpos, double lenpos, double[] pCompPos, double[] pCompNeg);

        [DllImport(DllName)]
        public static extern short smc_get_leadscrew_comp_config_unit(ushort ConnectNo, ushort axis, ref ushort n, ref double startpos, ref double lenpos, double[] pCompPos, double[] pCompNeg);

        #endregion

        #region 总线相关

        [DllImport(DllName)]
        public static extern short nmcs_get_axis_state_machine(ushort ConnectNo, ushort axis, ref ushort Axis_StateMachine);

        [DllImport(DllName)]
        public static extern short nmcs_get_errcode(ushort ConnectNo, ushort PortNum, ref ushort errcode);

        [DllImport(DllName)]
        public static extern short nmcs_clear_errcode(ushort ConnectNo, ushort axis);

        #endregion
    }
}
