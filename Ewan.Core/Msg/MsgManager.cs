using EwanCore;
using EwanCore.Attribute;
using EwanCore.Messaging;
using EwanCore.Messaging.Messages;
using EwanCommon.Logging;
using Ewan.Model.Messages;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ewan.Core.Msg
{
    /// <summary>
    /// 消息管理器 - 使用 MessageBus 进行消息分发
    /// 保留旧的 listener 模式以兼容现有代码
    /// 推荐新代码直接使用 MessageHub
    /// </summary>
    [Manager(Priority = 2)]
    public class MsgManager : IManager
    {
        private static readonly ILog s_logger = Log.GetLogger(typeof(MsgManager));

        // 旧的 listener 支持（兼容现有代码）
        private readonly BlockingCollection<MessageModel> _queue = new BlockingCollection<MessageModel>(100);
        private readonly List<MsgListener> _listeners = new List<MsgListener>();
        private readonly object _listenersLock = new object();
        private bool _isAlive;
        private bool _disposed;

        // MessageBus 订阅
        private IDisposable _uiLogSubscription;

        #region 单例支持（兼容现有代码）
        private static readonly Lazy<MsgManager> s_instance = new Lazy<MsgManager>(() => new MsgManager());

        /// <summary>
        /// 获取单例实例（兼容现有代码）
        /// </summary>
        public static MsgManager Instance() => s_instance.Value;
        #endregion

        /// <summary>
        /// 实现 IManager.Init - 初始化消息管理器
        /// </summary>
        public bool Init()
        {
            s_logger.Info("MsgManager 初始化开始");

            _isAlive = true;

            // 启动旧的消息处理循环（兼容现有 listener）
            Task.Factory.StartNew(() =>
            {
                while (_isAlive)
                {
                    try
                    {
                        var msg = _queue.Take();
                        NotifyListeners(msg);
                    }
                    catch (InvalidOperationException)
                    {
                        // 队列已完成，正常退出
                        break;
                    }
                    catch (Exception ex)
                    {
                        s_logger.Warn("消息管理器处理错误: " + ex.Message, ex);
                    }
                }
            }, TaskCreationOptions.LongRunning);

            // 订阅 MessageBus 上的 UILogMessage，转发给旧的 listener
            _uiLogSubscription = MessageHub.SubscribeBus.Subscribe<UILogMessage>(OnUILogMessage);

            s_logger.Info("MsgManager 初始化完成");
            return true;
        }

        /// <summary>
        /// 实现 IDisposable.Dispose - 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _isAlive = false;
            s_logger.Info("MsgManager 开始销毁");

            try
            {
                _uiLogSubscription?.Dispose();
                _queue.CompleteAdding();
            }
            catch (Exception ex)
            {
                s_logger.Warn("MsgManager 销毁时发生错误", ex);
            }

            s_logger.Info("MsgManager 销毁完成");
        }

        /// <summary>
        /// 兼容旧代码的 Destroy 方法
        /// </summary>
        [Obsolete("请使用 Dispose() 方法")]
        public void Destroy() => Dispose();

        /// <summary>
        /// 注册消息监听器（兼容旧代码）
        /// 推荐新代码使用 MessageHub.SubscribeBus.Subscribe&lt;T&gt;()
        /// </summary>
        public void RegisterListener(MsgListener listener)
        {
            if (listener == null)
            {
                return;
            }

            lock (_listenersLock)
            {
                _listeners.Add(listener);
            }
        }

        /// <summary>
        /// 取消注册消息监听器（兼容旧代码）
        /// </summary>
        public void UnRegisterListener(MsgListener listener)
        {
            if (listener == null)
            {
                return;
            }

            lock (_listenersLock)
            {
                _listeners.Remove(listener);
            }
        }

        /// <summary>
        /// 推送消息（兼容旧代码）
        /// 推荐新代码使用 MessageHub.PublishBus.Post&lt;T&gt;()
        /// </summary>
        public void PushMsg(MessageModel msg)
        {
            if (_disposed || _queue.IsAddingCompleted)
            {
                return;
            }

            var r = _queue.TryAdd(msg);
            if (!r)
            {
                s_logger.Warn("消息队列已满，丢弃消息");
            }
        }

        /// <summary>
        /// 处理 MessageBus 上的 UILogMessage，转发给旧的 listener
        /// </summary>
        private void OnUILogMessage(UILogMessage message)
        {
            if (message == null)
            {
                return;
            }

            var legacyLevel = ConvertToLegacyLevel(message.Level);
            var legacyMessage = message.Message;

            var legacy = new UILogMsg(legacyLevel, legacyMessage)
            {
                Timestamp = message.Timestamp.LocalDateTime,
            };

            PushMsg(new MessageModel(MsgSubject.UILog, legacy));
        }

        private static LogLevel ConvertToLegacyLevel(UILogLevel level)
        {
            switch (level)
            {
                case UILogLevel.Debug:
                    return LogLevel.Debug;
                case UILogLevel.Info:
                    return LogLevel.Info;
                case UILogLevel.Warn:
                    return LogLevel.Warn;
                case UILogLevel.Error:
                    return LogLevel.Error;
                case UILogLevel.Fatal:
                    return LogLevel.Fatal;
                default:
                    return LogLevel.Info;
            }
        }

        private void NotifyListeners(MessageModel msg)
        {
            MsgListener[] listenersSnapshot;
            lock (_listenersLock)
            {
                listenersSnapshot = _listeners.ToArray();
            }

            foreach (MsgListener listener in listenersSnapshot)
            {
                if (listener != null && listener.Subject.Equals(msg.Subject))
                {
                    try
                    {
                        listener.Update(msg);
                    }
                    catch (Exception ex)
                    {
                        s_logger.Error($"Listener 处理消息失败: {listener.Subject}", ex);
                    }
                }
            }
        }
    }
}
