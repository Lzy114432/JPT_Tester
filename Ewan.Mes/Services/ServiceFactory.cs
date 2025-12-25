using Ewan.Mes.Mqtt;
using Ewan.Mes.Transport;
using Ewan.Mes.Services.ZHJW;

namespace Ewan.Mes.Services
{
    /// <summary>
    /// 服务工厂，统一创建设备服务实例
    /// 后续可替换为 DI 容器注册
    /// </summary>
    public static class ServiceFactory
    {
        /// <summary>
        /// 创建激光切割机服务
        /// </summary>
        public static LaserCuttingService CreateLaserCuttingService(
            MqttConnectionOptions options,
            string deviceId,
            string deviceCode)
        {
            var client = new MqttClientWrapper(options);
            var transport = new MqttMessageTransport(client, disposeClient: true);
            return new LaserCuttingService(transport, deviceId, deviceCode);
        }

        /// <summary>
        /// 创建激光切割机服务（使用已有传输层）
        /// </summary>
        public static LaserCuttingService CreateLaserCuttingService(
            IMessageTransport transport,
            string deviceId,
            string deviceCode)
        {
            return new LaserCuttingService(transport, deviceId, deviceCode);
        }

        /// <summary>
        /// 创建镭修机服务
        /// </summary>
        public static LaserRepairService CreateLaserRepairService(
            MqttConnectionOptions options,
            string deviceId,
            string deviceCode)
        {
            var client = new MqttClientWrapper(options);
            var transport = new MqttMessageTransport(client, disposeClient: true);
            return new LaserRepairService(transport, deviceId, deviceCode);
        }

        /// <summary>
        /// 创建镭修机服务（使用已有传输层）
        /// </summary>
        public static LaserRepairService CreateLaserRepairService(
            IMessageTransport transport,
            string deviceId,
            string deviceCode)
        {
            return new LaserRepairService(transport, deviceId, deviceCode);
        }

        /// <summary>
        /// 创建环线服务
        /// </summary>
        public static RingLineService CreateRingLineService(
            MqttConnectionOptions options,
            string deviceId,
            string deviceCode)
        {
            var client = new MqttClientWrapper(options);
            var transport = new MqttMessageTransport(client, disposeClient: true);
            return new RingLineService(transport, deviceId, deviceCode);
        }

        /// <summary>
        /// 创建环线服务（使用已有传输层）
        /// </summary>
        public static RingLineService CreateRingLineService(
            IMessageTransport transport,
            string deviceId,
            string deviceCode)
        {
            return new RingLineService(transport, deviceId, deviceCode);
        }

        /// <summary>
        /// 创建划片机服务
        /// </summary>
        public static DicingMachineService CreateDicingMachineService(
            MqttConnectionOptions options,
            string deviceId,
            string deviceCode)
        {
            var client = new MqttClientWrapper(options);
            var transport = new MqttMessageTransport(client, disposeClient: true);
            return new DicingMachineService(transport, deviceId, deviceCode);
        }

        /// <summary>
        /// 创建划片机服务（使用已有传输层）
        /// </summary>
        public static DicingMachineService CreateDicingMachineService(
            IMessageTransport transport,
            string deviceId,
            string deviceCode)
        {
            return new DicingMachineService(transport, deviceId, deviceCode);
        }
    }
}
