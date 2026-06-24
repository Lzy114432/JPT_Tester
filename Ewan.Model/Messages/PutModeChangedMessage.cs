using EwanCore.Messaging;
using Ewan.Model.System;
using System;

namespace Ewan.Model.Messages
{
    /// <summary>
    /// 通知放车模式变化（true = 放3空1，false = 放1空4）
    /// </summary>
    public sealed class PutModeChangedMessage : IMessage
    {
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
        public bool IsPut3Empty1 { get; }
        public PutModeChangedMessage(bool isPut3Empty1)
        {
            IsPut3Empty1 = isPut3Empty1;
        }
    }
}