using System;
using PlcCommunication;
using PlcCommunication.Interfaces;
using PlcCommunication.Implementations.MCProtocol;
using PlcCommunication.Implementations.Simulated;

namespace PlcCommunication.Examples
{
    public class PlcUsageExample
    {
        public static void RunExample()
        {
            Console.WriteLine("=== PLC Communication Framework Example ===\n");

            // Example 1: Using Simulated PLC
            Console.WriteLine("1. Simulated PLC Example:");
            RunSimulatedPlcExample();
            
            Console.WriteLine("\n2. MC Protocol PLC Example:");
            RunMCProtocolExample();
            
            Console.WriteLine("\n3. Generic Interface Usage:");
            RunGenericPlcExample();
        }

        static void RunSimulatedPlcExample()
        {
            var plc = new SimulatedPlc();
            
            // Initialize and connect
            var initResult = plc.Initialize("simulated://localhost");
            if (!initResult.Success)
            {
                Console.WriteLine($"Failed to initialize: {initResult.ErrorMessage}");
                return;
            }

            var connectResult = plc.Connect();
            if (!connectResult.Success)
            {
                Console.WriteLine($"Failed to connect: {connectResult.ErrorMessage}");
                return;
            }

            // Write and read data
            var writeResult = plc.Write("D100", new short[] { 100, 200, 300 });
            Console.WriteLine($"Write result: {writeResult.Success}");

            var readResult = plc.Read<short>("D100", 3);
            if (readResult.Success)
            {
                Console.WriteLine($"Read data: [{string.Join(", ", readResult.Data)}]");
            }

            // Write and read string
            var stringWriteResult = plc.WriteString("D200", "Hello PLC!");
            Console.WriteLine($"String write result: {stringWriteResult.Success}");

            var stringReadResult = plc.ReadString("D200", 10);
            if (stringReadResult.Success)
            {
                Console.WriteLine($"Read string: {stringReadResult.Data}");
            }

            // Disconnect
            plc.Disconnect();
        }

        static void RunMCProtocolExample()
        {
            var plc = new MCProtocolPlc();
            
            // Initialize with MC protocol connection string
            var initResult = plc.Initialize("192.168.1.100:5000");
            Console.WriteLine($"MC Protocol initialization: {initResult.Success}");
            
            // Note: Actual connection would require a real PLC
            // This is just showing the API usage
        }

        static void RunGenericPlcExample()
        {
            // Using the interface allows switching implementations easily
            IPlcBase plc = CreatePlc("simulated");
            
            plc.Initialize("test://localhost");
            plc.Connect();
            
            // Generic read/write operations
            var data = new float[] { 3.14f, 2.718f, 1.414f };
            plc.Write("D300", data);
            
            var result = plc.Read<float>("D300", 3);
            if (result.Success)
            {
                Console.WriteLine($"Generic PLC read: [{string.Join(", ", result.Data)}]");
            }
            
            plc.Disconnect();
        }

        static IPlcBase CreatePlc(string type)
        {
            switch (type.ToLower())
            {
                case "mc":
                case "mcprotocol":
                    return new MCProtocolPlc();
                    
                case "simulated":
                case "sim":
                default:
                    return new SimulatedPlc();
            }
        }
    }
}