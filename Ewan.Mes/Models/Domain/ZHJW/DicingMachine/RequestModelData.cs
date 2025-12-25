/*****************************************************
** 命名空间: Ewan.Mes.Models.Domain.ZHJW.DicingMachine
** 文 件 名：RequestModelData
** 内容简述：划片机请求规格型号数据
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/15
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
using System;

namespace Ewan.Mes.Models.Domain.ZHJW.DicingMachine
{
    /// <summary>
    /// 请求规格型号数据
    /// </summary>
    public class RequestModelData
    {
        public string DeviceCode { get; set; }
        public string BillNo { get; set; }
        public string PlateCode { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
