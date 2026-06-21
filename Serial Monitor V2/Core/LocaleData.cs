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

            // P3+ 逐面板补英文映射在此
        };
    }
}
