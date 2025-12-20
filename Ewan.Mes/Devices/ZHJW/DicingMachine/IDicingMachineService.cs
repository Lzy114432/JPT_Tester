/*****************************************************
** 命名空间: Ewan.Mes.Devices.ZHJW.DicingMachine
** 文 件 名：IDicingMachineService
** 内容简述：划片机服务接口
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/15
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
using System;
using System.Threading.Tasks;
using Ewan.Mes.Models.Domain.ZHJW.DicingMachine;
using Ewan.Mes.Transport;

namespace Ewan.Mes.Devices.ZHJW.DicingMachine
{
    /// <summary>
    /// 划片机服务接口
    /// </summary>
    public interface IDicingMachineService : IDeviceService
    {
        #region 上行请求

        /// <summary>
        /// 上料
        /// </summary>
        ushort PublishFeeding(FeedingData payload);
        Task<ushort> PublishFeedingAsync(FeedingData payload);

        /// <summary>
        /// 扫描板片
        /// </summary>
        ushort PublishScanPlate(ScanPlateData payload);
        Task<ushort> PublishScanPlateAsync(ScanPlateData payload);

        /// <summary>
        /// 请求规格型号
        /// </summary>
        ushort PublishRequestModel(RequestModelData payload);
        Task<ushort> PublishRequestModelAsync(RequestModelData payload);

        /// <summary>
        /// 扫描NG
        /// </summary>
        ushort PublishScanNg(ScanNgData payload);
        Task<ushort> PublishScanNgAsync(ScanNgData payload);

        /// <summary>
        /// 报警
        /// </summary>
        ushort PublishAlarm(AlarmData payload);
        Task<ushort> PublishAlarmAsync(AlarmData payload);

        /// <summary>
        /// 报工数据
        /// </summary>
        ushort PublishReportData(ReportWorkData payload);
        Task<ushort> PublishReportDataAsync(ReportWorkData payload);

        /// <summary>
        /// 配方
        /// </summary>
        ushort PublishFormula(FormulaData payload);
        Task<ushort> PublishFormulaAsync(FormulaData payload);

        /// <summary>
        /// 卸料
        /// </summary>
        ushort PublishUnloading(UnloadingData payload);
        Task<ushort> PublishUnloadingAsync(UnloadingData payload);

        /// <summary>
        /// 设备状态
        /// </summary>
        ushort PublishDeviceState(DeviceStateData payload);
        Task<ushort> PublishDeviceStateAsync(DeviceStateData payload);

        /// <summary>
        /// 心跳
        /// </summary>
        ushort PublishHeartbeat(HeartbeatData payload);
        Task<ushort> PublishHeartbeatAsync(HeartbeatData payload);

        #endregion

        #region 下行响应

        /// <summary>
        /// 规格型号响应
        /// </summary>
        IDisposable OnResponseModel(Action<MessageContext<ResponseModelData>> handler);

        #endregion
    }
}
