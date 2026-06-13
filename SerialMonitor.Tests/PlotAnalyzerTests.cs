using OxyPlot;
using System;
using System.Collections.Generic;
using Xunit;

namespace 串口助手.Tests
{
    public class PlotAnalyzerTests
    {
        // OxyPlot DateTimeAxis.ToDouble → OLE 天数（1天 = 86400秒）
        private const double SecPerDay = 86400.0;

        private static List<DataPoint> MakeData(double[] values, double intervalSec = 0.05)
        {
            var data = new List<DataPoint>();
            double baseX = 45000.0; // OLE 天数基点
            double stepDays = intervalSec / SecPerDay;
            for (int i = 0; i < values.Length; i++)
                data.Add(new DataPoint(baseX + i * stepDays, values[i]));
            return data;
        }

        // ── 基础幅值 ──

        [Fact]
        public void 空数据_返回部分填充()
        {
            var stats = PlotAnalyzer.ComputeStats(new List<DataPoint>());
            Assert.Equal(0, stats.PointCount);
        }

        [Fact]
        public void 单点数据_不足2点_部分返回()
        {
            var data = MakeData(new[] { 3.0 });
            var stats = PlotAnalyzer.ComputeStats(data);
            Assert.Equal(1, stats.PointCount);
            // 不足 2 点不计算，字段保持默认值 0
            Assert.Equal(0, stats.Vmax);
            Assert.Equal(0, stats.Vpp);
        }

        [Fact]
        public void 两点数据_Vpp正确()
        {
            var data = MakeData(new[] { 1.0, 5.0 });
            var stats = PlotAnalyzer.ComputeStats(data);
            Assert.Equal(4.0, stats.Vpp);
            Assert.Equal(3.0, stats.Average);
        }

        [Fact]
        public void 正弦波_幅值和RMS()
        {
            // y = 2.5 + 2.5*sin(t), Vpp=5, avg=2.5, RMS ≈ 3.25
            var vals = new double[200];
            for (int i = 0; i < 200; i++)
                vals[i] = 2.5 + 2.5 * Math.Sin(i * 2 * Math.PI / 40);
            var data = MakeData(vals);
            var stats = PlotAnalyzer.ComputeStats(data);

            Assert.True(stats.Vpp > 4.8 && stats.Vpp < 5.2, $"Vpp={stats.Vpp} 期望≈5");
            Assert.True(stats.Average > 2.4 && stats.Average < 2.6, $"Avg={stats.Average} 期望≈2.5");
            // RMS of 2.5 + 2.5*sin: DC²+AC²/2 = 6.25+3.125=9.375, sqrt=3.062
            Assert.True(stats.RMS > 3.0 && stats.RMS < 3.2, $"RMS={stats.RMS}");
        }

        // ── 方波检测 ──

        [Fact]
        public void 方波_双电平置信度高()
        {
            // 方波: Y=0 和 Y=5 交替，每 20 点切换
            var vals = new double[200];
            for (int i = 0; i < 200; i++)
                vals[i] = (i / 20) % 2 == 0 ? 0.0 : 5.0;
            var data = MakeData(vals);
            var stats = PlotAnalyzer.ComputeStats(data);

            Assert.True(stats.DualLevelConfidence > 70,
                $"期望高置信度, 实际={stats.DualLevelConfidence:F1}%");
            Assert.True(stats.DutyCycle > 30 && stats.DutyCycle < 70,
                $"占空比={stats.DutyCycle:F1}% 期望≈50");
            Assert.True(stats.Frequency > 0, "应检测到频率");
        }

        [Fact]
        public void 斜边方波_置信度下降但仍有()
        {
            // 模拟电容滤波: 两个电平之间加过渡点
            var vals = new double[200];
            for (int i = 0; i < 200; i++)
            {
                int phase = i % 40;
                if (phase < 18) vals[i] = 0.0;
                else if (phase < 22) vals[i] = (phase - 18) * 1.25; // 斜坡
                else vals[i] = 5.0;
            }
            var data = MakeData(vals);
            var stats = PlotAnalyzer.ComputeStats(data);

            Assert.True(stats.DualLevelConfidence > 30,
                $"斜边方波应有双电平, 实际={stats.DualLevelConfidence:F1}%");
            Assert.True(stats.RiseTime > 0, "应测量到上升时间");
        }

        // ── DC 电平 ──

        [Fact]
        public void DC电平_Vpp为零()
        {
            var vals = new double[100];
            for (int i = 0; i < 100; i++) vals[i] = 3.3;
            var data = MakeData(vals);
            var stats = PlotAnalyzer.ComputeStats(data);

            Assert.True(stats.Vpp < 0.001, $"DC Vpp 应接近 0, 实际={stats.Vpp}");
            Assert.True(stats.DualLevelConfidence < 10,
                $"DC 不应有高双电平置信度, 实际={stats.DualLevelConfidence:F1}%");
            Assert.True(stats.Frequency < 0.01, "DC 不应有频率");
        }

        // ── 频率检测 ──

        [Fact]
        public void 规则方波_频率计算正确()
        {
            // 10Hz 方波, 采样率 200Hz (interval=0.005s), 周期=100ms=20个点
            var vals = new double[400];
            for (int i = 0; i < 400; i++)
                vals[i] = (i / 10) % 2 == 0 ? 0.0 : 3.3;
            var data = MakeData(vals, intervalSec: 0.005); // 5ms intervals
            var stats = PlotAnalyzer.ComputeStats(data);

            Assert.True(stats.Frequency > 8 && stats.Frequency < 12,
                $"频率期望≈10Hz, 实际={stats.Frequency:F2}");
            if (stats.Period > 0)
                Assert.True(stats.Period > 80 && stats.Period < 120,
                    $"周期期望≈100ms, 实际={stats.Period:F2}");
        }

        // ── 信号类型判断 ──

        [Fact]
        public void 脉冲尖峰_波峰因数高()
        {
            // 大部分为 0，偶尔尖峰
            var vals = new double[100];
            for (int i = 0; i < 100; i++) vals[i] = 0;
            vals[50] = 10.0; // 单个尖峰
            var data = MakeData(vals);
            var stats = PlotAnalyzer.ComputeStats(data);

            Assert.True(stats.CrestFactor > 2.0,
                $"脉冲波峰因数应>2, 实际={stats.CrestFactor:F2}");
        }

        [Fact]
        public void 噪声_无周期()
        {
            var rng = new Random(42);
            var vals = new double[200];
            for (int i = 0; i < 200; i++)
                vals[i] = rng.NextDouble() * 0.5 + 2.5; // 2.5-3.0 噪声
            var data = MakeData(vals);
            var stats = PlotAnalyzer.ComputeStats(data);

            // 噪声一般不会有高置信度双电平
            Assert.True(stats.DualLevelConfidence < 50,
                $"噪声不应有高置信度, 实际={stats.DualLevelConfidence:F1}%");
        }

        // ── 上升时间 ──

        [Fact]
        public void 快速边沿_上升时间极小()
        {
            // 近乎垂直的边沿
            var vals = new double[100];
            for (int i = 0; i < 50; i++) vals[i] = 0.0;
            vals[50] = 3.3;
            for (int i = 51; i < 100; i++) vals[i] = 3.3;
            var data = MakeData(vals, intervalSec: 0.1);
            var stats = PlotAnalyzer.ComputeStats(data);

            // 几乎垂直，上升时间应为 ≤采样间隔
            Assert.True(stats.RiseTime <= stats.SampleIntervalMs * 3
                || stats.RiseTime < 0,
                $"上升时间应极小, 实际={stats.RiseTime:F3}ms");
        }

        // ── 采样间隔 ──

        [Fact]
        public void 采样间隔计算()
        {
            var data = MakeData(new double[10], intervalSec: 0.02);
            var stats = PlotAnalyzer.ComputeStats(data);
            Assert.True(stats.SampleIntervalMs > 15 && stats.SampleIntervalMs < 25,
                $"采样间隔≈20ms, 实际={stats.SampleIntervalMs:F2}");
        }

        [Fact]
        public void 时间跨度正确()
        {
            var data = MakeData(new double[5], intervalSec: 0.1);
            var stats = PlotAnalyzer.ComputeStats(data);
            // 5 点 * 100ms 间隔 = 400ms 跨度
            Assert.True(stats.TimeSpanMs > 350 && stats.TimeSpanMs < 450,
                $"跨度≈400ms, 实际={stats.TimeSpanMs:F2}");
        }
    }
}
