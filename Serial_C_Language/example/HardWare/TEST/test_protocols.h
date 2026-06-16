/*
 * test_protocols.h
 */
#ifndef TEST_PROTOCOLS_H_
#define TEST_PROTOCOLS_H_

#include "Serial.h"
#include <string.h>
#include <stdio.h>

/* ── 串口别名 ── */
#ifndef Serial_PC
#define Serial_PC  SERIAL_DEVICE_1
#endif

/* ── 日志宏 ── */
#define Log(fmt, ...)  Serial_Printf(&huart1, fmt, ##__VA_ARGS__)

/* ── 协议字段提取 ── */
void GetField(const char *str, int index, char *out, int outSize);

/* ── 测试模块接口 ── */
void Test_Init(void);
void Test_Handle(void);
void Test_KeyProcess(const char *raw);

#endif /* TEST_PROTOCOLS_H_ */
