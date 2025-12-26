using EwanCommon.Logging;
using EwanModel;
using EwanModel.Plc.Interfaces;
using log4net;
using System;

namespace EwanCore.Plc.Cmd.Receiver
{
    /// <summary>
    /// PLC 写入命令接收者：通过委托注入写入实现（便于 DI/替换不同 PLC 驱动）
    /// </summary>
    public sealed class PlcCmdReceiver
    {
        private readonly ILog _logger = Log.GetLogger(typeof(PlcCmdReceiver));

        private readonly Func<string, byte[], IOperateResult> _writeBytes;
        private readonly Func<string, bool, IOperateResult> _writeBool;

        /// <summary>
        /// 创建一个接收者。
        /// </summary>
        /// <param name="writeBytes">写入字节（word/数值/字符串）。</param>
        /// <param name="writeBool">写入 bool。</param>
        public PlcCmdReceiver(
            Func<string, byte[], IOperateResult> writeBytes,
            Func<string, bool, IOperateResult> writeBool)
        {
            _writeBytes = writeBytes ?? throw new ArgumentNullException(nameof(writeBytes));
            _writeBool = writeBool ?? throw new ArgumentNullException(nameof(writeBool));
        }

        /// <summary>
        /// 执行一次写入。
        /// </summary>
        /// <param name="paramModel">要写入的 PLC 参数模型。</param>
        /// <returns>是否写入成功。</returns>
        public bool Exec(PlcBaseModel paramModel)
        {
            if (paramModel == null) return false;

            if (paramModel.SetKeyenceParam(_writeBytes, _writeBool))
            {
                _logger.Info("exec cmd succeed...");
                return true;
            }

            _logger.Info("exec cmd failed...");
            return false;
        }
    }
}
