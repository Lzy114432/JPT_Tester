using System;

namespace PlcCommunication.Helpers
{
    public static class InteropHelpers
    {
        public delegate IntPtr RustReadMethod(IntPtr addressPtr, IntPtr bufferPtr, uint len);
        public delegate IntPtr RustWriteMethod(IntPtr addressPtr, IntPtr bufferPtr, uint len);
        public delegate IntPtr RustReadStringMethod(IntPtr addressPtr, IntPtr dataPtr, uint length);
        public delegate IntPtr RustWriteStringMethod(IntPtr addressPtr, IntPtr valuePtr);
    }
}