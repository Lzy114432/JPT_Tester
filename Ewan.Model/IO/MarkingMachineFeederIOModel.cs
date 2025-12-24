using EwanIO.Core.Attributes;

namespace Ewan.Model.IO
{
    /// <summary>
    /// 贴标机上料系统 IO Layout（基于 config/io_mapping.json 的逻辑点位）
    /// 注意：Layout 仅声明属性；读写通过 IoContext&lt;T&gt; 的 ctx.R / ctx.On / ctx.Off / ctx.Edge 完成。
    /// </summary>
    public class MarkingMachineFeederIOModel
    {
        #region Inputs

        [IO(0, DisplayName = "急停(常闭)")]
        public InputSignal 急停按钮 { get; set; }

        [IO(1, DisplayName = "停止")]
        public InputSignal 停止按钮 { get; set; }

        [IO(2, DisplayName = "启动")]
        public InputSignal 启动按钮 { get; set; }

        [IO(3, DisplayName = "检测到料片信号")]
        public InputSignal 检测到料片信号 { get; set; }

        [IO(4, DisplayName = "机械臂抓取完成信号")]
        public InputSignal 机械臂抓取完成信号 { get; set; }

        [IO(5, DisplayName = "下相机精定位完成信号")]
        public InputSignal 下相机精定位完成信号 { get; set; }

        [IO(6, DisplayName = "机械臂定位完成信号")]
        public InputSignal 机械臂定位完成信号 { get; set; }

        [IO(7, DisplayName = "移至扫码区到位信号")]
        public InputSignal 移至扫码区到位信号 { get; set; }

        [IO(8, DisplayName = "机械臂放置完成信号")]
        public InputSignal 机械臂放置完成信号 { get; set; }

        [IO(9, DisplayName = "初始化信号")]
        public InputSignal 初始化信号 { get; set; }

        [IO(10, DisplayName = "机械臂取料完成信号")]
        public InputSignal 机械臂取料完成信号 { get; set; }

        [IO(11, DisplayName = "放入小车完成信号")]
        public InputSignal 放入小车完成信号 { get; set; }

        [IO(12, DisplayName = "料仓1下限位置信号(常闭)")]
        public InputSignal 料仓1下限位置信号 { get; set; }

        [IO(13, DisplayName = "料仓2下限位置信号(常闭)")]
        public InputSignal 料仓2下限位置信号 { get; set; }

        [IO(14, DisplayName = "料仓3下限位置信号(常闭)")]
        public InputSignal 料仓3下限位置信号 { get; set; }

        [IO(15, DisplayName = "机械手报警信号")]
        public InputSignal 机械手报警信号 { get; set; }

        [IO(16, DisplayName = "X16")]
        public InputSignal X16 { get; set; }

        [IO(17, DisplayName = "下相机报警信号")]
        public InputSignal 下相机报警信号 { get; set; }

        [IO(18, DisplayName = "X18")]
        public InputSignal X18 { get; set; }

        [IO(19, DisplayName = "机械臂气缸报警信号")]
        public InputSignal 机械臂气缸报警信号 { get; set; }

        [IO(20, DisplayName = "机械手忙碌状态信号")]
        public InputSignal 机械手忙碌状态信号 { get; set; }

        [IO(21, DisplayName = "X21")]
        public InputSignal X21 { get; set; }

        [IO(22, DisplayName = "X22")]
        public InputSignal X22 { get; set; }

        [IO(23, DisplayName = "X23")]
        public InputSignal X23 { get; set; }

        [IO(24, DisplayName = "前门电磁感应信号")]
        public InputSignal 前门电磁感应信号 { get; set; }

        [IO(25, DisplayName = "后门电磁感应信号")]
        public InputSignal 后门电磁感应信号 { get; set; }

        [IO(26, DisplayName = "侧门电磁感应信号")]
        public InputSignal 侧门电磁感应信号 { get; set; }

        [IO(27, DisplayName = "料仓1有料感应")]
        public InputSignal 料仓1有料感应 { get; set; }

        [IO(28, DisplayName = "料仓2有料感应")]
        public InputSignal 料仓2有料感应 { get; set; }

        [IO(29, DisplayName = "料仓3有料感应")]
        public InputSignal 料仓3有料感应 { get; set; }

        [IO(30, DisplayName = "X30")]
        public InputSignal X30 { get; set; }

        [IO(31, DisplayName = "机械臂门电磁感应信号")]
        public InputSignal 机械臂门电磁感应信号 { get; set; }

        [IO(32, DisplayName = "X32")]
        public InputSignal X32 { get; set; }

        [IO(33, DisplayName = "X33")]
        public InputSignal X33 { get; set; }

        [IO(34, DisplayName = "X34")]
        public InputSignal X34 { get; set; }

        [IO(35, DisplayName = "X35")]
        public InputSignal X35 { get; set; }

        [IO(36, DisplayName = "X36")]
        public InputSignal X36 { get; set; }

        [IO(37, DisplayName = "X37")]
        public InputSignal X37 { get; set; }

        [IO(38, DisplayName = "X38")]
        public InputSignal X38 { get; set; }

        [IO(39, DisplayName = "X39")]
        public InputSignal X39 { get; set; }

        #endregion

        #region Outputs

        [IO(0, DisplayName = "绿灯")]
        public OutputSignal 绿灯 { get; set; }

        [IO(1, DisplayName = "黄灯")]
        public OutputSignal 黄灯 { get; set; }

        [IO(2, DisplayName = "红灯")]
        public OutputSignal 红灯 { get; set; }

        [IO(3, DisplayName = "蜂鸣器")]
        public OutputSignal 蜂鸣器 { get; set; }

        [IO(4, DisplayName = "主控启动信号")]
        public OutputSignal 主控启动信号 { get; set; }

        [IO(5, DisplayName = "开始")]
        public OutputSignal 开始 { get; set; }

        [IO(6, DisplayName = "停止")]
        public OutputSignal 停止输出 { get; set; }

        [IO(7, DisplayName = "复位")]
        public OutputSignal 复位 { get; set; }

        [IO(8, DisplayName = "暂停")]
        public OutputSignal 暂停 { get; set; }

        [IO(9, DisplayName = "发送扫码完成信号")]
        public OutputSignal 发送扫码完成信号 { get; set; }

        [IO(10, DisplayName = "触发机械手放置料仓")]
        public OutputSignal 触发机械手放置料仓 { get; set; }

        [IO(11, DisplayName = "料仓1选择信号")]
        public OutputSignal 料仓1选择信号 { get; set; }

        [IO(12, DisplayName = "料仓2选择信号")]
        public OutputSignal 料仓2选择信号 { get; set; }

        [IO(13, DisplayName = "料仓3选择信号")]
        public OutputSignal 料仓3选择信号 { get; set; }

        [IO(14, DisplayName = "触发机械手皮带线允许取料")]
        public OutputSignal 触发机械手皮带线允许取料 { get; set; }

        [IO(15, DisplayName = "发送取料指令")]
        public OutputSignal 发送取料指令 { get; set; }

        [IO(16, DisplayName = "高速运行")]
        public OutputSignal 高速运行 { get; set; }

        [IO(17, DisplayName = "发送放入小车指令")]
        public OutputSignal 发送放入小车指令 { get; set; }

        [IO(18, DisplayName = "料仓1定位电磁阀")]
        public OutputSignal 料仓1定位电磁阀 { get; set; }

        [IO(19, DisplayName = "料仓1吹气电磁阀")]
        public OutputSignal 料仓1吹气电磁阀 { get; set; }

        [IO(20, DisplayName = "料仓2定位电磁阀")]
        public OutputSignal 料仓2定位电磁阀 { get; set; }

        [IO(21, DisplayName = "料仓2吹气电磁阀")]
        public OutputSignal 料仓2吹气电磁阀 { get; set; }

        [IO(22, DisplayName = "料仓定3位电磁阀")]
        public OutputSignal 料仓3定位电磁阀 { get; set; }

        [IO(23, DisplayName = "料仓吹3气电磁阀")]
        public OutputSignal 料仓3吹气电磁阀 { get; set; }

        [IO(24, DisplayName = "慢速运行")]
        public OutputSignal 慢速运行 { get; set; }

        [IO(25, DisplayName = "清除报警")]
        public OutputSignal 清除报警 { get; set; }

        [IO(26, DisplayName = "Y26")]
        public OutputSignal Y26 { get; set; }

        [IO(27, DisplayName = "Y27")]
        public OutputSignal Y27 { get; set; }

        [IO(28, DisplayName = "Y28")]
        public OutputSignal Y28 { get; set; }

        [IO(29, DisplayName = "Y29")]
        public OutputSignal Y29 { get; set; }

        [IO(30, DisplayName = "Y30")]
        public OutputSignal Y30 { get; set; }

        [IO(31, DisplayName = "Y31")]
        public OutputSignal Y31 { get; set; }

        [IO(32, DisplayName = "Y32")]
        public OutputSignal Y32 { get; set; }

        [IO(33, DisplayName = "Y33")]
        public OutputSignal Y33 { get; set; }

        #endregion
    }
}

