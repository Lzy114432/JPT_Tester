/*****************************************************
** 命名空间: Ewan.Mes.Models.Dto.ZHJW.DicingMachine
** 文 件 名：DicingMachineDto
** 内容简述：划片机DTO模型定义
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/15
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
using Newtonsoft.Json;

namespace Ewan.Mes.Models.Dto.ZHJW.DicingMachine
{
    /// <summary>
    /// 上料请求
    /// </summary>
    public class FeedingRequest
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("billno_wip")]
        public string BillNoWip { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 扫描板片请求
    /// </summary>
    public class ScanPlateRequest
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 请求规格型号
    /// </summary>
    public class RequestModel
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("billno")]
        public string BillNo { get; set; }

        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 规格型号响应
    /// </summary>
    public class ResponseModel
    {
        [JsonProperty("billno")]
        public string BillNo { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 扫描NG请求
    /// </summary>
    public class ScanNgRequest
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 报警上报
    /// </summary>
    public class AlarmReport
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("billno")]
        public string BillNo { get; set; }

        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("formula_name")]
        public string FormulaName { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 报工数据
    /// </summary>
    public class ReportData
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("billno")]
        public string BillNo { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("formula_name")]
        public string FormulaName { get; set; }

        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        [JsonProperty("result")]
        public int Result { get; set; }

        [JsonProperty("ng_reason")]
        public string NgReason { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 配方上报
    /// </summary>
    public class FormulaReport
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("formula_name")]
        public string FormulaName { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 卸料请求
    /// </summary>
    public class UnloadingRequest
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 设备状态上报
    /// </summary>
    public class DeviceStateReport
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("state")]
        public int State { get; set; }

        [JsonProperty("fault_message")]
        public string FaultMessage { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 心跳上报
    /// </summary>
    public class HeartbeatReport
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("state")]
        public int State { get; set; }

        [JsonProperty("fault_message")]
        public string FaultMessage { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }
}
