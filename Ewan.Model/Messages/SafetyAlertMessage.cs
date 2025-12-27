using EwanCore.Messaging;
using Ewan.Model.Safety;
using System;

namespace Ewan.Model.Messages
{
    /// <summary>
    /// 安全报警消息 - 用于安全相关报警的强类型消息
    /// </summary>
    public sealed class SafetyAlertMessage : IMessage
    {
        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

        /// <summary>
        /// 报警类型
        /// </summary>
        public SafetyAlertType AlertType { get; }

        /// <summary>
        /// 报警级别
        /// </summary>
        public SafetyAlertLevel AlertLevel { get; }

        /// <summary>
        /// 报警描述
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 是否需要立即停机
        /// </summary>
        public bool RequireEmergencyStop { get; }

        /// <summary>
        /// IO地址（如果相关）
        /// </summary>
        public string IOAddress { get; }

        /// <summary>
        /// 创建安全报警消息
        /// </summary>
        public SafetyAlertMessage(SafetyAlertType alertType, SafetyAlertLevel alertLevel, string description, bool requireEmergencyStop = false, string ioAddress = null)
        {
            AlertType = alertType;
            AlertLevel = alertLevel;
            Description = description ?? string.Empty;
            RequireEmergencyStop = requireEmergencyStop;
            IOAddress = ioAddress ?? string.Empty;
        }

        /// <summary>
        /// 从 SafetyAlert 创建消息
        /// </summary>
        public static SafetyAlertMessage FromSafetyAlert(SafetyAlert alert)
        {
            if (alert == null)
            {
                throw new ArgumentNullException(nameof(alert));
            }

            return new SafetyAlertMessage(
                alert.AlertType,
                alert.AlertLevel,
                alert.Description,
                alert.RequireEmergencyStop,
                alert.IOAddress);
        }

        /// <summary>
        /// 创建急停报警
        /// </summary>
        public static SafetyAlertMessage EmergencyStop(string description, string ioAddress = null)
            => new SafetyAlertMessage(SafetyAlertType.EmergencyStop, SafetyAlertLevel.Critical, description, true, ioAddress);

        /// <summary>
        /// 创建安全门报警
        /// </summary>
        public static SafetyAlertMessage SafetyDoor(string description, SafetyAlertLevel level = SafetyAlertLevel.Alarm, string ioAddress = null)
            => new SafetyAlertMessage(SafetyAlertType.SafetyDoorOpen, level, description, false, ioAddress);

        /// <summary>
        /// 创建电机故障报警
        /// </summary>
        public static SafetyAlertMessage MotorAlarm(string description, bool requireStop = true, string ioAddress = null)
            => new SafetyAlertMessage(SafetyAlertType.MotorAlarm, SafetyAlertLevel.Alarm, description, requireStop, ioAddress);

        /// <summary>
        /// 创建一般安全警告
        /// </summary>
        public static SafetyAlertMessage Warning(string description, string ioAddress = null)
            => new SafetyAlertMessage(SafetyAlertType.GeneralSafety, SafetyAlertLevel.Warning, description, false, ioAddress);
    }
}
