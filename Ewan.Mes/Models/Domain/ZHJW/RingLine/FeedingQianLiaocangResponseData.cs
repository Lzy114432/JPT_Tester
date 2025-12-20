/*****************************************************
** 命名空间: Ewan.Mes.Models.Domain.ZHJW.RingLine
** 文 件 名：FeedingQianLiaocangResponseData
** 内容简述：前料仓上料响应数据
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
    /// 前料仓上料响应数据
    /// </summary>
    public class FeedingQianLiaocangResponseData
    {
        public string BillNoA { get; set; }
        public string BillNoB { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
