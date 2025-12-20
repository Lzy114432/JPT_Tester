/*****************************************************
** 命名空间: Ewan.Mes.Devices.LaserCutting
** 文 件 名：ILaserCuttingService
** 内容简述：激光切割机服务接口定义（协议无关）
** 版    本：V3.0
** 创 建 人：Ewan
** 创建日期：2025/12/11
** 修改记录：
日期        版本      修改人    修改内容
2025/12/11  V2.0      Ewan      使用 Domain 模型替代 Dto
2025/12/12  V3.0      Ewan      重构为协议无关接口
*****************************************************/
using System;
using System.Threading.Tasks;
using Ewan.Mes.Devices;
using Ewan.Mes.Transport;
using Ewan.Mes.Models.Domain.ZHJW.LaserCutting;

namespace Ewan.Mes.Devices.ZHJW.LaserCutting
{
    /// <summary>
    /// 激光切割机服务接口（协议无关）
    /// 定义激光切割机的业务行为，不依赖具体协议实现
    /// 使用 Domain 模型作为参数和返回值
    /// </summary>
    public interface ILaserCuttingService : IDeviceService
    {
        #region 上行请求 (Up) - 使用 Domain 模型

        /// <summary>
        /// 扫描板片
        /// </summary>
        ushort PublishScanPlate(ScanPlateData payload);

        /// <summary>
        /// 扫描板片（异步）
        /// </summary>
        Task<ushort> PublishScanPlateAsync(ScanPlateData payload);

        /// <summary>
        /// 请求规格型号
        /// </summary>
        ushort PublishRequestModel(RequestModelData payload);

        /// <summary>
        /// 请求规格型号（异步）
        /// </summary>
        Task<ushort> PublishRequestModelAsync(RequestModelData payload);

        /// <summary>
        /// 扫描NG
        /// </summary>
        ushort PublishScanNg(ScanNgData payload);

        /// <summary>
        /// 扫描NG（异步）
        /// </summary>
        Task<ushort> PublishScanNgAsync(ScanNgData payload);

        /// <summary>
        /// 报警上报
        /// </summary>
        ushort PublishAlarm(AlarmData payload);

        /// <summary>
        /// 报警上报（异步）
        /// </summary>
        Task<ushort> PublishAlarmAsync(AlarmData payload);

        /// <summary>
        /// 上报生产数据
        /// </summary>
        ushort PublishReportData(ReportWorkData payload);

        /// <summary>
        /// 上报生产数据（异步）
        /// </summary>
        Task<ushort> PublishReportDataAsync(ReportWorkData payload);

        /// <summary>
        /// 上报配方
        /// </summary>
        ushort PublishFormula(FormulaData payload);

        /// <summary>
        /// 上报配方（异步）
        /// </summary>
        Task<ushort> PublishFormulaAsync(FormulaData payload);

        /// <summary>
        /// 设备状态上报
        /// </summary>
        ushort PublishDeviceState(DeviceStateData payload);

        /// <summary>
        /// 设备状态上报（异步）
        /// </summary>
        Task<ushort> PublishDeviceStateAsync(DeviceStateData payload);

        /// <summary>
        /// 心跳上报
        /// </summary>
        ushort PublishHeartbeat(HeartbeatData payload);

        /// <summary>
        /// 心跳上报（异步）
        /// </summary>
        Task<ushort> PublishHeartbeatAsync(HeartbeatData payload);

        #endregion

        #region 下行响应监听 (Down) - 使用 Domain 模型

        /// <summary>
        /// 监听返回规格型号
        /// </summary>
        IDisposable OnResponseModel(Action<MessageContext<ResponseModelData>> handler);

        #endregion
    }
}
