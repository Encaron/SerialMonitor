# Serial Monitor V2 — STM32 端完整示例

基于 STM32H743，用 STM32CubeIDE + CMake 构建。演示 Serial.c/h 的全部功能配合 PC 端 Serial Monitor V2。

## 快速验证

1. STM32CubeIDE → File → Import → Existing Projects into Workspace → 选这个目录
2. 编译 → 烧录
3. 打开 PC 端 Serial Monitor V2，连上 USART1 @ 115200 bps
4. 波形面板自动出现正弦波、滑杆拖动 PC13 LED 亮度变化、OLED 面板显示虚拟 UI

## 文件说明

```
example/
├── Core/               # CubeMX 生成（main.c / usart.c / gpio.c / tim.c）
├── Drivers/            # CMSIS + HAL 库（仅保留 CMake 引用的 .c 文件）
├── HardWare/
│   ├── Serial/         # Serial.c Serial.h（串口库）
│   └── TEST/           # 测试模块
│       ├── test_protocols.c/.h   # 滑杆处理 + 字段提取 + 路由
│       ├── test_key.c/.h         # 按键事件处理
│       ├── test_waveform.c/.h    # 9 种波形发生器
│       └── test_display.c/.h     # OLED 虚拟屏 UI 组件库
├── cmake/stm32cubemx/  # CMake 构建配置
└── H743_Serial_test.ioc # CubeMX 工程文件
```

## 硬件要求

- STM32H743（Nucleo / 自制板均可）
- USART1 连 PC（PA9=TX, PA10=RX）
- PC13 接 LED（滑杆 PWM 调光用）

> ⚠️ **本工程基于 STM32H743。** 不是 H743 也能用，但需要改：
> 1. CubeMX 中更换为你的芯片 → 重新生成代码
> 2. 保留 `HardWare/` 和 `Serial_C_Language/` 目录不动
> 3. `Serial.c` / `Serial.h` 本身只依赖 HAL 库，芯片无关——HAL 是统一的
> 4. 不会操作可交给 AI："帮我把这个 H743 工程迁移到 STM32F407"
> 
> 换芯片后 HAL 驱动文件不同，Drivers 目录会自动更新，无需手动选。
