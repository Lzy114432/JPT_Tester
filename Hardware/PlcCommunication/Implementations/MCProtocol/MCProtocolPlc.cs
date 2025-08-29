using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using PlcCommunication.Helpers;
using static PlcCommunication.Helpers.InteropHelpers;

namespace PlcCommunication.Implementations.MCProtocol
{
    /// <summary>
    /// Mitsubishi MC Protocol PLC implementation
    /// </summary>
    public class MCProtocolPlc : PlcBase
    {
        private static readonly Dictionary<Type, RustReadMethod> RustReadMethods = new Dictionary<Type, RustReadMethod>
        {
            { typeof(byte), RustInterop.ReadBytes },
            { typeof(bool), RustInterop.ReadBools },
            { typeof(int), RustInterop.ReadInts },
            { typeof(ushort), RustInterop.ReadUshorts },
            { typeof(short), RustInterop.ReadShorts },
            { typeof(uint), RustInterop.ReadUints },
            { typeof(float), RustInterop.ReadFloats },
            { typeof(double), RustInterop.ReadDoubles }
        };

        private static readonly Dictionary<Type, RustWriteMethod> RustWriteMethods = new Dictionary<Type, RustWriteMethod>
        {
            { typeof(byte), RustInterop.WriteBytes },
            { typeof(bool), RustInterop.WriteBools },
            { typeof(int), RustInterop.WriteInts },
            { typeof(ushort), RustInterop.WriteUshorts },
            { typeof(short), RustInterop.WriteShorts },
            { typeof(uint), RustInterop.WriteUints },
            { typeof(float), RustInterop.WriteFloats },
            { typeof(double), RustInterop.WriteDoubles }
        };

        public MCProtocolPlc() : base()
        {
            this.Name = "MCProtocol";
        }

        public override OperateResult Initialize(string connectionString)
        {
            try
            {
                using (var addressPtr = new UnmanagedPointer(Marshal.StringToHGlobalAnsi(connectionString)))
                {
                    IntPtr result = RustInterop.Init(addressPtr.Pointer);
                    if (result == IntPtr.Zero)
                    {
                        Log($"Initialized with connection string: {connectionString}");
                        return base.Initialize(connectionString);
                    }
                    else
                    {
                        using (var errorPtr = new UnmanagedPointer(result))
                        {
                            string errorMessage = PtrToStringUTF8(errorPtr.Pointer);
                            LogError($"Initialization failed: {errorMessage}");
                            return OperateResult.CreateFailureResult(errorMessage);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Exception during initialization: {ex.Message}");
                return OperateResult.CreateFailureResult($"Initialization failed: {ex.Message}");
            }
        }

        public override OperateResult Connect()
        {
            try
            {
                IntPtr result = RustInterop.Open();
                if (result == IntPtr.Zero)
                {
                    Log("Successfully connected to PLC");
                    return base.Connect();
                }
                else
                {
                    using (var errorPtr = new UnmanagedPointer(result))
                    {
                        string errorMessage = PtrToStringUTF8(errorPtr.Pointer);
                        LogError($"Connection failed: {errorMessage}");
                        return OperateResult.CreateFailureResult(errorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Exception during connection: {ex.Message}");
                return OperateResult.CreateFailureResult($"Connection failed: {ex.Message}");
            }
        }

        public override OperateResult Disconnect()
        {
            try
            {
                IntPtr result = RustInterop.Close();
                if (result == IntPtr.Zero)
                {
                    Log("Successfully disconnected from PLC");
                    
                    // Destroy the Rust library resources
                    RustInterop.Destroy();
                    
                    return base.Disconnect();
                }
                else
                {
                    using (var errorPtr = new UnmanagedPointer(result))
                    {
                        string errorMessage = PtrToStringUTF8(errorPtr.Pointer);
                        LogError($"Disconnection failed: {errorMessage}");
                        return OperateResult.CreateFailureResult(errorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Exception during disconnection: {ex.Message}");
                return OperateResult.CreateFailureResult($"Disconnection failed: {ex.Message}");
            }
        }

        public override OperateResult<T[]> Read<T>(string address, uint length)
        {
            if (!IsConnected)
                return OperateResult<T[]>.CreateFailureResult("PLC is not connected");

            var readMethod = GetRustReadMethod<T>();
            
            // Special handling for byte arrays
            if (typeof(T) == typeof(byte))
            {
                length *= 2;
            }
            
            return ReadData<T>(address, length, readMethod);
        }

        public override OperateResult<T> Read<T>(string address)
        {
            if (typeof(T) == typeof(byte) || typeof(T) == typeof(byte[]))
            {
                return OperateResult<T>.CreateFailureResult("Read byte or byte[] is not supported.");
            }

            var result = Read<T>(address, 1);
            if (result.Success)
                return OperateResult<T>.CreateSuccessResult(result.Data[0]);
            else
                return OperateResult<T>.CreateFailureResult(result.ErrorMessage);
        }

        public override OperateResult Write<T>(string address, T[] values)
        {
            if (!IsConnected)
                return OperateResult.CreateFailureResult("PLC is not connected");

            var writeMethod = GetRustWriteMethod<T>();
            return WriteData<T>(address, values, writeMethod);
        }

        public override OperateResult Write<T>(string address, T value)
        {
            if (typeof(T) == typeof(byte) || typeof(T) == typeof(byte[]))
            {
                return OperateResult.CreateFailureResult("Write byte or byte[] is not supported.");
            }
            
            return Write(address, new T[] { value });
        }

        public override OperateResult<string> ReadString(string address, uint length)
        {
            return ReadString(address, length, false);
        }

        public OperateResult<string> ReadString(string address, uint length, bool isBigEndian)
        {
            if (!IsConnected)
                return OperateResult<string>.CreateFailureResult("PLC is not connected");

            if (isBigEndian)
                return ReadStringViaRust(address, length, RustInterop.ReadBigString);
            else
                return ReadStringViaRust(address, length, RustInterop.ReadLittleString);
        }

        public override OperateResult WriteString(string address, string value)
        {
            return WriteString(address, value, false);
        }

        public OperateResult WriteString(string address, string value, bool isBigEndian)
        {
            if (!IsConnected)
                return OperateResult.CreateFailureResult("PLC is not connected");

            if (isBigEndian)
                return WriteStringViaRust(address, value, RustInterop.WriteBigString);
            else
                return WriteStringViaRust(address, value, RustInterop.WriteLittleString);
        }

        private unsafe OperateResult<T[]> ReadData<T>(string address, uint len, RustReadMethod interopMethod) where T : unmanaged
        {
            using (var addressPtr = new UnmanagedPointer(Marshal.StringToHGlobalAnsi(address)))
            {
                T[] result = new T[len];
                IntPtr error = IntPtr.Zero;
                try
                {
                    fixed (T* pResult = result)
                    {
                        IntPtr resultPtr = (IntPtr)pResult;
                        error = interopMethod(addressPtr.Pointer, resultPtr, len);
                        if (error == IntPtr.Zero)
                        {
                            Log($"Read {len} {typeof(T).Name} values from {address}");
                            return OperateResult<T[]>.CreateSuccessResult(result);
                        }
                        else
                        {
                            using (var errorPtr = new UnmanagedPointer(error))
                            {
                                string errorMessage = PtrToStringUTF8(errorPtr.Pointer);
                                LogError($"Read failed: {errorMessage}");
                                return OperateResult<T[]>.CreateFailureResult(errorMessage);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Exception during read: {ex.Message}");
                    return OperateResult<T[]>.CreateFailureResult("Failed to read data: " + ex.Message);
                }
            }
        }

        private OperateResult WriteData<T>(string address, T[] data, RustWriteMethod writeMethod) where T : unmanaged
        {
            using (var addressPtr = new UnmanagedPointer(Marshal.StringToHGlobalAnsi(address)))
            using (var pinnedValues = new PinnedArray<T>(data))
            {
                IntPtr result = IntPtr.Zero;
                try
                {
                    result = writeMethod(addressPtr.Pointer, pinnedValues.AddrOfPinnedObject(), (uint)data.Length);
                    if (result == IntPtr.Zero)
                    {
                        Log($"Wrote {data.Length} {typeof(T).Name} values to {address}");
                        return OperateResult.CreateSuccessResult();
                    }
                    else
                    {
                        using (var errorPtr = new UnmanagedPointer(result))
                        {
                            string errorMessage = PtrToStringUTF8(errorPtr.Pointer);
                            LogError($"Write failed: {errorMessage}");
                            return OperateResult.CreateFailureResult(errorMessage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Exception during write: {ex.Message}");
                    return OperateResult.CreateFailureResult("Failed to write data: " + ex.Message);
                }
            }
        }

        private OperateResult<string> ReadStringViaRust(string address, uint len, RustReadStringMethod rustReadStringMethod)
        {
            using (var addressPtr = new UnmanagedPointer(Marshal.StringToHGlobalAnsi(address)))
            using (var buffer = new UnmanagedPointer(Marshal.AllocHGlobal((int)len + 1)))
            {
                IntPtr result = rustReadStringMethod(addressPtr.Pointer, buffer.Pointer, len);

                if (result == IntPtr.Zero)
                {
                    string output = Marshal.PtrToStringAnsi(buffer.Pointer);
                    Log($"Read string from {address}: {output}");
                    return OperateResult<string>.CreateSuccessResult(output);
                }
                else
                {
                    using (var errorPtr = new UnmanagedPointer(result))
                    {
                        string errorMessage = Marshal.PtrToStringAnsi(errorPtr.Pointer);
                        LogError($"Read string failed: {errorMessage}");
                        return OperateResult<string>.CreateFailureResult(errorMessage);
                    }
                }
            }
        }

        private OperateResult WriteStringViaRust(string address, string value, RustWriteStringMethod rustWriteStringMethod)
        {
            using (var addressPtr = new UnmanagedPointer(Marshal.StringToHGlobalAnsi(address)))
            using (var valuePtr = new UnmanagedPointer(Marshal.StringToHGlobalAnsi(value)))
            {
                IntPtr result = rustWriteStringMethod(addressPtr.Pointer, valuePtr.Pointer);
                if (result == IntPtr.Zero)
                {
                    Log($"Wrote string to {address}: {value}");
                    return OperateResult.CreateSuccessResult();
                }
                else
                {
                    using (var errorPtr = new UnmanagedPointer(result))
                    {
                        string errorMessage = Marshal.PtrToStringAnsi(errorPtr.Pointer);
                        LogError($"Write string failed: {errorMessage}");
                        return OperateResult.CreateFailureResult(errorMessage);
                    }
                }
            }
        }

        private RustReadMethod GetRustReadMethod<T>() where T : unmanaged
        {
            if (RustReadMethods.TryGetValue(typeof(T), out RustReadMethod method))
                return method;
            else
                throw new NotSupportedException($"Type {typeof(T)} is not supported.");
        }

        private RustWriteMethod GetRustWriteMethod<T>() where T : unmanaged
        {
            if (RustWriteMethods.TryGetValue(typeof(T), out RustWriteMethod method))
                return method;
            else
                throw new NotSupportedException($"Type {typeof(T)} is not supported.");
        }

        private static string PtrToStringUTF8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;

            int len = 0;
            while (Marshal.ReadByte(ptr, len) != 0)
                len++;

            byte[] array = new byte[len];
            Marshal.Copy(ptr, array, 0, len);
            return Encoding.UTF8.GetString(array);
        }
    }
}