/*****************************************************
** 命名空间: Ewan.Mes.Mqtt
** 文 件 名：MqttTopicTemplate
** 内容简述：
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/3 17:59:41
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace Ewan.Mes.Mqtt
{
    public class MqttTopicTemplate
    {
        private static readonly Regex PlaceholderRegex = new Regex("{(?<name>[^{}]+)}", RegexOptions.Compiled);
        private readonly string[] _placeholders;

        public MqttTopicTemplate(string template, byte qosLevel = MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE)
        {
            if (template == null)
            {
                throw new ArgumentNullException("template");
            }

            Template = template;
            QosLevel = qosLevel;
            _placeholders = PlaceholderRegex
                .Matches(template)
                .Cast<Match>()
                .Select(m => m.Groups["name"].Value)
                .Distinct()
                .ToArray();
        }

        public string Template { get; private set; }
        public byte QosLevel { get; private set; }

        public string Resolve(IDictionary<string, string> tokens)
        {
            if (_placeholders.Length == 0)
            {
                return Template;
            }

            if (tokens == null)
            {
                throw new ArgumentNullException("tokens");
            }

            var resolved = Template;

            foreach (var placeholder in _placeholders)
            {
                string value;
                if (!tokens.TryGetValue(placeholder, out value) || string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException(string.Format("Missing topic token '{0}'", placeholder), "tokens");
                }

                resolved = resolved.Replace("{" + placeholder + "}", value);
            }

            return resolved;
        }
    }
}
