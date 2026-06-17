# 新 AI 前置信息 — v2.3.0

> ⚠️ 新对话开始时，先读完本文（3 分钟），再动手。

## 你在做什么

Serial Monitor V2 v2.3.0 开发。两个功能：

| # | 功能 | 难度 | 代码量 |
|:--:|------|:--:|:--:|
| 8 | **调参工作台** — Plot 底部抽屉，拖滑杆同时看波形 | 中 | ~150 行 |
| 10 | **FFT 频谱** — 新标签页，频域分析 | 中 | ~200 行 |

## 快速建立认知

1. 读 `Docs/Serial V2 Initial/当前状态.md`（27 约束 + 踩坑——必读）
2. 读 `Docs/Serial V2 修缮/开发计划2.md` → 找 #8 和 #10 章节（方案 + 代码量 + 涉及文件）
3. 读 `Docs/Serial V2 修缮/当前状态2.md`（#1~#5 已 ✅，#8/#10 待做）
4. memory 目录在 `C:\Users\fengy\.claude\projects\e--serial\memory\`，`MEMORY.md` 是索引

## 关键约束（碰了就崩）

- XAML 文件必须在项目根目录（不要移到子文件夹）
- OxyPlot 控件在 C# 中 `new`，不在 XAML 声明
- 所有颜色用 `DynamicResource`（21 色系统）
- 不要改 `DataConverter` 四个方法
- 编译：`dotnet publish -c Release`
- 测试：`dotnet test SerialMonitor.Tests/SerialMonitor.Tests.csproj`

## 版本号

csproj `<Version>2.2.0</Version>` — 唯一真相源，运行时 `Assembly.GetEntryAssembly().GetName().Version` 自动读取。

## #8 调参工作台 — 关键点

- **方案 B（抽屉）**：Plot 底部可伸缩面板，不是独立标签页
- **VM 共享**：调参条的滑杆和 Sliders 页面共用 `_sliderVM` 实例——禁止 `new SliderViewModel()`
- **#1 调度法必须同步上**：抽屉弹出后滑杆和波形同屏可见 → 需要 `DispatcherPriority.Background` 让渲染不抢 MouseMove
- **关键实现约束**：波形压扁不是 Bug（画布物理高度变小）、抽屉弹出与 IsActive 正交
- **文件**：`MainWindow.xaml` + `PlotViewModel.cs` + `Panels/Sliders.cs`

## #10 FFT 频谱 — 关键点

- **新协议**：`[fft,点数,bin0,bin1,...]`，STM32 CMSIS-DSP → PC
- **架构**：两个独立 `PlotModel`（`TimeModel` + `FreqModel`），`PlotView.Model` 换引用（不是共用 Model）
- **切换按钮**：工具栏最左 `⏱ 时域 ▾` / `📶 频域 ▾`，Toggle 行为——不放在图标栏
- **频域指标**：基频/THD/SNR/DC 偏置——PC 端从 raw FFT 数组计算，不依赖 STM32
- **文件**：`MainWindow.xaml` + `MainWindow.xaml.cs` + `PlotViewModel.cs` + `Panels/PlotDetail.cs`

## 新对话开头模板

用户会说"开始 v2.3.0"。先确认：

1. 理解了两个功能的方案（读开发计划2 相应章节）
2. 理解了为什么 #1 调度法和 #8 必须同步上
3. 读过约束和踩坑

然后问用户想做哪个先（建议 #8 先，因为 #1 调度法搭好基础设施后 #10 更顺）。

## 开发注意事项

- 用户是嵌入式开发者，不会 C#/WPF，只双击 exe
- 每完成一个功能 → `dotnet build` + `dotnet test` + 拷 exe 到根目录
- 做完说"做了什么"+"双击试试"
- 涉及 UI 改动先确认暗色/亮色双主题
