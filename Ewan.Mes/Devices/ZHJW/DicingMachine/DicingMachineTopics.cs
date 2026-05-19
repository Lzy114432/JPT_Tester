/*****************************************************
** 命名空间: Ewan.Mes.Devices.ZHJW.DicingMachine
** 文 件 名：DicingMachineTopics
** 内容简述：划片机端点定义
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/15
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
using Ewan.Mes.Transport;

namespace Ewan.Mes.Devices.ZHJW.DicingMachine
{
    /// <summary>
    /// 划片机端点定义（协议无关）
    /// </summary>
    public static class DicingMachineTopics
    {
        /// <summary>
        /// 上行端点（设备 -> MES）
        /// </summary>
        public static class Up
        {
            /// <summary>
            /// 上料
            /// </summary>
            public static readonly EndpointTemplate Feeding = new EndpointTemplate("/device/{设备ID}/up/feeding", qosLevel: 2);

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
            public static readonly EndpointTemplate Alarm = new EndpointTemplate("/device/{设备ID}/up/alarm", qosLevel: 1);

            /// <summary>
            /// 报工数据
            /// </summary>
            public static readonly EndpointTemplate ReportData = new EndpointTemplate("/device/{设备ID}/up/report_data", qosLevel: 1);

            /// <summary>
            /// 配方
            /// </summary>
            public static readonly EndpointTemplate Formula = new EndpointTemplate("/device/{设备ID}/up/formula", qosLevel: 1);

            /// <summary>
            /// 卸料
            /// </summary>
            public static readonly EndpointTemplate Unloading = new EndpointTemplate("/device/{设备ID}/up/unloading", qosLevel: 1);

            /// <summary>
            /// 设备状态
            /// </summary>
            public static readonly EndpointTemplate DeviceState = new EndpointTemplate("/device/{设备ID}/up/device_state", qosLevel: 1);

            /// <summary>
            /// 心跳
            /// </summary>
            public static readonly EndpointTemplate Heartbeat = new EndpointTemplate("/device/{设备ID}/up/heartbeat", qosLevel: 1);
        }

        /// <summary>
        /// 下行端点（MES -> 设备）
        /// </summary>
        public static class Down
        {
            /// <summary>
            /// 规格型号响应
            /// </summary>
            public static readonly EndpointTemplate ResponseModel = new EndpointTemplate("/device/{设备ID}/down/response_model/{设备编码}", qosLevel: 2);
            //public static readonly EndpointTemplate FeedingUnloadingStateResponse = new EndpointTemplate("/device/{设备ID}/down/feeding_unloading_state_response/{设备编码}", qosLevel: 2);

        }
    }
}
