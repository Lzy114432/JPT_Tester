using EwanModel.Plc.Interfaces;

namespace EwanModel.Plc
{
    /// <summary>
    /// <see cref="IOperateResult"/> 的默认实现。
    /// </summary>
    public class OperateResult : IOperateResult
    {
        /// <inheritdoc />
        public bool Success { get; set; }

        /// <inheritdoc />
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 创建一个成功结果。
        /// </summary>
        public static OperateResult Ok()
        {
            return new OperateResult { Success = true };
        }

        /// <summary>
        /// 创建一个失败结果。
        /// </summary>
        /// <param name="errorMessage">错误信息。</param>
        public static OperateResult Fail(string errorMessage)
        {
            return new OperateResult { Success = false, ErrorMessage = errorMessage };
        }
    }

    /// <summary>
    /// 带返回数据的操作结果实现。
    /// </summary>
    /// <typeparam name="T">数据类型。</typeparam>
    public class OperateResult<T> : OperateResult, IOperateResult<T>
    {
        /// <inheritdoc />
        public T Data { get; set; }

        /// <summary>
        /// 创建一个成功结果。
        /// </summary>
        /// <param name="data">返回数据。</param>
        public static OperateResult<T> Ok(T data)
        {
            return new OperateResult<T> { Success = true, Data = data };
        }

        /// <summary>
        /// 创建一个失败结果。
        /// </summary>
        /// <param name="errorMessage">错误信息。</param>
        public new static OperateResult<T> Fail(string errorMessage)
        {
            return new OperateResult<T> { Success = false, ErrorMessage = errorMessage };
        }
    }
}
