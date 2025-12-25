using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ewan.Mes.Transport
{
    /// <summary>
    /// 通用端点模板，用于定义消息的目标地址
    /// 支持 MQTT Topic、HTTP URL、WebSocket 路径等
    /// </summary>
    public class EndpointTemplate
    {
        private static readonly Regex PlaceholderRegex = new Regex("{(?<name>[^{}]+)}", RegexOptions.Compiled);
        private readonly string[] _placeholders;

        /// <summary>
        /// 创建端点模板
        /// </summary>
        /// <param name="template">模板字符串，支持 {placeholder} 占位符</param>
        /// <param name="qosLevel">服务质量等级（0-2，默认1）</param>
        public EndpointTemplate(string template, byte qosLevel = 1)
        {
            if (string.IsNullOrWhiteSpace(template))
                throw new ArgumentNullException(nameof(template));

            Template = template;
            QosLevel = qosLevel;
            _placeholders = PlaceholderRegex
                .Matches(template)
                .Cast<Match>()
                .Select(m => m.Groups["name"].Value)
                .Distinct()
                .ToArray();
        }

        /// <summary>
        /// 模板字符串
        /// </summary>
        public string Template { get; }

        /// <summary>
        /// 服务质量等级
        /// MQTT: 0=最多一次, 1=至少一次, 2=恰好一次
        /// HTTP/WebSocket: 可用于重试策略
        /// </summary>
        public byte QosLevel { get; }

        /// <summary>
        /// 获取模板中的占位符列表
        /// </summary>
        public IReadOnlyList<string> Placeholders => _placeholders;

        /// <summary>
        /// 解析模板，将占位符替换为实际值
        /// </summary>
        /// <param name="tokens">占位符-值字典</param>
        /// <returns>解析后的端点地址</returns>
        public string Resolve(IDictionary<string, string> tokens)
        {
            if (_placeholders.Length == 0)
                return Template;

            if (tokens == null)
                throw new ArgumentNullException(nameof(tokens));

            var resolved = Template;

            foreach (var placeholder in _placeholders)
            {
                if (!tokens.TryGetValue(placeholder, out var value) || string.IsNullOrEmpty(value))
                    throw new ArgumentException($"缺少端点占位符 '{placeholder}'", nameof(tokens));

                resolved = resolved.Replace("{" + placeholder + "}", value);
            }

            return resolved;
        }

        public override string ToString() => Template;
    }
}
