using System.Collections.Generic;
using System.Text;
using Xunit;

namespace 串口助手.Tests
{
    /// <summary>
    /// DataConverter 四个方法边界用例测试。
    /// 算法逻辑一字不改——仅验证输入/输出行为。
    /// </summary>
    public class DataConverterTests
    {
        static DataConverterTests()
        {
            // .NET 8 默认不含 GBK 等非 Unicode 编码页，需显式注册
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        // ═══════════════════════════════════════════════════════════════
        // BytesToHex
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void BytesToHex_空数组_返回空字符串()
        {
            var result = DataConverter.BytesToHex(new byte[0]);
            Assert.Equal("", result);
        }

        [Fact]
        public void BytesToHex_单个字节_大写两位加空格()
        {
            var result = DataConverter.BytesToHex(new byte[] { 0x0A });
            Assert.Equal("0A ", result);
        }

        [Fact]
        public void BytesToHex_多个字节_空格分隔()
        {
            var result = DataConverter.BytesToHex(new byte[] { 0x01, 0x02, 0x03 });
            Assert.Equal("01 02 03 ", result);
        }

        [Fact]
        public void BytesToHex_零值字节_输出00()
        {
            var result = DataConverter.BytesToHex(new byte[] { 0x00, 0x00 });
            Assert.Equal("00 00 ", result);
        }

        [Fact]
        public void BytesToHex_最大值FF()
        {
            var result = DataConverter.BytesToHex(new byte[] { 0xFF });
            Assert.Equal("FF ", result);
        }

        [Fact]
        public void BytesToHex_混合值()
        {
            var result = DataConverter.BytesToHex(new byte[] { 0x1A, 0x2B, 0x3C, 0x4D });
            Assert.Equal("1A 2B 3C 4D ", result);
        }

        // ═══════════════════════════════════════════════════════════════
        // HexToBytes
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void HexToBytes_空字符串_返回空数组()
        {
            var result = DataConverter.HexToBytes("");
            Assert.Empty(result);
        }

        [Fact]
        public void HexToBytes_标准大写两字符()
        {
            var result = DataConverter.HexToBytes("0A");
            Assert.Equal(new byte[] { 0x0A }, result);
        }

        [Fact]
        public void HexToBytes_小写也可解析()
        {
            var result = DataConverter.HexToBytes("0a1b");
            Assert.Equal(new byte[] { 0x0A, 0x1B }, result);
        }

        [Fact]
        public void HexToBytes_含空格_过滤空格后解析()
        {
            var result = DataConverter.HexToBytes("0A 1B 2C");
            Assert.Equal(new byte[] { 0x0A, 0x1B, 0x2C }, result);
        }

        [Fact]
        public void HexToBytes_含逗号_过滤非法字符()
        {
            var result = DataConverter.HexToBytes("0A,1B,2C");
            Assert.Equal(new byte[] { 0x0A, 0x1B, 0x2C }, result);
        }

        [Fact]
        public void HexToBytes_奇数长度_最后一位单独解析()
        {
            // "0A1" → 长度 3, strList = ["0A", "1"] → [0x0A, 0x01]
            var result = DataConverter.HexToBytes("0A1");
            Assert.Equal(new byte[] { 0x0A, 0x01 }, result);
        }

        [Fact]
        public void HexToBytes_全是非法字符_返回空数组()
        {
            var result = DataConverter.HexToBytes("xyz,./");
            Assert.Empty(result);
        }

        [Fact]
        public void HexToBytes_混合合法与非法字符()
        {
            var result = DataConverter.HexToBytes("A:1,B:2");
            Assert.Equal(new byte[] { 0xA1, 0xB2 }, result);
        }

        [Fact]
        public void HexToBytes_常见HEX字符串()
        {
            var result = DataConverter.HexToBytes("48 65 6C 6C 6F");
            // "Hello" in ASCII
            Assert.Equal(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }, result);
        }

        [Fact]
        public void HexToBytes_无分隔符连续HEX()
        {
            var result = DataConverter.HexToBytes("48656C6C6F");
            Assert.Equal(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }, result);
        }

        // ═══════════════════════════════════════════════════════════════
        // ValidateHexString
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void ValidateHexString_纯HEX大写_返回空()
        {
            var result = DataConverter.ValidateHexString("0A1B2C");
            Assert.Equal("", result);
        }

        [Fact]
        public void ValidateHexString_纯HEX小写_返回空()
        {
            var result = DataConverter.ValidateHexString("0a1b2c");
            Assert.Equal("", result);
        }

        [Fact]
        public void ValidateHexString_空格分隔_返回空()
        {
            var result = DataConverter.ValidateHexString("0A 1B 2C");
            Assert.Equal("", result);
        }

        [Fact]
        public void ValidateHexString_空字符串_返回空()
        {
            var result = DataConverter.ValidateHexString("");
            Assert.Equal("", result);
        }

        [Fact]
        public void ValidateHexString_逗号视为无效()
        {
            var result = DataConverter.ValidateHexString("0A,1B");
            Assert.Contains(",", result);
        }

        [Fact]
        public void ValidateHexString_非法字母G到Z()
        {
            var result = DataConverter.ValidateHexString("0A G 1B");
            Assert.Contains("G", result);
        }

        [Fact]
        public void ValidateHexString_全非法字符()
        {
            var result = DataConverter.ValidateHexString("xyz");
            Assert.Contains("x", result);
            Assert.Contains("y", result);
            Assert.Contains("z", result);
        }

        [Fact]
        public void ValidateHexString_混合有效与无效()
        {
            var result = DataConverter.ValidateHexString("0A:1B;test");
            // ':' ';' 't' 's' 是无效字符，'e' 是合法 hex (a-f)
            Assert.Contains(":", result);
            Assert.Contains(";", result);
            Assert.Contains("t", result);
            Assert.Contains("s", result);
            Assert.DoesNotContain("e", result);
        }

        [Fact]
        public void ValidateHexString_无效字符去重()
        {
            var result = DataConverter.ValidateHexString("x,x,x");
            // 'x' 和 ',' 都是无效字符，各自去重
            Assert.Contains("x", result);
            Assert.Contains(",", result);
        }

        [Fact]
        public void ValidateHexString_超过10种无效字符_截断()
        {
            // 12 种无效字符：g h i j k l m n o p q r
            var result = DataConverter.ValidateHexString("g h i j k l m n o p q r");
            var parts = result.Split(' ');
            Assert.True(parts.Length <= 10);
        }

        // ═══════════════════════════════════════════════════════════════
        // TextToBytes
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void TextToBytes_空字符串UTF8_返回空数组()
        {
            var result = DataConverter.TextToBytes("", "UTF-8");
            Assert.Empty(result);
        }

        [Fact]
        public void TextToBytes_ASCII文本_UTF8编码()
        {
            var result = DataConverter.TextToBytes("Hello", "UTF-8");
            Assert.Equal(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }, result);
        }

        [Fact]
        public void TextToBytes_中文文本_UTF8编码()
        {
            var result = DataConverter.TextToBytes("你好", "UTF-8");
            var expected = Encoding.UTF8.GetBytes("你好");
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TextToBytes_中文文本_GBK编码()
        {
            var result = DataConverter.TextToBytes("你好", "GBK");
            var expected = Encoding.GetEncoding("GBK").GetBytes("你好");
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TextToBytes_空字符串GBK_返回空数组()
        {
            var result = DataConverter.TextToBytes("", "GBK");
            Assert.Empty(result);
        }

        // ═══════════════════════════════════════════════════════════════
        // BytesToText
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void BytesToText_空数据和空缓冲_返回空字符串()
        {
            var buffer = new List<byte>();
            var result = DataConverter.BytesToText(new byte[0], "UTF-8", buffer);

            Assert.Equal("", result);
            Assert.Empty(buffer);
        }

        [Fact]
        public void BytesToText_ASCII单字节解码()
        {
            var buffer = new List<byte>();
            var ascii = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F };
            var result = DataConverter.BytesToText(ascii, "UTF-8", buffer);

            Assert.Equal("Hello", result);
            Assert.Empty(buffer);
        }

        [Fact]
        public void BytesToText_UTF8三字节中文()
        {
            var buffer = new List<byte>();
            // "你" = U+4F60, UTF-8: E4 BD A0
            var utf8Bytes = new byte[] { 0xE4, 0xBD, 0xA0 };
            var result = DataConverter.BytesToText(utf8Bytes, "UTF-8", buffer);

            Assert.Equal("你", result);
            Assert.Empty(buffer);
        }

        [Fact]
        public void BytesToText_UTF8多个中文()
        {
            var buffer = new List<byte>();
            // "你好" UTF-8: E4 BD A0 E5 A5 BD
            var utf8Bytes = new byte[] { 0xE4, 0xBD, 0xA0, 0xE5, 0xA5, 0xBD };
            var result = DataConverter.BytesToText(utf8Bytes, "UTF-8", buffer);

            Assert.Equal("你好", result);
            Assert.Empty(buffer);
        }

        [Fact]
        public void BytesToText_GBK中文()
        {
            var buffer = new List<byte>();
            // "你好" GBK: C4 E3 BA C3
            var gbkBytes = new byte[] { 0xC4, 0xE3, 0xBA, 0xC3 };
            var result = DataConverter.BytesToText(gbkBytes, "GBK", buffer);

            Assert.Equal("你好", result);
            Assert.Empty(buffer);
        }

        [Fact]
        public void BytesToText_不完整UTF8序列_留在缓冲()
        {
            var buffer = new List<byte>();
            // 只发一个 UTF-8 3 字节序列的首字节
            var incomplete = new byte[] { 0xE4 }; // "你" 的首字节
            var result = DataConverter.BytesToText(incomplete, "UTF-8", buffer);

            // 解码不出完整字符，字节留在 buffer
            Assert.Equal("", result);
            Assert.Equal(new byte[] { 0xE4 }, buffer);
        }

        [Fact]
        public void BytesToText_两次调用补全UTF8字符()
        {
            var buffer = new List<byte>();

            // 第一次：发首字节，解不出
            var r1 = DataConverter.BytesToText(new byte[] { 0xE4 }, "UTF-8", buffer);
            Assert.Equal("", r1);
            Assert.Single(buffer);

            // 第二次：发剩余两个字节，补全 "你"
            var r2 = DataConverter.BytesToText(new byte[] { 0xBD, 0xA0 }, "UTF-8", buffer);
            Assert.Equal("你", r2);
            Assert.Empty(buffer);
        }

        [Fact]
        public void BytesToText_不完整GBK序列_留在缓冲()
        {
            var buffer = new List<byte>();
            // GBK 双字节：只发首字节（>= 0x80）
            var incomplete = new byte[] { 0xC4 }; // "你" GBK 首字节
            var result = DataConverter.BytesToText(incomplete, "GBK", buffer);

            Assert.Equal("", result);
            Assert.Equal(new byte[] { 0xC4 }, buffer);
        }

        [Fact]
        public void BytesToText_两次调用补全GBK字符()
        {
            var buffer = new List<byte>();

            var r1 = DataConverter.BytesToText(new byte[] { 0xC4 }, "GBK", buffer);
            Assert.Equal("", r1);
            Assert.Single(buffer);

            var r2 = DataConverter.BytesToText(new byte[] { 0xE3 }, "GBK", buffer);
            Assert.Equal("你", r2);
            Assert.Empty(buffer);
        }

        [Fact]
        public void BytesToText_缓冲已有残留_新数据补充()
        {
            // 模拟串口分帧：上次残留 0xE4，这次收到完整 "好" + 下一个"你"的首字节
            var buffer = new List<byte> { 0xE4 }; // 上次留下的 "你" 首字节

            var newBytes = new byte[] { 0xBD, 0xA0, 0xE5, 0xA5, 0xBD, 0xE4 };
            //                          └─ "你" ─┘ └─ "好" ─┘ └ 下一个"你"首字节

            var result = DataConverter.BytesToText(newBytes, "UTF-8", buffer);

            Assert.Equal("你好", result);
            // 最后一个 E4 留在缓冲
            Assert.Equal(new byte[] { 0xE4 }, buffer);
        }

        [Fact]
        public void BytesToText_UTF8单字节字符_0x7F以下()
        {
            var buffer = new List<byte>();
            // 混合 ASCII + 中文
            var bytes = new byte[] { 0x48, 0x69, 0x21 }; // "Hi!"
            var result = DataConverter.BytesToText(bytes, "UTF-8", buffer);

            Assert.Equal("Hi!", result);
            Assert.Empty(buffer);
        }

        [Fact]
        public void BytesToText_空数据不改变缓冲已有内容()
        {
            var buffer = new List<byte> { 0xE4 };
            var result = DataConverter.BytesToText(new byte[0], "UTF-8", buffer);

            Assert.Equal("", result);
            Assert.Equal(new byte[] { 0xE4 }, buffer);
        }

        [Fact]
        public void BytesToText_UTF8四字节字符()
        {
            var buffer = new List<byte>();
            // U+1F600 😀 UTF-8: F0 9F 98 80
            var bytes = new byte[] { 0xF0, 0x9F, 0x98, 0x80 };
            var result = DataConverter.BytesToText(bytes, "UTF-8", buffer);

            Assert.Equal("😀", result);
            Assert.Empty(buffer);
        }
    }
}
