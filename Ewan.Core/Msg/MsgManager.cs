using Ewan.Core.Attribute;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ewan.Core.Msg
{
    [Manager(Priority = 2)]
    public class MsgManager : BaseManager<MsgManager>
    {
        private BlockingCollection<MessageModel> _queue = new BlockingCollection<MessageModel>(100);

        private List<MsgListener> _listeners = new List<MsgListener>();

        private bool _isAlive;

        public override bool Init()
        {
            base.Init();
            _isAlive = true;
            Task.Factory.StartNew(() =>
            {
                while (_isAlive)
                {
                    try
                    {
                        var msg = _queue.Take();
                        NotifyListeners(msg);
                    }
                    catch (Exception ex)
                    {
                        _uiLogger.Warn(() => Ewan.Resources.LogMessages.MsgManagerProcessError, ex.StackTrace);
                    }
                }
            }, TaskCreationOptions.LongRunning);

            return true;
        }

        public override void Destroy()
        {
            _isAlive = false;
            base.Destroy();
        }

        public void RegisterListener(MsgListener listener)
        {
            _listeners.Add(listener);
        }

        public void UnRegisterListener(MsgListener listener)
        {
            if (_listeners.Contains(listener))
            {
                _listeners.Remove(listener);
            }
        }

        public void PushMsg(MessageModel msg)
        {
            //add 方法会阻塞(如果队列满),所以换成TryAdd方法,满的时候会返回false
            var r = _queue.TryAdd(msg);
            if (!r)
            {
                //队列已满
                _uiLogger.Warn(() => Ewan.Resources.LogMessages.MsgManagerQueueFull);
            }
        }
        private void NotifyListeners(MessageModel msg)
        {
            foreach (MsgListener listener in _listeners)
            {
                if (listener.Subject.Equals(msg.Subject))
                {
                    listener.Update(msg);
                }
            }
        }
    }
}
