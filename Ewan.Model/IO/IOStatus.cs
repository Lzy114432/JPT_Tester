using System;

namespace Ewan.Model.IO
{
    /// <summary>
    /// IO状态数据模型 - 64个输入点(X0-X63) + 64个输出点(Y0-Y63)
    /// </summary>
    public class IOStatus
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
        /// 输入点映射名称数组 (映射模式时使用)
        /// </summary>
        public string[] XNames { get; set; }

        /// <summary>
        /// 输出点映射名称数组 (映射模式时使用)
        /// </summary>
        public string[] YNames { get; set; }

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
        public IOStatus()
        {
            X = new bool[IO_COUNT];
            Y = new bool[IO_COUNT];
            XNames = new string[IO_COUNT];
            YNames = new string[IO_COUNT];
            
            // 初始化默认名称
            for (int i = 0; i < IO_COUNT; i++)
            {
                XNames[i] = $"X{i}";  // 默认名称 X0-X63
                YNames[i] = $"Y{i}";  // 默认名称 Y0-Y63
            }
            
            LastUpdateTime = DateTime.Now;
        }

        /// <summary>
        /// 获取输入点值 (X0-X63)
        /// </summary>
        /// <param name="index">点位索引 0-63</param>
        /// <returns>点位值</returns>
        public bool GetX(int index)
        {
            if (index < 0 || index >= IO_COUNT)
                throw new ArgumentOutOfRangeException(nameof(index), $"输入点索引必须在0-{IO_COUNT - 1}之间");
            return X[index];
        }

        /// <summary>
        /// 设置输入点值 (X0-X63)
        /// </summary>
        /// <param name="index">点位索引 0-63</param>
        /// <param name="value">点位值</param>
        public void SetX(int index, bool value)
        {
            if (index < 0 || index >= IO_COUNT)
                throw new ArgumentOutOfRangeException(nameof(index), $"输入点索引必须在0-{IO_COUNT - 1}之间");
            X[index] = value;
            LastUpdateTime = DateTime.Now;
        }

        /// <summary>
        /// 获取输出点值 (Y0-Y63)
        /// </summary>
        /// <param name="index">点位索引 0-63</param>
        /// <returns>点位值</returns>
        public bool GetY(int index)
        {
            if (index < 0 || index >= IO_COUNT)
                throw new ArgumentOutOfRangeException(nameof(index), $"输出点索引必须在0-{IO_COUNT - 1}之间");
            return Y[index];
        }

        /// <summary>
        /// 设置输出点值 (Y0-Y63)
        /// </summary>
        /// <param name="index">点位索引 0-63</param>
        /// <param name="value">点位值</param>
        public void SetY(int index, bool value)
        {
            if (index < 0 || index >= IO_COUNT)
                throw new ArgumentOutOfRangeException(nameof(index), $"输出点索引必须在0-{IO_COUNT - 1}之间");
            Y[index] = value;
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