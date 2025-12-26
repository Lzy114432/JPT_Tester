using EwanCore.Plc.Cmd.Interface;
using EwanCore.Plc.Cmd.Receiver;
using EwanModel;

namespace EwanCore.Plc.Cmd
{
    /// <summary>
    /// 通用命令：将“执行模型/撤销模型”交给 <see cref="PlcCmdReceiver"/> 写入。
    /// </summary>
    /// <typeparam name="T">PLC 参数模型类型。</typeparam>
    public class CommonCommand<T> : IPlcCommand where T : PlcBaseModel
    {
        private readonly PlcCmdReceiver _receiver;
        private readonly T _exeParamModel;
        private readonly T _unDoParamModel;

        /// <summary>
        /// 创建命令。
        /// </summary>
        /// <param name="receiver">写入接收者。</param>
        /// <param name="exeParamModel">执行时写入的模型。</param>
        /// <param name="unDoParamModel">撤销时写入的模型。</param>
        public CommonCommand(PlcCmdReceiver receiver, T exeParamModel, T unDoParamModel)
        {
            _receiver = receiver;
            _exeParamModel = exeParamModel;
            _unDoParamModel = unDoParamModel;
        }

        /// <inheritdoc />
        public bool Exec()
        {
            return _receiver.Exec(_exeParamModel);
        }

        /// <inheritdoc />
        public bool Undo()
        {
            return _receiver.Exec(_unDoParamModel);
        }
    }
}
