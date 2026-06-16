/*
 * test_protocols.c
 *
 * Serial Monitor V2 — 滑杆呼吸灯
 *
 * 协议：
 *   [slider,Slider1,百分比]  → 滑杆 Slider1，值 0.0~100.0 → PWM 调光
 *
 * 硬件：PC13 接 LED，GPIO_PIN_RESET 点亮
 * TIM6 软件 PWM: 100Hz / 100 级分辨率
 */

#include "test_protocols.h"
#include "test_key.h"
#include "test_display.h"
#include "tim.h"
#include <stdlib.h>

// ——— PWM 占空比 0.0~100.0 ———
float slider_val = 0.0f;

// ——— 简易字段提取 ———
void GetField(const char *str, int index, char *out, int outSize)
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

// ——— 软件 PWM 回调（TIM6 每 100µs 触发一次）———
void HAL_TIM_PeriodElapsedCallback(TIM_HandleTypeDef *htim)
{
    if (htim->Instance == TIM6) {
        static uint8_t pwm_cnt = 0;
        if (++pwm_cnt >= 100) pwm_cnt = 0;
        // PC13: RESET=亮, SET=灭
        if (pwm_cnt < (uint8_t)slider_val)
            GPIOC->BSRR = (uint32_t)GPIO_PIN_13 << 16;  // BR13 → 拉低 → LED ON
        else
            GPIOC->BSRR = GPIO_PIN_13;                   // BS13 → 拉高 → LED OFF
    }
}

// ——— 初始化 ———
void Test_Init(void)
{
    MX_TIM6_Init();
    HAL_TIM_Base_Start_IT(&htim6);
    slider_val = 0.0f;
    Log("---- 滑杆呼吸灯就绪 ----\r\n");
    Log("TIM6 软件PWM: 100Hz / 100级\r\n");
}

// ——— 协议处理 ———
void Test_Handle(void)
{
    if (!UART_GetRxFlag(Serial_PC))
        return;

    char *raw = UART_GetRxData(Serial_PC);
    if (!raw || raw[0] == '\0') return;

    char type[16];
    GetField(raw, 0, type, sizeof(type));

    if (strcmp(type, "slider") == 0) {
        char name[32], valStr[16];
        GetField(raw, 1, name, sizeof(name));
        GetField(raw, 2, valStr, sizeof(valStr));

        if (strcmp(name, "Slider1") == 0) {
            slider_val = (float)atof(valStr);
            Log("[slider,%s,%.1f] → PWM=%.0f%%\r\n", name, slider_val, slider_val);
        }
    } else {
        /* 不是 slider，交给其他测试模块处理 */
        Test_KeyProcess(raw);
        Test_DisplayProcess(raw);
    }
}
