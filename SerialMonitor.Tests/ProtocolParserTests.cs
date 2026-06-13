using Xunit;

namespace 串口助手.Tests
{
    public class ProtocolParserTests
    {
        [Fact]
        public void 单条消息_基本解析()
        {
            var result = ProtocolParser.Parse("[plot,ch1,25.3]");

            Assert.Single(result.Messages);
            Assert.Equal("plot", result.Messages[0].Type);
            Assert.Equal(2, result.Messages[0].Args.Count);
            Assert.Equal("ch1", result.Messages[0].Args[0]);
            Assert.Equal("25.3", result.Messages[0].Args[1]);
            Assert.Equal("", result.PlainText);
        }

        [Fact]
        public void 同行多条消息()
        {
            var result = ProtocolParser.Parse("[plot,P,0.5][plot,I,0.3]");

            Assert.Equal(2, result.Messages.Count);
            Assert.Equal("plot", result.Messages[0].Type);
            Assert.Equal("P", result.Messages[0].Args[0]);
            Assert.Equal("0.5", result.Messages[0].Args[1]);
            Assert.Equal("plot", result.Messages[1].Type);
            Assert.Equal("I", result.Messages[1].Args[0]);
            Assert.Equal("0.3", result.Messages[1].Args[1]);
        }

        [Fact]
        public void 引号包裹的逗号_不分割()
        {
            var result = ProtocolParser.Parse("[display,0,0,\"hello,world\",16]");

            Assert.Single(result.Messages);
            Assert.Equal("display", result.Messages[0].Type);
            Assert.Equal(4, result.Messages[0].Args.Count);
            Assert.Equal("0", result.Messages[0].Args[0]);
            Assert.Equal("0", result.Messages[0].Args[1]);
            Assert.Equal("hello,world", result.Messages[0].Args[2]);
            Assert.Equal("16", result.Messages[0].Args[3]);
        }

        [Fact]
        public void 括号外普通文本_保留()
        {
            var result = ProtocolParser.Parse("Hello [plot,P,0.5]");

            Assert.Single(result.Messages);
            Assert.Equal("plot", result.Messages[0].Type);
            Assert.Equal("Hello ", result.PlainText);
        }

        [Fact]
        public void 纯文本_无消息()
        {
            var result = ProtocolParser.Parse("普通文本 hello 123");

            Assert.Empty(result.Messages);
            Assert.Equal("普通文本 hello 123", result.PlainText);
        }

        [Fact]
        public void 空字符串()
        {
            var result = ProtocolParser.Parse("");

            Assert.Empty(result.Messages);
            Assert.Equal("", result.PlainText);
        }

        [Fact]
        public void 空方括号_忽略()
        {
            var result = ProtocolParser.Parse("before[]after");

            Assert.Empty(result.Messages);
            Assert.Equal("beforeafter", result.PlainText);
        }

        [Fact]
        public void 未闭合括号_当作普通文本()
        {
            var result = ProtocolParser.Parse("[plot,ch1,25.3");

            Assert.Empty(result.Messages);
            Assert.Equal("[plot,ch1,25.3", result.PlainText);
        }

        [Fact]
        public void 未知类型_照样解析()
        {
            var result = ProtocolParser.Parse("[custom,arg1,arg2]");

            Assert.Single(result.Messages);
            Assert.Equal("custom", result.Messages[0].Type);
            Assert.Equal(2, result.Messages[0].Args.Count);
        }

        [Fact]
        public void 按键消息()
        {
            var result = ProtocolParser.Parse("[key,btn_a,down]");

            Assert.Single(result.Messages);
            Assert.Equal("key", result.Messages[0].Type);
            Assert.Equal("btn_a", result.Messages[0].Args[0]);
            Assert.Equal("down", result.Messages[0].Args[1]);
        }

        [Fact]
        public void 摇杆消息_5个参数()
        {
            var result = ProtocolParser.Parse("[joystick,0,128,128,0,0]");

            Assert.Single(result.Messages);
            Assert.Equal("joystick", result.Messages[0].Type);
            Assert.Equal(5, result.Messages[0].Args.Count);
            Assert.Equal("0", result.Messages[0].Args[0]);
            Assert.Equal("128", result.Messages[0].Args[1]);
            Assert.Equal("128", result.Messages[0].Args[2]);
        }

        [Fact]
        public void 清屏消息_无参数()
        {
            var result = ProtocolParser.Parse("[display-clear]");

            Assert.Single(result.Messages);
            Assert.Equal("display-clear", result.Messages[0].Type);
            Assert.Empty(result.Messages[0].Args);
        }

        [Fact]
        public void OLED显示_带颜色()
        {
            var result = ProtocolParser.Parse("[display,10,20,\"hello\",16,#00FF00]");

            Assert.Single(result.Messages);
            Assert.Equal("display", result.Messages[0].Type);
            Assert.Equal(5, result.Messages[0].Args.Count);
            Assert.Equal("#00FF00", result.Messages[0].Args[4]);
        }

        [Fact]
        public void 嵌套括号_当作错误文本()
        {
            // 括号内再出现 [ 的情况——解析器把第一个 [ 到第一个 ] 之间当消息
            var result = ProtocolParser.Parse("[[nested]]");

            // 解析器会把 "[nested" 当作消息类型（只有一个参数），
            // 然后 "]" 会在外面当作普通文本
            Assert.Single(result.Messages);
            Assert.Equal("[nested", result.Messages[0].Type);
        }

        [Fact]
        public void 多行含换行符的行_PlainText去换行()
        {
            var result = ProtocolParser.Parse("hello\r\n");

            Assert.Empty(result.Messages);
            Assert.Equal("hello", result.PlainText);
        }
    }
}
