/*****************************************************
** 命名空间: Ewan.Mes.Models.Domain.ZHJW.RingLine
** 文 件 名：UnloadingHouLiaocangData
** 内容简述：后料仓卸料数据
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/15
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
using System;

namespace Ewan.Mes.Models.Domain.ZHJW.RingLine
{
    /// <summary>
    /// 后料仓卸料数据
    /// </summary>
    public class UnloadingHouLiaocangData
    {
        public string DeviceCode { get; set; }
        public string PlateCode { get; set; }
        public string FeedingLiaokuangCode { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
