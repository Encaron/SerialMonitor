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

            // P4.3: Joystick
            ["⟲ 全部回中"] = "⟲ Center All",
            ["⟲ 回中"] = "⟲ Center",

            // P4.4: Display / OLED
            ["🗑 清屏"] = "🗑 Clear",
            ["选择颜色"] = "Pick Color",
            ["🔓 锁定"] = "🔓 Locked",
            ["实心填充"] = "Fill",
            ["↑ 置顶"] = "↑ To Front",
            ["↓ 置底"] = "↓ To Back",
            ["🗑 删除图形"] = "🗑 Delete Shape",
            ["🗑 删除此滑杆"] = "🗑 Delete Slider",

            // Receive area
            ["消息回显"] = "Echo",
            ["行号显示"] = "Line Numbers",
            ["系统消息独立显示"] = "Separate System Log",
            ["定时发送"] = "Timed Send",
            ["发送后清空发送区"] = "Clear After Send",
            ["导出日志"] = "Export Log",
            ["清空接收区"] = "Clear Receive",
            ["🔍 搜索"] = "🔍 Search",
            ["↓ 回到底部"] = "↓ To Bottom",
            ["清空发送区"] = "Clear Send",
            ["发送"] = "Send",

            // Plot
            ["Y 轴自动"] = "Auto Y",
            ["显示标点"] = "Markers",
            ["显示连线"] = "Lines",
            ["数值显示"] = "Values",
            ["🗑 清除全部曲线"] = "🗑 Clear All",
            ["📥 导出 CSV"] = "📥 Export CSV",
            ["← 返回绘图设置"] = "← Back to Plot",
            ["← 返回频谱设置"] = "← Back to Spectrum",
            ["📄 复制数据"] = "📄 Copy Data",
            ["📊 复制统计"] = "📊 Copy Stats",
            ["📋 全部复制"] = "📋 Copy All",
            ["📊 复制指标"] = "📊 Copy Metrics",
            ["🗑 清除频谱"] = "🗑 Clear Spectrum",
            ["🗑 清除"] = "🗑 Clear",
            ["📥 CSV"] = "📥 CSV",
            ["🔍 适应"] = "🔍 Fit",
            ["📊 详细"] = "📊 Detail",
            ["▲ 调参工作台"] = "▲ Tuning Panel",

            // Keys
            ["🔄 生成松开发送值"] = "🔄 Gen Release",
            ["自锁（点击保持按下，再点松开）"] = "Self-lock (click to hold, click again to release)",
            ["全部归零"] = "All Zero",
            ["全部置中"] = "All Mid",
            ["全部最大"] = "All Max",
            ["🗑 删除此模块"] = "🗑 Delete Module",

            // Settings
            ["📡 串口配置"] = "📡 Serial",
            ["⌨ 快捷键提示"] = "⌨ Shortcuts",
            ["📖 使用示例"] = "📖 Examples",
            ["🎨 素材自定义"] = "🎨 Assets",
            ["ℹ 关于"] = "ℹ About",
            ["← 返回"] = "← Back",
            ["DTR（数据终端就绪）"] = "DTR (Data Terminal Ready)",
            ["RTS（请求发送）"] = "RTS (Request to Send)",
            ["自动重连"] = "Auto Reconnect",
            ["串口打开时不重置流量计数"] = "Preserve traffic on open",

            // P4.4: Display
            ["OLED 设置"] = "OLED Settings",
            ["文字编辑"] = "Text Edit",
            ["图形属性"] = "Shape Properties",

            // P4.5: Sensors
            ["卡片管理"] = "Card Manager",
            ["卡片详情"] = "Card Detail",
            ["传感面板"] = "Sensors",
            ["确认删除?"] = "Confirm Delete?",
            ["＋ 换行"] = "+ New Line",
            ["添加"] = "Add",
            ["模板"] = "Template",
            ["名字"] = "Name",
            ["+ 添加组"] = "+ Add Group",
            ["完成编辑"] = "Done Editing",
            ["暂无卡片"] = "No Cards",
            ["+ 卡片"] = "+ Card",
            ["名称："] = "Name:",
            ["显示迷你波形"] = "Show Mini Plot",
            ["颜色："] = "Color:",
            ["选择颜色..."] = "Pick Color...",
            ["当前值："] = "Value:",
            ["协议预览"] = "Protocol Preview",
            ["── 换行 ──"] = "── Break ──",

            // P5: Settings + misc补漏
        };
    }
}
