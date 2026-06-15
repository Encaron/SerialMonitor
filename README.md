<h1 align="center">
  <img src="assets/logo.png" alt="Serial Monitor" width="128" />
  <br>
  Serial Monitor V2
  <br>
</h1>

<h3 align="center">
基于 WPF + AvalonEdit + OxyPlot 的串口调试工具。
</h3>

<p align="center">
  Languages:
  <a href="./README.md">简体中文</a> ·
  <a href="./docs/README_en.md">English</a>
</p>

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-lightgrey.svg)
![Version](https://img.shields.io/badge/version-v2.1.0-green.svg)

## Preview

| 🌙 暗色主题 | ☀️ 亮色主题 |
|:-----------:|:-----------:|
| ![Dark](assets/dark.png) | ![Light](assets/light.png) |

## Install

> 请到 [Release 页面](https://github.com/Encaron/SerialMonitor/releases) 下载最新安装包。

1. 下载 `SerialMonitor-Setup-vX.X.X.exe`
2. 双击运行，按提示完成安装
3. 安装器会自动检测并安装 .NET 8 运行时（如未安装）
4. 安装完成后桌面和开始菜单均有快捷方式

仅支持 **Windows 10+（x64）**。

## Features

### 串口核心
- 动态扫描串口、17 种预设 + 自定义波特率
- HEX / 文本双模式收发、UTF-8 / GBK 编码
- 硬件流控（RTS/CTS、XON/XOFF）、DTR/RTS 控制信号
- USB 热插拔自动检测 + 自动重连
- TX/RX 流量统计

### 接收区
- AvalonEdit 虚拟化渲染，高频数据不掉帧
- 彩色日志（系统消息 / 发送回显 / 接收数据，三色区分）
- 时间戳（三种格式）、行号跟随主题
- 智能滚屏锁定、暂停显示 + 缓冲满提醒
- Ctrl+F 搜索（关键字 / 正则 / 大小写敏感）
- 日志导出

### 发送区
- 快捷发送面板（chip 按钮 + 右键编辑删除 + 预置 AT 指令）
- 发送历史（最近 20 条去重）
- HEX 实时格式化、定时发送
- 换行符可选（\r\n / \n / \r / 无）
- Enter = 发送 / Shift+Enter = 换行

### 📈 波形图面板
- OxyPlot 实时曲线，通道名自动成为图例
- 滚动 / 扫描双模式，30Hz 刷新限流
- 数值 HUD 半透明叠加、标点 / 连线可切换
- 信号分析：频率 / 幅值 / 占空比 / 波形类型识别
- CSV 导出、Y 轴手动 / 自动范围

### 🎮 控制面板（双向通信）
| 面板 | 协议格式 | 说明 |
|:----:|---------|------|
| 按键 | `[key,name,state]` | 6 种键盘布局预设 + 自定义按键，颜色可调，支持批量编辑 |
| 滑杆 | `[slider,name,val]` | 自定义颜色轨道 + 拇指，拖拽实时回控 STM32 |
| 摇杆 | `[joystick,id,x1,y1,x2,y2]` | 3 种内置风格（手柄/极简/经典）+ 自定义图片素材 |
| OLED | `[display,x,y,text,size,#color]` | 虚拟 OLED 屏幕，支持彩色文字渲染 |

### 🎨 主题与动效
- VS Code Dark+ 风格主题，暗色/亮色一键切换
- 21 色 DynamicResource 全量覆盖
- 按键调色盘（40 色 Material Design 色板 + hex 自定义）
- 弹性动画（按键脉冲 / 滑杆缩弹 / 图标抖动）

### 🛡️ 健壮性
- 三个致命路径（后台 Read / 发送 Write / 协议路由）全部 try-catch 保护
- 串口打开 5 层异常分类，中文错误提示
- 崩溃日志自动写入 `%LocalAppData%\SerialMonitor\crash.log`
- ProtocolParser 15 条单元测试

## Protocol Format

STM32 通过串口发送协议数据，软件零配置自动识别通道名：

```c
// PID 调参
Serial_Printf(&huart1, "[plot,P,%f][plot,I,%f][plot,D,%f]\r\n", p, i, d);
Serial_Printf(&huart1, "[slider,kp,%f]\r\n", kp_slider_value);

// 加速度计
Serial_Printf(&huart1, "[plot,ax,%f][plot,ay,%f][plot,az,%f]\r\n", ax, ay, az);
// 三条曲线自动创建，名字自动成为图例，颜色自动分配

// 按键/OLED
Serial_Printf(&huart1, "[key,btn1,down][display,0,0,\"Hello\",18]\r\n");
```

## Development

### 环境要求
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（8.0.422）
- Windows 10+ x64

### 编译

```bash
git clone https://github.com/Encaron/SerialMonitor.git
cd SerialMonitor/"Serial Monitor V2"
dotnet publish -c Release
```

输出：`bin/Release/net8.0-windows/win-x64/publish/Serial Monitor.exe`

### 运行测试

```bash
dotnet test SerialMonitor.Tests/SerialMonitor.Tests.csproj
```

## Roadmap

> 📋 详细方案见 [`Docs/Serial V2 修缮/开发计划2.md`](Docs/Serial%20V2%20修缮/开发计划2.md)。有想法？欢迎 [提交 Issue](https://github.com/Encaron/SerialMonitor/issues/new)。

### 🔴 快速改进（Bug 修复 / 体验优化）

| # | 计划 | 难度 |
|:--:|------|:--:|
| 1 | **绘图期间拖拽滑杆不卡**——检测滑杆按住时绘图自动降频，松手恢复 | 小 |
| 2 | **HexToBytes 非法字符实时提醒**——发送含无效 HEX 时按钮旁显示 `⚠ 无效字符: G` | 极小 |
| 3 | **串口断开波形冻结保留**——关串口后波形不清空，叠加"数据冻结"水印 | 小 |
| 4 | **波形游标测量**——暂停后长按显示数据点精确值 | 中 |
| 5 | **版本号自动注入**——csproj `<Version>` 自动同步到关于页 | 极小 |

### 🟡 功能扩展（新标签页 / 新协议）

| # | 计划 | 难度 |
|:--:|------|:--:|
| 6 | **OLED 绘图指令**——`[draw,...]` 协议支持画点/线/圆/矩形/色块填充 + 小图片上传 | 中 |
| 7 | **📊 数据仪表盘**——四个面板关键数据聚合在一页，适合投屏/截图汇报 | 中 |
| 8 | **🔬 协议调试器**——侧面板实时显示最近 20 条解析后消息，类型用颜色编码 | 小 |

### 🟢 打磨优化（UI 精致化 / 架构改进）

| # | 计划 | 难度 |
|:--:|------|:--:|
| 9 | **i18n 预埋**——逻辑值与显示文字分家，为未来中英双语铺路 | 中 |
| 10 | **毛玻璃侧面板**——Win10+ AcrylicBrush 半透明模糊背景 | 小 |
| 11 | **窗口圆角**——WindowChrome CornerRadius，现代 Windows 11 风格 | 极小 |
| 12 | **Segmented Control 标签**——侧面板分段按钮替代 RadioButton | 中 |
| 13 | **面板路由抽出**——MainWindow 减重 400+ 行 | 中 |
| 14 | **主题切换绑定化**——消除切主题闪烁 | 大 |

### 🔵 远期创意

| # | 想法 |
|:--:|------|
| 15 | NFC 标签页——`[nfc,...]` 门禁/电子标签调试 |
| 16 | 音频频谱页——`[fft,...]` OxyPlot 频谱瀑布图 |
| 17 | 自动化宏录制——操作序列录制回放 |
| 18 | 自定义主题色——像 VSCode 一样导出/导入配色方案 |

### ⬜ 暂无计划

- 多串口（需要时再做）
- macOS 1:1 仿冒（汲取设计精华即可，当前 VS Code 风格已足够）

## FAQ

**Q: 软件启动提示缺少 .NET 运行时？**
A: 安装 .NET 8 Desktop Runtime，[点击下载](https://dotnet.microsoft.com/download/dotnet/8.0)。使用安装包安装会自动处理。

**Q: 串口列表为空？**
A: 确认设备已连接、驱动已安装。部分 USB 转串口芯片需要手动安装驱动（CH340/CP2102）。

**Q: 如何反馈问题？**
A: 请在 [Issues](https://github.com/Encaron/SerialMonitor/issues) 提交，附上串口参数、操作步骤和截图。

## License

[MIT](LICENSE) © 2026 冯毅力 (Encaron)
