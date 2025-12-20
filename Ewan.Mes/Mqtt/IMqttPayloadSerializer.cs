/*****************************************************
** 命名空间: Ewan.Mes.Mqtt
** 文 件 名：IMqttPayloadSerializer
** 内容简述：
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/3 17:59:41
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
namespace Ewan.Mes.Mqtt
{
    public interface IMqttPayloadSerializer
    {
        string Serialize<T>(T payload);
        T Deserialize<T>(string payload);
    }
}
