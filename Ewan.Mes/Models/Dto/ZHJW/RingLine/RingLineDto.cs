/*****************************************************
** 命名空间: Ewan.Mes.Models.Dto.ZHJW.RingLine
** 文 件 名：RingLineDto
** 内容简述：环线DTO模型定义
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/15
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
using Newtonsoft.Json;

namespace Ewan.Mes.Models.Dto.ZHJW.RingLine
{
    /// <summary>
    /// 前料仓上料请求
    /// </summary>
    public class FeedingQianLiaocangRequest
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("billno_wip")]
        public string BillNoWip { get; set; }

        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 前料仓上料响应
    /// </summary>
    public class FeedingQianLiaocangResponse
    {
        [JsonProperty("billno_A")]
        public string BillNoA { get; set; }

        [JsonProperty("billno_B")]
        public string BillNoB { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 前料仓上料成功请求
    /// </summary>
    public class FeedingQianLiaocangSuccessRequest
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        [JsonProperty("feeding_liaokuang_code")]
        public string FeedingLiaokuangCode { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 前料仓卸料请求
    /// </summary>
    public class UnloadingQianLiaocangRequest
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        [JsonProperty("feeding_liaokuang_code")]
        public string FeedingLiaokuangCode { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 中料仓上料请求
    /// </summary>
    public class FeedingZhongLiaocangRequest
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        [JsonProperty("feeding_liaokuang_code")]
        public string FeedingLiaokuangCode { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 中料仓卸料请求
    /// </summary>
    public class UnloadingZhongLiaocangRequest
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        [JsonProperty("feeding_liaokuang_code")]
        public string FeedingLiaokuangCode { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 清洗烘干机上料请求
    /// </summary>
    public class FeedingQingxihongganjiRequest
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 后料仓上料请求
    /// </summary>
    public class FeedingHouLiaocangRequest
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        [JsonProperty("feeding_liaokuang_code")]
        public string FeedingLiaokuangCode { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 后料仓卸料请求
    /// </summary>
    public class UnloadingHouLiaocangRequest
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        [JsonProperty("feeding_liaokuang_code")]
        public string FeedingLiaokuangCode { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }
}
