using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ewan.Model.Production
{
    /// <summary>
    /// 物料操作状态模型
    /// 用于表示机械手装载和卸载的完成状态
    /// </summary>
    public class MaterialOperationStatus
    {
        /// <summary>
        /// 机械手装载完成状态（机械手将料片放入料仓完成）
        /// </summary>
        public bool LoadingCompleted { get; set; } = false;

        /// <summary>
        /// 机械手卸载完成状态（机械手从料仓取料完成）
        /// </summary>
        public bool UnloadingCompleted { get; set; } = false;
    }
}
