namespace IOLibrary.Core.Interfaces
{
    /// <summary>
    /// IO硬件接口真实层
    /// </summary>
    public interface IHardwareIO
    {
        string HardwareType { get; }  // "PLC", "IOC0640", "Simulate"
        string ConnectionInfo { get; }
        bool IsConnected { get; }

        int InputCount { get; }
        int OutputCount { get; }

        // 连接管理
        bool Connect(string connectionString);
        bool Disconnect();

        void DataSync();

        bool ReadInBit(int bit);
        bool ReadOutBit(int bit);
        bool WriteOutBit(int bit, bool sts);
        bool ReadRisingBit(int bit);
        void ClearRisingBit(int bit);
        bool ReadFallingBit(int bit);
        void ClearFallingBit(int bit);
    }
}