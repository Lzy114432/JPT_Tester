using System;

namespace EwanCore.Messaging
{
    /// <summary>
    /// Subscribe 语义的消息总线接口。
    /// </summary>
    public interface ISubscribeBus
    {
        /// <summary>
        /// 订阅指定消息类型（支持订阅基类/接口：发布派生类型时也会收到）。
        /// </summary>
        /// <remarks>返回的 <see cref="IDisposable"/> 用于取消订阅。</remarks>
        IDisposable Subscribe<TMessage>(Action<TMessage> handler) where TMessage : IMessage;

        /// <summary>
        /// 弱引用订阅（防泄漏）：当 <paramref name="target"/> 被 GC 回收后，订阅会自动失效并在后续发布中被清理。
        /// </summary>
        /// <remarks>
        /// 推荐写法：<c>bus.SubscribeWeak(this, (me, msg) =&gt; me.OnMsg(msg));</c>
        /// （lambda 不要捕获 <paramref name="target"/>，用第一个参数 <c>me</c> 访问实例即可）。
        /// </remarks>
        IDisposable SubscribeWeak<TTarget, TMessage>(
            TTarget target,
            Action<TTarget, TMessage> handler)
            where TTarget : class
            where TMessage : IMessage;

        /// <summary>
        /// 弱引用订阅便捷重载：适合直接传实例方法组（method group），例如 <c>bus.SubscribeWeak(this, OnMsg);</c>。
        /// </summary>
        /// <exception cref="ArgumentException">
        /// 当 <paramref name="handler"/> 不是绑定在 <paramref name="target"/> 上的实例方法时抛出；
        /// 请改用 <see cref="SubscribeWeak{TTarget,TMessage}(TTarget,Action{TTarget,TMessage})"/>。
        /// </exception>
        IDisposable SubscribeWeak<TTarget, TMessage>(
            TTarget target,
            Action<TMessage> handler)
            where TTarget : class
            where TMessage : IMessage;

        /// <summary>
        /// 订阅所有消息（用于日志/监控等）。
        /// </summary>
        IDisposable SubscribeAll(Action<IMessage> handler);
    }
}
