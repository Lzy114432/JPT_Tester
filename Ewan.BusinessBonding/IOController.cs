using Ewan.Core;
using Ewan.Core.IO;
using System;

namespace Ewan.BusinessBonding
{
    public class IOController : BaseManager<IOController>
    {
        public void WriteOutput(int index, bool value, bool useMapping = true)
        {
            try
            {
                // 使用LayeredIOManager的WriteOutput方法
                // 根据当前映射模式决定是否使用映射
                bool result = LayeredIOManager.Instance().WriteOutput(index, value, useMapping);
                
                if (result)
                {
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.IOWriteSuccess, $"Y{index}", value ? "ON" : "OFF");
                }
                else
                {
                    _uiLogger.Warn(() => Ewan.Resources.LogMessages.IOWriteFailed, $"Y{index}");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.IOWriteError, $"Y{index}", ex.Message);
                //return false;
            }
        }
        }
}
