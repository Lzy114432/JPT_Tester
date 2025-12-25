using System;
using Ewan.Mes.Mappers;
using Ewan.Mes.Models.Domain.ZHJW.LaserRepair;
using Ewan.Mes.Models.Dto.ZHJW.LaserRepair;

namespace Ewan.Mes.Mappers.ZHJW.LaserRepair
{
    #region Mapper 实现类

    public class FeedingMapper : IMapper<FeedingData, FeedingRequest>
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

        public FeedingData ToDomain(FeedingRequest dto)
        {
            if (dto == null) return null;
            return new FeedingData
            {
                DeviceCode = dto.DeviceCode,
                BillNoWip = dto.BillNoWip,
                Timestamp = DateTime.TryParse(dto.Timestamp, out var dt) ? dt : DateTime.Now
            };
        }
    }

    public class FeedingResponseMapper : IToDomainMapper<FeedingResponseData, FeedingResponse>
    {
        public static readonly FeedingResponseMapper Instance = new FeedingResponseMapper();

        public FeedingResponseData ToDomain(FeedingResponse dto)
        {
            if (dto == null) return null;
            return new FeedingResponseData
            {
                Success = dto.Success,
                Message = dto.Message,
                Timestamp = DateTime.TryParse(dto.Timestamp, out var dt) ? dt : DateTime.Now
            };
        }
    }

    public class ConfirmFeedingMapper : IToDtoMapper<ConfirmFeedingData, ConfirmFeedingRequest>
    {
        public static readonly ConfirmFeedingMapper Instance = new ConfirmFeedingMapper();

        public ConfirmFeedingRequest ToDto(ConfirmFeedingData domain)
        {
            if (domain == null) return null;
            return new ConfirmFeedingRequest
            {
                DeviceCode = domain.DeviceCode,
                Result = domain.Result,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    public class ScanNgMapper : IToDtoMapper<ScanNgData, ScanNgRequest>
    {
        public static readonly ScanNgMapper Instance = new ScanNgMapper();

        public ScanNgRequest ToDto(ScanNgData domain)
        {
            if (domain == null) return null;
            return new ScanNgRequest
            {
                DeviceCode = domain.DeviceCode,
                BillNoWip = domain.BillNoWip,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

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
                ProbecardCode = domain.ProbecardCode,
                Size = domain.Size,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

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
                ProbecardCode = domain.ProbecardCode,
                Size = domain.Size,
                Result = domain.Result,
                NgReason = domain.NgReason,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    public class UnloadingMapper : IToDtoMapper<UnloadingData, UnloadingRequest>
    {
        public static readonly UnloadingMapper Instance = new UnloadingMapper();

        public UnloadingRequest ToDto(UnloadingData domain)
        {
            if (domain == null) return null;
            return new UnloadingRequest
            {
                DeviceCode = domain.DeviceCode,
                BillNo = domain.BillNoWip,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    public class UnloadingResponseMapper : IToDomainMapper<UnloadingResponseData, UnloadingResponse>
    {
        public static readonly UnloadingResponseMapper Instance = new UnloadingResponseMapper();

        public UnloadingResponseData ToDomain(UnloadingResponse dto)
        {
            if (dto == null) return null;
            return new UnloadingResponseData
            {
                Success = dto.Success,
                Message = dto.Message,
                Timestamp = DateTime.TryParse(dto.Timestamp, out var dt) ? dt : DateTime.Now
            };
        }
    }

    public class ConfirmUnloadingMapper : IToDtoMapper<ConfirmUnloadingData, ConfirmUnloadingRequest>
    {
        public static readonly ConfirmUnloadingMapper Instance = new ConfirmUnloadingMapper();

        public ConfirmUnloadingRequest ToDto(ConfirmUnloadingData domain)
        {
            if (domain == null) return null;
            return new ConfirmUnloadingRequest
            {
                DeviceCode = domain.DeviceCode,
                BillNo = domain.BillNo,
                PlateCode = domain.PlateCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

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
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    public class ScanProbecardMapper : IToDtoMapper<ScanProbecardData, ScanProbecardRequest>
    {
        public static readonly ScanProbecardMapper Instance = new ScanProbecardMapper();

        public ScanProbecardRequest ToDto(ScanProbecardData domain)
        {
            if (domain == null) return null;
            return new ScanProbecardRequest
            {
                DeviceCode = domain.DeviceCode,
                ProbecardCode = domain.ProbecardCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    public class ScanProbecardResponseMapper : IToDomainMapper<ScanProbecardResponseData, ScanProbecardResponse>
    {
        public static readonly ScanProbecardResponseMapper Instance = new ScanProbecardResponseMapper();

        public ScanProbecardResponseData ToDomain(ScanProbecardResponse dto)
        {
            if (dto == null) return null;
            return new ScanProbecardResponseData
            {
                ProbecardCode = dto.ProbecardCode,
                Size = dto.Size,
                ProbecardRemainingCount = dto.ProbecardRemainingCount,
                Success = dto.Success,
                Message = dto.Message,
                Timestamp = DateTime.TryParse(dto.Timestamp, out var dt) ? dt : DateTime.Now
            };
        }
    }

    public class CompleteLiaokuangMapper : IToDtoMapper<CompleteLiaokuangData, CompleteLiaokuang>
    {
        public static readonly CompleteLiaokuangMapper Instance = new CompleteLiaokuangMapper();

        public CompleteLiaokuang ToDto(CompleteLiaokuangData domain)
        {
            if (domain == null) return null;
            return new CompleteLiaokuang
            {
                DeviceCode = domain.DeviceCode,
                BillNo = domain.BillNo,
                PlateCode = domain.PlateCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    #endregion
}
