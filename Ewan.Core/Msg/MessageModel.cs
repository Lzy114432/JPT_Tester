using Ewan.Core.Utils;

namespace Ewan.Core.Msg
{
    public class MessageModel
    {
        public MsgSubject Subject { get; set; }

        public string Time { get; set; }

        public object Data { get; set; }

        public MessageModel(MsgSubject subject, object data)
        {
            Subject = subject;
            Data = data;
            Time = DateTimeUtil.GetNowTimestamp();
        }

        public T GetData<T>()
        {
            return (T)Data;
        }
    }
}
