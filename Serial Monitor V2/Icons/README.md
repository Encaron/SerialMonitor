# Serial Monitor V2 — 图标规格

## 方案 A：AI 自行设计（推荐，零外部依赖）

使用 WPF XAML Path 矢量图标，嵌入 `MainWindow.xaml` 的 `IconBarButtonStyle` 模板内。

**优点**：
- 主题色自动适配（通过 `{DynamicResource}` 绑定前景色）
- 任意缩放不失真
- 无需外部文件
- 暗色/亮色主题自动跟随

**各图标 Path 设计（16×16 viewBox）**：

每个图标是一个 42×42 的 RadioButton，内容为 Viewbox 包裹的 Canvas/Path。
前景色绑定到 RadioButton.Foreground（选中=PrimaryBrush，默认=TextMutedBrush）。

```xml
<!-- 接收区图标：串口/插头形状 -->
<Viewbox Width="18" Height="18">
    <Path Fill="{Binding Foreground, RelativeSource={RelativeSource FindAncestor, AncestorType=RadioButton}}"
          Data="M4,8 L6,8 L6,4 L10,4 L10,8 L12,8 L8,13 Z M3,14 L13,14 L13,16 L3,16 Z" />
</Viewbox>

<!-- 绘图图标：折线图 -->
<Viewbox Width="18" Height="18">
    <Path Fill="none" Stroke="..." StrokeThickness="1.5"
          Data="M2,14 L5,10 L8,12 L11,4 L14,7" />
</Viewbox>

<!-- 设置图标：齿轮 -->
<Viewbox Width="18" Height="18">
    <Path Fill="..."
          Data="M8,1 L8,3 L7,3 C6.7,3 6.4,3.1 6.1,3.2 L5.3,2.4 L3.9,3.9 L4.7,4.6 ..." />
</Viewbox>
```

## 方案 B：用户提供图标文件

如果 AI 设计的矢量图标不满意，用户可将图标文件放入本目录。

### 格式要求

| 参数 | 规格 |
|------|------|
| 格式 | **PNG**（支持透明背景） |
| 尺寸 | **32×32 像素**（在 42×42 按钮中居中显示，留 5px 内边距） |
| 颜色 | **单色深灰**（#888888 左右），软件会自动着色 |
| 命名 | 见下表（区分大小写） |

### 文件命名

| 文件名 | 对应功能 |
|--------|---------|
| `receive.png` | 接收区/串口调试 |
| `plot.png` | 波形图/绘图 |
| `keys.png` | 按键面板 |
| `sliders.png` | 滑杆面板 |
| `joystick.png` | 摇杆面板 |
| `oled.png` | 虚拟 OLED |
| `settings.png` | 设置 |

### 使用方式

新 AI 在 XAML 中将图标栏按钮改为：

```xml
<RadioButton Style="{DynamicResource IconBarButtonStyle}" ToolTip="接收区">
    <Image Source="Icons/receive.png" Width="18" Height="18" />
</RadioButton>
```

文件放在 `Serial Monitor V2/Icons/` 目录下，编译时需在 `.csproj` 中注册为 Resource：

```xml
<Resource Include="Icons\receive.png" />
```
