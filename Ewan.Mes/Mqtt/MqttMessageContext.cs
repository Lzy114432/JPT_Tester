/*****************************************************
** 命名空间: Ewan.Mes.Mqtt
** 文 件 名：MqttMessageContext
** 内容简述：
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/3 17:59:41
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
using System;

namespace Ewan.Mes.Mqtt
{
    public class MqttMessageContext<T>
    {
        public MqttMessageContext(string topic, T payload, byte[] rawPayload, byte qosLevel, bool retained, bool duplicate)
        {
            Topic = topic;
            Payload = payload;
            RawPayload = rawPayload;
            QosLevel = qosLevel;
            Retained = retained;
            Duplicate = duplicate;
            ReceivedAt = DateTimeOffset.UtcNow;
        }

        public string Topic { get; private set; }
        public T Payload { get; private set; }
        public byte[] RawPayload { get; private set; }
        public byte QosLevel { get; private set; }
        public bool Retained { get; private set; }
        public bool Duplicate { get; private set; }
        public DateTimeOffset ReceivedAt { get; private set; }
    }
}
