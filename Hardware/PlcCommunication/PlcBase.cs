using System;
using PlcCommunication.Interfaces;

namespace PlcCommunication
{
    /// <summary>
    /// Abstract base class for PLC communication implementations
    /// </summary>
    public abstract class PlcBase : IPlcBase
    {
        private bool isConnected = false;
        
        public string? Name { get; set; }
        
        public bool IsConnected => isConnected;

        /// <summary>
        /// Connection string for the PLC
        /// </summary>
        protected string ConnectionString { get; set; } = string.Empty;

        protected PlcBase()
        {
            // Auto-generate name if not provided
            if (this.Name == null)
            {
                Type currentType = this.GetType();
                string typeName = currentType.Name;
                this.Name = typeName + "_" + Guid.NewGuid().ToString().Substring(0, 8);
            }
        }

        public virtual OperateResult Initialize(string connectionString)
        {
            try
            {
                ConnectionString = connectionString;
                return OperateResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                return OperateResult.CreateFailureResult($"Failed to initialize: {ex.Message}");
            }
        }

        public virtual OperateResult Connect()
        {
            isConnected = true;
            return OperateResult.CreateSuccessResult();
        }

        public virtual OperateResult Disconnect()
        {
            isConnected = false;
            return OperateResult.CreateSuccessResult();
        }

        // Abstract methods that must be implemented by derived classes
        public abstract OperateResult<T[]> Read<T>(string address, uint length) where T : unmanaged;
        
        public abstract OperateResult<T> Read<T>(string address) where T : unmanaged;
        
        public abstract OperateResult Write<T>(string address, T[] values) where T : unmanaged;
        
        public abstract OperateResult Write<T>(string address, T value) where T : unmanaged;
        
        public abstract OperateResult<string> ReadString(string address, uint length);
        
        public abstract OperateResult WriteString(string address, string value);

        /// <summary>
        /// Validate address format (can be overridden by derived classes)
        /// </summary>
        protected virtual bool ValidateAddress(string address)
        {
            return !string.IsNullOrWhiteSpace(address);
        }

        /// <summary>
        /// Log message (can be overridden for custom logging)
        /// </summary>
        protected virtual void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{Name}] {message}");
        }

        /// <summary>
        /// Log error message
        /// </summary>
        protected virtual void LogError(string message)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{Name}] ERROR: {message}");
        }
    }
}