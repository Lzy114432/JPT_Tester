using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ewan.Model.Production
{
    /// <summary>
    /// 物料操作状态模型
    /// 用于表示装料和卸料的完成状态
    /// </summary>
    public class MaterialOperationStatus
    {
        /// <summary>
        /// 装料完成状态
        /// </summary>
        public bool Loading { get; set; } = false;

        /// <summary>
        /// 卸料完成状态
        /// </summary>
        public bool Unloading { get; set; } = false;
    }
}
