using System;

namespace EwanCore.Messaging
{
    /// <summary>
    /// 供扩展方法/外部组件上报处理器异常（用于 SubscribeAsync 等场景）。
    /// </summary>
    public interface IMessageBusExceptionReporter
    {
        void ReportHandlerException(Exception exception, IMessage message, Delegate handler);
    }
}

