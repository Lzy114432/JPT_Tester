using Ewan.Core;
using Ewan.Core.Axis;
using Ewan.Core.IO;
using System;
using System.Threading;

namespace Ewan.BusinessBonding
{
    /// <summary>
    /// 料仓下料控制器
    /// 负责料仓1的自动下料控制
    /// </summary>
    public class BinFeedController : BaseManager<BinFeedController>
    {
        private readonly AxisManager _axisManager = AxisManager.Instance();
        private readonly LayeredIOManager _ioManager = LayeredIOManager.Instance();

        // 料仓配置
        private const int BIN1_AXIS_ID = 0;           // 料仓1轴ID
        private const int BIN2_AXIS_ID = 1;           // 料仓2轴ID
        private const int BIN3_AXIS_ID = 2;           // 料仓3轴ID
        
        private const int BIN1_SENSOR_INDEX = 27;     // 料仓1有料感应 LogicalIndex
        private const int BIN2_SENSOR_INDEX = 28;     // 料仓2有料感应 LogicalIndex
        private const int BIN3_SENSOR_INDEX = 29;     // 料仓3有料感应 LogicalIndex

        // 控制参数
        private const int SENSOR_CHECK_INTERVAL = 50; // 感应器检测间隔(ms)
        private const int OPERATION_TIMEOUT = 30000;  // 操作超时时间(ms) - 30秒
        private const int ASCEND_COMPLETION_TIMEOUT = 10000; // 上升阶段完成超时时间(ms) - 10秒

        /// <summary>
        /// 料仓1下料控制
        /// 逻辑：
        /// - 如果感应=true：直接下降到感应=false
        /// - 如果感应=false：先上升到感应=true，再下降到感应=false
        /// </summary>
        /// <returns>是否成功完成下料</returns>
        public bool FeedBin1()
        {
            return FeedBin(1, BIN1_AXIS_ID, BIN1_SENSOR_INDEX);
        }

        /// <summary>
        /// 料仓2下料控制
        /// 逻辑：
        /// - 如果感应=true：直接下降到感应=false
        /// - 如果感应=false：先上升到感应=true，再下降到感应=false
        /// </summary>
        /// <returns>是否成功完成下料</returns>
        public bool FeedBin2()
        {
            return FeedBin(2, BIN2_AXIS_ID, BIN2_SENSOR_INDEX);
        }

        /// <summary>
        /// 料仓3下料控制
        /// 逻辑：
        /// - 如果感应=true：直接下降到感应=false
        /// - 如果感应=false：先上升到感应=true，再下降到感应=false
        /// </summary>
        /// <returns>是否成功完成下料</returns>
        public bool FeedBin3()
        {
            return FeedBin(3, BIN3_AXIS_ID, BIN3_SENSOR_INDEX);
        }

        #region 私有方法

        /// <summary>
        /// 料仓下料控制通用方法
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        /// <param name="axisId">轴ID</param>
        /// <param name="sensorIndex">感应器索引</param>
        /// <returns>是否成功完成下料</returns>
        private bool FeedBin(int binNumber, int axisId, int sensorIndex)
        {
            try
            {
                _uiLogger.InfoRaw("处理已开始: {0}", $"料仓{binNumber}下料控制");

                if (!AscendUntilSensorOn(binNumber, axisId, sensorIndex))
                {
                    _uiLogger.ErrorRaw("处理错误: {0}", $"料仓{binNumber}上升失败");
                    return false;
                }

                if (!DescendUntilSensorOff(binNumber, axisId, sensorIndex))
                {
                    _uiLogger.ErrorRaw("处理错误: {0}", $"料仓{binNumber}下降失败");
                    return false;
                }

                _uiLogger.InfoRaw("处理已完成: {0}", $"料仓{binNumber}升降循环成功");
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", $"料仓{binNumber}下料控制", ex.Message);
                StopBinAxis(binNumber, axisId);
                return false;
            }
        }

        /// <summary>
        /// 上升直到感应器为true
        /// </summary>
        private bool AscendUntilSensorOn(int binNumber, int axisId, int sensorIndex)
        {
            try
            {
                if (ReadBinSensor(binNumber, sensorIndex))
                {
                    _uiLogger.DebugRaw("料仓{0}已处于上升完成状态，跳过上升动作", binNumber);
                    return true;
                }

                StartBinJogUp(binNumber, axisId);

                DateTime startTime = DateTime.Now;

                while (true)
                {
                    var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

                    if (elapsed > ASCEND_COMPLETION_TIMEOUT)
                    {
                        StopBinAxis(binNumber, axisId);
                        _uiLogger.InfoRaw("料仓{0}上升阶段10秒未感应，视为已到达上限位置完成", binNumber);
                        return true;
                    }

                    if (elapsed > OPERATION_TIMEOUT)
                    {
                        StopBinAxis(binNumber, axisId);
                        _uiLogger.ErrorRaw("处理错误: {0}", $"料仓{binNumber}上升超时");
                        return false;
                    }

                    if (ReadBinSensor(binNumber, sensorIndex))
                    {
                        StopBinAxis(binNumber, axisId);
                        return true;
                    }

                    Thread.Sleep(SENSOR_CHECK_INTERVAL);
                }
            }
            catch (Exception ex)
            {
                StopBinAxis(binNumber, axisId);
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", $"料仓{binNumber}上升", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 下降直到感应器为false
        /// </summary>
        private bool DescendUntilSensorOff(int binNumber, int axisId, int sensorIndex)
        {
            try
            {
                if (!ReadBinSensor(binNumber, sensorIndex))
                {
                    _uiLogger.DebugRaw("料仓{0}已处于下降完成状态，跳过下降动作", binNumber);
                    return true;
                }

                StartBinJogDown(binNumber, axisId);

                DateTime startTime = DateTime.Now;

                while (true)
                {
                    if ((DateTime.Now - startTime).TotalMilliseconds > OPERATION_TIMEOUT)
                    {
                        StopBinAxis(binNumber, axisId);
                        _uiLogger.ErrorRaw("处理错误: {0}", $"料仓{binNumber}下降超时");
                        return false;
                    }

                    if (!ReadBinSensor(binNumber, sensorIndex))
                    {
                        StopBinAxis(binNumber, axisId);
                        return true;
                    }

                    Thread.Sleep(SENSOR_CHECK_INTERVAL);
                }
            }
            catch (Exception ex)
            {
                StopBinAxis(binNumber, axisId);
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", $"料仓{binNumber}下降", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 读取料仓感应器状态
        /// </summary>
        private bool ReadBinSensor(int binNumber, int sensorIndex)
        {
            try
            {
                return _ioManager.LayeredIO.ReadInBit(sensorIndex, true);
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("读取 {0} 错误: {1}", $"料仓{binNumber}感应器", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 开始料仓 Jog上升
        /// </summary>
        private void StartBinJogUp(int binNumber, int axisId)
        {
            var axisConfig = _axisManager.GetAxisConfig(axisId);
            if (axisConfig != null)
            {
                _axisManager.JogUp(axisConfig);
            }
            else
            {
                _uiLogger.ErrorRaw("处理错误: {0}", $"料仓{binNumber}轴配置未找到");
            }
        }

        /// <summary>
        /// 开始料仓 Jog下降
        /// </summary>
        private void StartBinJogDown(int binNumber, int axisId)
        {
            var axisConfig = _axisManager.GetAxisConfig(axisId);
            if (axisConfig != null)
            {
                _axisManager.JogDown(axisConfig);
            }
            else
            {
                _uiLogger.ErrorRaw("处理错误: {0}", $"料仓{binNumber}轴配置未找到");
            }
        }

        /// <summary>
        /// 停止料仓轴运动
        /// </summary>
        private void StopBinAxis(int binNumber, int axisId)
        {
            try
            {
                var axisConfig = _axisManager.GetAxisConfig(axisId);
                if (axisConfig != null)
                {
                    _axisManager.JogStop(axisConfig);
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", $"料仓{binNumber}停止", ex.Message);
            }
        }

        #endregion
    }
}
