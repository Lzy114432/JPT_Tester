namespace EwanModel.Plc.Interfaces
{
    /// <summary>
    /// 操作结果（是否成功 + 错误信息）。
    /// </summary>
    public interface IOperateResult
    {
        /// <summary>
        /// 是否成功。
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// 错误信息（成功时可为空）。
        /// </summary>
        string ErrorMessage { get; }
    }

    /// <summary>
    /// 带返回数据的操作结果。
    /// </summary>
    /// <typeparam name="T">数据类型。</typeparam>
    public interface IOperateResult<T> : IOperateResult
    {
        /// <summary>
        /// 返回数据。
        /// </summary>
        T Data { get; }
    }
}
