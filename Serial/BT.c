/*
 * bt.c
 *
 * Created on: Nov 3, 2025
 *      Author: FengYiLi
 * Description: 蓝牙模块发送函数
 *              只包含发送相关函数，接收在Serial.c中处理
 */

#include "bt.h"
#include "Serial.h"

/**
  * @brief 蓝牙模块唤醒
  * @attention 根据实际硬件决定是否使用
  */
void BT_WEEKUP (void)
{
    // 如果蓝牙模块有WAKEUP引脚，在此实现
    // HAL_GPIO_WritePin(BTCS_GPIO_Port,BTCS_Pin, GPIO_PIN_RESET);
    // HAL_Delay(10);
}

/**
  * @brief 蓝牙模块睡眠
  * @attention 根据实际硬件决定是否使用
  */
void BT_SLEEP (void)
{
    // 如果蓝牙模块有SLEEP引脚，在此实现
    // HAL_GPIO_WritePin(BTCS_GPIO_Port,BTCS_Pin, GPIO_PIN_SET);
    // HAL_Delay(10);
}

/**
  * @brief 蓝牙发送数据
  * @param fmt 格式化字符串
  * @param ... 可变参数
  * @retval 无
  * @attention 直接调用Serial_Printf，使用USART3
  */
void BT_printf (char *fmt, ...)
{
    char buff[100];
    uint16_t i = 0;
    va_list arg_ptr;

    va_start(arg_ptr, fmt);
    vsnprintf(buff, sizeof(buff), fmt, arg_ptr);
    i = strlen(buff);

    // 使用Serial_SendString发送
    Serial_SendString(&huart3, buff);

    va_end(arg_ptr);
}

/**
  * @brief 发送蓝牙绘图数据
  * @param value1 第一个数据
  * @param value2 第二个数据
  * @param value3 第三个数据
  * @retval 无
  */
void BT_SendPlot(float value1, float value2, float value3)
{
    BT_printf("[plot,%.2f,%.2f,%.2f]", value1, value2, value3);
}

/**
  * @brief 发送蓝牙显示文本
  * @param x 横坐标
  * @param y 纵坐标
  * @param text 显示文本
  * @param fontSize 字体大小
  * @retval 无
  */
void BT_SendDisplay(uint16_t x, uint16_t y, char *text, uint8_t fontSize)
{
    BT_printf("[display,%d,%d,%s,%d]", x, y, text, fontSize);
}

/**
  * @brief 发送原始蓝牙命令
  * @param command 命令字符串
  * @retval 无
  */
void BT_SendCommand(char *command)
{
    BT_printf("[%s]", command);
}

/**
  * @brief 清除蓝牙显示屏
  * @param 无
  * @retval 无
  */
void BT_ClearDisplay(void)
{
    BT_printf("[display-clear]");
}

/**
  * @brief 清除蓝牙绘图区
  * @param 无
  * @retval 无
  */
void BT_ClearPlot(void)
{
    BT_printf("[plot-clear]");
}
