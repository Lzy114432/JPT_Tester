/*****************************************************
** 命名空间: Ewan.Mes.Devices.LaserCutting
** 文 件 名：LaserCuttingConstants
** 内容简述：激光切割机常量定义
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/11
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/

namespace Ewan.Mes.Devices.ZHJW.LaserCutting
{
    /// <summary>
    /// 激光切割机常量定义
    /// </summary>
    public static class LaserCuttingConstants
    {
        /// <summary>
        /// 设备类型名称
        /// </summary>
        public const string DeviceTypeName = "LaserCutting";

        /// <summary>
        /// 默认 Broker 地址
        /// </summary>
        public const string DefaultBrokerHost = "172.24.10.28";

        /// <summary>
        /// 测试 Broker 地址
        /// </summary>
        public const string TestBrokerHost = "172.24.10.21";

        /// <summary>
        /// 默认 Broker 端口
        /// </summary>
        public const int DefaultBrokerPort = 1883;

        /// <summary>
        /// 默认 QoS 级别
        /// </summary>
        public const int DefaultQos = 2;

        /// <summary>
        /// 设备状态枚举
        /// </summary>
        public static class DeviceState
        {
            /// <summary>
            /// 运行
            /// </summary>
            public const int Running = 1;

            /// <summary>
            /// 故障
            /// </summary>
            public const int Fault = 2;

            /// <summary>
            /// 待机
            /// </summary>
            public const int Standby = 3;

            /// <summary>
            /// 停机
            /// </summary>
            public const int Shutdown = 4;
        }

        /// <summary>
        /// 生产结果
        /// </summary>
        public static class ProductionResult
        {
            /// <summary>
            /// OK
            /// </summary>
            public const int OK = 1;

            /// <summary>
            /// NG
            /// </summary>
            public const int NG = 0;
        }
    }
}
