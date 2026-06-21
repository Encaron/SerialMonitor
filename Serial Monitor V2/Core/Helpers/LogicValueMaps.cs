using System.Collections.Generic;

namespace 串口助手
{
    /// <summary>
    /// i18n 预埋：逻辑值（英文 key）与显示文字（中文）的双向映射。
    /// prefs.json 存储英文 key；UI 显示中文。
    /// 加载老 prefs.json 时通过 LegacyToKey 映射自动迁移。
    /// DisplayXxx 方法内置 Tr() 翻译——调用方无需手动 T()。
    /// </summary>
    public static class LogicValueMaps
    {
        /// <summary>安全取双语：有资源→翻译，无→回退原文</summary>
        private static string Tr(string zh) =>
            System.Windows.Application.Current?.Resources[zh] as string ?? zh;
        // ═══ 颜色 ═══

        public static readonly Dictionary<string, string> LegacyColorToKey = new()
        {
            ["红色"] = "red", ["绿色"] = "green", ["蓝色"] = "blue",
            ["黄色"] = "yellow", ["白色"] = "white", ["灰色"] = "gray",
            ["默认"] = "default",
        };

        public static readonly string[] ColorKeys = { "default", "red", "green", "blue", "yellow", "white", "gray" };

        public static string MigrateColor(string v) =>
            !string.IsNullOrEmpty(v) && LegacyColorToKey.TryGetValue(v, out var k) ? k : v;

        public static string DisplayColor(string k) => Tr(k switch
        {
            "red" => "红色", "green" => "绿色", "blue" => "蓝色",
            "yellow" => "黄色", "white" => "白色", "gray" => "灰色",
            "default" => "默认",
            _ => k,
        });

        // ═══ 发送模式 ═══

        public static readonly Dictionary<string, string> LegacySendModeToKey = new()
        {
            ["文本"] = "text", ["HEX"] = "hex", ["数据包"] = "packet", ["无"] = "none",
        };

        public static readonly string[] SendModeKeys = { "text", "hex", "packet", "none" };

        public static string MigrateSendMode(string v) =>
            !string.IsNullOrEmpty(v) && LegacySendModeToKey.TryGetValue(v, out var k) ? k : v;

        public static string DisplaySendMode(string k) => Tr(k switch
        {
            "text" => "文本", "hex" => "HEX", "packet" => "数据包", "none" => "无",
            _ => k,
        });

        // ═══ 风格（摇杆 + 滑杆共用） ═══

        public static readonly Dictionary<string, string> LegacyStyleToKey = new()
        {
            ["手柄风"] = "gamepad", ["极简风"] = "minimal", ["经典风"] = "classic",
            ["极简"] = "minimal", ["默认"] = "default",
        };

        public static readonly string[] BuiltInJoyStyleKeys = { "gamepad", "minimal", "classic" };

        public static string MigrateStyle(string v) =>
            !string.IsNullOrEmpty(v) && LegacyStyleToKey.TryGetValue(v, out var k) ? k : v;

        public static string DisplayJoyStyle(string k) => Tr(k switch
        {
            "gamepad" => "手柄风", "minimal" => "极简风", "classic" => "经典风",
            _ => k,
        });

        public static string DisplaySliderStyle(string k) => Tr(k switch
        {
            "default" => "默认", "minimal" => "极简",
            _ => k,
        });

        // ═══ 收发模式（串口用，和 SendMode 同 key 体系但不同显示名） ═══

        public static readonly string[] IoModeKeys = { "hex", "text" };

        public static string DisplayIoMode(string k) => Tr(k switch
        {
            "hex" => "HEX模式", "text" => "文本模式",
            _ => k,
        });

        // ═══ 校验位 ═══

        public static readonly string[] ParityKeys = { "none", "odd", "even" };

        public static string DisplayParity(string k) => Tr(k switch
        {
            "none" => "无", "odd" => "奇校验", "even" => "偶校验",
            _ => k,
        });

        // ═══ 流控 ═══

        public static readonly string[] FlowControlKeys = { "none", "rts", "xonxoff" };

        public static string DisplayFlowControl(string k) => Tr(k switch
        {
            "none" => "无", "rts" => "RTS/CTS", "xonxoff" => "XON/XOFF",
            _ => k,
        });

        // ═══ 时间戳格式 ═══

        public static readonly string[] TimestampFormatKeys = { "none", "HH:mm:ss", "HH:mm:ss:fff" };

        public static string DisplayTimestampFormat(string k) => Tr(k switch
        {
            "none" => "不显示", "HH:mm:ss" => "HH:mm:ss", "HH:mm:ss:fff" => "HH:mm:ss:fff",
            _ => k,
        });

        // ═══ 换行符 ═══

        public static readonly string[] NewlineKeys = { "crlf", "lf", "cr", "none" };

        public static string DisplayNewline(string k) => Tr(k switch
        {
            "crlf" => "\\r\\n", "lf" => "\\n", "cr" => "\\r", "none" => "无",
            _ => k,
        });

        // ═══ 绘图模式 ═══

        public static readonly string[] PlotModeKeys = { "roll", "sweep" };

        public static string DisplayPlotMode(string k) => Tr(k switch
        {
            "roll" => "滚动", "sweep" => "扫描",
            _ => k,
        });

        // ═══ 窗函数 ═══

        public static readonly string[] WindowFunctionKeys = { "hanning", "rectangular", "hamming", "blackman" };

        public static string DisplayWindowFunction(string k) => Tr(k switch
        {
            "hanning" => "汉宁 (Hanning)", "rectangular" => "矩形 (Rectangular)",
            "hamming" => "汉明 (Hamming)", "blackman" => "布莱克曼 (Blackman)",
            _ => k,
        });
    }
}
