using System;
using System.Linq.Expressions;
using System.Resources;

namespace EwanCommon.Logging
{
    /// <summary>
    /// IO操作专用日志记录器
    /// 将IO相关日志写入独立的日志文件
    /// </summary>
    public class IOLogger : FileLogger
    {
        private static readonly Lazy<IOLogger> _instance = new Lazy<IOLogger>(() => new IOLogger());
        private ResourceManager _ioResourceManager;

        /// <summary>
        /// 获取IOLogger实例
        /// </summary>
        public static IOLogger Instance => _instance.Value;

        /// <summary>
        /// 私有构造函数，确保单例
        /// </summary>
        protected IOLogger() : base("IOLogger", null)
        {
            // 注意：资源类型由使用者通过SetResourceType方法设置
        }

        /// <summary>
        /// 设置资源类型（用于国际化）
        /// </summary>
        /// <param name="resourceType">资源类型</param>
        public void SetResourceType(Type resourceType)
        {
            if (resourceType != null && _ioResourceManager == null)
            {
                _ioResourceManager = new ResourceManager(resourceType);
            }
        }

        /// <summary>
        /// 记录IO调试信息
        /// </summary>
        /// <param name="message">消息</param>
        public void DebugIO(string message)
        {
            Debug($"[IO] {message}");
        }

        /// <summary>
        /// 记录IO信息
        /// </summary>
        /// <param name="message">消息</param>
        public void InfoIO(string message)
        {
            Info($"[IO] {message}");
        }

        /// <summary>
        /// 记录IO警告
        /// </summary>
        /// <param name="message">消息</param>
        public void WarnIO(string message)
        {
            Warn($"[IO] {message}");
        }

        /// <summary>
        /// 记录IO错误
        /// </summary>
        /// <param name="message">消息</param>
        public void ErrorIO(string message)
        {
            Error($"[IO] {message}");
        }

        /// <summary>
        /// 记录IO错误（带异常）
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="exception">异常</param>
        public void ErrorIO(string message, Exception exception)
        {
            Error($"[IO] {message}", exception);
        }

        /// <summary>
        /// 记录PLC通信日志
        /// </summary>
        /// <param name="plcAddress">PLC地址</param>
        /// <param name="operation">操作类型</param>
        /// <param name="data">数据</param>
        /// <param name="success">是否成功</param>
        public void LogPLCCommunication(string plcAddress, string operation, string data, bool success)
        {
            var status = success ? "SUCCESS" : "FAILED";
            var message = $"[PLC] Address: {plcAddress}, Operation: {operation}, Data: {data}, Status: {status}";

            if (success)
            {
                Info(message);
            }
            else
            {
                Warn(message);
            }
        }

        /// <summary>
        /// 记录Modbus通信日志
        /// </summary>
        /// <param name="slaveAddress">从站地址</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="count">数量</param>
        /// <param name="success">是否成功</param>
        public void LogModbusCommunication(byte slaveAddress, byte functionCode, ushort startAddress, ushort count, bool success)
        {
            var status = success ? "SUCCESS" : "FAILED";
            var message = $"[MODBUS] Slave: {slaveAddress}, Function: {functionCode:X2}, Address: {startAddress}, Count: {count}, Status: {status}";

            if (success)
            {
                Debug(message);
            }
            else
            {
                Warn(message);
            }
        }

        /// <summary>
        /// 记录串口通信日志
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <param name="operation">操作类型</param>
        /// <param name="data">数据</param>
        public void LogSerialCommunication(string portName, string operation, byte[] data)
        {
            var hexData = data != null ? BitConverter.ToString(data) : "NULL";
            var message = $"[SERIAL] Port: {portName}, Operation: {operation}, Data: {hexData}";
            Debug(message);
        }

        /// <summary>
        /// 记录TCP/IP通信日志
        /// </summary>
        /// <param name="ipAddress">IP地址</param>
        /// <param name="port">端口</param>
        /// <param name="operation">操作类型</param>
        /// <param name="success">是否成功</param>
        public void LogTcpCommunication(string ipAddress, int port, string operation, bool success)
        {
            var status = success ? "SUCCESS" : "FAILED";
            var message = $"[TCP] IP: {ipAddress}:{port}, Operation: {operation}, Status: {status}";

            if (success)
            {
                Info(message);
            }
            else
            {
                Error(message);
            }
        }

        /// <summary>
        /// 记录IO模块状态
        /// </summary>
        /// <param name="moduleName">模块名称</param>
        /// <param name="status">状态</param>
        /// <param name="details">详细信息</param>
        public void LogModuleStatus(string moduleName, string status, string details = null)
        {
            var message = $"[MODULE] {moduleName} - Status: {status}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $", Details: {details}";
            }

            Info(message);
        }

        /// <summary>
        /// 记录数据同步操作
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="destination">目标</param>
        /// <param name="dataCount">数据数量</param>
        /// <param name="elapsedMs">耗时（毫秒）</param>
        public void LogDataSync(string source, string destination, int dataCount, long elapsedMs)
        {
            var message = $"[SYNC] {source} -> {destination}, Count: {dataCount}, Time: {elapsedMs}ms";
            Debug(message);
        }

        /// <summary>
        /// 记录IO性能指标
        /// </summary>
        /// <param name="operation">操作名称</param>
        /// <param name="elapsedMs">耗时（毫秒）</param>
        /// <param name="throughput">吞吐量</param>
        public void LogPerformance(string operation, long elapsedMs, string throughput = null)
        {
            var message = $"[PERF] Operation: {operation}, Time: {elapsedMs}ms";
            if (!string.IsNullOrEmpty(throughput))
            {
                message += $", Throughput: {throughput}";
            }

            Debug(message);
        }

        /// <summary>
        /// 记录国际化的IO消息
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="messageExpression">资源表达式</param>
        /// <param name="parameters">参数</param>
        public void LogLocalizedIO(LogLevel level, Expression<Func<string>> messageExpression, params object[] parameters)
        {
            var messageKey = GetResourceKeyFromExpression(messageExpression);
            LogLocalized(level, messageKey, parameters);
        }

        /// <summary>
        /// 从资源表达式获取消息键
        /// </summary>
        /// <param name="messageExpression">资源表达式</param>
        /// <returns>消息键</returns>
        private string GetResourceKeyFromExpression(Expression<Func<string>> messageExpression)
        {
            try
            {
                if (messageExpression.Body is MemberExpression memberExpression)
                {
                    return memberExpression.Member.Name;
                }

                return "UnknownResource";
            }
            catch
            {
                return "UnknownResource";
            }
        }
    }
}
