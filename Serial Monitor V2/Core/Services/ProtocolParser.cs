using System;
using System.Collections.Generic;
using System.Text;

namespace 串口助手
{
    /// <summary>
    /// 协议解析结果
    /// </summary>
    public class ParseResult
    {
        /// <summary>解析出的协议消息列表</summary>
        public List<ProtocolMessage> Messages { get; set; } = new List<ProtocolMessage>();

        /// <summary>方括号外的普通文本（保留原样用于接收区显示）</summary>
        public string PlainText { get; set; } = "";
    }

    /// <summary>
    /// 协议解析器：从接收到的文本行中提取 [type,arg1,arg2,...] 格式的消息。
    ///
    /// 格式规则：
    ///   - 方括号包裹一条消息，多个括号可同行
    ///   - 参数以逗号分隔
    ///   - 含逗号的参数用双引号包裹："hello,18"
    ///   - 无逗号的参数引号可省
    ///   - 括号外的普通文本照原样保留
    ///
    /// 示例：
    ///   "[plot,P,0.5][plot,I,0.3]" → 2 条 plot 消息
    ///   "[display,0,0,\"hello,18\",24]" → 1 条 display 消息，Args=["0","0","hello,18","24"]
    ///   "Hello [plot,P,0.5]" → PlainText="Hello " + 1 条 plot 消息
    /// </summary>
    public static class ProtocolParser
    {
        /// <summary>
        /// 解析一行文本，提取所有协议消息和普通文本。
        /// </summary>
        public static ParseResult Parse(string rawLine)
        {
            var result = new ParseResult();

            if (string.IsNullOrEmpty(rawLine))
            {
                result.PlainText = rawLine ?? "";
                return result;
            }

            var plainBuilder = new StringBuilder();
            int i = 0;
            int len = rawLine.Length;

            while (i < len)
            {
                if (rawLine[i] == '[')
                {
                    // 尝试解析方括号内的协议消息
                    int start = i;
                    int end = FindClosingBracket(rawLine, i + 1);

                    if (end >= 0)
                    {
                        // 成功找到匹配的 ]
                        string bracketContent = rawLine.Substring(start + 1, end - start - 1);
                        var msg = ParseBracketContent(bracketContent);
                        if (msg != null)
                        {
                            result.Messages.Add(msg);
                        }
                        i = end + 1;
                    }
                    else
                    {
                        // 未找到匹配的 ]，当作普通文本
                        plainBuilder.Append(rawLine[i]);
                        i++;
                    }
                }
                else
                {
                    plainBuilder.Append(rawLine[i]);
                    i++;
                }
            }

            result.PlainText = plainBuilder.ToString().TrimEnd('\r', '\n');
            return result;
        }

        /// <summary>
        /// 从 start 位置开始查找匹配的 ]。
        /// 处理引号内的 ] 不算结束符。
        /// 返回 ] 的索引，未找到返回 -1。
        /// </summary>
        private static int FindClosingBracket(string text, int start)
        {
            bool inQuotes = false;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ']' && !inQuotes)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 解析方括号内的内容：[ 和 ] 之间的部分。
        /// 按逗号分割参数，处理引号包裹。
        /// </summary>
        private static ProtocolMessage ParseBracketContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return null;

            var args = SplitArgs(content);
            if (args.Count == 0)
                return null;

            var msg = new ProtocolMessage
            {
                Type = args[0],
                Args = new List<string>()
            };

            // 剩余参数从索引 1 开始
            for (int i = 1; i < args.Count; i++)
            {
                msg.Args.Add(args[i]);
            }

            return msg;
        }

        /// <summary>
        /// 按逗号分割参数，处理双引号包裹的字段。
        /// 引号内的逗号不作为分隔符，引号本身会被剥离。
        ///
        /// 示例：
        ///   "0,0,\"hello,18\",24" → ["0","0","hello,18","24"]
        ///   "P,0.5"               → ["P","0.5"]
        /// </summary>
        internal static List<string> SplitArgs(string content)
        {
            var result = new List<string>();
            int i = 0;
            int len = content.Length;
            var current = new StringBuilder();

            while (i < len)
            {
                char c = content[i];

                if (c == '"')
                {
                    // 引号包裹的字段：收集到闭合引号为止
                    i++; // 跳过开始引号
                    while (i < len)
                    {
                        if (content[i] == '"')
                        {
                            i++; // 跳过闭合引号
                            break;
                        }
                        current.Append(content[i]);
                        i++;
                    }
                }
                else if (c == ',')
                {
                    // 逗号分隔符
                    result.Add(current.ToString());
                    current.Clear();
                    i++;
                }
                else
                {
                    current.Append(c);
                    i++;
                }
            }

            // 最后一个参数
            result.Add(current.ToString());

            return result;
        }
    }
}
