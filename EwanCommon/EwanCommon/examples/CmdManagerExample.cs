using EwanCore.Plc.Cmd;
using EwanCore.Plc.Cmd.Receiver;
using EwanModel;
using EwanModel.Plc.Interfaces;
using System;
using System.Reflection;

// 说明：
// - 这是“使用示例文件”，不参与 EwanCommon.csproj 编译。
// - 演示：命令模式（Exec/Undo）+ 委托注入写入实现（便于替换 PLC 驱动、便于测试）。

namespace EwanCommon.Examples
{
    public static class CmdManagerExample
    {
        private sealed class DemoParamModel : PlcBaseModel
        {
            public DemoParamModel()
            {
                var props = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
                mPropertyInfos = props;
                mPropertyInfosOfRead = props;
            }

            [Plc(Prefix = "D", Addr = 0)]
            public short Speed { get; set; }

            [Plc(Prefix = "M", Addr = 100)]
            public bool Enable { get; set; }
        }

        public static void Run()
        {
            Func<string, byte[], IOperateResult> writeBytes = (addr, bytes) =>
            {
                Console.WriteLine($"WriteBytes addr={addr}, len={bytes?.Length ?? 0}");
                return EwanModel.Plc.OperateResult.Ok();
            };

            Func<string, bool, IOperateResult> writeBool = (addr, value) =>
            {
                Console.WriteLine($"WriteBool addr={addr}, value={value}");
                return EwanModel.Plc.OperateResult.Ok();
            };

            var receiver = new PlcCmdReceiver(writeBytes, writeBool);

            var execModel = new DemoParamModel { Speed = 100, Enable = true };
            var undoModel = new DemoParamModel { Speed = 0, Enable = false };

            var cmd = new CommonCommand<DemoParamModel>(receiver, execModel, undoModel);

            // 执行命令，并把命令放入 Undo 栈（可配合 Redo/内部 Undo 使用）
            var ok = CmdManager.Instance().ExecuteCommand(cmd);
            Console.WriteLine($"Exec ok={ok}");

            // 需要重做时（例如 UI 点击“重做”）
            CmdManager.Instance().Redo();
        }
    }
}

