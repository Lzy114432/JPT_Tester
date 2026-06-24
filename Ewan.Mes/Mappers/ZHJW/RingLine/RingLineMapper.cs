/*****************************************************
** 命名空间: Ewan.Mes.Mappers.ZHJW.RingLine
** 文 件 名：RingLineMapper
** 内容简述：环线数据映射器
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/15
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
using Ewan.Mes.Mappers;
using Ewan.Mes.Models.Domain.ZHJW.DicingMachine;
using Ewan.Mes.Models.Domain.ZHJW.RingLine;
using Ewan.Mes.Models.Dto.ZHJW.DicingMachine;
using Ewan.Mes.Models.Dto.ZHJW.RingLine;
using Newtonsoft.Json;
using System;

namespace Ewan.Mes.Mappers.ZHJW.RingLine
{
    #region 前料仓上料

    public class FeedingQianLiaocangMapper : IToDtoMapper<FeedingQianLiaocangData, FeedingQianLiaocangRequest>
    {
        public static readonly FeedingQianLiaocangMapper Instance = new FeedingQianLiaocangMapper();

        public FeedingQianLiaocangRequest ToDto(FeedingQianLiaocangData domain)
        {
            if (domain == null) return null;
            return new FeedingQianLiaocangRequest
            {
                DeviceCode = domain.DeviceCode,
                BillNoWip = domain.BillNoWip,
                PlateCode = domain.PlateCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    public class FeedingQianLiaocangResponseMapper : IToDomainMapper<FeedingQianLiaocangResponseData, FeedingQianLiaocangResponse>
    {
        public static readonly FeedingQianLiaocangResponseMapper Instance = new FeedingQianLiaocangResponseMapper();

        public FeedingQianLiaocangResponseData ToDomain(FeedingQianLiaocangResponse dto)
        {
            if (dto == null) return null;
            return new FeedingQianLiaocangResponseData
            {
                BillNoA = dto.BillNoA,
                BillNoB = dto.BillNoB,
                Success = dto.Success,
                Message = dto.Message,
                Timestamp = DateTime.TryParse(dto.Timestamp, out var dt) ? dt : DateTime.Now,
                SizeA = dto.SizeA,
                MetalA = dto.MetalA,
                PowerA = dto.PowerA,
                ResisA = dto.ResisA,
                SuffixA = dto.SuffixA,
                SizeB = dto.SizeB,
                MetalB = dto.MetalB,
                PowerB = dto.PowerB,
                ResisB = dto.ResisB,
                SuffixB = dto.SuffixB




            };
        }
    }

    #endregion

    #region 前料仓上料成功

    public class FeedingQianLiaocangSuccessMapper : IToDtoMapper<FeedingQianLiaocangSuccessData, FeedingQianLiaocangSuccessRequest>
    {
        public static readonly FeedingQianLiaocangSuccessMapper Instance = new FeedingQianLiaocangSuccessMapper();

        public FeedingQianLiaocangSuccessRequest ToDto(FeedingQianLiaocangSuccessData domain)
        {
            if (domain == null) return null;
            return new FeedingQianLiaocangSuccessRequest
            {
                DeviceCode = domain.DeviceCode,
                PlateCode = domain.PlateCode,
                FeedingLiaokuangCode = domain.FeedingLiaokuangCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    #endregion

    #region 前料仓卸料

    public class UnloadingQianLiaocangMapper : IToDtoMapper<UnloadingQianLiaocangData, UnloadingQianLiaocangRequest>
    {
        public static readonly UnloadingQianLiaocangMapper Instance = new UnloadingQianLiaocangMapper();

        public UnloadingQianLiaocangRequest ToDto(UnloadingQianLiaocangData domain)
        {
            if (domain == null) return null;
            return new UnloadingQianLiaocangRequest
            {
                DeviceCode = domain.DeviceCode,
                PlateCode = domain.PlateCode,
                FeedingLiaokuangCode = domain.FeedingLiaokuangCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    #endregion

    #region 中料仓上料

    public class FeedingZhongLiaocangMapper : IToDtoMapper<FeedingZhongLiaocangData, FeedingZhongLiaocangRequest>
    {
        public static readonly FeedingZhongLiaocangMapper Instance = new FeedingZhongLiaocangMapper();

        public FeedingZhongLiaocangRequest ToDto(FeedingZhongLiaocangData domain)
        {
            if (domain == null) return null;
            return new FeedingZhongLiaocangRequest
            {
                DeviceCode = domain.DeviceCode,
                PlateCode = domain.PlateCode,
                FeedingLiaokuangCode = domain.FeedingLiaokuangCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    #endregion

    #region 中料仓卸料

    public class UnloadingZhongLiaocangMapper : IToDtoMapper<UnloadingZhongLiaocangData, UnloadingZhongLiaocangRequest>
    {
        public static readonly UnloadingZhongLiaocangMapper Instance = new UnloadingZhongLiaocangMapper();

        public UnloadingZhongLiaocangRequest ToDto(UnloadingZhongLiaocangData domain)
        {
            if (domain == null) return null;
            return new UnloadingZhongLiaocangRequest
            {
                DeviceCode = domain.DeviceCode,
                PlateCode = domain.PlateCode,
                FeedingLiaokuangCode = domain.FeedingLiaokuangCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    #endregion

    #region 清洗烘干机上料

    public class FeedingQingxihongganjiMapper : IToDtoMapper<FeedingQingxihongganjiData, FeedingQingxihongganjiRequest>
    {
        public static readonly FeedingQingxihongganjiMapper Instance = new FeedingQingxihongganjiMapper();

        public FeedingQingxihongganjiRequest ToDto(FeedingQingxihongganjiData domain)
        {
            if (domain == null) return null;
            return new FeedingQingxihongganjiRequest
            {
                DeviceCode = domain.DeviceCode,
                PlateCode = domain.PlateCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    #endregion

    #region 后料仓上料

    public class FeedingHouLiaocangMapper : IToDtoMapper<FeedingHouLiaocangData, FeedingHouLiaocangRequest>
    {
        public static readonly FeedingHouLiaocangMapper Instance = new FeedingHouLiaocangMapper();

        public FeedingHouLiaocangRequest ToDto(FeedingHouLiaocangData domain)
        {
            if (domain == null) return null;
            return new FeedingHouLiaocangRequest
            {
                DeviceCode = domain.DeviceCode,
                PlateCode = domain.PlateCode,
                FeedingLiaokuangCode = domain.FeedingLiaokuangCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    #endregion

    #region 后料仓卸料
    public class FeedingUnloadingStateResponseModelMapper : IToDomainMapper<FeedingUnloadingStateResponseData, FeedingUnloadingStateResponseModel>
    {
        public static readonly FeedingUnloadingStateResponseModelMapper Instance = new FeedingUnloadingStateResponseModelMapper();

        public FeedingUnloadingStateResponseData ToDomain(FeedingUnloadingStateResponseModel dto)
        {
            if (dto == null) return null;
            return new FeedingUnloadingStateResponseData
            {
                Device_Code = dto.DeviceCode,
                IsFeeding = dto.IsFeeding,
                IsRunning = dto.IsRunning,
                IsUnloading = dto.IsUnloading,
                Timestamp = DateTime.TryParse(dto.Timestamp, out var dt) ? dt : DateTime.Now
            };
        }
    }

    public class UnloadingHouLiaocangMapper : IToDtoMapper<UnloadingHouLiaocangData, UnloadingHouLiaocangRequest>
    {
        public static readonly UnloadingHouLiaocangMapper Instance = new UnloadingHouLiaocangMapper();

        public UnloadingHouLiaocangRequest ToDto(UnloadingHouLiaocangData domain)
        {
            if (domain == null) return null;
            return new UnloadingHouLiaocangRequest
            {
                DeviceCode = domain.DeviceCode,
                PlateCode = domain.PlateCode,
                FeedingLiaokuangCode = domain.FeedingLiaokuangCode,
                Timestamp = domain.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    #endregion
}
