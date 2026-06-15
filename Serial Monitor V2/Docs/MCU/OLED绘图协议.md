# OLED 绘图协议（PC 端实现后启用）

> 状态：⏳ 待 PC 端实现 `[draw,...]` 协议
> 用途：STM32 端将物理 LCD 的 UI 代码无缝移植到串口虚拟 OLED 上

## 设计思路

物理 LCD 和串口虚拟 OLED 共用同一套组件层代码（`ui.c`）。唯一区别在最底层 wrapper：

```
物理 LCD:   LCD_DrawLine() ──→ SPI/FSMC ──→ 屏幕
虚拟 OLED:  Draw_Line()   ──→ UART   ──→ PC 端 Canvas
```

MCU 不做任何渲染——只把坐标+颜色拼成字符串发出去。PC 端 WPF Canvas 收到后执行真正的绘制。

## 协议格式

```
[draw,point,  x,y,#RRGGBB]
[draw,line,   x1,y1,x2,y2,#RRGGBB]
[draw,rect,   x,y,w,h,#RRGGBB]
[draw,fill,   x,y,w,h,#RRGGBB]
[draw,circle, cx,cy,r,#RRGGBB]
[draw,clear]
```

## MCU 端 Wrapper 函数

```c
/**
 * OLED 绘图原语 —— 串口虚拟 OLED 底层
 *
 * 每个函数就是一行 Log()，不做渲染。
 * 替换物理 LCD 的 LCD_DrawLine / LCD_Fill 等函数即可。
 */

#include <stdio.h>

// 画点
void Draw_Point(int x, int y, const char *color)
{
    Log("[draw,point,%d,%d,%s]\r\n", x, y, color);
}

// 画线
void Draw_Line(int x1, int y1, int x2, int y2, const char *color)
{
    Log("[draw,line,%d,%d,%d,%d,%s]\r\n", x1, y1, x2, y2, color);
}

// 画空心矩形
void Draw_Rect(int x, int y, int w, int h, const char *color)
{
    Log("[draw,rect,%d,%d,%d,%d,%s]\r\n", x, y, w, h, color);
}

// 画填充矩形
void Draw_Fill(int x, int y, int w, int h, const char *color)
{
    Log("[draw,fill,%d,%d,%d,%d,%s]\r\n", x, y, w, h, color);
}

// 画圆
void Draw_Circle(int cx, int cy, int r, const char *color)
{
    Log("[draw,circle,%d,%d,%d,%s]\r\n", cx, cy, r, color);
}

// 清空画布
void Draw_Clear(void)
{
    Log("[draw,clear]\r\n");
}
```

## 移植示例

以 `ui.c` 中的 `disp_select_box` 为例：

```c
// 物理 LCD 版本
void disp_select_box(int x, int y, int w, int h)
{
    LCD_Fill(x, y, x+w, y+h, DARKBLUE);
    LCD_DrawLine(x, y, x+w, y+h, CYAN);
}

// 串口虚拟 OLED 版本 —— 只改两行
void disp_select_box(int x, int y, int w, int h)
{
    Draw_Fill(x, y, w, h, "#00008B");           // LCD_Fill  → Draw_Fill
    Draw_Line(x, y, x+w, y+h, "#00FFFF");       // LCD_DrawLine → Draw_Line
}
// 其余 ui.c 代码一行不动
```

## 传输速度参考

| 图形 | 协议数据 | 115200bps 耗时 |
|------|---------|:--:|
| 画一条线 | ~40 字节 | <4ms |
| 画一个填充矩形 | ~35 字节 | <3ms |
| 画一个圆 | ~30 字节 | <3ms |
| 上传 64×64 RGB565 图片 | ~16KB（Hex） | ~1.4s |
| 上传 240×240 RGB565 图片 | ~230KB（Hex） | ~20s |

> 结论：矢量图形（点/线/圆/矩形）瞬时完成。小尺寸图片（≤64×64）可用。大图/动画不适合。
