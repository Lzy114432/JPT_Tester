using System;
using Ewan.Mes.Mappers;
using Ewan.Mes.Models.Domain.ZHJW.LaserCutting;
using Ewan.Mes.Models.Dto.ZHJW.LaserCutting;

namespace Ewan.Mes.Mappers.ZHJW.LaserCutting
{
    #region Mapper 实现类

    /// <summary>
    /// 扫描板片映射器
    /// </summary>
    public class ScanPlateMapper : IToDtoMapper<ScanPlateData, CuttingScanPlateRequest>
    {
        public static readonly ScanPlateMapper Instance = new ScanPlateMapper();

        public CuttingScanPlateRequest ToDto(ScanPlateData domain)
        {
            if (domain == null) return null;
            return new CuttingScanPlateRequest
            {
                DeviceCode = domain.DeviceCode,
                PlateCode = domain.PlateCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    /// <summary>
    /// 请求规格型号映射器
    /// </summary>
    public class RequestModelMapper : IToDtoMapper<RequestModelData, CuttingRequestModel>
    {
        public static readonly RequestModelMapper Instance = new RequestModelMapper();

        public CuttingRequestModel ToDto(RequestModelData domain)
        {
            if (domain == null) return null;
            return new CuttingRequestModel
            {
                DeviceCode = domain.DeviceCode,
                BillNo = domain.BillNo,
                PlateCode = domain.PlateCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    /// <summary>
    /// 规格型号响应映射器
    /// </summary>
    public class ResponseModelMapper : IToDomainMapper<ResponseModelData, CuttingResponseModel>
    {
        public static readonly ResponseModelMapper Instance = new ResponseModelMapper();

        public ResponseModelData ToDomain(CuttingResponseModel dto)
        {
            if (dto == null) return null;
            return new ResponseModelData
            {
                BillNo = dto.BillNo,
                Model = dto.Model,
                Timestamp = DateTime.TryParse(dto.Timestamp, out var dt) ? dt : DateTime.Now
            };
        }
    }

    /// <summary>
    /// 扫描NG映射器
    /// </summary>
    public class ScanNgMapper : IToDtoMapper<ScanNgData, CuttingScanNgRequest>
    {
        public static readonly ScanNgMapper Instance = new ScanNgMapper();

        public CuttingScanNgRequest ToDto(ScanNgData domain)
        {
            if (domain == null) return null;
            return new CuttingScanNgRequest
            {
                DeviceCode = domain.DeviceCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    /// <summary>
    /// 报警映射器
    /// </summary>
    public class AlarmMapper : IToDtoMapper<AlarmData, CuttingAlarmReport>
    {
        public static readonly AlarmMapper Instance = new AlarmMapper();

        public CuttingAlarmReport ToDto(AlarmData domain)
        {
            if (domain == null) return null;
            return new CuttingAlarmReport
            {
                DeviceCode = domain.DeviceCode,
                BillNo = domain.BillNo,
                PlateCode = domain.PlateCode,
                Model = domain.Model,
                FormulaName = domain.FormulaName,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    /// <summary>
    /// 报工数据映射器
    /// </summary>
    public class ReportWorkMapper : IToDtoMapper<ReportWorkData, CuttingReportData>
    {
        public static readonly ReportWorkMapper Instance = new ReportWorkMapper();

        public CuttingReportData ToDto(ReportWorkData domain)
        {
            if (domain == null) return null;
            return new CuttingReportData
            {
                DeviceCode = domain.DeviceCode,
                BillNo = domain.BillNo,
                Model = domain.Model,
                FormulaName = domain.FormulaName,
                PlateCode = domain.PlateCode,
                Result = domain.Result,
                NgReason = domain.NgReason,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    /// <summary>
    /// 配方映射器
    /// </summary>
    public class FormulaMapper : IToDtoMapper<FormulaData, CuttingFormulaReport>
    {
        public static readonly FormulaMapper Instance = new FormulaMapper();

        public CuttingFormulaReport ToDto(FormulaData domain)
        {
            if (domain == null) return null;
            return new CuttingFormulaReport
            {
                DeviceCode = domain.DeviceCode,
                FormulaName = domain.FormulaName,
                FormulaData = domain.FormulaContent,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    /// <summary>
    /// 设备状态映射器
    /// </summary>
    public class DeviceStateMapper : IToDtoMapper<DeviceStateData, CuttingDeviceStateReport>
    {
        public static readonly DeviceStateMapper Instance = new DeviceStateMapper();

        public CuttingDeviceStateReport ToDto(DeviceStateData domain)
        {
            if (domain == null) return null;
            return new CuttingDeviceStateReport
            {
                DeviceCode = domain.DeviceCode,
                State = domain.State,
                FaultMessage = domain.FaultMessage,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    /// <summary>
    /// 心跳映射器
    /// </summary>
    public class HeartbeatMapper : IToDtoMapper<HeartbeatData, CuttingHeartbeatReport>
    {
        public static readonly HeartbeatMapper Instance = new HeartbeatMapper();

        public CuttingHeartbeatReport ToDto(HeartbeatData domain)
        {
            if (domain == null) return null;
            return new CuttingHeartbeatReport
            {
                DeviceCode = domain.DeviceCode,
                State = domain.State,
                FaultMessage = domain.FaultMessage,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    #endregion
}
