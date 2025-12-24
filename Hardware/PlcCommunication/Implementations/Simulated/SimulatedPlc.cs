using System;
using System.Collections.Generic;
using System.Linq;

namespace PlcCommunication.Implementations.Simulated
{
    /// <summary>
    /// Simulated PLC for testing purposes
    /// </summary>
    public class SimulatedPlc : PlcBase
    {
        private readonly Dictionary<string, object> memory = new Dictionary<string, object>();
        private readonly Random random = new Random();

        public SimulatedPlc() : base()
        {
            this.Name = "SimulatedPLC";
        }

        public override OperateResult Initialize(string connectionString)
        {
            Log($"Initializing simulated PLC with: {connectionString}");
            // Initialize some default values
            InitializeDefaultValues();
            return base.Initialize(connectionString);
        }

        public override OperateResult Connect()
        {
            Log("Connecting to simulated PLC...");
            System.Threading.Thread.Sleep(100); // Simulate connection delay
            return base.Connect();
        }

        public override OperateResult Disconnect()
        {
            Log("Disconnecting from simulated PLC...");
            return base.Disconnect();
        }

        public override OperateResult<T[]> Read<T>(string address, uint length)
        {
            if (!IsConnected)
                return OperateResult<T[]>.CreateFailureResult("PLC is not connected");

            if (!ValidateAddress(address))
                return OperateResult<T[]>.CreateFailureResult("Invalid address");

            try
            {
                T[] result = new T[length];
                Type type = typeof(T);

                for (int i = 0; i < length; i++)
                {
                    string key = $"{address}[{i}]";
                    
                    if (memory.ContainsKey(key))
                    {
                        result[i] = (T)memory[key];
                    }
                    else
                    {
                        // Generate default values based on type
                        result[i] = GenerateDefaultValue<T>();
                        memory[key] = result[i];
                    }
                }

                Log($"Read {length} {type.Name} values from {address}");
                return OperateResult<T[]>.CreateSuccessResult(result);
            }
            catch (Exception ex)
            {
                LogError($"Failed to read from {address}: {ex.Message}");
                return OperateResult<T[]>.CreateFailureResult($"Read failed: {ex.Message}");
            }
        }

        public override OperateResult<T> Read<T>(string address)
        {
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

            if (!ValidateAddress(address))
                return OperateResult.CreateFailureResult("Invalid address");

            try
            {
                for (int i = 0; i < values.Length; i++)
                {
                    string key = $"{address}[{i}]";
                    memory[key] = values[i];
                }

                Log($"Wrote {values.Length} {typeof(T).Name} values to {address}");
                return OperateResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                LogError($"Failed to write to {address}: {ex.Message}");
                return OperateResult.CreateFailureResult($"Write failed: {ex.Message}");
            }
        }

        public override OperateResult Write<T>(string address, T value)
        {
            return Write(address, new T[] { value });
        }

        public override OperateResult<string> ReadString(string address, uint length)
        {
            if (!IsConnected)
                return OperateResult<string>.CreateFailureResult("PLC is not connected");

            if (!ValidateAddress(address))
                return OperateResult<string>.CreateFailureResult("Invalid address");

            try
            {
                string key = $"{address}_string";
                
                if (memory.ContainsKey(key))
                {
                    string value = (string)memory[key];
                    if (value.Length > length)
                        value = value.Substring(0, (int)length);
                    
                    Log($"Read string from {address}: {value}");
                    return OperateResult<string>.CreateSuccessResult(value);
                }
                else
                {
                    // Generate random string
                    string value = GenerateRandomString((int)length);
                    memory[key] = value;
                    return OperateResult<string>.CreateSuccessResult(value);
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to read string from {address}: {ex.Message}");
                return OperateResult<string>.CreateFailureResult($"Read string failed: {ex.Message}");
            }
        }

        public override OperateResult WriteString(string address, string value)
        {
            if (!IsConnected)
                return OperateResult.CreateFailureResult("PLC is not connected");

            if (!ValidateAddress(address))
                return OperateResult.CreateFailureResult("Invalid address");

            try
            {
                string key = $"{address}_string";
                memory[key] = value;
                
                Log($"Wrote string to {address}: {value}");
                return OperateResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                LogError($"Failed to write string to {address}: {ex.Message}");
                return OperateResult.CreateFailureResult($"Write string failed: {ex.Message}");
            }
        }

        private void InitializeDefaultValues()
        {
            // Initialize some test data
            memory["D100[0]"] = (short)100;
            memory["D100[1]"] = (short)200;
            memory["D100[2]"] = (short)300;
            
            memory["M0[0]"] = true;
            memory["M0[1]"] = false;
            memory["M0[2]"] = true;
            
            memory["D200[0]"] = 3.14159f;
            memory["D200[1]"] = 2.71828f;
            
            memory["D300_string"] = "Hello PLC World!";
        }

        private T GenerateDefaultValue<T>()
        {
            Type type = typeof(T);
            
            if (type == typeof(bool))
                return (T)(object)(random.Next(2) == 1);
            else if (type == typeof(byte))
                return (T)(object)(byte)random.Next(256);
            else if (type == typeof(short))
                return (T)(object)(short)random.Next(-32768, 32768);
            else if (type == typeof(ushort))
                return (T)(object)(ushort)random.Next(65536);
            else if (type == typeof(int))
                return (T)(object)random.Next();
            else if (type == typeof(uint))
                return (T)(object)(uint)random.Next();
            else if (type == typeof(float))
                return (T)(object)((float)random.NextDouble() * 100f);
            else if (type == typeof(double))
                return (T)(object)(random.NextDouble() * 1000.0);
            else
                return default(T);
        }

        private string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        /// <summary>
        /// Get all memory contents (for debugging)
        /// </summary>
        public Dictionary<string, object> GetMemoryContents()
        {
            return new Dictionary<string, object>(memory);
        }

        /// <summary>
        /// Clear all memory
        /// </summary>
        public void ClearMemory()
        {
            memory.Clear();
            Log("Memory cleared");
        }
    }
}