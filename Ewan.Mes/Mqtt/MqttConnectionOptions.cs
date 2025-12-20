/*****************************************************
** 命名空间: Ewan.Mes.Mqtt
** 文 件 名：MqttConnectionOptions
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
    public class MqttConnectionOptions
    {
        public MqttConnectionOptions()
        {
            BrokerHost = "localhost";
            BrokerPort = 1883;
            ClientId = Guid.NewGuid().ToString("N");
            CleanSession = true;
            KeepAliveSeconds = 30;
        }

        public string BrokerHost { get; set; }
        public int BrokerPort { get; set; }
        public string ClientId { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public bool CleanSession { get; set; }
        public ushort KeepAliveSeconds { get; set; }

        /// <summary>
        /// 库内部错误的回调（由宿主程序赋值）
        /// 参数1: 错误描述, 参数2: 异常对象(可选)
        /// </summary>
        public Action<string, Exception> OnError { get; set; }

        /// <summary>
        /// 库运行日志的回调（可选，用于调试）
        /// </summary>
        public Action<string> OnLog { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(BrokerHost))
            {
                throw new ArgumentException("BrokerHost is required", "BrokerHost");
            }

            if (BrokerPort <= 0)
            {
                throw new ArgumentOutOfRangeException("BrokerPort");
            }

            if (string.IsNullOrWhiteSpace(ClientId))
            {
                throw new ArgumentException("ClientId is required", "ClientId");
            }
        }
    }
}
