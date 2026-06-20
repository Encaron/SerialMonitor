# OLED 绘图协议（v2.5.0 最终版 + F5 增量同步 + F4 旋转）

> 状态：✅ 已实现
> 用途：STM32 ↔ PC 串口虚拟 OLED 双向绘图

## 图形协议（11 条 + 3 旋转）

```
[draw,text,    x,y,content,fontSize,#RRGGBB]       → 文字
[draw,point,   x,y,#RRGGBB]                         → 1×1 像素点
[draw,line,    x1,y1,x2,y2,#RRGGBB,w]               → 直线（w 可选，默认 1）
[draw,rect,    x,y,w,h,#RRGGBB,w]                   → 空心矩形
[draw,rect,    x,y,w,h,#RRGGBB,w,fill]              → 实心矩形
[draw,rect,    x,y,w,h,#RRGGBB,w,a<angle>]          → 空心旋转矩形
[draw,rect,    x,y,w,h,#RRGGBB,w,fill,a<angle>]     → 实心旋转矩形
[draw,circle,  cx,cy,r,#RRGGBB,w]                   → 空心圆
[draw,circle,  cx,cy,r,#RRGGBB,w,fill]              → 实心圆饼
[draw,ellipse, cx,cy,a,b,#RRGGBB,w]                 → 空心椭圆
[draw,ellipse, cx,cy,a,b,#RRGGBB,w,fill]            → 实心椭圆
[draw,ellipse, cx,cy,a,b,#RRGGBB,w,a<angle>]        → 空心旋转椭圆
[draw,ellipse, cx,cy,a,b,#RRGGBB,w,fill,a<angle>]   → 实心旋转椭圆
[draw,triangle,x0,y0,x1,y1,x2,y2,#RRGGBB,w]         → 空心三角形
[draw,triangle,x0,y0,x1,y1,x2,y2,#RRGGBB,w,fill]   → 实心三角形
[draw,clear]                                         → 清屏（默认底色 #111111）
[draw,clear,#RRGGBB]                                 → 清屏 + 填指定色

注：线/三角/弧旋转已烘焙进坐标，无需 a<angle>。圆角矩形旋转忽略圆角。
```

## F5 增量同步协议

```
[draw,set,<id>,<type>,<params>...]    → 创建/更新图形（upsert，永远1帧）
[draw,del,<id>]                        → 删除图形（1帧）
```

- `<id>` 格式：`a1,a2,a3...`（PC 分配，自增不回收）
- `<type>,<params>` 同上述图形协议
- 设备端维护图形数组，收到 set/del → 更新数组 → 本地全屏重绘
- 旧格式 `[draw,<type>,...]` 兼容保留（MCU→PC 方向使用）
```

### fill 参数说明

`fill` 作为末尾可选参数，加到 `rect`/`circle`/`ellipse`/`triangle` 末尾表示填充。填充模式下忽略线宽 `w`。

例：`[draw,rect,10,10,50,30,#FF0000,2]` → 空心矩形，红色，线宽 2
　　`[draw,rect,10,10,50,30,#FF0000,2,fill]` → 实心矩形，红色（线宽 2 被忽略）

### 文字说明

`[draw,text]` 替代旧 `[display]` 协议，统一纳入 draw 命名空间。`[draw,clear]` 同时清除文字和图形。

例：`[draw,text,0,0,hello,24,#FFFFFF]` → 在 (0,0) 以 24px 字号显示白色 "hello"

## MCU 端 Wrapper 函数

```c
#include <stdio.h>

// 文字
void Draw_Text(int x, int y, const char *text, int fontSize, const char *color)
{
    Log("[draw,text,%d,%d,%s,%d,%s]\r\n", x, y, text, fontSize, color);
}

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
void Draw_LineW(int x1, int y1, int x2, int y2, const char *color, int w)
{
    Log("[draw,line,%d,%d,%d,%d,%s,%d]\r\n", x1, y1, x2, y2, color, w);
}

// 空心矩形
void Draw_Rect(int x, int y, int w, int h, const char *color)
{
    Log("[draw,rect,%d,%d,%d,%d,%s]\r\n", x, y, w, h, color);
}

// 实心矩形（等价于 OLED_DrawRectangle(IsFilled=true)）
void Draw_Fill(int x, int y, int w, int h, const char *color)
{
    Log("[draw,rect,%d,%d,%d,%d,%s,1,fill]\r\n", x, y, w, h, color);
}

// 空心圆
void Draw_Circle(int cx, int cy, int r, const char *color)
{
    Log("[draw,circle,%d,%d,%d,%s]\r\n", cx, cy, r, color);
}

// 实心圆饼
void Draw_CircleFilled(int cx, int cy, int r, const char *color)
{
    Log("[draw,circle,%d,%d,%d,%s,1,fill]\r\n", cx, cy, r, color);
}

// 空心椭圆
void Draw_Ellipse(int cx, int cy, int a, int b, const char *color)
{
    Log("[draw,ellipse,%d,%d,%d,%d,%s]\r\n", cx, cy, a, b, color);
}

// 空心三角形
void Draw_Triangle(int x0, int y0, int x1, int y1, int x2, int y2, const char *color)
{
    Log("[draw,triangle,%d,%d,%d,%d,%d,%d,%s]\r\n", x0, y0, x1, y1, x2, y2, color);
}

// 实心三角面
void Draw_TriangleFilled(int x0, int y0, int x1, int y1, int x2, int y2, const char *color)
{
    Log("[draw,triangle,%d,%d,%d,%d,%d,%d,%s,1,fill]\r\n", x0, y0, x1, y1, x2, y2, color);
}

// 清屏
void Draw_Clear(void)
{
    Log("[draw,clear]\r\n");
}
void Draw_ClearColor(const char *color)
{
    Log("[draw,clear,%s]\r\n", color);
}
```

## 移植示例

以 `ui.c` 中的 `disp_select_box` 为例：

```c
// 物理 LCD 版本（两点式）
void disp_select_box(int x, int y, int w, int h)
{
    LCD_Fill(x, y, x+w, y+h, DARKBLUE);
    LCD_DrawLine(x, y, x+w, y+h, CYAN);
}

// 串口虚拟 OLED 版本（宽高式）—— 只改底层 wrapper
void disp_select_box(int x, int y, int w, int h)
{
    Draw_Fill(x, y, w, h, "#00008B");
    Draw_Line(x, y, x+w, y+h, "#00FFFF");
}
// 其余 ui.c 代码一行不动
```

## OLED API 直通映射

| 江科大 OLED API | 协议 | 说明 |
|----------------|------|------|
| `OLED_DrawPoint(x,y)` | `[draw,point,x,y,#color]` | |
| `OLED_DrawLine(x1,y1,x2,y2)` | `[draw,line,x1,y1,x2,y2,#color,w]` | |
| `OLED_DrawRectangle(x,y,w,h,IsFilled)` | `[draw,rect,x,y,w,h,#color,w]` 或 `...,fill]` | IsFilled 决定末尾是否加 fill |
| `OLED_DrawCircle(cx,cy,r,IsFilled)` | `[draw,circle,cx,cy,r,#color,w]` 或 `...,fill]` | |
| `OLED_DrawEllipse(x,y,a,b,IsFilled)` | `[draw,ellipse,x,y,a,b,#color,w]` 或 `...,fill]` | |
| `OLED_DrawTriangle(x0,y0,x1,y1,x2,y2,IsFilled)` | `[draw,triangle,x0..y2,#color,w]` 或 `...,fill]` | |
| `OLED_Clear()` | `[draw,clear]` | |
| `OLED_ShowChar(x,y,ch,size)` | `[draw,text,x,y,ch,size,#color]` | |
| `OLED_ShowString(x,y,str,size)` | 逐字符发 `[draw,text,...]` | |

## 传输速度参考

| 图形 | 协议数据 | 115200bps 耗时 |
|------|---------|:--:|
| 画一条线 | ~40 字节 | <4ms |
| 画一个填充矩形 | ~40 字节 | <4ms |
| 画一个圆 | ~30 字节 | <3ms |
| 写一行文字 | ~45 字节 | <4ms |
| 清屏 | ~20 字节 | <2ms |
| 上传 64×64 RGB565 图片 | ~16KB（Hex） | ~1.4s |
| 上传 240×240 RGB565 图片 | ~230KB（Hex） | ~20s |

> 结论：矢量图形和文字瞬时完成。小尺寸图片（≤64×64）可用。大图/动画不适合。
