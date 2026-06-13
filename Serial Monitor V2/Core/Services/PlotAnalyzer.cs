using System;
using System.Collections.Generic;
using System.Linq;

namespace 串口助手
{
    /// <summary>
    /// 波形统计分析 — 纯算法，零依赖。处理规则波形/方波/噪声/DC 等全部情况。
    /// OxyPlot X 轴 = DateTimeAxis.ToDouble → OLE Automation 天数（1 天 = 86400000 ms）
    /// </summary>
    public class PlotAnalyzer
    {
        // X 轴单位换算：OLE 天数 → 毫秒 / 秒
        private const double MsPerDay = 86400000.0;
        private const double SecPerDay = 86400.0;

        public class WaveformStats
        {
            // 幅值（所有信号均适用）
            public double Vpp { get; set; }
            public double Vmax { get; set; }
            public double Vmin { get; set; }
            public double Amplitude { get; set; }       // (Vmax - Vmin) / 2
            public double Average { get; set; }
            public double RMS { get; set; }

            // 频率 / 时间
            public double Frequency { get; set; }        // Hz, 0 = 无法检测
            public double Period { get; set; }           // ms
            public string FrequencyNote { get; set; }

            // 方波特有（自动显隐）
            public double DutyCycle { get; set; }        // 0-100%
            public double HighTime { get; set; }         // ms
            public double LowTime { get; set; }          // ms
            public double RiseTime { get; set; }         // ms, 10%→90%
            public double FallTime { get; set; }         // ms
            public double DualLevelConfidence { get; set; } // 0-100%

            // 波形特征
            public double StdDev { get; set; }
            public double CrestFactor { get; set; }      // Vpeak / RMS
            public double JitterSigma { get; set; }      // ms
            public double EdgeSteepness { get; set; }    // V/sample

            // 元数据
            public int PointCount { get; set; }
            public double TimeSpanMs { get; set; }
            public double SampleIntervalMs { get; set; }

            // 直方图诊断
            public int HistogramPeaks { get; set; }
        }

        public static WaveformStats ComputeStats(List<OxyPlot.DataPoint> data)
        {
            var stats = new WaveformStats { PointCount = data.Count };
            if (data.Count < 2) return stats;

            double[] xVals = data.Select(p => p.X).ToArray(); // OLE 天数
            double[] yVals = data.Select(p => p.Y).ToArray();

            // ── 采样间隔 & 时间跨度 ──
            stats.TimeSpanMs = (xVals.Last() - xVals.First()) * MsPerDay;
            if (data.Count >= 3)
            {
                var intervals = new List<double>();
                for (int i = 1; i < data.Count; i++)
                    intervals.Add(xVals[i] - xVals[i - 1]);
                intervals.Sort();
                stats.SampleIntervalMs = intervals[intervals.Count / 2] * MsPerDay;
            }

            // ── 幅值 ──
            stats.Vmax = yVals.Max();
            stats.Vmin = yVals.Min();
            stats.Vpp = stats.Vmax - stats.Vmin;
            stats.Amplitude = stats.Vpp / 2.0;
            stats.Average = yVals.Average();

            // ── RMS ──
            double sumSq = 0;
            for (int i = 0; i < yVals.Length; i++)
                sumSq += yVals[i] * yVals[i];
            stats.RMS = Math.Sqrt(sumSq / yVals.Length);

            // ── 标准差 ──
            double avg = stats.Average;
            double sumDev = 0;
            for (int i = 0; i < yVals.Length; i++)
                sumDev += (yVals[i] - avg) * (yVals[i] - avg);
            stats.StdDev = Math.Sqrt(sumDev / yVals.Length);

            // ── 波峰因数 ──
            if (stats.RMS > 1e-9)
            {
                double peak = Math.Max(Math.Abs(stats.Vmax), Math.Abs(stats.Vmin));
                stats.CrestFactor = peak / stats.RMS;
            }

            // ── 直方图找峰值 ──
            int binCount = Math.Min(100, Math.Max(10, yVals.Length / 5));
            double histMin = stats.Vmin, histMax = stats.Vmax;
            if (histMax - histMin < 1e-9) histMax = histMin + 1;
            double binW = (histMax - histMin) / binCount;
            int[] hist = new int[binCount];
            for (int i = 0; i < yVals.Length; i++)
            {
                int idx = (int)((yVals[i] - histMin) / binW);
                if (idx >= binCount) idx = binCount - 1;
                if (idx < 0) idx = 0;
                hist[idx]++;
            }

            var peaks = new List<int>();
            for (int i = 0; i < binCount; i++)
            {
                double left  = i > 0         ? hist[i - 1] : 0;
                double right = i < binCount-1 ? hist[i + 1] : 0;
                if (hist[i] > left && hist[i] > right
                    && hist[i] > yVals.Length * 0.02)
                    peaks.Add(i);
            }
            stats.HistogramPeaks = peaks.Count;

            // ── 双电平分析 ──
            if (peaks.Count >= 2 && stats.Vpp > 0.01)
            {
                var sorted = peaks.OrderByDescending(p => hist[p]).Take(2).OrderBy(p => p).ToList();
                double lowLevel = histMin + sorted[0] * binW + binW / 2;
                double highLevel = histMin + sorted[1] * binW + binW / 2;
                double midLevel = (lowLevel + highLevel) / 2;

                double tolerance = (highLevel - lowLevel) * 0.3;
                int lowCount = 0, highCount = 0, transitCount = 0;
                for (int i = 0; i < yVals.Length; i++)
                {
                    if (Math.Abs(yVals[i] - lowLevel) < tolerance) lowCount++;
                    else if (Math.Abs(yVals[i] - highLevel) < tolerance) highCount++;
                    else if (yVals[i] > lowLevel + tolerance && yVals[i] < highLevel - tolerance)
                        transitCount++;
                }
                double totalClose = lowCount + highCount;
                stats.DualLevelConfidence = totalClose / yVals.Length * 100;

                if (lowCount + highCount > 0)
                    stats.DutyCycle = (double)highCount / (lowCount + highCount) * 100;

                // ── 上升/下降时间 ──
                double rise10 = lowLevel + (highLevel - lowLevel) * 0.1;
                double rise90 = lowLevel + (highLevel - lowLevel) * 0.9;

                double riseStart = -1, riseEnd = -1, fallStart = -1, fallEnd = -1;
                for (int i = 1; i < yVals.Length; i++)
                {
                    if (yVals[i - 1] <= midLevel && yVals[i] > midLevel)
                    {
                        for (int j = i - 1; j >= 0 && riseStart < 0; j--)
                            if (yVals[j] <= rise10) { riseStart = xVals[j]; break; }
                        for (int j = i; j < yVals.Length && riseEnd < 0; j++)
                            if (yVals[j] >= rise90) { riseEnd = xVals[j]; break; }
                    }
                    if (yVals[i - 1] >= midLevel && yVals[i] < midLevel)
                    {
                        for (int j = i - 1; j >= 0 && fallStart < 0; j--)
                            if (yVals[j] >= rise90) { fallStart = xVals[j]; break; }
                        for (int j = i; j < yVals.Length && fallEnd < 0; j++)
                            if (yVals[j] <= rise10) { fallEnd = xVals[j]; break; }
                    }
                    if (riseStart > 0 && riseEnd > 0 && fallStart > 0 && fallEnd > 0) break;
                }

                stats.RiseTime = (riseStart > 0 && riseEnd > 0) ? (riseEnd - riseStart) * MsPerDay : -1;
                stats.FallTime = (fallStart > 0 && fallEnd > 0) ? (fallStart - fallEnd) * MsPerDay : -1;

                // ── 频率与周期（过零法+滞回） ──
                double hysteresis = (highLevel - lowLevel) * 0.15;
                double thUp = midLevel + hysteresis;
                double thDn = midLevel - hysteresis;
                bool above = yVals[0] > thUp;
                var crossings = new List<double>();
                for (int i = 1; i < yVals.Length; i++)
                {
                    if (!above && yVals[i] > thUp)
                    {
                        double frac = (thUp - yVals[i - 1]) / (yVals[i] - yVals[i - 1]);
                        crossings.Add(xVals[i - 1] + frac * (xVals[i] - xVals[i - 1]));
                        above = true;
                    }
                    else if (above && yVals[i] < thDn)
                    {
                        above = false;
                    }
                }

                if (crossings.Count >= 3)
                {
                    var periods = new List<double>();
                    for (int i = 1; i < crossings.Count; i++)
                        periods.Add(crossings[i] - crossings[i - 1]);
                    periods.Sort();
                    double medPeriod = periods[periods.Count / 2]; // 天数
                    stats.Period = medPeriod * MsPerDay;            // ms
                    stats.Frequency = medPeriod > 1e-12 ? 1.0 / (medPeriod * SecPerDay) : 0; // Hz

                    stats.HighTime = stats.Period * stats.DutyCycle / 100;
                    stats.LowTime = stats.Period - stats.HighTime;

                    if (periods.Count > 1)
                    {
                        double avgPer = periods.Average();
                        double sumJit = 0;
                        for (int i = 0; i < periods.Count; i++)
                            sumJit += (periods[i] - avgPer) * (periods[i] - avgPer);
                        stats.JitterSigma = Math.Sqrt(sumJit / periods.Count) * MsPerDay;
                    }

                    if (stats.RiseTime > 0 && stats.SampleIntervalMs > 0)
                    {
                        double riseV = (highLevel - lowLevel) * 0.8;
                        double riseSamples = stats.RiseTime / stats.SampleIntervalMs;
                        if (riseSamples > 0) stats.EdgeSteepness = riseV / riseSamples;
                    }
                }
                else
                {
                    stats.FrequencyNote = "未检测到明显周期";
                }
            }
            else
            {
                stats.DualLevelConfidence = 0;
                stats.FrequencyNote = peaks.Count switch
                {
                    0 => "DC 电平或噪声",
                    1 => "单电平信号",
                    _ => "复杂信号，无明显双电平"
                };
            }

            return stats;
        }
    }
}
