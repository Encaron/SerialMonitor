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
    }
}
