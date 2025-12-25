using System;

namespace Ewan.Mes.Devices.ZHJW.LaserRepair
{
    /// <summary>
    /// 设备状态枚举
    /// </summary>
    public enum DeviceState
    {
        /// <summary>
        /// 运行
        /// </summary>
        Running = 1,

        /// <summary>
        /// 故障
        /// </summary>
        Fault = 2,

        /// <summary>
        /// 待机
        /// </summary>
        Standby = 3,

        /// <summary>
        /// 停机
        /// </summary>
        Stopped = 4
    }

    /// <summary>
    /// 上料结果枚举
    /// </summary>
    public enum FeedingResult
    {
        /// <summary>
        /// 上料不成功
        /// </summary>
        Failed = 0,

        /// <summary>
        /// 上料成功
        /// </summary>
        Success = 1
    }

    /// <summary>
    /// 生产结果枚举
    /// </summary>
    public enum ProductionResult
    {
        /// <summary>
        /// NG
        /// </summary>
        NG = 0,

        /// <summary>
        /// OK
        /// </summary>
        OK = 1
    }

    /// <summary>
    /// 时间戳格式化辅助类
    /// </summary>
    public static class TimestampHelper
    {
        /// <summary>
        /// 标准时间格式：2025-11-05 15:32:00.000
        /// </summary>
        public const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

        /// <summary>
        /// 获取当前时间的格式化字符串
        /// </summary>
        public static string Now()
        {
            return DateTime.Now.ToString(TimestampFormat);
        }

        /// <summary>
        /// 格式化指定时间
        /// </summary>
        public static string Format(DateTime dateTime)
        {
            return dateTime.ToString(TimestampFormat);
        }

        /// <summary>
        /// 尝试解析时间戳字符串
        /// </summary>
        public static bool TryParse(string timestamp, out DateTime result)
        {
            return DateTime.TryParseExact(timestamp, TimestampFormat,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out result);
        }

        /// <summary>
        /// 解析时间戳字符串，失败返回null
        /// </summary>
        public static DateTime? Parse(string timestamp)
        {
            DateTime result;
            if (TryParse(timestamp, out result))
            {
                return result;
            }
            return null;
        }
    }

    /// <summary>
    /// 异常信息辅助类
    /// </summary>
    public static class FaultMessageHelper
    {
        /// <summary>
        /// 异常信息分隔符（英文下划线）
        /// </summary>
        public const string Separator = "_";

        /// <summary>
        /// 合并多个异常信息
        /// </summary>
        public static string Combine(params string[] messages)
        {
            if (messages == null || messages.Length == 0)
            {
                return string.Empty;
            }
            return string.Join(Separator, messages);
        }

        /// <summary>
        /// 拆分异常信息
        /// </summary>
        public static string[] Split(string faultMessage)
        {
            if (string.IsNullOrEmpty(faultMessage))
            {
                return new string[0];
            }
            return faultMessage.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
