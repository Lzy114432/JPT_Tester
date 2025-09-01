using System;

namespace Ewan.Model.IO
{
    /// <summary>
    /// 真实IO数据模型 - 64个输入点(X1-X64) + 64个输出点(Y1-Y64)
    /// </summary>
    public class RealIO
    {
        /// <summary>
        /// IO点总数
        /// </summary>
        public const int IO_COUNT = 64;

        /// <summary>
        /// 输入点数组 X1-X64 (索引0-63对应X1-X64)
        /// </summary>
        public bool[] X { get; set; }

        /// <summary>
        /// 输出点数组 Y1-Y64 (索引0-63对应Y1-Y64)
        /// </summary>
        public bool[] Y { get; set; }

        /// <summary>
        /// 连接状态
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// 硬件类型
        /// </summary>
        public string HardwareType { get; set; }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public RealIO()
        {
            X = new bool[IO_COUNT];
            Y = new bool[IO_COUNT];
            LastUpdateTime = DateTime.Now;
        }

        /// <summary>
        /// 获取输入点值 (X1-X64)
        /// </summary>
        /// <param name="index">点位索引 1-64</param>
        /// <returns>点位值</returns>
        public bool GetX(int index)
        {
            if (index < 1 || index > IO_COUNT)
                throw new ArgumentOutOfRangeException(nameof(index), $"输入点索引必须在1-{IO_COUNT}之间");
            return X[index - 1];
        }

        /// <summary>
        /// 设置输入点值 (X1-X64)
        /// </summary>
        /// <param name="index">点位索引 1-64</param>
        /// <param name="value">点位值</param>
        public void SetX(int index, bool value)
        {
            if (index < 1 || index > IO_COUNT)
                throw new ArgumentOutOfRangeException(nameof(index), $"输入点索引必须在1-{IO_COUNT}之间");
            X[index - 1] = value;
            LastUpdateTime = DateTime.Now;
        }

        /// <summary>
        /// 获取输出点值 (Y1-Y64)
        /// </summary>
        /// <param name="index">点位索引 1-64</param>
        /// <returns>点位值</returns>
        public bool GetY(int index)
        {
            if (index < 1 || index > IO_COUNT)
                throw new ArgumentOutOfRangeException(nameof(index), $"输出点索引必须在1-{IO_COUNT}之间");
            return Y[index - 1];
        }

        /// <summary>
        /// 设置输出点值 (Y1-Y64)
        /// </summary>
        /// <param name="index">点位索引 1-64</param>
        /// <param name="value">点位值</param>
        public void SetY(int index, bool value)
        {
            if (index < 1 || index > IO_COUNT)
                throw new ArgumentOutOfRangeException(nameof(index), $"输出点索引必须在1-{IO_COUNT}之间");
            Y[index - 1] = value;
            LastUpdateTime = DateTime.Now;
        }

        /// <summary>
        /// 批量更新输入点
        /// </summary>
        /// <param name="values">新的输入值数组</param>
        public void UpdateInputs(bool[] values)
        {
            if (values != null && values.Length == IO_COUNT)
            {
                Array.Copy(values, X, IO_COUNT);
                LastUpdateTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 批量更新输出点
        /// </summary>
        /// <param name="values">新的输出值数组</param>
        public void UpdateOutputs(bool[] values)
        {
            if (values != null && values.Length == IO_COUNT)
            {
                Array.Copy(values, Y, IO_COUNT);
                LastUpdateTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 清除所有IO点状态
        /// </summary>
        public void Clear()
        {
            Array.Clear(X, 0, IO_COUNT);
            Array.Clear(Y, 0, IO_COUNT);
            LastUpdateTime = DateTime.Now;
        }
    }
}