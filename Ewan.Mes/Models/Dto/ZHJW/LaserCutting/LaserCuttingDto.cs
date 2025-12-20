/*****************************************************
** 命名空间: Ewan.Mes.Models.Dto.LaserCutting
** 文 件 名：LaserCuttingDto
** 内容简述：激光切割机 MQTT 数据传输对象（DTO）
** 版    本：V2.0
** 创 建 人：Ewan
** 创建日期：2025/12/11
** 修改记录：
日期        版本      修改人    修改内容
2025/12/11  V2.0      Ewan      重构为DTO层，与Domain层分离
*****************************************************/
using Newtonsoft.Json;

namespace Ewan.Mes.Models.Dto.ZHJW.LaserCutting
{
    #region 上行消息 (Up)

    /// <summary>
    /// 扫描板片请求
    /// </summary>
    public class CuttingScanPlateRequest
    {
        /// <summary>
        /// 设备编码
        /// </summary>
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        /// <summary>
        /// 板片二维码
        /// </summary>
        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        /// <summary>
        /// 时间（格式：2025-11-05 15:32:00.000）
        /// </summary>
        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 请求规格型号
    /// </summary>
    public class CuttingRequestModel
    {
        /// <summary>
        /// 设备编码
        /// </summary>
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        /// <summary>
        /// 工单号
        /// </summary>
        [JsonProperty("billno")]
        public string BillNo { get; set; }

        /// <summary>
        /// 板片二维码
        /// </summary>
        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        /// <summary>
        /// 时间（格式：2025-11-05 15:32:00.000）
        /// </summary>
        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 扫描NG请求
    /// </summary>
    public class CuttingScanNgRequest
    {
        /// <summary>
        /// 设备编码
        /// </summary>
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        /// <summary>
        /// 时间（格式：2025-11-05 15:32:00.000）
        /// </summary>
        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 报警上报
    /// </summary>
    public class CuttingAlarmReport
    {
        /// <summary>
        /// 设备编码
        /// </summary>
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        /// <summary>
        /// 工单号
        /// </summary>
        [JsonProperty("billno")]
        public string BillNo { get; set; }

        /// <summary>
        /// 板片二维码
        /// </summary>
        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        /// <summary>
        /// 规格型号
        /// </summary>
        [JsonProperty("model")]
        public string Model { get; set; }

        /// <summary>
        /// 配方名
        /// </summary>
        [JsonProperty("formula_name")]
        public string FormulaName { get; set; }

        /// <summary>
        /// 时间（格式：2025-11-05 15:32:00.000）
        /// </summary>
        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 上报生产数据
    /// </summary>
    public class CuttingReportData
    {
        /// <summary>
        /// 设备编码
        /// </summary>
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        /// <summary>
        /// 工单号
        /// </summary>
        [JsonProperty("billno")]
        public string BillNo { get; set; }

        /// <summary>
        /// 规格型号
        /// </summary>
        [JsonProperty("model")]
        public string Model { get; set; }

        /// <summary>
        /// 配方名
        /// </summary>
        [JsonProperty("formula_name")]
        public string FormulaName { get; set; }

        /// <summary>
        /// 板片二维码
        /// </summary>
        [JsonProperty("plate_code")]
        public string PlateCode { get; set; }

        /// <summary>
        /// 生产结果（1：OK，0：NG）
        /// </summary>
        [JsonProperty("result")]
        public int Result { get; set; }

        /// <summary>
        /// NG原因
        /// </summary>
        [JsonProperty("ng_reason")]
        public string NgReason { get; set; }

        /// <summary>
        /// 时间（格式：2025-11-05 15:32:00.000）
        /// </summary>
        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 配方上报
    /// </summary>
    public class CuttingFormulaReport
    {
        /// <summary>
        /// 设备编码
        /// </summary>
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        /// <summary>
        /// 配方名
        /// </summary>
        [JsonProperty("formula_name")]
        public string FormulaName { get; set; }

        /// <summary>
        /// 配方内容json
        /// </summary>
        [JsonProperty("formula_data")]
        public object FormulaData { get; set; }

        /// <summary>
        /// 时间（格式：2025-11-05 15:32:00.000）
        /// </summary>
        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 设备状态上报
    /// </summary>
    public class CuttingDeviceStateReport
    {
        /// <summary>
        /// 设备编码
        /// </summary>
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        /// <summary>
        /// 设备状态：1代表运行，2代表故障，3代表待机，4代表停机
        /// </summary>
        [JsonProperty("state")]
        public int State { get; set; }

        /// <summary>
        /// 异常信息，若存在多个异常时，使用英文下划线分隔符
        /// </summary>
        [JsonProperty("fault_message")]
        public string FaultMessage { get; set; }

        /// <summary>
        /// 时间（格式：2025-11-05 15:32:00.000）
        /// </summary>
        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// 心跳上报
    /// </summary>
    public class CuttingHeartbeatReport
    {
        /// <summary>
        /// 设备编码
        /// </summary>
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        /// <summary>
        /// 设备状态：1代表运行，2代表故障，3代表待机，4代表停机
        /// </summary>
        [JsonProperty("state")]
        public int State { get; set; }

        /// <summary>
        /// 异常信息
        /// </summary>
        [JsonProperty("fault_message")]
        public string FaultMessage { get; set; }

        /// <summary>
        /// 时间（格式：2025-11-05 15:32:00.000）
        /// </summary>
        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    #endregion

    #region 下行消息 (Down)

    /// <summary>
    /// 返回规格型号
    /// </summary>
    public class CuttingResponseModel
    {
        /// <summary>
        /// 工单号
        /// </summary>
        [JsonProperty("billno")]
        public string BillNo { get; set; }

        /// <summary>
        /// 规格型号
        /// </summary>
        [JsonProperty("model")]
        public string Model { get; set; }

        /// <summary>
        /// 时间（格式：2025-11-05 15:32:00.000）
        /// </summary>
        [JsonProperty("t")]
        public string Timestamp { get; set; }
    }

    #endregion
}
