/*****************************************************
** 命名空间: Ewan.Mes.Models.Domain.ZHJW.DicingMachine
** 文 件 名：HeartbeatData
** 内容简述：划片机心跳数据
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
    /// 心跳数据
    /// </summary>
    public class HeartbeatData
    {
        public string DeviceCode { get; set; }
        /// <summary>
        /// 设备状态：1=运行，2=故障，3=待机，4=停机
        /// </summary>
        public int State { get; set; }
        public string FaultMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
