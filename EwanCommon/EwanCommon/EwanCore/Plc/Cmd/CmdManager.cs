using EwanCore.Plc.Cmd.Interface;
using System;
using System.Threading;

namespace EwanCore.Plc.Cmd
{
    /// <summary>
    /// 命令管理器（支持 Undo/Redo）
    /// </summary>
    public sealed class CmdManager : global::EwanCore.BaseManager<CmdManager>
    {
        private readonly Deque<IPlcCommand> _undoDeque = new Deque<IPlcCommand>(5);
        private readonly Deque<IPlcCommand> _redoDeque = new Deque<IPlcCommand>(5);
        private readonly object _lockObj = new object();

        private CmdManager() { }

        /// <summary>
        /// 执行命令。
        /// </summary>
        /// <param name="command">命令。</param>
        /// <param name="withUndo">是否执行后立即撤销（调试/验证用途）。</param>
        /// <returns>是否执行成功。</returns>
        public bool ExecuteCommand(IPlcCommand command, bool withUndo = false)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            try
            {
                var result = command.Exec();
                lock (_lockObj)
                {
                    _undoDeque.EnqueueLast(command);

                    if (withUndo)
                    {
#if DEBUG
                        Thread.Sleep(100);
#endif
                        result = Undo();
                    }
                }
                return result;
            }
            catch
            {
                return false;
            }
        }

        private bool Undo()
        {
            try
            {
                var cmd = _undoDeque.DequeueLast();
                var result = cmd.Undo();
                _redoDeque.EnqueueLast(cmd);
                return result;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 重做上一次撤销的命令。
        /// </summary>
        public void Redo()
        {
            try
            {
                lock (_lockObj)
                {
                    var cmd = _redoDeque.DequeueLast();
                    cmd.Exec();
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
