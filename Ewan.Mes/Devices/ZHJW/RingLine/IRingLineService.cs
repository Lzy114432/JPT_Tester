/*****************************************************
** 命名空间: Ewan.Mes.Devices.ZHJW.RingLine
** 文 件 名：IRingLineService
** 内容简述：环线服务接口
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/15
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
using System;
using System.Threading.Tasks;
using Ewan.Mes.Models.Domain.ZHJW.RingLine;
using Ewan.Mes.Transport;

namespace Ewan.Mes.Devices.ZHJW.RingLine
{
    /// <summary>
    /// 环线服务接口
    /// </summary>
    public interface IRingLineService : IDeviceService
    {
        #region 上行请求

        /// <summary>
        /// 前料仓上料
        /// </summary>
        ushort PublishFeedingQianLiaocang(FeedingQianLiaocangData payload);
        Task<ushort> PublishFeedingQianLiaocangAsync(FeedingQianLiaocangData payload);

        /// <summary>
        /// 前料仓上料成功
        /// </summary>
        ushort PublishFeedingQianLiaocangSuccess(FeedingQianLiaocangSuccessData payload);
        Task<ushort> PublishFeedingQianLiaocangSuccessAsync(FeedingQianLiaocangSuccessData payload);

        /// <summary>
        /// 前料仓卸料
        /// </summary>
        ushort PublishUnloadingQianLiaocang(UnloadingQianLiaocangData payload);
        Task<ushort> PublishUnloadingQianLiaocangAsync(UnloadingQianLiaocangData payload);

        /// <summary>
        /// 中料仓上料
        /// </summary>
        ushort PublishFeedingZhongLiaocang(FeedingZhongLiaocangData payload);
        Task<ushort> PublishFeedingZhongLiaocangAsync(FeedingZhongLiaocangData payload);

        /// <summary>
        /// 中料仓卸料
        /// </summary>
        ushort PublishUnloadingZhongLiaocang(UnloadingZhongLiaocangData payload);
        Task<ushort> PublishUnloadingZhongLiaocangAsync(UnloadingZhongLiaocangData payload);

        /// <summary>
        /// 清洗烘干机上料
        /// </summary>
        ushort PublishFeedingQingxihongganji(FeedingQingxihongganjiData payload);
        Task<ushort> PublishFeedingQingxihongganjiAsync(FeedingQingxihongganjiData payload);

        /// <summary>
        /// 后料仓上料
        /// </summary>
        ushort PublishFeedingHouLiaocang(FeedingHouLiaocangData payload);
        Task<ushort> PublishFeedingHouLiaocangAsync(FeedingHouLiaocangData payload);

        /// <summary>
        /// 后料仓卸料
        /// </summary>
        ushort PublishUnloadingHouLiaocang(UnloadingHouLiaocangData payload);
        Task<ushort> PublishUnloadingHouLiaocangAsync(UnloadingHouLiaocangData payload);

        #endregion

        #region 下行响应

        /// <summary>
        /// 前料仓上料响应
        /// </summary>
        IDisposable OnFeedingQianLiaocangResponse(Action<MessageContext<FeedingQianLiaocangResponseData>> handler);

        #endregion
    }
}
