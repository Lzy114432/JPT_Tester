using System;

namespace Ewan.Model.Safety
{
    /// <summary>
    /// 安全报警类型
    /// </summary>
    public enum SafetyAlertType
    {
        /// <summary>
        /// 急停按钮触发
        /// </summary>
        EmergencyStop,
        
        /// <summary>
        /// 安全门打开
        /// </summary>
        SafetyDoorOpen,
        
        /// <summary>
        /// 电机故障
        /// </summary>
        MotorAlarm,
        
        /// <summary>
        /// 气压不足
        /// </summary>
        PressureLow,
        
        /// <summary>
        /// 通用安全报警
        /// </summary>
        GeneralSafety
    }

    /// <summary>
    /// 安全报警级别
    /// </summary>
    public enum SafetyAlertLevel
    {
        /// <summary>
        /// 警告 - 不需要停机
        /// </summary>
        Warning,
        
        /// <summary>
        /// 报警 - 需要处理但可继续运行
        /// </summary>
        Alarm,
        
        /// <summary>
        /// 危险 - 立即急停
        /// </summary>
        Critical
    }

    /// <summary>
    /// 安全报警消息
    /// </summary>
    public class SafetyAlert
    {
        /// <summary>
        /// 报警类型
        /// </summary>
        public SafetyAlertType AlertType { get; set; }
        
        /// <summary>
        /// 报警级别
        /// </summary>
        public SafetyAlertLevel AlertLevel { get; set; }
        
        /// <summary>
        /// 报警描述
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// 是否需要立即停机
        /// </summary>
        public bool RequireEmergencyStop { get; set; }
        
        /// <summary>
        /// 报警时间
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// IO地址（如果相关）
        /// </summary>
        public string IOAddress { get; set; }

        public SafetyAlert()
        {
            Timestamp = DateTime.Now;
        }

        public SafetyAlert(SafetyAlertType alertType, SafetyAlertLevel alertLevel, string description, bool requireEmergencyStop = false)
        {
            AlertType = alertType;
            AlertLevel = alertLevel;
            Description = description;
            RequireEmergencyStop = requireEmergencyStop;
            Timestamp = DateTime.Now;
        }
    }
}