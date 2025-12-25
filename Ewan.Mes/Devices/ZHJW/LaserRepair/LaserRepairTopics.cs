/*****************************************************
** 命名空间: Ewan.Mes.Devices.LaserRepair
** 文 件 名：LaserRepairTopics
** 内容简述：镭修机端点定义（协议无关）
** 版    本：V2.0
** 创 建 人：Ewan
** 创建日期：2025/12/3 17:59:41
** 修改记录：
日期        版本      修改人    修改内容
2025/12/12  V2.0     Ewan     重构为协议无关的 EndpointTemplate   
*****************************************************/
using Ewan.Mes.Transport;

namespace Ewan.Mes.Devices.ZHJW.LaserRepair
{
    /// <summary>
    /// 镭修机端点定义（协议无关）
    /// 支持 MQTT、HTTP、WebSocket 等多种传输协议
    /// QosLevel: 0=最多一次, 1=至少一次, 2=恰好一次
    /// </summary>
    public static class LaserRepairTopics
    {
        /// <summary>
        /// 上行端点（设备 -> MES）
        /// </summary>
        public static class Up
        {
            public static readonly EndpointTemplate Feeding = new EndpointTemplate("/device/{设备ID}/up/feeding", qosLevel: 2);
            public static readonly EndpointTemplate ConfirmFeeding = new EndpointTemplate("/device/{设备ID}/up/confirm_feeding", qosLevel: 2);
            public static readonly EndpointTemplate ScanNg = new EndpointTemplate("/device/{设备ID}/up/scan_ng", qosLevel: 2);
            public static readonly EndpointTemplate ScanPlate = new EndpointTemplate("/device/{设备ID}/up/scan_plate", qosLevel: 2);
            public static readonly EndpointTemplate RequestModel = new EndpointTemplate("/device/{设备ID}/up/request_model", qosLevel: 2);
            public static readonly EndpointTemplate Alarm = new EndpointTemplate("/device/{设备ID}/up/alarm", qosLevel: 1);
            public static readonly EndpointTemplate ReportData = new EndpointTemplate("/device/{设备ID}/up/report_data", qosLevel: 1);
            public static readonly EndpointTemplate CompleteLiaokuang = new EndpointTemplate("/device/{设备ID}/up/complete_liaokuang", qosLevel: 1);
            public static readonly EndpointTemplate Unloading = new EndpointTemplate("/device/{设备ID}/up/unloading", qosLevel: 1);
            public static readonly EndpointTemplate ConfirmUnloading = new EndpointTemplate("/device/{设备ID}/up/confirm_unloading", qosLevel: 1);
            public static readonly EndpointTemplate Formula = new EndpointTemplate("/device/{设备ID}/up/formula", qosLevel: 1);
            public static readonly EndpointTemplate DeviceState = new EndpointTemplate("/device/{设备ID}/up/device_state", qosLevel: 1);
            public static readonly EndpointTemplate Heartbeat = new EndpointTemplate("/device/{设备ID}/up/heartbeat", qosLevel: 1);
            public static readonly EndpointTemplate ScanProbecard = new EndpointTemplate("/device/{设备ID}/up/scan_probecard", qosLevel: 1);
        }

        /// <summary>
        /// 下行端点（MES -> 设备）
        /// </summary>
        public static class Down
        {
            public static readonly EndpointTemplate FeedingResponse = new EndpointTemplate("/device/{设备ID}/down/feeding_response/{设备编码}", qosLevel: 2);
            public static readonly EndpointTemplate ResponseModel = new EndpointTemplate("/device/{设备ID}/down/response_model/{设备编码}", qosLevel: 1);
            public static readonly EndpointTemplate UnloadingResponse = new EndpointTemplate("/device/{设备ID}/down/unloading_response/{设备编码}", qosLevel: 1);
            public static readonly EndpointTemplate ScanProbecardResponse = new EndpointTemplate("/device/{设备ID}/down/scan_probecard_response/{设备编码}", qosLevel: 1);
        }
    }
}
