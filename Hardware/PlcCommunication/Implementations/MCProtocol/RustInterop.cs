using System;
using System.Runtime.InteropServices;

namespace PlcCommunication.Implementations.MCProtocol
{
    internal static class RustInterop
    {
        public const string RustDll = "communication.dll";

        [DllImport(RustDll, EntryPoint = "init", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr Init(IntPtr address);

        [DllImport(RustDll, EntryPoint = "open", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr Open();

        [DllImport(RustDll, EntryPoint = "close", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr Close();

        [DllImport(RustDll, EntryPoint = "destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Destroy();

        [DllImport(RustDll, EntryPoint = "read_u16s", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ReadUshorts(IntPtr address, IntPtr result, uint len);

        [DllImport(RustDll, EntryPoint = "write_u16s", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr WriteUshorts(IntPtr address, IntPtr values, uint len);

        [DllImport(RustDll, EntryPoint = "read_i16s", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ReadShorts(IntPtr address, IntPtr result, uint len);

        [DllImport(RustDll, EntryPoint = "write_i16s", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr WriteShorts(IntPtr address, IntPtr values, uint len);

        [DllImport(RustDll, EntryPoint = "read_u32s", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ReadUints(IntPtr address, IntPtr result, uint len);

        [DllImport(RustDll, EntryPoint = "write_u32s", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr WriteUints(IntPtr address, IntPtr values, uint len);

        [DllImport(RustDll, EntryPoint = "read_i32s", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ReadInts(IntPtr address, IntPtr result, uint len);

        [DllImport(RustDll, EntryPoint = "write_i32s", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr WriteInts(IntPtr address, IntPtr values, uint len);

        [DllImport(RustDll, EntryPoint = "read_f32s", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ReadFloats(IntPtr address, IntPtr result, uint len);

        [DllImport(RustDll, EntryPoint = "write_f32s", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr WriteFloats(IntPtr address, IntPtr values, uint len);

        [DllImport(RustDll, EntryPoint = "read_f64s", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ReadDoubles(IntPtr address, IntPtr result, uint len);

        [DllImport(RustDll, EntryPoint = "write_f64s", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr WriteDoubles(IntPtr address, IntPtr values, uint len);

        [DllImport(RustDll, EntryPoint = "read_bools", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ReadBools(IntPtr address, IntPtr result, uint len);

        [DllImport(RustDll, EntryPoint = "write_bools", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr WriteBools(IntPtr address, IntPtr values, uint len);

        [DllImport(RustDll, EntryPoint = "read_u8s", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ReadBytes(IntPtr address, IntPtr result, uint len);

        [DllImport(RustDll, EntryPoint = "write_u8s", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr WriteBytes(IntPtr address, IntPtr values, uint len);

        [DllImport(RustDll, EntryPoint = "read_big_string", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ReadBigString(IntPtr address, IntPtr result, uint len);

        [DllImport(RustDll, EntryPoint = "write_big_string", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr WriteBigString(IntPtr address, IntPtr value);

        [DllImport(RustDll, EntryPoint = "read_little_string", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ReadLittleString(IntPtr address, IntPtr result, uint len);

        [DllImport(RustDll, EntryPoint = "write_little_string", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr WriteLittleString(IntPtr address, IntPtr value);
    }
}