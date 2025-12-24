/*===================================================
 * 类名称: AxisParameter
 * 类描述: 轴参数配置
 * 创建人: Ewan
 * 创建时间: 2025-12-21
 * 版本: V1.0
 =====================================================*/

using System;
using Newtonsoft.Json;

namespace EwanAxis.Core.Interfaces
{
    /// <summary>
    /// 轴参数
    /// </summary>
    [Serializable]
    public class AxisParameter
    {
        /// <summary>
        /// 轴名称
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// 轴号（硬件轴号）
        /// </summary>
        public int AxisNum { get; set; }

        /// <summary>
        /// 脉冲当量（脉冲/单位）
        /// 例如：1000 脉冲/mm
        /// </summary>
        public double Step { get; set; } = 1000;

        /// <summary>
        /// 运动方向
        /// true = 反向
        /// false = 正向
        /// </summary>
        public bool Dir { get; set; } = false;

        #region 运动参数

        /// <summary>
        /// 运行速度（单位/秒）
        /// </summary>
        public double Speed { get; set; } = 100;

        /// <summary>
        /// 加速时间（秒）
        /// </summary>
        public double Acc { get; set; } = 0.1;

        /// <summary>
        /// 减速时间（秒）
        /// </summary>
        public double Dec { get; set; } = 0.1;

        /// <summary>
        /// 点动速度（单位/秒）
        /// </summary>
        public double JogSpeed { get; set; } = 10;

        #endregion

        #region 回原点参数

        /// <summary>
        /// 回原点方向
        /// true = 正向
        /// false = 负向
        /// </summary>
        public bool HomeDir { get; set; } = false;

        /// <summary>
        /// 回原点模式
        /// </summary>
        public int HomeMode { get; set; } = 0;

        /// <summary>
        /// 回原点速度（单位/秒）
        /// </summary>
        public double HomeSpeed { get; set; } = 10;

        /// <summary>
        /// 回原点关联的IO（某些模式需要）
        /// </summary>
        public int HomeIO { get; set; } = 0;

        #endregion

        #region 软限位

        /// <summary>
        /// 启用软限位
        /// </summary>
        public bool SoftLimitEnable { get; set; } = false;

        /// <summary>
        /// 正向软限位位置
        /// </summary>
        public double SoftLimitPositive { get; set; } = 1000;

        /// <summary>
        /// 负向软限位位置
        /// </summary>
        public double SoftLimitNegative { get; set; } = -1000;

        #endregion

        #region 补偿

        /// <summary>
        /// 反向间隙补偿
        /// </summary>
        public double BacklashCompensation { get; set; } = 0;

        /// <summary>
        /// 丝杠补偿数据
        /// </summary>
        [JsonIgnore]
        public CalibrationData CalibrationData { get; set; } = new CalibrationData();

        #endregion

        /// <summary>
        /// 克隆参数
        /// </summary>
        public AxisParameter Clone()
        {
            return new AxisParameter
            {
                Name = this.Name,
                AxisNum = this.AxisNum,
                Step = this.Step,
                Dir = this.Dir,
                Speed = this.Speed,
                Acc = this.Acc,
                Dec = this.Dec,
                JogSpeed = this.JogSpeed,
                HomeDir = this.HomeDir,
                HomeMode = this.HomeMode,
                HomeSpeed = this.HomeSpeed,
                HomeIO = this.HomeIO,
                SoftLimitEnable = this.SoftLimitEnable,
                SoftLimitPositive = this.SoftLimitPositive,
                SoftLimitNegative = this.SoftLimitNegative,
                BacklashCompensation = this.BacklashCompensation,
                CalibrationData = this.CalibrationData.Clone()
            };
        }
    }

    /// <summary>
    /// 丝杠补偿数据
    /// </summary>
    [Serializable]
    public class CalibrationData
    {
        /// <summary>
        /// 是否启用补偿
        /// </summary>
        public bool Enable { get; set; } = false;

        /// <summary>
        /// 补偿点数量
        /// </summary>
        public int PointNum { get; set; } = 0;

        /// <summary>
        /// 起始点位置
        /// </summary>
        public double StartPoint { get; set; } = 0;

        /// <summary>
        /// 补偿长度
        /// </summary>
        public double Length { get; set; } = 0;

        /// <summary>
        /// 补偿数据列表
        /// </summary>
        public System.Collections.Generic.List<double> CalData { get; set; } = new System.Collections.Generic.List<double>();

        public CalibrationData Clone()
        {
            return new CalibrationData
            {
                Enable = this.Enable,
                PointNum = this.PointNum,
                StartPoint = this.StartPoint,
                Length = this.Length,
                CalData = new System.Collections.Generic.List<double>(this.CalData)
            };
        }
    }
}
