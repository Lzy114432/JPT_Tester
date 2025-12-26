using EwanModel.Common;
using EwanModel.Plc;

namespace EwanModel
{
    /// <summary>
    /// PLC 标签属性：描述一个属性对应的 PLC 地址、长度、字节序等信息。
    /// </summary>
    public class PlcAttribute : System.Attribute
    {
        /// <summary>
        /// plc地址前缀
        /// 如M区,L区,D区...
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        /// plc地址
        /// </summary>
        public int Addr { get; set; }

        /// <summary>
        /// 只有string类型的属性才有此属性
        /// </summary>
        public int Len { get; set; }

        /// <summary>
        /// 子信息的序号 如X1000为0,X1001为1,X1002为2
        /// 从0开始
        /// </summary>
        public string BitIndex { get; set; } = "0";

        /// <summary>
        /// 倍数
        /// </summary>
        public string Multiple { get; set; }

        /// <summary>
        /// 兼容旧逻辑：仅用于字符串/word 字节互换
        /// </summary>
        public bool IsBigEndian { get; set; } = false;

        /// <summary>
        /// 可选：更精细的字节序控制（默认 Auto：使用项目级默认配置；特殊 PLC 再单独覆盖）
        /// </summary>
        public PlcByteOrder ByteOrder { get; set; } = PlcByteOrder.Auto;

        /// <summary>
        /// 是否报警属性
        /// </summary>
        public bool IsAlarmProperty { get; set; } = false;

        /// <summary>
        /// 报警描述
        /// </summary>
        public string AlarmDesc { get; set; }

        /// <summary>
        /// 报警级别
        /// </summary>
        public EAlarmLevel EAlarmLevel { get; set; }

        /// <summary>
        /// 是否需要复位后才能继续（仅对 <see cref="IsAlarmProperty"/> 为 true 的属性有意义）。
        /// - null：由上层策略决定（例如：默认 H 级需要复位）
        /// - true：强制需要复位
        /// - false：不需要复位
        /// </summary>
        public bool? NeedReset { get; set; }
    }
}
