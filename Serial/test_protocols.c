/*
 * test_protocols.c
 *
 * Serial Monitor V2 — 按键+滑杆+摇杆 控制 PC13 LED
 *
 * 协议：
 *   [key,Key1,ON]              → 按键 Key1，值 ON
 *   [key,Key2,OFF]             → 按键 Key2，值 OFF
 *   [slider,Slider1,百分比]     → 滑杆 Slider1，值 0.0~100.0
 *   [joystick,1,x,y]           → 摇杆 1，坐标 0~255，中心 127
 *
 * LED 规则（互相独立，谁最后触发听谁的）：
 *   Key1 → ON       → LED 亮
 *   Key2 → OFF      → LED 灭
 *   Slider >= 50.0  → LED 亮
 *   Slider <  50.0  → LED 灭
 *   摇杆距中心 > 80 → LED 亮
 *   摇杆距中心 ≤ 80 → LED 灭
 *
 * 硬件：PC13 接 LED，GPIO_PIN_RESET 点亮，复位默认亮
 */

#include "Serial.h"
#include <string.h>
#include <stdlib.h>
#include <math.h>

#ifndef Serial_PC
#define Serial_PC  SERIAL_DEVICE_1
#endif

#define Log(fmt, ...)  Serial_Printf(&huart1, fmt, ##__VA_ARGS__)

// ——— LED 状态 ———
static float   slider_val  = 0.0f;   // 0.0~100.0
static float   joy_x       = 127.0f; // 摇杆 X（中心 127）
static float   joy_y       = 127.0f; // 摇杆 Y（中心 127）

// ——— 简易字段提取 ———
static void GetField(const char *str, int index, char *out, int outSize)
{
    const char *start = str;
    for (int i = 0; i < index; i++) {
        const char *p = strchr(start, ',');
        if (!p) { out[0] = '\0'; return; }
        start = p + 1;
    }
    const char *end = strchr(start, ',');
    int len = end ? (int)(end - start) : (int)strlen(start);
    if (len >= outSize) len = outSize - 1;
    memcpy(out, start, len);
    out[len] = '\0';
}

// ——— 直接设 LED ———
static void SetLED(uint8_t on)
{
    HAL_GPIO_WritePin(GPIOC, GPIO_PIN_13, on ? GPIO_PIN_RESET : GPIO_PIN_SET);
}

// ——— 初始化 ———
void Test_Init(void)
{
    SetLED(1);  // 复位亮
    slider_val = 0.0f;
    joy_x = joy_y = 127.0f;
    Log("---- 按键+滑杆+摇杆 LED 控制就绪 ----\r\n");
    Log("PC13 LED = ON (复位亮)\r\n");
}

// ——— 协议处理（while(1) 中轮询）———
void Test_Handle(void)
{
    if (!UART_GetRxFlag(Serial_PC))
        return;

    char *raw = UART_GetRxData(Serial_PC);
    if (!raw || raw[0] == '\0') return;

    char type[16];
    GetField(raw, 0, type, sizeof(type));

    // ——— 按键（独立）———
    if (strcmp(type, "key") == 0) {
        char name[32], state[8];
        GetField(raw, 1, name, sizeof(name));
        GetField(raw, 2, state, sizeof(state));

        if (strcmp(name, "Key1") == 0 && strcmp(state, "ON") == 0) {
            SetLED(1);
            Log("[key,%s,%s] → LED ON\r\n", name, state);
        }
        else if (strcmp(name, "Key2") == 0 && strcmp(state, "OFF") == 0) {
            SetLED(0);
            Log("[key,%s,%s] → LED OFF\r\n", name, state);
        }
        else {
            Log("[key,%s,%s] 未识别\r\n", name, state);
        }
        return;
    }

    // ——— 滑杆（独立）———
    if (strcmp(type, "slider") == 0) {
        char name[32], valStr[16];
        GetField(raw, 1, name, sizeof(name));
        GetField(raw, 2, valStr, sizeof(valStr));

        if (strcmp(name, "Slider1") == 0) {
            slider_val = (float)atof(valStr);
            if (slider_val >= 50.0f) {
                SetLED(1);
                Log("[slider,%s,%.1f] >=50 → LED ON\r\n", name, slider_val);
            } else {
                SetLED(0);
                Log("[slider,%s,%.1f] <50  → LED OFF\r\n", name, slider_val);
            }
        }
        else {
            Log("[slider,%s,%s] 未识别\r\n", name, valStr);
        }
        return;
    }

    // ——— 摇杆（独立）———
    if (strcmp(type, "joystick") == 0) {
        char idStr[8], xStr[16], yStr[16];
        GetField(raw, 1, idStr, sizeof(idStr));
        GetField(raw, 2, xStr,  sizeof(xStr));
        GetField(raw, 3, yStr,  sizeof(yStr));
        int   id = atoi(idStr);
        float  x = (float)atof(xStr);
        float  y = (float)atof(yStr);

        if (id == 1) {
            joy_x = x; joy_y = y;
            float dx = x - 127.0f, dy = y - 127.0f;
            float dist = sqrtf(dx * dx + dy * dy);
            if (dist > 80.0f) {
                SetLED(1);
                Log("[joystick,1,%.0f,%.0f] dist=%.0f >80 → LED ON\r\n", x, y, dist);
            } else {
                SetLED(0);
                Log("[joystick,1,%.0f,%.0f] dist=%.0f ≤80 → LED OFF\r\n", x, y, dist);
            }
        }
        else {
            Log("[joystick,%d,%.0f,%.0f] 未识别\r\n", id, x, y);
        }
        return;
    }
}
