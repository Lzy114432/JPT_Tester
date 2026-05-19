using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;

namespace Ewan.Mes.Mqtt
{
    public class mqttNet
    {
        // 1. 使用 Lazy 静态化实例，确保线程安全的单例模式
        private static readonly Lazy<mqttNet> _instance = new Lazy<mqttNet>(() => new mqttNet());

        /// <summary>
        /// 获取 mqttNet 的单例实例
        /// </summary>
        public static mqttNet Instance => _instance.Value;

        // 2. 将普通的 Dictionary 替换为 ConcurrentDictionary 保证跨线程安全更新
        public ConcurrentDictionary<string, string> DeviceDictionary { get; } = new ConcurrentDictionary<string, string>();

        private IMqttClient _mqttClient;
        private MqttClientOptions _options; // 提取为全局变量，供重连时使用

        // 3. 私有化构造函数，防止外部 new 实例化
        private mqttNet()
        {
        }

        /// <summary>
        /// 启动连接并订阅 (在程序初始化时调用一次即可，例如 await mqttNet.Instance.StartAsync();)
        /// </summary>
        public async Task StartAsync()
        {
            if (_mqttClient != null && _mqttClient.IsConnected)
                return;

            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            // 配置连接参数
            _options = new MqttClientOptionsBuilder()
                         .WithTcpServer("172.24.10.21", 1883)
                         .WithCredentials("6055dbb98b0e418aba9514f43964e74e:f3d94d25ef3d42b2ae133bd976f26b4b",
                                          "f1f51b1c1c9d47eeb64b8be1658a8034")
                         .WithClientId("MarkingMachineFeeder_" + Guid.NewGuid().ToString()) // 明确指定唯一客户端ID，避免ID冲突被踢下线
                         .WithKeepAlivePeriod(TimeSpan.FromSeconds(30)) // 明确设置30秒心跳保活包，防止被交换机/防火墙断开
                         .Build();

            // 注册消息接收事件
            _mqttClient.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;

            // 注册断线重连事件机制
            _mqttClient.DisconnectedAsync += OnDisconnectedAsync;

            await ConnectAndSubscribeInnerAsync();
        }

        /// <summary>
        /// 内部用来执行连接和订阅的逻辑单元
        /// </summary>
        private async Task ConnectAndSubscribeInnerAsync()
        {
            try
            {
                // 连接
                if (!_mqttClient.IsConnected)
                {
                    await _mqttClient.ConnectAsync(_options);
                    System.Diagnostics.Debug.WriteLine("[MQTT] 服务器连接/重连成功。");
                }

                // 指定订阅的主题 
                var topicFilter = new MqttTopicFilterBuilder()
                    .WithTopic("/device/c3030bd6dfe94dc48be471b438985c3b/down/feeding_unloading_state_response/Z-JQ-S-101-001")
                    .Build();

                var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(topicFilter)
                    .Build();

                // 核心：每次连接成功后都必须重新发送订阅指令
                await _mqttClient.SubscribeAsync(subscribeOptions);
                System.Diagnostics.Debug.WriteLine("[MQTT] 主题订阅成功。");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MQTT 异常] 连接或订阅失败: {ex.Message}");
                throw; // 抛出异常由重连循环捕获
            }
        }

        /// <summary>
        /// 断线重连机制
        /// </summary>
        private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[MQTT 掉线] 与服务器的连接已断开。原因: {e.Reason}。开始自动重连...");

            // 开启一个无限循环，直到再次连上为止
            while (true)
            {
                try
                {
                    // 延迟 5 秒后重试，避免死循环消耗过多 CPU
                    await Task.Delay(TimeSpan.FromSeconds(1));

                    if (!_mqttClient.IsConnected)
                    {
                        System.Diagnostics.Debug.WriteLine("[MQTT 重连] 尝试重新连接服务器...");
                        await ConnectAndSubscribeInnerAsync();
                    }

                    // 如果代码走到这里，说明上面没有抛出异常，连上并订阅成功了，退出死循环
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MQTT 重连失败] {ex.Message}，将继续下一轮重连。");
                }
            }
        }

        /// <summary>
        /// 收到 MQTT 消息的处理机制
        /// </summary>
        private Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            string topic = e.ApplicationMessage.Topic;
            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment.Array,
                                                     e.ApplicationMessage.PayloadSegment.Offset,
                                                     e.ApplicationMessage.PayloadSegment.Count);

            try
            {
                var jsonObj = Newtonsoft.Json.Linq.JObject.Parse(payload);
                string deviceCode = jsonObj["device_code"]?.ToString();

                if (!string.IsNullOrEmpty(deviceCode))
                {
                    DeviceDictionary.AddOrUpdate(deviceCode, payload, (key, oldValue) => payload);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[解析失败] 无法提取device_code: {ex.Message}");
            }

            return Task.CompletedTask;
        }
    }
}