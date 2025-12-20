/*****************************************************
** 命名空间: Ewan.Mes.Devices.LaserCutting
** 文 件 名：LaserCuttingTopics
** 内容简述：激光切割机端点定义（协议无关）
** 版    本：V2.0
** 创 建 人：Ewan
** 创建日期：2025/12/11
** 修改记录：
日期        版本      修改人    修改内容
2025/12/12  V2.0     Ewan     重构为协议无关的 EndpointTemplate   
*****************************************************/
using Ewan.Mes.Transport;

namespace Ewan.Mes.Devices.ZHJW.LaserCutting
{
    /// <summary>
    /// 激光切割机端点定义（协议无关）
    /// 支持 MQTT、HTTP、WebSocket 等多种传输协议
    /// QosLevel: 0=最多一次, 1=至少一次, 2=恰好一次
    /// </summary>
    public static class LaserCuttingTopics
    {
        /// <summary>
        /// 上行端点（设备 -> MES）
        /// </summary>
        public static class Up
        {
            /// <summary>
            /// 扫描板片
            /// </summary>
            public static readonly EndpointTemplate ScanPlate = new EndpointTemplate("/device/{设备ID}/up/scan_plate", qosLevel: 2);

            /// <summary>
            /// 请求规格型号
            /// </summary>
            public static readonly EndpointTemplate RequestModel = new EndpointTemplate("/device/{设备ID}/up/request_model", qosLevel: 2);

            /// <summary>
            /// 扫描NG
            /// </summary>
            public static readonly EndpointTemplate ScanNg = new EndpointTemplate("/device/{设备ID}/up/scan_ng", qosLevel: 2);

            /// <summary>
            /// 报警
            /// </summary>
            public static readonly EndpointTemplate Alarm = new EndpointTemplate("/device/{设备ID}/up/alarm", qosLevel: 2);

            /// <summary>
            /// 上报生产数据
            /// </summary>
            public static readonly EndpointTemplate ReportData = new EndpointTemplate("/device/{设备ID}/up/report_data", qosLevel: 2);

            /// <summary>
            /// 上报配方
            /// </summary>
            public static readonly EndpointTemplate Formula = new EndpointTemplate("/device/{设备ID}/up/formula", qosLevel: 2);

            /// <summary>
            /// 设备状态
            /// </summary>
            public static readonly EndpointTemplate DeviceState = new EndpointTemplate("/device/{设备ID}/up/device_state", qosLevel: 2);

            /// <summary>
            /// 心跳
            /// </summary>
            public static readonly EndpointTemplate Heartbeat = new EndpointTemplate("/device/{设备ID}/up/heartbeat", qosLevel: 2);
        }

        /// <summary>
        /// 下行端点（MES -> 设备）
        /// </summary>
        public static class Down
        {
            /// <summary>
            /// 返回规格型号
            /// </summary>
            public static readonly EndpointTemplate ResponseModel = new EndpointTemplate("/device/{设备ID}/down/response_model/{设备编码}", qosLevel: 2);
        }
    }
}
