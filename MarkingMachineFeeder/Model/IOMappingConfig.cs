using System;
using System.Collections.Generic;

namespace MarkingMachineFeeder.Model
{
    public class IOMappingConfig
    {
        public string HardwareType { get; set; }
        public string ConnectionString { get; set; }
        public List<IOMapping> InputMappings { get; set; }
        public List<IOMapping> OutputMappings { get; set; }
        public Dictionary<string, object> SimulatedInputs { get; set; }
        public DateTime SaveTime { get; set; }

        public IOMappingConfig()
        {
            InputMappings = new List<IOMapping>();
            OutputMappings = new List<IOMapping>();
            SimulatedInputs = new Dictionary<string, object>();
            SaveTime = DateTime.Now;
        }
    }
}