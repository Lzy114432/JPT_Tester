using System;

namespace PlcCommunication.Interfaces
{
    /// <summary>
    /// Base interface for all PLC communication implementations
    /// </summary>
    public interface IPlcBase
    {
        /// <summary>
        /// Gets the name of the PLC instance
        /// </summary>
        string? Name { get; set; }

        /// <summary>
        /// Gets whether the PLC connection is open
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Initialize the PLC with connection parameters
        /// </summary>
        /// <param name="connectionString">Connection string containing IP, port, and other parameters</param>
        /// <returns>Operation result</returns>
        OperateResult Initialize(string connectionString);

        /// <summary>
        /// Open connection to the PLC
        /// </summary>
        /// <returns>Operation result</returns>
        OperateResult Connect();

        /// <summary>
        /// Close connection to the PLC
        /// </summary>
        /// <returns>Operation result</returns>
        OperateResult Disconnect();

        /// <summary>
        /// Read data from PLC
        /// </summary>
        /// <typeparam name="T">Data type to read</typeparam>
        /// <param name="address">PLC address</param>
        /// <param name="length">Number of elements to read</param>
        /// <returns>Operation result with data</returns>
        OperateResult<T[]> Read<T>(string address, uint length) where T : unmanaged;

        /// <summary>
        /// Read single value from PLC
        /// </summary>
        /// <typeparam name="T">Data type to read</typeparam>
        /// <param name="address">PLC address</param>
        /// <returns>Operation result with data</returns>
        OperateResult<T> Read<T>(string address) where T : unmanaged;

        /// <summary>
        /// Write data to PLC
        /// </summary>
        /// <typeparam name="T">Data type to write</typeparam>
        /// <param name="address">PLC address</param>
        /// <param name="values">Values to write</param>
        /// <returns>Operation result</returns>
        OperateResult Write<T>(string address, T[] values) where T : unmanaged;

        /// <summary>
        /// Write single value to PLC
        /// </summary>
        /// <typeparam name="T">Data type to write</typeparam>
        /// <param name="address">PLC address</param>
        /// <param name="value">Value to write</param>
        /// <returns>Operation result</returns>
        OperateResult Write<T>(string address, T value) where T : unmanaged;

        /// <summary>
        /// Read string from PLC
        /// </summary>
        /// <param name="address">PLC address</param>
        /// <param name="length">String length</param>
        /// <returns>Operation result with string</returns>
        OperateResult<string> ReadString(string address, uint length);

        /// <summary>
        /// Write string to PLC
        /// </summary>
        /// <param name="address">PLC address</param>
        /// <param name="value">String value</param>
        /// <returns>Operation result</returns>
        OperateResult WriteString(string address, string value);
    }
}