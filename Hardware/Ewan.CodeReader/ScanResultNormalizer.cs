using System;
using System.Text;

namespace Ewan.CodeReader
{
    /// <summary>
    /// 扫码结果规范化器
    /// 统一处理各厂商扫码器返回结果的清洗和规范化
    /// </summary>
    public static class ScanResultNormalizer
    {
        /// <summary>
        /// GS1/FNC1 分隔符，不应被删除
        /// </summary>
        private const char GroupSeparator = (char)0x1D;

        /// <summary>
        /// 失败结果标识
        /// </summary>
        private static readonly string[] s_failureIndicators = { "NG", "NoRead", "NOREAD" };

        /// <summary>
        /// 规范化扫码结果
        /// </summary>
        /// <param name="scanResult">原始扫码结果</param>
        /// <returns>规范化后的结果，失败返回空字符串</returns>
        public static string Normalize(string scanResult)
        {
            if (string.IsNullOrEmpty(scanResult))
            {
                return string.Empty;
            }

            string cleaned = RemoveControlCharacters(scanResult);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return string.Empty;
            }

            if (IsFailureResult(cleaned))
            {
                return string.Empty;
            }

            return cleaned;
        }

        /// <summary>
        /// 移除控制字符
        /// </summary>
        private static string RemoveControlCharacters(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                // 跳过常见控制字符
                if (c == '\0' || c == '\u0002' || c == '\u0003' || c == '\r' || c == '\n')
                {
                    continue;
                }

                // 跳过其他控制字符（保留 GS1 分隔符）
                if (char.IsControl(c) && c != GroupSeparator)
                {
                    continue;
                }

                builder.Append(c);
            }

            return builder.ToString().Trim();
        }

        /// <summary>
        /// 判断是否为失败结果
        /// </summary>
        private static bool IsFailureResult(string result)
        {
            if (string.IsNullOrWhiteSpace(result))
            {
                return true;
            }

            foreach (var indicator in s_failureIndicators)
            {
                if (string.Equals(result, indicator, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断扫码结果是否有效
        /// </summary>
        public static bool IsValidResult(string result)
        {
            return !string.IsNullOrEmpty(Normalize(result));
        }
    }
}
