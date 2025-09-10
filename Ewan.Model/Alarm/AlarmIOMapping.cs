namespace Ewan.Model.Alarm
{
    /// <summary>
    /// 报警系统IO点位映射
    /// </summary>
    public static class AlarmIOMapping
    {
        // 三色灯控制输出点位
        public const int RED_LIGHT = 20;      // Y20 - 红灯
        public const int YELLOW_LIGHT = 21;   // Y21 - 黄灯  
        public const int GREEN_LIGHT = 22;    // Y22 - 绿灯
        
        // 蜂鸣器控制输出点位
        public const int BUZZER = 23;         // Y23 - 蜂鸣器
        
        // 停机控制输出点位
        public const int MAIN_STOP = 24;      // Y24 - 主停机信号
        public const int EMERGENCY_STOP = 25; // Y25 - 紧急停机信号
        
        // 报警输入检测点位
        public const int EMERGENCY_BUTTON = 10;   // X10 - 急停按钮
        public const int SAFETY_DOOR = 11;        // X11 - 安全门
        public const int MOTOR_ALARM = 12;        // X12 - 电机报警
        public const int PRESSURE_LOW = 13;       // X13 - 气压不足
    }
}