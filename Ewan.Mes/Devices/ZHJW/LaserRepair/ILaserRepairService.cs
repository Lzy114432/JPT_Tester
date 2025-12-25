using System;
using System.Threading.Tasks;
using Ewan.Mes.Devices;
using Ewan.Mes.Transport;
using Ewan.Mes.Models.Domain.ZHJW.LaserRepair;

namespace Ewan.Mes.Devices.ZHJW.LaserRepair
{
    /// <summary>
    /// 镭修机服务接口（协议无关）
    /// 定义镭修机的业务行为，不依赖具体协议实现
    /// 使用 Domain 模型作为参数和返回值
    /// </summary>
    public interface ILaserRepairService : IDeviceService
    {
        // 上行请求 (Up) - 使用 Domain 模型
        ushort PublishFeeding(FeedingData payload);
        Task<ushort> PublishFeedingAsync(FeedingData payload);
        
        ushort PublishConfirmFeeding(ConfirmFeedingData payload);
        Task<ushort> PublishConfirmFeedingAsync(ConfirmFeedingData payload);
        
        ushort PublishScanNg(ScanNgData payload);
        Task<ushort> PublishScanNgAsync(ScanNgData payload);
        
        ushort PublishScanPlate(ScanPlateData payload);
        Task<ushort> PublishScanPlateAsync(ScanPlateData payload);
        
        ushort PublishRequestModel(RequestModelData payload);
        Task<ushort> PublishRequestModelAsync(RequestModelData payload);
        
        ushort PublishAlarm(AlarmData payload);
        Task<ushort> PublishAlarmAsync(AlarmData payload);
        
        ushort PublishReportData(ReportWorkData payload);
        Task<ushort> PublishReportDataAsync(ReportWorkData payload);
        
        ushort PublishCompleteLiaokuang(CompleteLiaokuangData payload);
        Task<ushort> PublishCompleteLiaokuangAsync(CompleteLiaokuangData payload);
        
        ushort PublishUnloading(UnloadingData payload);
        Task<ushort> PublishUnloadingAsync(UnloadingData payload);
        
        ushort PublishConfirmUnloading(ConfirmUnloadingData payload);
        Task<ushort> PublishConfirmUnloadingAsync(ConfirmUnloadingData payload);
        
        ushort PublishFormula(FormulaData payload);
        Task<ushort> PublishFormulaAsync(FormulaData payload);
        
        ushort PublishDeviceState(DeviceStateData payload);
        Task<ushort> PublishDeviceStateAsync(DeviceStateData payload);
        
        ushort PublishHeartbeat(HeartbeatData payload);
        Task<ushort> PublishHeartbeatAsync(HeartbeatData payload);
        
        ushort PublishScanProbecard(ScanProbecardData payload);
        Task<ushort> PublishScanProbecardAsync(ScanProbecardData payload);

        // 下行响应监听 (Down) - 使用 Domain 模型
        IDisposable OnFeedingResponse(Action<MessageContext<FeedingResponseData>> handler);
        IDisposable OnResponseModel(Action<MessageContext<ResponseModelData>> handler);
        IDisposable OnUnloadingResponse(Action<MessageContext<UnloadingResponseData>> handler);
        IDisposable OnScanProbecardResponse(Action<MessageContext<ScanProbecardResponseData>> handler);
    }
}
