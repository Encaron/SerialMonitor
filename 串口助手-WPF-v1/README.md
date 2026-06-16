# Serial Monitor v1.0.0（历史存档）

这是 2025 年基于江科大 WinForms 串口助手的 WPF 初版重写。文件结构保留了开发当时的原貌，未做深度整理。

## 与本项目的关系

```
江科大 WinForms 原版 → 本版（v1.0.0 WPF） → [Serial Monitor V2](../Serial%20Monitor%20V2/)（当前活跃版本）
```

V2 在此基础上重写：协议系统 + OxyPlot 波形 + 4 控制面板 + .NET 8 + 21 色双主题。**本目录仅供存档参考，不再维护。**

## 编译（如需要）

```bash
# 需 .NET Framework 4.8 + Visual Studio Build Tools
msbuild 串口助手-WPF.csproj -t:Build -p:Configuration=Release
```

输出：`bin\Release\Serial Monitor.exe`

## 目录

| 目录 | 内容 |
|------|------|
| `App/` | 入口逻辑（异常处理、主窗口初始化） |
| `Views/` | 界面 partial class（动画、日志、快捷发送、设置、主题） |
| `Docs/` | 开发文档（当前状态、评估报告） |
| `Icons/` | 应用图标 |
| `Lib/` | 第三方 DLL（AvalonEdit） |
| `Properties/` | 程序集信息 |
