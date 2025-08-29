namespace PlcCommunication
{
    /// <summary>
    /// OperateResult is a class that represents the result of an operation. It is not generic.
    /// </summary>
    public class OperateResult
    {
        public bool Success { get; protected set; }
        public string ErrorMessage { get; protected set; }

        protected OperateResult(bool success, string errorMessage = "")
        {
            Success = success;
            ErrorMessage = errorMessage;
        }

        public static OperateResult CreateSuccessResult()
        {
            return new OperateResult(true);
        }

        public static OperateResult CreateFailureResult(string errorMessage)
        {
            return new OperateResult(false, errorMessage);
        }
    }

    /// <summary>
    /// The generic version inherits from the base class
    /// </summary>
    /// <typeparam name="T">The type of the data to return</typeparam>
    public class OperateResult<T> : OperateResult
    {
        public T Data { get; private set; }

        private OperateResult(bool success, T data, string errorMessage = "") : base(success, errorMessage)
        {
            Data = data;
        }

        public static OperateResult<T> CreateSuccessResult(T data = default(T))
        {
            return new OperateResult<T>(true, data);
        }

        public static new OperateResult<T> CreateFailureResult(string errorMessage)
        {
            return new OperateResult<T>(false, default(T), errorMessage);
        }
    }
}