using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ewan.Model
{
    public class RingLineModel
    {
        public bool IsLoading { get; set; } = false;
        
        /// <summary>
        /// 上升沿标志（False → True）
        /// </summary>
        public bool RisingEdge { get; set; } = false;
        
        /// <summary>
        /// 下降沿标志（True → False）
        /// </summary>
        public bool FallingEdge { get; set; } = false;
    }
}
