# Serial Monitor V2 — 图标与图片素材规范

## 目录结构

```
Icons/
├── README.md            ← 本文件
├── tabs/                ← 左侧标签栏图标（7 个）
│   ├── receive.png
│   ├── plot.png
│   ├── keys.png
│   ├── sliders.png
│   ├── joystick.png
│   ├── oled.png
│   └── settings.png
└── joystick/            ← 摇杆大圆盘素材（用户提供）
    ├── pad_gamepad.png
    ├── thumb_gamepad.png
    ├── pad_minimal.png
    ├── thumb_minimal.png
    ├── pad_classic.png
    └── thumb_classic.png
```

---

## 一、标签栏图标（tabs/）

### 方案 A：AI 自行设计（推荐，零外部依赖）

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

### 方案 B：用户提供图标文件

如果 AI 设计的矢量图标不满意，用户可将图标文件放入 `Icons/tabs/` 目录。

#### 格式要求

| 参数 | 规格 |
|------|------|
| 格式 | **PNG**（支持透明背景） |
| 尺寸 | **32×32 像素**（在 42×42 按钮中居中显示，留 5px 内边距） |
| 颜色 | **单色深灰**（#888888 左右），软件用 `OpacityMask` 自动着色 |
| 位置 | `Icons/tabs/` |

#### 文件命名

| 文件名 | 对应功能 |
|--------|---------|
| `receive.png` | 接收区/串口调试 |
| `plot.png` | 波形图/绘图 |
| `keys.png` | 按键面板 |
| `sliders.png` | 滑杆面板 |
| `joystick.png` | 摇杆面板 |
| `oled.png` | 虚拟 OLED |
| `settings.png` | 设置 |

#### 在 .csproj 中注册

```xml
<Resource Include="Icons\tabs\receive.png" />
```

---

## 二、摇杆大圆盘素材（joystick/）

摇杆面板有三种视觉风格（手柄风 / 极简风 / 经典风），每种需要底座 + 拇指两张图。
图片放在 `Icons/joystick/`，**文件缺失时自动回退到代码绘制**。

### 格式要求

| 参数 | 底座 (pad) | 拇指 (thumb) |
|------|:---:|:---:|
| 格式 | PNG（透明背景） | PNG（透明背景） |
| 尺寸 | **140×140** px | **32×32** px |
| 位置 | `Icons/joystick/` | `Icons/joystick/` |

### 文件命名

```
Icons/joystick/
├── pad_gamepad.png       ← 手柄风底座
├── thumb_gamepad.png     ← 手柄风拇指
├── pad_minimal.png       ← 极简风底座
├── thumb_minimal.png     ← 极简风拇指
├── pad_classic.png       ← 经典风底座
└── thumb_classic.png     ← 经典风拇指
```

### 在 .csproj 中注册

```xml
<Resource Include="Icons\joystick\pad_gamepad.png" />
<Resource Include="Icons\joystick\thumb_gamepad.png" />
<!-- 以此类推 -->
```

### 代码逻辑

渲染时优先查找图片：

```csharp
// 尝试加载图片，不存在则返回 null → 回退代码绘制
var padUri = $"Icons/joystick/pad_{style}.png";
var thumbUri = $"Icons/joystick/thumb_{style}.png";
var padBrush = TryLoadImageBrush(padUri);   // null → 用 Ellipse 代码画
var thumbBrush = TryLoadImageBrush(thumbUri); // null → 用 Ellipse 代码画
```

用户只需把 PNG 放进去 + 注册 .csproj，软件自动生效，三套风格独立。
