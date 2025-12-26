namespace EwanCore.Plc.Cmd.Interface
{
    /// <summary>
    /// PLC 命令接口（支持执行/撤销）。
    /// </summary>
    public interface IPlcCommand
    {
        /// <summary>
        /// 执行命令。
        /// </summary>
        /// <returns>是否执行成功。</returns>
        bool Exec();

        /// <summary>
        /// 撤销命令。
        /// </summary>
        /// <returns>是否撤销成功。</returns>
        bool Undo();
    }
}
