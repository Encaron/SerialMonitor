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

            // P5: 收尾 — 状态栏 + 侧栏标题 + 动态按钮
            ["已连接"] = "Connected",
            ["就绪"] = "Ready",
            ["💡 更纱黑体未安装 → 使用备用等宽字体"] = "💡 Monospace font not installed → using fallback",
            ["▶ 继续显示"] = "▶ Resume Display",
            ["暂停显示"] = "Pause Display",
            ["收发设置"] = "Receive Settings",
            ["绘图设置"] = "Plot Settings",
            ["按键属性"] = "Key Properties",
            ["滑杆属性"] = "Slider Properties",
            ["摇杆设置"] = "Joystick Settings",
            ["设置"] = "Settings",
            ["📶 频域  →  时域 ⏱"] = "📶 Freq → Time ⏱",
            ["▼ 收起"] = "▼ Collapse",
            ["▲ 调参"] = "▲ Tuning",
            ["等待 FFT 数据…"] = "Waiting for FFT data…",
            ["等待串口数据…"] = "Waiting for serial data…",
            ["确定"] = "OK",
            ["📡 筛选 ▾"] = "📡 Filter ▾",
            ["⏱ 时域  →  频域 📶"] = "⏱ Time → Freq 📶",
            ["⌨ 键盘布局"] = "⌨ Layout",
            ["↗ 打开"] = "↗ Open",
            ["🌙 暗色模式"] = "🌙 Dark Mode",
            ["☀ 亮色模式"] = "☀ Light Mode",

            // P5: ToolTip
            ["复制协议到剪贴板"] = "Copy protocol to clipboard",
            ["锁定/解锁元素"] = "Lock / Unlock element",
            ["按下→文本模式，值为按键名"] = "Press→Text mode, value=key name",
            ["按下→文本模式，值为 up"] = "Press→Text mode, value=up",
            ["按下→文本模式，值为 down"] = "Press→Text mode, value=down",
            ["按下→文本模式，值为 on"] = "Press→Text mode, value=on",
            ["按下→文本模式，值为 off"] = "Press→Text mode, value=off",
            ["松开→文本模式，值为按键名"] = "Release→Text mode, value=key name",
            ["松开→文本模式，值为 up"] = "Release→Text mode, value=up",
            ["松开→文本模式，值为 down"] = "Release→Text mode, value=down",
            ["松开→文本模式，值为 on"] = "Release→Text mode, value=on",
            ["松开→文本模式，值为 off"] = "Release→Text mode, value=off",
            ["按下→无"] = "Press→None",
            ["松开→无"] = "Release→None",
            ["为模块内所有按键的松开发送填入 [key,Name,up] 文本值"] = "Fill all release values with [key,Name,up]",
            ["复制按下"] = "Copy Press",
            ["复制松开"] = "Copy Release",
            ["取消编辑"] = "Cancel Edit",
            ["复制文字内容"] = "Copy Text",
            ["取消选中"] = "Deselect",
            ["选择（点击选中/拖拽调整）"] = "Select (click to select / drag to resize)",
            ["矩形 (左键选择，右键切换类型)"] = "Rectangle (left=select, right=switch type)",
            ["圆 (左键选择，右键切换类型)"] = "Circle (left=select, right=switch type)",
            ["画笔"] = "Pencil",
            ["直线"] = "Line",
            ["三角形"] = "Triangle",
            ["弧线"] = "Arc",
            ["文字"] = "Text",
            ["橡皮擦"] = "Eraser",
            ["解锁画布（默认锁定，只看不画）"] = "Unlock canvas (locked by default)",
            ["重发全部图形到串口"] = "Resend all shapes to serial",
            ["清除 OLED 屏幕（display-clear）"] = "Clear OLED (display-clear)",
            ["筛选接收区显示内容"] = "Filter receive content",
            ["冻结接收区（Ctrl+P）"] = "Freeze receive (Ctrl+P)",
            ["导出日志"] = "Export Log",
            ["清空（Ctrl+L）"] = "Clear (Ctrl+L)",
            ["搜索（Ctrl+F）"] = "Search (Ctrl+F)",
            ["回到底部"] = "Scroll to bottom",
            ["清空发送区"] = "Clear Send",
            ["发送历史"] = "Send History",
            ["切换时域/频域视角"] = "Toggle Time/Frequency view",
            ["暂停后查看指定曲线的统计数据"] = "Pause then click a curve to view stats",
            ["展开/收起调参滑杆"] = "Expand / Collapse tuning sliders",
            ["进入编辑模式"] = "Enter Edit Mode",
            ["退出编辑模式"] = "Exit Edit Mode",
            ["添加一个按键"] = "Add a key",
            ["从键盘布局预设批量创建按键"] = "Batch create keys from layout preset",
            ["删除所有按键"] = "Delete all keys",
            ["将所有摇杆归零"] = "Center all joysticks",
            ["切换摇杆底板（大圆盘）风格"] = "Switch pad style",
            ["切换摇杆拇指（拖拽圆钮）风格"] = "Switch thumb style",
            ["每个字节的数据位数。常用 8 位"] = "Data bits per byte. Typically 8",
            ["每个字节后的停止位数。常用 1 位"] = "Stop bits per byte. Typically 1",
            ["错误检测方式。None=不校验，Odd=奇校验，Even=偶校验"] = "Parity check. None/Even/Odd",
            ["数据流控制。None=无，RTS=硬件流控，XOnXOff=软件流控"] = "Flow control. None/RTS/XOnXOff",
            ["数据终端就绪信号。某些设备需拉高此信号才开始工作"] = "DTR signal. Some devices require this",
            ["请求发送信号。用于硬件流控或唤醒目标设备"] = "RTS signal. For flow control or wakeup",
            ["串口意外断开后自动尝试重新连接"] = "Auto reconnect on disconnect",
            ["保持上次连接的收发字节计数，不清零"] = "Preserve traffic counters on reconnect",
            ["点击复制路径"] = "Click to copy path",
            ["右键点击重置流量计数"] = "Right-click to reset traffic counter",
            ["刷新数据源列表 + 重算频谱"] = "Refresh source list + recalculate FFT",
            ["复制原始波形数据点（CSV，可贴 Excel）"] = "Copy raw data (CSV, paste to Excel)",
            ["复制波形分析统计（幅值/频率/占空比等）"] = "Copy analysis stats (amplitude/freq/duty)",
            ["复制原始数据 + 分析统计，一份全带走"] = "Copy raw data + stats, all in one",
            ["复制 FFT 频谱数据（CSV，可贴 Excel）"] = "Copy FFT data (CSV, paste to Excel)",
            ["复制频域分析指标"] = "Copy frequency domain metrics",
            ["复制频谱数据 + 分析指标，一份全带走"] = "Copy spectrum data + metrics, all in one",

            // FormLabel
            ["时间戳"] = "Timestamp",
            ["换行符"] = "Line Ending",
            ["间隔(ms)"] = "Interval(ms)",
            ["接收模式"] = "Receive Mode",
            ["接收编码"] = "RX Encoding",
            ["发送模式"] = "Send Mode",
            ["发送编码"] = "TX Encoding",
            ["显示模式"] = "Display Mode",
            ["数据点数"] = "Data Points",
            ["最小值"] = "Min",
            ["最大值"] = "Max",
            ["频率"] = "Frequency",
            ["周期"] = "Period",
            ["模式"] = "Mode",
            ["名字"] = "Name",
            ["内容"] = "Content",
            ["宽度"] = "Width",
            ["高度"] = "Height",
            ["颜色"] = "Color",
            ["模块名"] = "Module Name",
            ["步长"] = "Step",
            ["轨道"] = "Track",
            ["拇指"] = "Thumb",
            ["发送间隔"] = "Send Interval",
            ["统一颜色"] = "Uniform Color",
            ["预设尺寸"] = "Preset Size",
            ["FFT 点数"] = "FFT Points",
            ["采样率"] = "Sample Rate",
            ["窗函数"] = "Window",
            ["频率范围"] = "Freq Range",
            ["占空比"] = "Duty Cycle",
            ["双电平置信度"] = "2-Level Confidence",
            ["边沿陡度"] = "Edge Steepness",
            ["信号类型"] = "Signal Type",
            ["时间跨度"] = "Time Span",
            ["基频"] = "Fundamental",
            ["基频幅度"] = "Fund. Magnitude",
            ["DC 偏置"] = "DC Offset",
            ["信噪比 SNR"] = "Signal-to-Noise",
            ["频率分辨率"] = "Freq Resolution",
            ["有效带宽"] = "Eff. Bandwidth",
            ["数据位"] = "Data Bits",
            ["停止位"] = "Stop Bits",
            ["校验位"] = "Parity",
            ["流控"] = "Flow Control",
            ["峰峰值 Vpp"] = "Peak-to-Peak Vpp",
            ["最大值 Vmax"] = "Maximum Vmax",
            ["最小值 Vmin"] = "Minimum Vmin",
            ["平均值 Avg"] = "Average Avg",
            ["有效值 RMS"] = "RMS",
            ["高电平 Th"] = "High Th",
            ["低电平 Tl"] = "Low Tl",
            ["上升时间 Tr"] = "Rise Tr",
            ["下降时间 Tf"] = "Fall Tf",
            ["抖动 σ"] = "Jitter σ",
            ["⏸ 暂停显示"] = "⏸ Pause Display",

            // 切换按钮
            ["切换中/English"] = "Switch ZH/EN",

            // PlotDetail 卡标题
            ["数据源"] = "Data Source",
            ["📊 幅值"] = "📊 Amplitude",
            ["⏱ 时间 / 频率"] = "⏱ Time / Freq",
            ["📈 波形特征"] = "📈 Waveform",
            ["📦 数据"] = "📦 Data",
            ["📶 频域"] = "📶 Freq Domain",

            // 各面板侧栏 — 卡/节标题 + 标签
            ["按键反馈"] = "Key Feedback",
            ["按下："] = "Press:",
            ["发送内容："] = "Content:",
            ["模块设置"] = "Module Settings",
            ["按下发送"] = "Press Value",
            ["松开发送"] = "Release Value",
            ["值："] = "Value:",
            ["批量编辑"] = "Batch Edit",
            ["外观"] = "Appearance",
            ["滑杆反馈"] = "Slider Feedback",
            ["快速预设"] = "Quick Presets",
            ["最近操作："] = "Last:",
            ["当前值："] = "Current:",
            ["范围："] = "Range:",
            ["最小值："] = "Min:",
            ["最大值："] = "Max:",
            ["步长："] = "Step:",
            ["发送间隔(ms)："] = "Interval (ms):",
            ["气压基准值(hPa)："] = "Baseline (hPa):",
            ["非标准参考值"] = "Non-standard baseline",
            // 传感器卡片详情标签（AddDetailRow）
            ["辅助参数："] = "Extra:",
            ["告警原因："] = "Alarm:",
            ["单位："] = "Unit:",
            ["参考值："] = "Baseline:",
            ["摇杆反馈"] = "Joystick Feedback",
            ["橡皮擦大小"] = "Eraser Size",
            ["画布尺寸"] = "Canvas Size",
            ["位置"] = "Position",
            ["内容"] = "Content",
            ["字号"] = "Font Size",
            ["颜色"] = "Color",
            ["线宽"] = "Line Width",

            // 空态 & 提示
            ["输入要发送的内容…"] = "Type to send...",
            ["Enter 发送  ·  Shift+Enter 换行  ·  Ctrl+L 清空接收  ·  Ctrl+P 暂停  ·  Ctrl+Enter 开关串口"] = "Enter send · Shift+Enter newline · Ctrl+L clear · Ctrl+P pause · Ctrl+Enter toggle",
            ["串口已断开 · 数据冻结"] = "Disconnected · Data frozen",
            ["  Shift 切换大小写"] = "  Shift toggles case",

            // Joystick 标签
            ["底板"] = "Pad",
            ["拇指"] = "Thumb",

            // 设置页标题（不带 emoji）
            ["串口配置"] = "Serial Settings",
            ["快捷键提示"] = "Shortcuts",
            ["使用示例"] = "Examples",
            ["素材自定义"] = "Assets",

            // About 页
            ["制作"] = "Made by",
            ["技术栈"] = "Tech Stack",
            ["运行时"] = "Runtime",
            ["数据"] = "Data",
            ["反馈"] = "Feedback",
            ["📂 素材文件夹"] = "📂 Assets Folder",

            // 空态文字
            ["点击"] = "Click",
            ["[编辑]"] = "[Edit]",
            ["[+ 添加]"] = "[+ Add]",
            [" 开始添加按键"] = " to start adding keys",
            [" 或 "] = " or ",
            ["[⌨ 键盘布局]"] = "[⌨ Layout]",
            [" 批量创建"] = " to batch create",
            [" 开始添加滑杆"] = " to start adding sliders",
            [" 创建滑杆控件"] = " to create slider controls",

            // 面板说明文字
            ["点击左侧按键发送数据。消息回显可在接收区设置中开启。"] = "Click keys to send data. Echo can be enabled in receive settings.",
            ["选择左侧按键以编辑属性。Ctrl+点击可多选，Ctrl+A 全选。右键快速删除。"] = "Select a key to edit. Ctrl+click for multi-select, Ctrl+A select all. Right-click to delete.",
            ["拖拽滑杆发送数值。拖拽过程中节流发送，松手时发送最终值。"] = "Drag sliders to send values. Throttled during drag, final value sent on release.",
            ["选择左侧滑杆以编辑属性。"] = "Select a slider to edit.",

            // Run 说明文字
            ["🖱 操作："] = "🖱 Controls:",
            ["滚轮缩放"] = "Scroll to zoom",
            [" · 右键拖拽平移"] = " · Right-drag to pan",
            [" · 悬停查看数值"] = " · Hover to see values",
            ["📊 暂停后点上方「详细」按钮，选择曲线查看统计数据"] = "📊 Pause then click Detail above, select a curve to view stats",
            ["🖱 滚轮缩放 · 右键拖拽平移"] = "🖱 Scroll zoom · Right-drag pan",
            ["📊 暂停后点「详细」按钮查看频谱指标"] = "📊 Pause then click Detail to view FFT metrics",
            ["拖拽摇杆圆钮发送 X/Y 轴位置。松手时发送最终值。"] = "Drag knob to send X/Y position. Final value sent on release.",
            ["协议："] = "Protocol:",
            ["📡 正常模式："] = "📡 Normal:",
            ["侧栏显示卡片概览和快速跳转。"] = "Sidebar shows card overview and quick navigation.",
            ["✏ 编辑模式："] = "✏ Edit Mode:",
            ["侧栏变为行管理器——添加/删除/排序卡片和组。"] = "Sidebar becomes row manager — add/delete/sort cards and groups.",
            ["坐标轴：起/中/终点标有刻度数字"] = "Axes: start/mid/end labeled with scale values",
            ["C 代码写法："] = "C code format:",
            ["⚠ 内层引号前加 \\ 转义"] = "⚠ Escape inner quotes with \\",
            ["[display-clear] 清屏"] = "[display-clear] clears screen",

            // 设置页素材自定义
            ["滑杆和摇杆支持自定义图片素材。将 PNG 放入素材文件夹，软件在下拉菜单中自动识别新风格。"] = "Sliders and joysticks support custom image assets. Place PNGs in the assets folder — they appear in dropdown menus automatically.",
            ["👆 点击上方蓝色路径即可复制，粘贴到文件管理器打开此文件夹"] = "👆 Click the path above to copy, paste into file manager to open this folder",
            ["在此文件夹下创建 joystick/ 和 sliders/ 子目录，按下方命名规则放入 PNG 即可。"] = "Create joystick/ and sliders/ subdirectories, place PNGs following the naming rules below.",
            ["🕹 摇杆 — 底板 & 拇指"] = "🕹 Joystick — Pad & Thumb",
            ["📍 操作：摇杆标签页 → 找到「⟲ 回中」按钮 → 旁边有「底板 ▾」和「拇指 ▾」两个下拉按钮 → 点开即见你的自定义风格。"] = "📍 Usage: Joystick tab → find ⟲ Center button → Pad/Thumb dropdowns beside it → open to see your custom styles.",
            ["内置风格（手柄风 / 极简风 / 经典风）始终可用。同名 PNG 可覆盖内置的代码绘制。"] = "Built-in styles (Gamepad / Minimal / Classic) are always available. Same-name PNG overrides the coded version.",
            ["🎚 滑杆 — 轨道 & 滑钮"] = "🎚 Slider — Track & Thumb",
            ["📍 操作：滑杆标签页 → 点击 [编辑] → 右侧面板滚动到底部 → 点「轨道 ▾」或「拇指 ▾」下拉按钮选择风格。轨道和滑钮可以独立混搭。"] = "📍 Usage: Sliders tab → click Edit → scroll side panel to bottom → use Track/Thumb dropdowns. Track and thumb can be mixed independently.",
            ["内置风格（默认 / 极简）始终可用。删掉对应 PNG 文件后重启软件，菜单中自动消失。"] = "Built-in styles (Default / Minimal) are always available. Delete the PNG and restart — it disappears from the menu.",

            // 含逗号/等号 → C# Inlines 重建
            ["🔬 数据源选 [plot,...] 通道 → PC 自动算 FFT"] = "🔬 Select [plot,...] channel → PC auto-computes FFT",
            ["收到 [fft,...] 协议则覆盖自动频谱"] = "Receiving [fft,...] protocol overrides auto spectrum",
            ["末尾 #RRGGBB 可选，不写=白色"] = "Trailing #RRGGBB is optional, defaults to white",
            ["设备通过串口发送 "] = "Devices send messages over serial in ",
            [" 格式的消息，"] = " format. ",
            ["工具自动解析并路由到对应面板。方括号 "] = "The tool auto-parses and routes to the corresponding panel. ",
            [" 包裹每条消息，参数用逗号分隔。含逗号的参数用双引号包裹。"] = " brackets wrap each message, parameters separated by commas. Parameters containing commas should be quoted.",

            // 使用示例页 — 标题
            ["波形数据"] = "Waveform Data",
            ["按键事件"] = "Key Events",
            ["滑杆数值"] = "Slider Values",
            ["摇杆位置"] = "Joystick Position",
            ["传感数据"] = "Sensor Data",
            ["控制指令"] = "Control Commands",
            ["频谱数据"] = "FFT Spectrum Data",
            ["OLED 绘图协议"] = "OLED Drawing Protocol",

            // 使用示例页 — 描述
            ["发送数值到 PC，在 Plot 面板实时绘制波形曲线。建议发送频率 10~100 Hz。"] = "Send values to PC, plot real-time waveform curves. Recommended rate: 10-100 Hz.",
            ["设备端按键按下/松开时通知 PC，KeyPanel 对应按键高亮并可选回传命令。"] = "Notify PC on key press/release — KeyPanel highlights the key and optionally sends commands back.",
            ["发送数值到 PC，Slider 面板对应滑块同步位置。常用于传感器反馈。"] = "Send values to PC, SliderPanel syncs position. Commonly used for sensor feedback.",
            ["发送摇杆坐标到 PC，Joystick 面板实时显示位置。常用于遥控器或姿态反馈。"] = "Send joystick coordinates to PC, JoystickPanel shows position in real time. Useful for remote control or attitude feedback.",
            ["MCU 上报传感器数据，PC 端传感面板自动建卡。子类型决定卡片样式（竖条色/进度条/波形/开关）。"] = "MCU reports sensor data, PC SensorPanel auto-creates cards. Sub-type determines card style (bar color / progress bar / waveform / switch).",
            ["PC 端向 MCU 发送控制指令。开关卡点击自动发送，滑杆卡拖拽节流发送（间隔在卡片详情面板设置）。"] = "PC sends control commands to MCU. Switch cards auto-send on click; slider cards throttle-send on drag (interval configurable in card detail panel).",
            ["MCU 发送 FFT 频谱数据，PC 端频域页显示柱状图。PC 端亦可从 [plot,...] 原始波形自动滑窗 FFT（MCU 零代码）。"] = "MCU sends FFT spectrum data — PC FFT page shows bar chart. PC can also auto-run sliding-window FFT from [plot,...] raw waveform (zero MCU code).",
            ["通过串口发送绘图指令，PC 虚拟 OLED 面板实时渲染。支持 11 种图形 + 清屏 + F5 增量同步（[draw,set,id,...]/[draw,del,id]）。旧 [display,...] 由 [draw,text,...] 取代。\n\n协议统一在 [draw,<type>,<params>...] 命名空间下。末尾可追加 fill（填充，忽略线宽）或 a<角度>（旋转度数，仅 rect/ellipse）。"] = "Send drawing commands over serial; PC virtual OLED panel renders in real time. Supports 11 shapes + clear + F5 incremental sync ([draw,set,id,...]/[draw,del,id]). Legacy [display,...] is replaced by [draw,text,...].\n\nProtocol is unified under [draw,<type>,<params>...] namespace. Append fill (filled, ignores line width) or a<degrees> (rotation, rect/ellipse only).",

            // 使用示例页 — 静态标签
            ["📋 复制代码"] = "📋 Copy Code",
            ["子类型速查"] = "Sub-type QuickRef",
            ["基础图形"] = "Basic Shapes",
            ["进阶 & 增量同步"] = "Advanced & Sync",
            ["fill → 填充模式（忽略线宽），a<度数> → 旋转（仅 rect / ellipse）"] = "fill → filled mode (ignores line width), a<deg> → rotation (rect/ellipse only)",
            ["常用示例"] = "Common Examples",
            ["设备端代码（MCU printf wrapper）"] = "MCU Code (printf wrapper)",
            ["协议格式"] = "Protocol Format",
            ["协议示例"] = "Protocol Example",
            ["参数"] = "Parameters",
            ["设备端代码"] = "MCU Code",

            // Draw 子类型速查 — 注释
            ["画点"] = "draw point",
            ["画线，w 默认 1"] = "draw line, w defaults to 1",
            ["空心/实心矩形，支持旋转"] = "hollow/filled rectangle, supports rotation",
            ["空心/实心圆"] = "hollow/filled circle",
            ["空心/实心三角形"] = "hollow/filled triangle",
            ["文字"] = "text",
            ["清屏，默认底色 #111111"] = "clear screen, default BG #111111",
            ["空心/实心椭圆，支持旋转"] = "hollow/filled ellipse, supports rotation",
            ["弧线/扇形，角度制·顺时针"] = "arc/sector, degrees·clockwise",
            ["圆角矩形"] = "rounded rectangle",
            ["增量同步：创建或更新"] = "incremental: create or update",
            ["增量同步：删除图形"] = "incremental: delete shape",

            // Draw 常用示例 — 标签
            ["空心矩形·省略线宽（默认 1）"] = "hollow rect, default line width (1)",
            ["空心矩形·指定线宽 2"] = "hollow rect, line width 2",
            ["实心矩形（fill 忽略线宽）"] = "filled rect (fill ignores line width)",
            ["空心矩形·旋转 45°"] = "hollow rect, rotated 45°",
            ["空心圆·省略线宽"] = "hollow circle, default line width",
            ["实心圆"] = "filled circle",
            ["清屏（默认底色）"] = "clear (default BG)",
            ["清屏·指定底色"] = "clear, custom BG",

            // 快捷键页
            ["全局"] = "Global",
            ["发送区"] = "Send",
            ["打开 / 关闭串口"] = "Open / Close Serial",
            ["暂停 / 继续显示"] = "Pause / Resume",
            ["清空接收区"] = "Clear Receive",
            ["清空发送区"] = "Clear Send",
            ["呼出接收区搜索栏"] = "Open Search Bar",
            ["发送"] = "Send",
            ["亮"] = "Light",
            ["暗"] = "Dark",
            ["📡 协议消息"] = "📡 Protocol Messages",
            ["普通文本"] = "Plain Text",
            // "+" 管理标签 Popup
            ["📡 接收区"] = "📡 Receive",
            ["📈 波形图"] = "📈 Plot",
            ["🎮 按键面板"] = "🎮 Keys",
            ["🎚 滑杆面板"] = "🎚 Sliders",
            ["📱 OLED"] = "📱 OLED",
            ["🕹 摇杆面板"] = "🕹 Joystick",
            ["✓ 已复制"] = "✓ Copied",
            ["点击查看更新内容"] = "Click to view update details",
            ["展开侧面板"] = "Expand sidebar",
            ["（暂无历史记录）"] = "(No history)",
            ["编辑标签"] = "Edit Label",
            ["删除"] = "Delete",
            ["将当前输入区内容添加为快捷发送按钮"] = "Save current input as quick-send button",
            ["角度：右=0°，顺时针为正（下=90°）"] = "Angle: right=0°, CW positive (down=90°)",
            ["📄 全选"] = "📄 Select All",
            ["🗑 清空接收区"] = "🗑 Clear Receive",
            ["⏸ 暂停显示"] = "⏸ Pause Display",
            ["▭ 矩形"] = "▭ Rectangle",
            ["▢ 圆角矩形"] = "▢ Rounded Rect",
            ["◯ 圆"] = "◯ Circle",
            ["⬭ 椭圆"] = "⬭ Ellipse",
            // 内置风格名
            ["默认"] = "Default",
            ["极简"] = "Minimal",
            ["手柄风"] = "Gamepad",
            ["极简风"] = "Minimal",
            ["经典风"] = "Classic",
            ["自定义"] = "Custom",
            // 素材命名示例
            ["pad_风格名.png"] = "pad_stylename.png",
            ["thumb_风格名.png"] = "thumb_stylename.png",
            ["track_风格名.png"] = "track_stylename.png",
            // 素材尺寸标注
            ["底板   140×140 px"] = "Base   140×140 px",
            ["拇指    32×32 px"] = "Thumb    32×32 px",
            ["轨道横条   ≥200×4~12 px"] = "Track bar   ≥200×4~12 px",
            ["拖拽滑钮   16~48 px 方形"] = "Thumb knob   16~48 px square",
            // 风格菜单描述
            ["4px / 2px 轨道高度"] = "4px / 2px track height",
            ["16px / 12px 圆形拖钮"] = "16px / 12px round thumb",
            ["14×14 方角矩形 · 无描边"] = "14×14 square corner · no stroke",
            ["图片自定义"] = "Custom image",
            ["方块"] = "Square",
            ["同心参考圆 + 8 方向标记 + 方向指示线"] = "Concentric guide circle + 8-way markers + direction indicator",
            ["圆角底座 + 网格点阵 + X 色条"] = "Rounded base + grid dots + X color bar",
            ["50% 虚线圆 + 阴影 + X/Y 分行"] = "50% dashed circle + shadow + X/Y split",
            ["📡 传感面板"] = "📡 Sensors",
            // 协议子类型标签
            ["传感"] = "Sensor",
            ["控制"] = "Control",
            ["波形"] = "Plot",
            ["滑杆"] = "Slider",
            ["按键"] = "Key",
            ["摇杆"] = "Joystick",
            ["OLED"] = "OLED",
            ["频谱"] = "FFT",
            ["绘图"] = "Draw",
            ["换行"] = "New Line",

            // 使用示例 — 参数名
            ["通道名"] = "Channel Name",
            ["数值"] = "Value",
            ["名称"] = "Name",
            ["状态"] = "State",
            ["子类型"] = "Sub-type",
            ["卡片名"] = "Card Name",
            ["辅助参数"] = "Extra Param",
            ["动作"] = "Action",
            ["点数"] = "Bin Count",
            ["bin值"] = "Bin Values",

            // 使用示例 — 参数描述 (plot)
            ["曲线的标识名称，如 \"ch1\"、\"温度\""] = "Curve identifier, e.g. \"ch1\", \"temperature\"",
            ["浮点数，如 25.3、1024、-0.5"] = "Floating-point number, e.g. 25.3, 1024, -0.5",

            // 使用示例 — 参数描述 (key)
            ["按键标识，需匹配 KeyPanel 中已定义的按键名"] = "Key identifier, must match a key name defined in KeyPanel",
            ["\"down\"（按下）或 \"up\"（松开）"] = "\"down\" (pressed) or \"up\" (released)",

            // 使用示例 — 参数描述 (slider)
            ["滑块标识，匹配 SliderPanel 中已定义的滑块名"] = "Slider identifier, matching a defined slider name in SliderPanel",
            ["浮点数，范围由面板设置决定，如 0~1023"] = "Floating-point number, range defined by panel settings, e.g. 0~1023",

            // 使用示例 — 参数描述 (joystick)
            ["摇杆编号（整数），如 0"] = "Joystick ID (integer), e.g. 0",
            ["X 轴坐标，范围 0~255"] = "X-axis coordinate, range 0~255",
            ["Y 轴坐标，范围 0~255"] = "Y-axis coordinate, range 0~255",

            // 使用示例 — 参数描述 (ctrl)
            ["led / relay / slider（PC→MCU 方向。固件端自行解析，可扩展）"] = "led / relay / slider (PC→MCU direction. Firmware parses as needed, extensible)",
            ["匹配传感面板中已存在的卡片名"] = "Matches an existing card name in SensorPanel",
            ["开关类：on / off；滑杆类：浮点数值"] = "Switch type: on / off; Slider type: floating-point value",

            // 使用示例 — 参数描述 (fft)
            ["FFT 数据标识名，出现在频域页数据源下拉框（📶 前缀）。兼容旧格式省略通道名"] = "FFT data identifier, appears in Freq page source dropdown (📶 prefix). Backward-compatible with old format (omit channel name)",
            ["FFT bin 数量（整数），如 64/128/256/512"] = "Number of FFT bins (integer), e.g. 64/128/256/512",
            ["各频率 bin 归一化幅度（0~1），从低到高排列。bin 数需与点数一致"] = "Normalized magnitude per bin (0~1), ordered low to high. Bin count must match point count",

            // 协议格式字符串
            ["[plot,通道名,数值]"] = "[plot,Channel,Value]",
            ["[key,名称,状态]"] = "[key,Name,State]",
            ["[slider,名称,数值]"] = "[slider,Name,Value]",
            ["[sensor,子类型,卡片名,数值,辅助参数]"] = "[sensor,SubType,CardName,Value,Extra]",
            ["[ctrl,子类型,卡片名,动作/数值]"] = "[ctrl,SubType,CardName,Action/Value]",
            ["[fft,通道名,点数,bin0,bin1,...]"] = "[fft,Channel,BinCount,bin0,bin1,...]",

            // 代码示例注释
            ["// 每 50ms 发送一次\r\nprintf(\"[plot,ch1,%.1f]\\r\\n\", adc_val);"] = "// Send every 50ms\r\nprintf(\"[plot,ch1,%.1f]\\r\\n\", adc_val);",
            ["// 按键按下\r\nprintf(\"[key,btn_a,down]\\r\\n\");\r\n// 按键松开\r\nprintf(\"[key,btn_a,up]\\r\\n\");"] = "// Key press\r\nprintf(\"[key,btn_a,down]\\r\\n\");\r\n// Key release\r\nprintf(\"[key,btn_a,up]\\r\\n\");",
            ["// 发送传感器读数\r\nprintf(\"[slider,speed,%.1f]\\r\\n\", sensor_val);"] = "// Send sensor reading\r\nprintf(\"[slider,speed,%.1f]\\r\\n\", sensor_val);",
            ["// 发送摇杆位置\r\nprintf(\"[joystick,0,%d,%d]\\r\\n\", x_adc, y_adc);"] = "// Send joystick position\r\nprintf(\"[joystick,0,%d,%d]\\r\\n\", x_adc, y_adc);",
            ["// 温度（带最大值）\r\nprintf(\"[sensor,temp,芯片温度,%.1f,%.1f]\\r\\n\", val, max_val);\r\n// 湿度（自动进度条）\r\nprintf(\"[sensor,humidity,环境湿度,%.1f]\\r\\n\", humidity);\r\n// 状态（在线/告警/离线）\r\nprintf(\"[sensor,status,主板,online]\\r\\n\");\r\n// 开关（点击回控 MCU）\r\nprintf(\"[sensor,control,主板LED,off]\\r\\n\");\r\n// 通用（自定义单位/颜色）\r\nprintf(\"[sensor,generic,电池电压,%.2f]\\r\\n\", battery_v);"] = "// Temperature (with max)\r\nprintf(\"[sensor,temp,芯片温度,%.1f,%.1f]\\r\\n\", val, max_val);\r\n// Humidity (auto progress bar)\r\nprintf(\"[sensor,humidity,环境湿度,%.1f]\\r\\n\", humidity);\r\n// Status (online/alarm/offline)\r\nprintf(\"[sensor,status,主板,online]\\r\\n\");\r\n// Switch (click to control MCU)\r\nprintf(\"[sensor,control,主板LED,off]\\r\\n\");\r\n// Generic (custom unit/color)\r\nprintf(\"[sensor,generic,电池电压,%.2f]\\r\\n\", battery_v);",
            ["// MCU 端解析 ctrl 消息控制硬件\r\nif (strcmp(type, \"ctrl\") == 0) {\r\n    if (strcmp(subType, \"led\") == 0)\r\n        HAL_GPIO_WritePin(LED_PORT, LED_PIN,\r\n            strcmp(action, \"on\") == 0\r\n                ? GPIO_PIN_RESET : GPIO_PIN_SET);\r\n}"] = "// MCU parses ctrl message to control hardware\r\nif (strcmp(type, \"ctrl\") == 0) {\r\n    if (strcmp(subType, \"led\") == 0)\r\n        HAL_GPIO_WritePin(LED_PORT, LED_PIN,\r\n            strcmp(action, \"on\") == 0\r\n                ? GPIO_PIN_RESET : GPIO_PIN_SET);\r\n}",

            // 传感器参数描述
            ["浮点数 / on·off / online·alarm·error·offline"] = "Float / on·off / online·alarm·error·offline",
            ["可选。温度=最大值，湿度=露点，气压=趋势，状态=告警原因…"] = "Optional. Temp=max value, Humidity=dew point, Pressure=trend, Status=alarm reason...",
            ["显示名称，支持中文。跨所有组唯一。"] = "Display name, supports CJK. Must be unique across all groups.",

            // FFT 代码
            ["// CMSIS-DSP FFT（MCU 端运算）\r\nprintf(\"[fft,ch1,%d\", N);\r\nfor (int i = 0; i < N; i++)\r\n    printf(\",%.2f\", mag[i]);\r\nprintf(\"]\\r\\n\");\r\n// 或交给 PC 端自动 FFT：发 [plot,...] 后选 📈 数据源即可"] = "// CMSIS-DSP FFT (MCU computes)\r\nprintf(\"[fft,ch1,%d\", N);\r\nfor (int i = 0; i < N; i++)\r\n    printf(\",%.2f\", mag[i]);\r\nprintf(\"]\\r\\n\");\r\n// Or let PC auto-FFT: send [plot,...] then select 📈 as data source",

            // 传感器子类型速查（长描述）
            ["8 种（与添加卡片面板一致）：\n" +
             "  temp      温度卡 — 黄色竖条 + 迷你波形\n" +
             "  humidity  湿度卡 — 蓝色竖条 + 进度条 + 迷你波形\n" +
             "  pressure  气压卡 — 青色竖条 + 进度条 + 迷你波形\n" +
             "  status    状态卡 — 绿/红色竖条，无波形，支持 alarm/error/offline\n" +
             "  control   开关卡 — 橙色竖条 + 胶囊开关，点击回控 MCU\n" +
             "  motor     电机卡 — 紫色竖条 + 迷你波形 + 转速单位\n" +
             "  slider    滑杆卡 — 靛蓝竖条 + Slider + ±微调 + 发送间隔\n" +
             "  generic   通用卡 — 灰色竖条，自定义名称/单位/颜色"] = "8 types (same as Add Card panel):\n" +
             "  temp      Temperature — yellow bar + mini waveform\n" +
             "  humidity  Humidity    — blue bar + progress + mini waveform\n" +
             "  pressure  Pressure    — cyan bar + progress + mini waveform\n" +
             "  status    Status      — green/red bar, no waveform, supports alarm/error/offline\n" +
             "  control   Switch      — orange bar + toggle, click to control MCU\n" +
             "  motor     Motor       — purple bar + mini waveform + RPM unit\n" +
             "  slider    Slider      — indigo bar + Slider + ±adjust + send interval\n" +
             "  generic   Generic     — gray bar, custom name/unit/color",

            // Draw 代码库
            ["// ═══ MCU printf wrapper（复制到工程任意 .c 文件） ═══\r\n#include <stdio.h>\r\n\r\n" +
             "// ── 文字 ──\r\nvoid Draw_Text(int x, int y, const char *t, int fs, const char *c)\r\n" +
             "{ printf(\"[draw,text,%d,%d,%s,%d,%s]\\r\\n\", x, y, t, fs, c); }\r\n\r\n" +
             "// ── 画点 ──\r\nvoid Draw_Point(int x, int y, const char *c)\r\n" +
             "{ printf(\"[draw,point,%d,%d,%s]\\r\\n\", x, y, c); }\r\n\r\n" +
             "// ── 画线 ──\r\nvoid Draw_Line(int x1, int y1, int x2, int y2, const char *c)\r\n" +
             "{ printf(\"[draw,line,%d,%d,%d,%d,%s]\\r\\n\", x1, y1, x2, y2, c); }\r\n" +
             "void Draw_LineW(int x1, int y1, int x2, int y2, const char *c, int w)\r\n" +
             "{ printf(\"[draw,line,%d,%d,%d,%d,%s,%d]\\r\\n\", x1, y1, x2, y2, c, w); }\r\n\r\n" +
             "// ── 矩形（空心/实心） ──\r\nvoid Draw_Rect(int x, int y, int w, int h, const char *c)\r\n" +
             "{ printf(\"[draw,rect,%d,%d,%d,%d,%s]\\r\\n\", x, y, w, h, c); }\r\n" +
             "void Draw_Fill(int x, int y, int w, int h, const char *c)\r\n" +
             "{ printf(\"[draw,rect,%d,%d,%d,%d,%s,1,fill]\\r\\n\", x, y, w, h, c); }\r\n\r\n" +
             "// ── 圆（空心/实心） ──\r\nvoid Draw_Circle(int cx, int cy, int r, const char *c)\r\n" +
             "{ printf(\"[draw,circle,%d,%d,%d,%s]\\r\\n\", cx, cy, r, c); }\r\n" +
             "void Draw_CircleFilled(int cx, int cy, int r, const char *c)\r\n" +
             "{ printf(\"[draw,circle,%d,%d,%d,%s,1,fill]\\r\\n\", cx, cy, r, c); }\r\n\r\n" +
             "// ── 椭圆 ──\r\nvoid Draw_Ellipse(int cx, int cy, int a, int b, const char *c)\r\n" +
             "{ printf(\"[draw,ellipse,%d,%d,%d,%d,%s]\\r\\n\", cx, cy, a, b, c); }\r\n\r\n" +
             "// ── 三角形（空心/实心） ──\r\nvoid Draw_Triangle(int x0, int y0, int x1, int y1, int x2, int y2, const char *c)\r\n" +
             "{ printf(\"[draw,triangle,%d,%d,%d,%d,%d,%d,%s]\\r\\n\", x0, y0, x1, y1, x2, y2, c); }\r\n" +
             "void Draw_TriangleFilled(int x0, int y0, int x1, int y1, int x2, int y2, const char *c)\r\n" +
             "{ printf(\"[draw,triangle,%d,%d,%d,%d,%d,%d,%s,1,fill]\\r\\n\", x0, y0, x1, y1, x2, y2, c); }\r\n\r\n" +
             "// ── 清屏 ──\r\nvoid Draw_Clear(void)\r\n" +
             "{ printf(\"[draw,clear]\\r\\n\"); }\r\n" +
             "void Draw_ClearColor(const char *c)\r\n" +
             "{ printf(\"[draw,clear,%s]\\r\\n\", c); }"] =
             "// ═══ MCU printf wrapper (copy to any .c file) ═══\r\n#include <stdio.h>\r\n\r\n" +
             "// ── Text ──\r\nvoid Draw_Text(int x, int y, const char *t, int fs, const char *c)\r\n" +
             "{ printf(\"[draw,text,%d,%d,%s,%d,%s]\\r\\n\", x, y, t, fs, c); }\r\n\r\n" +
             "// ── Point ──\r\nvoid Draw_Point(int x, int y, const char *c)\r\n" +
             "{ printf(\"[draw,point,%d,%d,%s]\\r\\n\", x, y, c); }\r\n\r\n" +
             "// ── Line ──\r\nvoid Draw_Line(int x1, int y1, int x2, int y2, const char *c)\r\n" +
             "{ printf(\"[draw,line,%d,%d,%d,%d,%s]\\r\\n\", x1, y1, x2, y2, c); }\r\n" +
             "void Draw_LineW(int x1, int y1, int x2, int y2, const char *c, int w)\r\n" +
             "{ printf(\"[draw,line,%d,%d,%d,%d,%s,%d]\\r\\n\", x1, y1, x2, y2, c, w); }\r\n\r\n" +
             "// ── Rectangle (hollow/filled) ──\r\nvoid Draw_Rect(int x, int y, int w, int h, const char *c)\r\n" +
             "{ printf(\"[draw,rect,%d,%d,%d,%d,%s]\\r\\n\", x, y, w, h, c); }\r\n" +
             "void Draw_Fill(int x, int y, int w, int h, const char *c)\r\n" +
             "{ printf(\"[draw,rect,%d,%d,%d,%d,%s,1,fill]\\r\\n\", x, y, w, h, c); }\r\n\r\n" +
             "// ── Circle (hollow/filled) ──\r\nvoid Draw_Circle(int cx, int cy, int r, const char *c)\r\n" +
             "{ printf(\"[draw,circle,%d,%d,%d,%s]\\r\\n\", cx, cy, r, c); }\r\n" +
             "void Draw_CircleFilled(int cx, int cy, int r, const char *c)\r\n" +
             "{ printf(\"[draw,circle,%d,%d,%d,%s,1,fill]\\r\\n\", cx, cy, r, c); }\r\n\r\n" +
             "// ── Ellipse ──\r\nvoid Draw_Ellipse(int cx, int cy, int a, int b, const char *c)\r\n" +
             "{ printf(\"[draw,ellipse,%d,%d,%d,%d,%s]\\r\\n\", cx, cy, a, b, c); }\r\n\r\n" +
             "// ── Triangle (hollow/filled) ──\r\nvoid Draw_Triangle(int x0, int y0, int x1, int y1, int x2, int y2, const char *c)\r\n" +
             "{ printf(\"[draw,triangle,%d,%d,%d,%d,%d,%d,%s]\\r\\n\", x0, y0, x1, y1, x2, y2, c); }\r\n" +
             "void Draw_TriangleFilled(int x0, int y0, int x1, int y1, int x2, int y2, const char *c)\r\n" +
             "{ printf(\"[draw,triangle,%d,%d,%d,%d,%d,%d,%s,1,fill]\\r\\n\", x0, y0, x1, y1, x2, y2, c); }\r\n\r\n" +
             "// ── Clear ──\r\nvoid Draw_Clear(void)\r\n" +
             "{ printf(\"[draw,clear]\\r\\n\"); }\r\n" +
             "void Draw_ClearColor(const char *c)\r\n" +
             "{ printf(\"[draw,clear,%s]\\r\\n\", c); }",

            // 使用示例 — 协议描述
            ["发送数值到 PC，在 Plot 面板实时绘制波形曲线。建议发送频率 10~100 Hz。"] = "Send values to PC; plots real-time waveform curves in the Plot panel. Recommended send rate 10~100 Hz.",
            ["设备端按键按下/松开时通知 PC，KeyPanel 对应按键高亮并可选回传命令。"] = "Notifies PC on key press/release; KeyPanel highlights the corresponding key and can optionally send back commands.",
            ["发送数值到 PC，Slider 面板对应滑块同步位置。常用于传感器反馈。"] = "Sends values to PC; Slider panel synchronizes the corresponding slider position. Commonly used for sensor feedback.",
            ["发送摇杆坐标到 PC，Joystick 面板实时显示位置。常用于遥控器或姿态反馈。"] = "Sends joystick coordinates to PC; Joystick panel shows real-time position. Commonly used for remote control or attitude feedback.",
            ["MCU 上报传感器数据，PC 端传感面板自动建卡。子类型决定卡片样式（竖条色/进度条/波形/开关）。"] = "MCU reports sensor data; PC Sensor panel auto-creates cards. Sub-type determines card style (color bar/progress/waveform/switch).",
            ["PC 端向 MCU 发送控制指令。开关卡点击自动发送，滑杆卡拖拽节流发送（间隔在卡片详情面板设置）。"] = "PC sends control commands to MCU. Switch cards auto-send on click; slider cards throttle-send on drag (interval set in card detail panel).",
            ["MCU 发送 FFT 频谱数据，PC 端频域页显示柱状图。PC 端亦可从 [plot,...] 原始波形自动滑窗 FFT（MCU 零代码）。"] = "MCU sends FFT spectrum data; PC Frequency page shows bar chart. PC can also auto FFT from [plot,...] raw waveform (zero MCU code).",
            ["通过串口发送绘图指令，PC 虚拟 OLED 面板实时渲染。支持 11 种图形 + 清屏 + F5 增量同步（[draw,set,id,...]/[draw,del,id]）。旧 [display,...] 由 [draw,text,...] 取代。\n\n协议统一在 [draw,<type>,<params>...] 命名空间下。末尾可追加 fill（填充，忽略线宽）或 a<角度>（旋转度数，仅 rect/ellipse）。"] = "Send drawing commands via serial; PC virtual OLED panel renders in real-time. Supports 11 shapes + clear + F5 incremental sync ([draw,set,id,...]/[draw,del,id]). Legacy [display,...] replaced by [draw,text,...].\n\nProtocol unified under [draw,<type>,<params>...] namespace. Append fill (filled, ignores line width) or a<angle> (rotation degrees, rect/ellipse only).",
            ["fill → 填充模式（忽略线宽），a<度数> → 旋转（仅 rect / ellipse）"] = "fill → filled mode (ignore line width), a<degrees> → rotation (rect / ellipse only)",
            ["[joystick,id,x,y]"] = "[joystick,id,x,y]",
            ["[draw,<type>,<params>...]"] = "[draw,<type>,<params>...]",

            // 系统日志模板（{0} 为运行时参数）
            ["开"] = "ON",
            ["关"] = "OFF",
            ["---- 已打开串行端口 {0} ----"] = "---- Port {0} opened ----",
            ["---- 关闭串行端口 {0} ----"] = "---- Port {0} closed ----",
            ["---- 流量计数已重置 ----"] = "---- Traffic counter reset ----",
            ["---- 流量计数持久化：开 ----"] = "---- Traffic persist: ON ----",
            ["---- 流量计数持久化：关 ----"] = "---- Traffic persist: OFF ----",
            ["---- 暂停显示：界面已冻结，后台照常接收 ----"] = "---- Display paused: UI frozen, receiving in background ----",
            ["---- 继续显示：补回暂停期间的 {0} 条数据 ----"] = "---- Resumed: {0} buffered messages ----",
            ["---- 继续显示 ----"] = "---- Resumed ----",
            ["---- 时间戳：关 ----"] = "---- Timestamp: OFF ----",
            ["---- 时间戳：{0} ----"] = "---- Timestamp: {0} ----",
            ["---- 定时发送：开（每 {0} ms）----"] = "---- Timed send: ON (every {0} ms) ----",
            ["---- 定时发送：关 ----"] = "---- Timed send: OFF ----",
            ["---- 消息回显：开 ----"] = "---- Echo: ON ----",
            ["---- 消息回显：关 ----"] = "---- Echo: OFF ----",
            ["---- 行号显示：开 ----"] = "---- Line numbers: ON ----",
            ["---- 行号显示：关 ----"] = "---- Line numbers: OFF ----",
            ["---- 系统消息独立显示：开 ----"] = "---- System messages separate: ON ----",
            ["---- 系统消息独立显示：关 ----"] = "---- System messages separate: OFF ----",
            ["---- 自动重连：开 ----"] = "---- Auto-reconnect: ON ----",
            ["---- 自动重连：关 ----"] = "---- Auto-reconnect: OFF ----",
            ["---- 自动重连：已重新连接 {0} ----"] = "---- Auto-reconnect: reconnected {0} ----",
            ["---- 自动重连超时：未检测到 {0} ----"] = "---- Auto-reconnect timeout: {0} not found ----",
            ["---- DTR：{0} ----"] = "---- DTR: {0} ----",
            ["---- RTS：{0} ----"] = "---- RTS: {0} ----",
            ["---- 已自动选中 {0} ----"] = "---- Auto-selected {0} ----",
            ["---- 日志已导出至 {0} ----"] = "---- Log exported to {0} ----",
            ["---- 波形数据已导出到: {0} ----"] = "---- Waveform data exported to: {0} ----",
            ["---- 导出 CSV 失败: {0} ----"] = "---- CSV export failed: {0} ----",
            ["---- 无{0}数据可导出 ----"] = "---- No {0} data to export ----",
            ["串口打开失败：被其他程序占用或没有访问权限"] = "Port open failed: in use or access denied",
            ["串口打开失败：硬件通信错误 — {0}"] = "Port open failed: hardware error — {0}",
            ["串口打开失败：参数无效 — {0}"] = "Port open failed: invalid params — {0}",
            ["串口打开失败：已被占用 — {0}"] = "Port open failed: port in use — {0}",
            ["HEX 输入含无效字符，已忽略：{0}"] = "HEX input invalid chars ignored: {0}",
            ["发送失败：{0}"] = "Send failed: {0}",
            ["协议路由异常：{0}"] = "Protocol routing error: {0}",
            ["未知协议类型: [{0}]"] = "Unknown protocol type: [{0}]",

            // ComboBox 逻辑值 —— 收发模式
            ["HEX模式"] = "HEX Mode",
            ["文本模式"] = "Text Mode",
            // 校验位
            ["无"] = "None",
            ["奇校验"] = "Odd",
            ["偶校验"] = "Even",
            // 流控
            ["RTS/CTS"] = "RTS/CTS",
            ["XON/XOFF"] = "XON/XOFF",
            // 时间戳
            ["不显示"] = "Off",
            // 换行符 (显示用字面量)
            ["\\r\\n"] = "\\r\\n",
            ["\\n"] = "\\n",
            ["\\r"] = "\\r",
            // 绘图模式
            ["滚动"] = "Roll",
            ["扫描"] = "Sweep",
            // 窗函数（始终双语显示）
            ["汉宁 (Hanning)"] = "Hanning (汉宁)",
            ["矩形 (Rectangular)"] = "Rectangular (矩形)",
            ["汉明 (Hamming)"] = "Hamming (汉明)",
            ["布莱克曼 (Blackman)"] = "Blackman (布莱克曼)",
            // 波形分析
            ["未检测到明显周期"] = "No significant period detected",
            ["周期检测"] = "Period Detection",
            // 使用示例 — 参数名（纯英文标识）
            ["id"] = "id",
            ["x"] = "x",
            ["y"] = "y",
            // FFT 数据源占位
            ["（不选）"] = "(None)",
            // Keys 面板发送模式 (LogicValueMaps.DisplaySendMode 用)
            ["文本"] = "Text",
            ["HEX"] = "HEX",
            ["数据包"] = "Packet",

            // OLED 图形属性面板 — 图形类型
            ["点"] = "Point",
            ["矩形"] = "Rectangle",
            ["圆"] = "Circle",
            ["椭圆"] = "Ellipse",
            ["实心矩形"] = "Filled Rect",
            // OLED 图形属性 — 属性标签
            ["尺寸"] = "Size",
            ["圆心"] = "Center",
            ["半径"] = "Radius",
            ["半轴"] = "Semi-axis",
            ["起点"] = "Start",
            ["终点"] = "End",
            ["圆角"] = "Radius",
            ["宽"] = "W",
            ["高"] = "H",
            ["X"] = "X",
            ["Y"] = "Y",
            ["CX"] = "CX",
            ["CY"] = "CY",
            ["R"] = "R",
            ["A"] = "A",
            ["B"] = "B",
            ["角度 (°)"] = "Angle (°)",
            ["起始"] = "Start",
            ["终止"] = "End",
            ["顶点①"] = "Vertex①",
            ["顶点②"] = "Vertex②",
            ["顶点③"] = "Vertex③",
            // OLED 图形属性 — 动态格式
            ["图形属性 — {0}"] = "Properties — {0}",
            ["类型: {0}"] = "Type: {0}",
            ["类型: {0}（实心）"] = "Type: {0} (filled)",
            // 杂项
            ["⚠ 确认清空"] = "⚠ Confirm Clear",
            ["⚠ 无效 HEX 字符: {0}"] = "⚠ Invalid HEX chars: {0}",
            ["可用"] = "available",
            // Sliders 特有风格
            ["方块"] = "Square",
            // OLED 画布锁定
            ["解锁画布"] = "Unlock Canvas",
            ["锁定画布"] = "Lock Canvas",
            ["🔒 解锁"] = "🔒 Unlock",
            ["🔓 锁定"] = "🔓 Lock",

        };
    }
}
