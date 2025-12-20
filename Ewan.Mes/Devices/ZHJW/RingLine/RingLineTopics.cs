/*****************************************************
** 命名空间: Ewan.Mes.Devices.ZHJW.RingLine
** 文 件 名：RingLineTopics
** 内容简述：环线端点定义
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/15
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
using Ewan.Mes.Transport;

namespace Ewan.Mes.Devices.ZHJW.RingLine
{
    /// <summary>
    /// 环线端点定义（协议无关）
    /// </summary>
    public static class RingLineTopics
    {
        /// <summary>
        /// 上行端点（设备 -> MES）
        /// </summary>
        public static class Up
        {
            /// <summary>
            /// 前料仓上料
            /// </summary>
            public static readonly EndpointTemplate FeedingQianLiaocang = new EndpointTemplate("/device/{设备ID}/up/feeding_qian_liaocang", qosLevel: 2);

            /// <summary>
            /// 前料仓上料成功
            /// </summary>
            public static readonly EndpointTemplate FeedingQianLiaocangSuccess = new EndpointTemplate("/device/{设备ID}/up/feeding_qian_liaocang_success", qosLevel: 2);

            /// <summary>
            /// 前料仓卸料
            /// </summary>
            public static readonly EndpointTemplate UnloadingQianLiaocang = new EndpointTemplate("/device/{设备ID}/up/unloading_qian_liaocang", qosLevel: 2);

            /// <summary>
            /// 中料仓上料
            /// </summary>
            public static readonly EndpointTemplate FeedingZhongLiaocang = new EndpointTemplate("/device/{设备ID}/up/feeding_zhong_liaocang", qosLevel: 2);

            /// <summary>
            /// 中料仓卸料
            /// </summary>
            public static readonly EndpointTemplate UnloadingZhongLiaocang = new EndpointTemplate("/device/{设备ID}/up/unloading_zhong_liaocang", qosLevel: 2);

            /// <summary>
            /// 清洗烘干机上料
            /// </summary>
            public static readonly EndpointTemplate FeedingQingxihongganji = new EndpointTemplate("/device/{设备ID}/up/feeding_qingxihongganji", qosLevel: 2);

            /// <summary>
            /// 后料仓上料
            /// </summary>
            public static readonly EndpointTemplate FeedingHouLiaocang = new EndpointTemplate("/device/{设备ID}/up/feeding_hou_liaocang", qosLevel: 2);

            /// <summary>
            /// 后料仓卸料
            /// </summary>
            public static readonly EndpointTemplate UnloadingHouLiaocang = new EndpointTemplate("/device/{设备ID}/up/unloading_hou_liaocang", qosLevel: 2);
        }

        /// <summary>
        /// 下行端点（MES -> 设备）
        /// </summary>
        public static class Down
        {
            /// <summary>
            /// 前料仓上料响应
            /// </summary>
            public static readonly EndpointTemplate FeedingQianLiaocangResponse = new EndpointTemplate("/device/{设备ID}/down/feeding_qian_liaocang_response/{设备编码}", qosLevel: 2);
        }
    }
}
