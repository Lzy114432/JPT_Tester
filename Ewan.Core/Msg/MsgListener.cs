using System;

namespace Ewan.Core.Msg
{
    public class MsgListener
    {
        private Action<MessageModel> _updateAction;

        public MsgSubject Subject { get; set; }

        public MsgListener(MsgSubject subject, Action<MessageModel> updateAction)
        {
            Subject = subject;
            _updateAction = updateAction;
        }

        public void Update(MessageModel msg)
        {
            _updateAction(msg);
        }

    }
}
