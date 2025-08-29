using IOLibrary.Core.Models;
using System;
using System.Collections.Generic;

namespace IOLibrary.Core.Layered
{
    /// <summary>
    /// 分层IO配置
    /// </summary>
    public class LayeredIOConfiguration
    {
        public string HardwareType { get; set; }
        public string ConnectionString { get; set; }
        public List<IOMapping> InputMappings { get; set; } = new();
        public List<IOMapping> OutputMappings { get; set; } = new();
        public Dictionary<int, SimulateMode> SimulatedInputs { get; set; } = new();
        public DateTime SaveTime { get; set; }
    }
}