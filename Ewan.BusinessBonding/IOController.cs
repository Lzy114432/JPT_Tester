using System;
using System.Linq.Expressions;
using Ewan.Core;
using Ewan.Core.IO;
using Ewan.Model.IO;
using EwanIO.Core.Attributes;
using EwanIO.Core.Simulation;

namespace Ewan.BusinessBonding
{
    public class IOController : BaseManager<IOController>
    {
        internal void WriteOutput(Expression<Func<MarkingMachineFeederIOModel, OutputSignal>> expr, bool value, bool now = false)
        {
            try
            {
                var ctx = LayeredIOManager.Instance().Ctx;
                if (ctx == null)
                {
                    _uiLogger.WarnRaw("写入失败: 未获取到IO上下文实例");
                    return;
                }

                if (value)
                {
                    ctx.On(expr, now);
                }
                else
                {
                    ctx.Off(expr, now);
                }

                _uiLogger.InfoRaw("成功写入 {0} = {1}", expr, value ? "ON" : "OFF");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("写入错误: {0} - {1}", expr, ex.Message);
            }
        }

        public void WriteOutput(int index, bool value, bool useMapping = true)
        {
            try
            {
                var ctx = LayeredIOManager.Instance().Ctx;

                if (value)
                {
                    ctx.On(index);
                }
                else
                {
                    ctx.Off(index);
                }

                _uiLogger.InfoRaw("成功写入 {0} = {1}", $"Y{index}", value ? "ON" : "OFF");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("写入 {0} 错误: {1}", $"Y{index}", ex.Message);
            }
        }

        /// <summary>
        /// 设置输入点模拟状态
        /// </summary>
        /// <param name="index">输入点索引</param>
        /// <param name="mode">模拟模式 (0=None, 1=ForceOn, 2=ForceOff)</param>
        /// <param name="useMapping">是否使用映射</param>
        public void SetInputSimulate(int index, int mode, bool useMapping = true)
        {
            try
            {
                // 转换为 SimMode 枚举 (0=None, 1=ForceOn, 2=ForceOff)
                SimMode simulateMode = (SimMode)mode;

                // 使用LayeredIOManager的SetInputSimulate方法
                bool result = LayeredIOManager.Instance().SetInputSimulate(index, simulateMode);

                if (result)
                {
                    string modeName;
                    switch (mode)
                    {
                        case 1:
                            modeName = "ForceOn";
                            break;
                        case 2:
                            modeName = "ForceOff";
                            break;
                        default:
                            modeName = "None";
                            break;
                    }

                    _uiLogger.InfoRaw("IO模拟模式设置: {0} = {1}", $"X{index}", modeName);
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("IO模拟错误: {0} - {1}", $"X{index}", ex.Message);
            }
        }

        /// <summary>
        /// 清除所有输入点模拟状态
        /// </summary>
        public void ClearAllSimulations()
        {
            try
            {
                // 使用LayeredIOManager的ClearAllSimulations方法
                bool result = LayeredIOManager.Instance().ClearAllSimulations();

                if (result)
                {
                    _uiLogger.InfoRaw("清除所有IO模拟状态");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("IO模拟错误: {0} - {1}", "Clear", ex.Message);
            }
        }
    }
}
