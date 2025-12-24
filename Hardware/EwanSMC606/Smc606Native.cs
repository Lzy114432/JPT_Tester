using System.Runtime.InteropServices;

namespace EwanSMC606
{
    internal static class Smc606Native
    {
        private const string DllName = "LTSMC.dll";

        [DllImport(DllName)]
        public static extern short smc_board_init(ushort ConnectNo, ushort ConnectType, string pconnectstring, uint baud);

        [DllImport(DllName)]
        public static extern short smc_board_close(ushort ConnectNo);
    }
}

