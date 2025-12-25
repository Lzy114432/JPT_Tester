/*****************************************************
** 命名空间: Ewan.Mes.Models.Dto.LaserRepair
** 文 件 名：LaserRepairDto
** 内容简述：激光修复 MQTT 数据传输对象（DTO）
** 版    本：V2.0
** 创 建 人：Ewan
** 创建日期：2025/12/3 17:59:41
** 修改记录：
日期        版本      修改人    修改内容
2025/12/11  V2.0      Ewan      重构为DTO层，与Domain层分离
*****************************************************/
using Newtonsoft.Json;

namespace Ewan.Mes.Models.Dto.ZHJW.LaserRepair
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
    /// 上料响应
    /// </summary>
    public class FeedingResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 确认上料请求
    /// </summary>
    public class ConfirmFeedingRequest
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("result")]
        public int Result { get; set; }

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

        [JsonProperty("billno_wip")]
        public string BillNoWip { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 扫描板号请求
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
    /// 请求机型
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
    /// 响应机型
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

        [JsonProperty("probecard_code")]
        public string ProbecardCode { get; set; }

        [JsonProperty("size")]
        public string Size { get; set; }

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

        [JsonProperty("good_qty")]
        public int GoodQty { get; set; }

        [JsonProperty("high_qty")]
        public int HighQty { get; set; }

        [JsonProperty("low_qty")]
        public int LowQty { get; set; }

        [JsonProperty("open_qty")]
        public int OpenQty { get; set; }

        [JsonProperty("total_qty")]
        public int TotalQty { get; set; }

        [JsonProperty("nominal")]
        public double Nominal { get; set; }

        [JsonProperty("probecard_code")]
        public string ProbecardCode { get; set; }

        [JsonProperty("probecard_count")]
        public int ProbecardCount { get; set; }

        [JsonProperty("size")]
        public string Size { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 完成料框
    /// </summary>
    public class CompleteLiaokuang
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
    /// 下料请求
    /// </summary>
    public class UnloadingRequest
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("billno")]
        public string BillNo { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 下料响应
    /// </summary>
    public class UnloadingResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 确认下料请求
    /// </summary>
    public class ConfirmUnloadingRequest
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

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 扫描探针卡请求
    /// </summary>
    public class ScanProbecardRequest
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("probecard_code")]
        public string ProbecardCode { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 扫描探针卡响应
    /// </summary>
    public class ScanProbecardResponse
    {
        [JsonProperty("probecard_code")]
        public string ProbecardCode { get; set; }

        [JsonProperty("size")]
        public string Size { get; set; }

        [JsonProperty("probecard_remaining_count")]
        public string ProbecardRemainingCount { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }
}
