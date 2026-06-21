using System.Collections.Generic;

namespace 串口助手
{
    /// <summary>中英双语映射数据。中文即 key，这里只存英文映射。</summary>
    internal static class LocaleData
    {
        /// <summary>中文 key → English value。不在此表的 key 切英文时保留中文。</summary>
        internal static readonly Dictionary<string, string> EnMap = new()
        {
            // P0: 按钮本身
            ["中"] = "中",
            ["EN"] = "EN",
            ["/"] = "/",

            // P2: PlotDetail — 按钮 + 信号类型 + 占位
            ["▶ 继续"] = "▶ Resume",
            ["⏸ 暂停"] = "⏸ Pause",
            ["≤ 采样间隔"] = "≤ sample interval",
            ["DC 电平"] = "DC Level",
            ["方波"] = "Square Wave",
            ["矩形波"] = "Rectangular",
            ["斜边方波（滤波/容性）"] = "Sloped Square (filtered/cap.)",
            ["脉冲/尖峰"] = "Pulse / Spike",
            ["正弦波/三角波"] = "Sine / Triangle",
            ["未知 / 混合"] = "Unknown / Mixed",

            // P3: 顶栏 + 图标栏
            ["打开串口"] = "Open",
            ["关闭串口"] = "Close",
            ["打开/关闭串口（Ctrl+Enter）"] = "Open/Close (Ctrl+Enter)",
            ["切换亮色/暗色主题"] = "Light/Dark Theme",
            ["折叠侧面板"] = "Collapse Sidebar",
            ["串口设置"] = "Settings",
            ["管理标签显示"] = "Manage Tabs",
            ["接收区"] = "Receive",
            ["波形图"] = "Plot",
            ["按键面板"] = "Keys",
            ["滑杆面板"] = "Sliders",
            ["OLED"] = "OLED",
            ["摇杆面板"] = "Joystick",
            ["传感面板"] = "Sensors",

            // P4.1: Keys
            ["自定义颜色"] = "Custom Color",
            ["取消"] = "Cancel",
            ["确认"] = "OK",
            ["（该按键没有配置发送内容）"] = "(No send content configured)",

            // P4.1: Keys (continued — shared with other panels)
            // "取消"/"确认"/"编辑"/"完成" shared by multiple panels

            // P4.2: Sliders
            ["📋 复制"] = "📋 Copy",
            ["🎨 自定义颜色…"] = "🎨 Custom Color…",
            ["+ 添加"] = "+ Add",
            ["🗑 清空全部"] = "🗑 Clear All",
            ["⚠ 确认清空"] = "⚠ Confirm Clear",

            // Shared: Keys + Sliders + Sensors 共用
            ["编辑"] = "Edit",
            ["完成"] = "Done",
            ["🗑 删除此模块"] = "🗑 Delete Module",
            ["🗑 删除此按键"] = "🗑 Delete Key",

            // P4.3+ 逐面板补英文映射在此
        };
    }
}
