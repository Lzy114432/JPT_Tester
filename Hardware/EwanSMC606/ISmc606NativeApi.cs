namespace EwanSMC606
{
    internal interface ISmc606NativeApi
    {
        short BoardInit(ushort cardNo, ushort connectType, string connectString, uint baudRate);
        short BoardClose(ushort cardNo);
    }

    internal sealed class DllImportSmc606NativeApi : ISmc606NativeApi
    {
        public short BoardInit(ushort cardNo, ushort connectType, string connectString, uint baudRate)
        {
            return Smc606Native.smc_board_init(cardNo, connectType, connectString, baudRate);
        }

        public short BoardClose(ushort cardNo)
        {
            return Smc606Native.smc_board_close(cardNo);
        }
    }
}

