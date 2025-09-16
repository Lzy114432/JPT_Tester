namespace Ewan.Model.Alarm
{
    /// <summary>
    /// 报警系统IO点位映射
    /// </summary>
    public static class AlarmIOMapping
    {
        // 三色灯控制输出点位
        public const int RED_LIGHT = 2;      // Y2 - 红灯
        public const int YELLOW_LIGHT = 1;   // Y1 - 黄灯  
        public const int GREEN_LIGHT = 0;    // Y0 - 绿灯
        
        // 蜂鸣器控制输出点位
        public const int BUZZER = 3;         // Y3 - 蜂鸣器
        
        // 停机控制输出点位
        public const int MAIN_STOP = 5;      // Y24 - 主停机信号
        public const int EMERGENCY_STOP = 6; // Y25 - 紧急停机信号
        
        // 严重报警输入点位 - 触发紧急停机
        public const int EMERGENCY_BUTTON = 0;    // X0 - 急停按钮
        public const int ROBOT_ALARM = 15;        // X15 - 机械手报警信号
        public const int LOWER_CAMERA_ALARM = 17; // X17 - 下相机报警信号
        public const int CYLINDER_ALARM = 19;     // X19 - 机械臂气缸报警信号

        // 暂停级报警输入点位 - 触发系统暂停
        public const int BIN1_LIMIT_ALARM = 12;   // X12 - 料仓1下限位置信号
        public const int BIN2_LIMIT_ALARM = 13;   // X13 - 料仓2下限位置信号
        public const int BIN3_LIMIT_ALARM = 14;   // X14 - 料仓3下限位置信号


    }
}