/*****************************************************
** 命名空间: Ewan.Mes.Mqtt
** 文 件 名：NewtonsoftPayloadSerializer
** 内容简述：
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/3 17:59:41
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
using Newtonsoft.Json;

namespace Ewan.Mes.Mqtt
{
    /// <summary>
    /// 基于 Newtonsoft.Json 的 MQTT 消息序列化器
    /// </summary>
    public class NewtonsoftPayloadSerializer : IMqttPayloadSerializer
    {
        /// <summary>
        /// 将对象序列化为 JSON 字符串
        /// </summary>
        public string Serialize<T>(T payload)
        {
            return JsonConvert.SerializeObject(payload);
        }

        /// <summary>
        /// 将 JSON 字符串反序列化为指定类型对象
        /// </summary>
        public T Deserialize<T>(string payload)
        {
            return JsonConvert.DeserializeObject<T>(payload);
        }
    }
}
