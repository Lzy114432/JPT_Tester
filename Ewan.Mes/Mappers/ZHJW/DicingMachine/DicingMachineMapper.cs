/*****************************************************
** 命名空间: Ewan.Mes.Mappers.ZHJW.DicingMachine
** 文 件 名：DicingMachineMapper
** 内容简述：划片机数据映射器
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/15
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
using System;
using Ewan.Mes.Mappers;
using Ewan.Mes.Models.Domain.ZHJW.DicingMachine;
using Ewan.Mes.Models.Dto.ZHJW.DicingMachine;

namespace Ewan.Mes.Mappers.ZHJW.DicingMachine
{
    #region 上料

    public class FeedingMapper : IToDtoMapper<FeedingData, FeedingRequest>
    {
        public static readonly FeedingMapper Instance = new FeedingMapper();

        public FeedingRequest ToDto(FeedingData domain)
        {
            if (domain == null) return null;
            return new FeedingRequest
            {
                DeviceCode = domain.DeviceCode,
                BillNoWip = domain.BillNoWip,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    #endregion

    #region 扫描板片

    public class ScanPlateMapper : IToDtoMapper<ScanPlateData, ScanPlateRequest>
    {
        public static readonly ScanPlateMapper Instance = new ScanPlateMapper();

        public ScanPlateRequest ToDto(ScanPlateData domain)
        {
            if (domain == null) return null;
            return new ScanPlateRequest
            {
                DeviceCode = domain.DeviceCode,
                PlateCode = domain.PlateCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    #endregion

    #region 请求规格型号

    public class RequestModelMapper : IToDtoMapper<RequestModelData, RequestModel>
    {
        public static readonly RequestModelMapper Instance = new RequestModelMapper();

        public RequestModel ToDto(RequestModelData domain)
        {
            if (domain == null) return null;
            return new RequestModel
            {
                DeviceCode = domain.DeviceCode,
                BillNo = domain.BillNo,
                PlateCode = domain.PlateCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    public class ResponseModelMapper : IToDomainMapper<ResponseModelData, ResponseModel>
    {
        public static readonly ResponseModelMapper Instance = new ResponseModelMapper();

        public ResponseModelData ToDomain(ResponseModel dto)
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

    #endregion

    #region 扫描NG

    public class ScanNgMapper : IToDtoMapper<ScanNgData, ScanNgRequest>
    {
        public static readonly ScanNgMapper Instance = new ScanNgMapper();

        public ScanNgRequest ToDto(ScanNgData domain)
        {
            if (domain == null) return null;
            return new ScanNgRequest
            {
                DeviceCode = domain.DeviceCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    #endregion

    #region 报警

    public class AlarmMapper : IToDtoMapper<AlarmData, AlarmReport>
    {
        public static readonly AlarmMapper Instance = new AlarmMapper();

        public AlarmReport ToDto(AlarmData domain)
        {
            if (domain == null) return null;
            return new AlarmReport
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

    #endregion

    #region 报工数据

    public class ReportWorkMapper : IToDtoMapper<ReportWorkData, ReportData>
    {
        public static readonly ReportWorkMapper Instance = new ReportWorkMapper();

        public ReportData ToDto(ReportWorkData domain)
        {
            if (domain == null) return null;
            return new ReportData
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

    #endregion

    #region 配方

    public class FormulaMapper : IToDtoMapper<FormulaData, FormulaReport>
    {
        public static readonly FormulaMapper Instance = new FormulaMapper();

        public FormulaReport ToDto(FormulaData domain)
        {
            if (domain == null) return null;
            return new FormulaReport
            {
                DeviceCode = domain.DeviceCode,
                FormulaName = domain.FormulaName,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    #endregion

    #region 卸料

    public class UnloadingMapper : IToDtoMapper<UnloadingData, UnloadingRequest>
    {
        public static readonly UnloadingMapper Instance = new UnloadingMapper();

        public UnloadingRequest ToDto(UnloadingData domain)
        {
            if (domain == null) return null;
            return new UnloadingRequest
            {
                DeviceCode = domain.DeviceCode,
                PlateCode = domain.PlateCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    #endregion

    #region 设备状态

    public class DeviceStateMapper : IToDtoMapper<DeviceStateData, DeviceStateReport>
    {
        public static readonly DeviceStateMapper Instance = new DeviceStateMapper();

        public DeviceStateReport ToDto(DeviceStateData domain)
        {
            if (domain == null) return null;
            return new DeviceStateReport
            {
                DeviceCode = domain.DeviceCode,
                State = domain.State,
                FaultMessage = domain.FaultMessage,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    #endregion

    #region 心跳

    public class HeartbeatMapper : IToDtoMapper<HeartbeatData, HeartbeatReport>
    {
        public static readonly HeartbeatMapper Instance = new HeartbeatMapper();

        public HeartbeatReport ToDto(HeartbeatData domain)
        {
            if (domain == null) return null;
            return new HeartbeatReport
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
