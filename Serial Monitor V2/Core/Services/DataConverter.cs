using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace 串口助手
{
    /// <summary>
    /// 数据转换 — 从原始 WinForms 版本直接移植，算法逻辑一字未改。
    /// 仅将 byteBuffer 从 MainWindow 字段改为参数传入，内部实现 100% 保持原样。
    /// </summary>
    public static class DataConverter
    {
        /// <summary>
        /// 字节数组 → 文本（支持 GBK 和 UTF-8 多字节解码）。
        /// byteBuffer：外部维护的累积缓冲区（方法会修改它）。
        /// </summary>
        public static string BytesToText(byte[] bytes, string encoding, List<byte> byteBuffer)
        {
            List<byte> byteDecode = new List<byte>();
            byteBuffer.AddRange(bytes);

            int count = byteBuffer.Count;
            for (int i = 0; i < count; i++)
            {
                if (byteBuffer.Count == 0) break;

                if (encoding == "GBK")
                {
                    if (byteBuffer[0] < 0x80)
                    {
                        byteDecode.Add(byteBuffer[0]);
                        byteBuffer.RemoveAt(0);
                    }
                    else
                    {
                        if (byteBuffer.Count >= 2)
                        {
                            byteDecode.Add(byteBuffer[0]);
                            byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]);
                            byteBuffer.RemoveAt(0);
                        }
                    }
                }
                else if (encoding == "UTF-8")
                {
                    if ((byteBuffer[0] & 0x80) == 0x00)
                    {
                        byteDecode.Add(byteBuffer[0]);
                        byteBuffer.RemoveAt(0);
                    }
                    else if ((byteBuffer[0] & 0xE0) == 0xC0)
                    {
                        if (byteBuffer.Count >= 2)
                        {
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                        }
                    }
                    else if ((byteBuffer[0] & 0xF0) == 0xE0)
                    {
                        if (byteBuffer.Count >= 3)
                        {
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                        }
                    }
                    else if ((byteBuffer[0] & 0xF8) == 0xF0)
                    {
                        if (byteBuffer.Count >= 4)
                        {
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                        }
                    }
                    else
                    {
                        byteDecode.Add(byteBuffer[0]);
                        byteBuffer.RemoveAt(0);
                    }
                }
            }

            return Encoding.GetEncoding(encoding).GetString(byteDecode.ToArray());
        }

        /// <summary>
        /// 字节数组 → HEX 字符串（每字节两字符大写 + 空格分隔）
        /// </summary>
        public static string BytesToHex(byte[] bytes)
        {
            var sb = new System.Text.StringBuilder(bytes.Length * 3);
            foreach (byte b in bytes)
                sb.Append(b.ToString("X2")).Append(' ');
            return sb.ToString();
        }

        /// <summary>
        /// 文本 → 字节数组（按指定编码）
        /// </summary>
        public static byte[] TextToBytes(string str, string encoding)
        {
            return Encoding.GetEncoding(encoding).GetBytes(str);
        }

        /// <summary>
        /// HEX 字符串 → 字节数组（过滤非法字符后每两个字符解析一个字节）
        /// </summary>
        public static byte[] HexToBytes(string str)
        {
            string str1 = Regex.Replace(str, "[^A-F^a-f^0-9]", "");

            double i = str1.Length;
            int len = 2;
            string[] strList = new string[int.Parse(Math.Ceiling(i / len).ToString())];
            for (int j = 0; j < strList.Length; j++)
            {
                len = len <= str1.Length ? len : str1.Length;
                strList[j] = str1.Substring(0, len);
                str1 = str1.Substring(len, str1.Length - len);
            }

            int count = strList.Length;
            byte[] bytes = new byte[count];
            for (int j = 0; j < count; j++)
            {
                bytes[j] = byte.Parse(strList[j], NumberStyles.HexNumber);
            }

            return bytes;
        }

        /// <summary>
        /// 检查 HEX 字符串中被 HexToBytes 过滤掉的无效字符。
        /// 0-9、A-F、a-f 为合法 HEX 字符，空格视为分隔符不做提示，
        /// '^' 因历史正则兼容也被保留。
        /// 返回无效字符集合（去重，最多 10 个），无则返回空字符串。
        /// </summary>
        public static string ValidateHexString(string str)
        {
            var invalid = new HashSet<char>();
            foreach (char c in str)
            {
                if ((c >= '0' && c <= '9')
                    || (c >= 'A' && c <= 'F')
                    || (c >= 'a' && c <= 'f')
                    || c == ' ' || c == '^')
                    continue;
                invalid.Add(c);
                if (invalid.Count >= 10) break;
            }
            if (invalid.Count == 0) return "";
            return string.Join(" ", invalid);
        }
    }
}
