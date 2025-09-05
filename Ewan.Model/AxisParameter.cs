using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ewan.Model
{
    public class AxisParameter
    {
        /// <summary>
        ///  轴号   
        /// </summary> 
        public int AxisNum { get; set; }


        public bool Dir { get; set; }


        public double Speed{set; get;}

        public double Acc { get; set; }


        public double Dec { get; set; }
    }
}
