<h1 align="center">
  <img src="../assets/logo.png" alt="Serial Monitor" width="128" />
  <br>
  Serial Monitor V2
  <br>
</h1>

<h3 align="center">
A serial debugging tool based on WPF + AvalonEdit + OxyPlot.
</h3>

<p align="center">
  Languages:
  <a href="../README.md">简体中文</a> ·
  <a href="./README_en.md">English</a>
</p>

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-lightgrey.svg)
![Version](https://img.shields.io/badge/version-v2.1.0-green.svg)

## Preview

| 🌙 Dark | ☀️ Light |
|:-------:|:--------:|
| ![Dark](../assets/dark.png) | ![Light](../assets/light.png) |

## Install

> Go to the [Release page](https://github.com/Encaron/SerialMonitor/releases) to download the latest installer.

1. Download `SerialMonitor-Setup-vX.X.X.exe`
2. Double-click and follow the setup wizard
3. The installer auto-detects and installs .NET 8 Runtime if needed
4. Desktop shortcut and Start Menu entry are created automatically

Supports **Windows 10+ (x64)** only.

## Features

### Serial Core
- Dynamic port scanning, 17 presets + custom baud rate
- HEX / Text dual-mode send/receive, UTF-8 / GBK encoding
- Hardware flow control (RTS/CTS, XON/XOFF), DTR/RTS signals
- USB hot-plug detection + auto-reconnect
- TX/RX byte counters

### Receive Area
- AvalonEdit virtualized rendering, smooth at high data rates
- Color-coded log (system / echo / receive, three distinct colors)
- Timestamp (3 formats), line numbers follow theme
- Smart scroll lock, pause display + buffer overflow warning
- Ctrl+F search (plain text / regex / case-sensitive)
- Log export

### Send Area
- Quick-send panel (chip buttons + right-click edit/delete + preset AT commands)
- Send history (last 20 unique entries)
- Real-time HEX formatting, timed send
- Newline options (\r\n / \n / \r / none)
- Enter = send / Shift+Enter = newline

### 📈 Plot Panel
- OxyPlot real-time curves, channel names auto-become legends
- Scroll / Sweep dual display modes, 30Hz throttle
- Numeric HUD semi-transparent overlay, markers / lines toggle
- Signal analysis: frequency / amplitude / duty cycle / waveform classification
- CSV export, Y-axis manual / auto range

### 🎮 Control Panels (bidirectional)
| Panel | Protocol | Description |
|:-----:|---------|-------------|
| Keys | `[key,name,state]` | 6 layout presets + custom keys, color picker, batch editing |
| Sliders | `[slider,name,val]` | Custom color track + thumb, drag to send back to STM32 |
| Joystick | `[joystick,id,x1,y1,x2,y2]` | 3 built-in styles (Gamepad/Minimal/Classic) + custom image assets |
| OLED | `[display,x,y,text,size,#color]` | Virtual OLED screen with color text rendering |

### 🎨 Theme & Animation
- VS Code Dark+ style theme, one-click dark/light switch
- 21 DynamicResource colors, full coverage
- Key color picker (40 Material Design swatches + hex custom)
- Elastic animations (key pulse / slider spring / icon shake)

### 🛡️ Robustness
- Three critical paths (background Read / send Write / protocol routing) all protected with try-catch
- Serial port open: 5-layer exception classification
- Crash log auto-written to `%LocalAppData%\SerialMonitor\crash.log`
- ProtocolParser: 15 unit tests

## Protocol Format

STM32 sends protocol data via serial port. Channel names are auto-detected — zero configuration needed:

```c
// PID tuning
Serial_Printf(&huart1, "[plot,P,%f][plot,I,%f][plot,D,%f]\r\n", p, i, d);
Serial_Printf(&huart1, "[slider,kp,%f]\r\n", kp_slider_value);

// Accelerometer
Serial_Printf(&huart1, "[plot,ax,%f][plot,ay,%f][plot,az,%f]\r\n", ax, ay, az);
// Three curves auto-created, names become legends, colors auto-assigned

// Keys / OLED
Serial_Printf(&huart1, "[key,btn1,down][display,0,0,\"Hello\",18]\r\n");
```

## Development

### Requirements
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (8.0.422)
- Windows 10+ x64

### Build

```bash
git clone https://github.com/Encaron/SerialMonitor.git
cd SerialMonitor/"Serial Monitor V2"
dotnet publish -c Release
```

Output: `bin/Release/net8.0-windows/win-x64/publish/Serial Monitor.exe`

### Run Tests

```bash
dotnet test SerialMonitor.Tests/SerialMonitor.Tests.csproj
```

## Roadmap

> 📋 Full details: [`Docs/Serial V2 修缮/开发计划2.md`](../Docs/Serial%20V2%20修缮/开发计划2.md) (Chinese). Ideas? [Open an Issue](https://github.com/Encaron/SerialMonitor/issues/new).

### 📦 Release Plan

| Version | Content | Notes |
|------|------|------|
| **v2.2.0** | 🔴 All 5 quick fixes | Stutter fix, HEX warning, waveform freeze, version auto-inject, sidebar enrichment |
| **v2.3.0** | 🟡 OLED Drawing + Tuning Workbench | Draw primitives + drag sliders while viewing plots |
| **v2.4.0** | 🟡 FFT Spectrum | New tab: frequency-domain analysis |
| **v2.5.0** | 🟡 PC Drawing → STM32 Screen | PC-side canvas → `[draw,...]` → physical display |
| **v2.6.0** | 🟢 Internal polish | i18n prep + routing refactor + theme optimization |

### 🔴 Quick Fixes (v2.2.0)

| # | Plan | Difficulty |
|:--:|------|:--:|
| 1 | **Smooth slider drag during plotting** — Skip rendering when not visible + background-priority rendering when co-visible | Small |
| 2 | **HEX invalid character warning** — Show `⚠ invalid: G` next to send button | Tiny |
| 3 | **Waveform freeze on disconnect** — Keep plot on screen with "Disconnected · Frozen" watermark | Small |
| 4 | **Version auto-injection** — csproj `<Version>` syncs to About page | Tiny |
| 5 | **Slider/Joystick sidebar enrichment** — Quick-preset buttons + live detail + joystick feedback consistency | Small |

### 🟡 Feature Expansion

| # | Plan | Difficulty |
|:--:|------|:--:|
| 6 | **OLED Drawing Primitives** — `[draw,...]` protocol: point/line/circle/rect/fill | Medium |
| 7 | **✏ PC Drawing → STM32 Screen** — PC toolbar → `[draw,...]` → STM32 LCD + Export C array | Medium |
| 8 | **📊 Tuning Workbench** — Plot bottom drawer, drag sliders while watching waveforms | Medium |
| 10 | **📶 FFT Spectrum** — STM32 CMSIS-DSP → `[fft,...]` → OxyPlot spectrogram | Medium |

### 🟢 Polish

| # | Plan | Difficulty |
|:--:|------|:--:|
| 11 | **i18n preparation** — Separate display text from storage keys | Medium |
| 15 | **Panel routing refactor** — Move handlers into ViewModels, keep switch dispatcher | Medium |
| 16 | **Theme switch optimization** — Recolor in-place instead of rebuilding | Small |

### 🔵 Future

| # | Idea |
|:--:|------|
| 17 | NFC tag page — `[nfc,...]` for access control/electronic tag debugging |
| 18 | Macro recorder — record and replay PC-side operations |

### ⬜ Explicitly Out of Scope

- Multi-serial-port (open two exe windows instead)
- macOS 1:1 clone (diminishing returns with WPF)
- CI/CD (`dotnet publish` is enough)
- DI container (unnecessary for a personal project)

## FAQ

**Q: App complains about missing .NET runtime?**
A: Install .NET 8 Desktop Runtime — [download here](https://dotnet.microsoft.com/download/dotnet/8.0). The installer handles this automatically.

**Q: Serial port list is empty?**
A: Make sure the device is connected and drivers are installed. Some USB-to-serial chips (CH340/CP2102) require manual driver installation.

**Q: How to report an issue?**
A: Please submit at [Issues](https://github.com/Encaron/SerialMonitor/issues) with port settings, steps to reproduce, and screenshots.

## License

[MIT](../LICENSE) © 2026 Feng Yili (Encaron)
