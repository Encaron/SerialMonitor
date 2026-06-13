using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;

namespace 串口助手
{
    public enum PlotDisplayMode { Scroll, Sweep }

    /// <summary>
    /// 波形图 ViewModel —— Phase 3 实现。
    /// </summary>
    public class PlotViewModel
    {
        public PlotModel Model { get; private set; }
        public bool IsPaused { get; set; }
        public bool ShowValueHud { get; set; } = true;
        public bool XAxisAutoRange { get; set; } = true;
        public bool YAxisAutoRange { get; set; } = true;
        public int MaxDataPoints { get; set; } = 200;
        public PlotDisplayMode DisplayMode { get; set; } = PlotDisplayMode.Scroll;

        // 扫描模式：固定 X 轴窗口起始时间
        private double _sweepStartX;
        public double YMin { get; set; } = 0;
        public double YMax { get; set; } = 100;
        public bool ShowMarkers { get; set; }
        public bool ShowLines { get; set; } = true;
        public Dictionary<string, double> LatestValues { get; } = new Dictionary<string, double>();

        private readonly Dictionary<string, LineSeries> _series = new Dictionary<string, LineSeries>();
        private readonly DateTimeAxis _xAxis;
        private readonly LinearAxis _yAxis;
        private bool _isDark;

        // 刷新限流：30Hz（≈33ms 间隔）
        private DateTime _lastRefresh = DateTime.MinValue;
        private bool _dirty;

        private const double SecPerDay = 86400.0;

        public PlotViewModel()
        {
            Model = new PlotModel
            {
                Title = null,
                PlotAreaBorderThickness = new OxyThickness(0),
                IsLegendVisible = true,
            };
            Model.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.LeftTop,
                LegendOrientation = LegendOrientation.Vertical,
            });

            _xAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "HH:mm:ss",
                MajorGridlineStyle = LineStyle.Dash,
                MinorGridlineStyle = LineStyle.None,
            };
            Model.Axes.Add(_xAxis);

            _yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                MajorGridlineStyle = LineStyle.Dash,
                MinorGridlineStyle = LineStyle.None,
                Minimum = YMin,
                Maximum = YMax,
            };
            Model.Axes.Add(_yAxis);

            UpdateThemeColors(false);
        }

        /// <summary>
        /// 扫描所有曲线的数据点，自动计算 Y 轴范围（留 10% 边距）。
        /// 在 Y 轴自动模式下调用。
        /// </summary>
        public void RecalcYAxis()
        {
            if (!YAxisAutoRange) return;

            double min = double.MaxValue, max = double.MinValue;
            // 扫描所有 Series（包括测试曲线等未注册到 _series 的）
            foreach (var s in Model.Series)
            {
                if (!(s is LineSeries ls)) continue;
                foreach (var pt in ls.Points)
                {
                    if (pt.Y < min) min = pt.Y;
                    if (pt.Y > max) max = pt.Y;
                }
            }
            if (min < double.MaxValue)
            {
                double margin = (max - min) * 0.1;
                if (margin < 0.01) margin = 1;
                _yAxis.Zoom(min - margin, max + margin);
            }
            else
            {
                _yAxis.Zoom(YMin, YMax);
            }
            Model.InvalidatePlot(true);
        }

        /// <summary>
        /// 主题切换时更新 OxyPlot 颜色（OxyPlot 不支持 DynamicResource，需手动切换）。
        /// </summary>
        public void UpdateThemeColors(bool isDark)
        {
            _isDark = isDark;

            // 亮色主题：深色文字，浅色网格线
            // 暗色主题：浅色文字，暗色网格线
            var textColor = isDark
                ? OxyColor.FromRgb(0xD4, 0xD4, 0xD4)
                : OxyColor.FromRgb(0x2D, 0x2D, 0x2D);
            var gridColor = isDark
                ? OxyColor.FromRgb(0x3E, 0x3E, 0x42)
                : OxyColor.FromRgb(0xE0, 0xE0, 0xE0);
            var tickColor = isDark
                ? OxyColor.FromRgb(0x5A, 0x5A, 0x5A)
                : OxyColor.FromRgb(0xBB, 0xBB, 0xBB);
            var legendTextColor = isDark
                ? OxyColor.FromRgb(0xD4, 0xD4, 0xD4)
                : OxyColor.FromRgb(0x2D, 0x2D, 0x2D);

            Model.TextColor = textColor;
            Model.PlotAreaBackground = isDark
                ? OxyColor.FromRgb(0x1A, 0x1A, 0x1C)   // 画图区域背景（暗色=深黑）
                : OxyColor.FromRgb(0xFA, 0xFA, 0xFA);   // 亮色=浅灰

            foreach (var axis in Model.Axes)
            {
                axis.TextColor = textColor;
                axis.TicklineColor = tickColor;
                axis.MajorGridlineColor = gridColor;
            }

            if (Model.Legends.Count > 0)
                Model.Legends[0].LegendTextColor = legendTextColor;

            Model.InvalidatePlot(true);
        }

        public void OnPlotMessage(string name, double value, DateTime timestamp)
        {
            if (IsPaused) return;

            if (!_series.TryGetValue(name, out var series))
            {
                series = CreateSeries(name);
                _series[name] = series;
                Model.Series.Add(series);
            }

            series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(timestamp), value));
            LatestValues[name] = value;

            // 内存安全
            if (series.Points.Count > 5000)
                series.Points.RemoveAt(0);

            // 扫描模式：数据点超出窗口 → 清空重画
            if (DisplayMode == PlotDisplayMode.Sweep)
            {
                double sweepSec = MaxDataPoints * 0.05; // N点 × 假设20Hz间隔
                double sweepWidth = DateTimeAxis.ToDouble(_xAxis.ConvertToDateTime(_sweepStartX).AddSeconds(sweepSec)) - _sweepStartX;
                double currentX = DateTimeAxis.ToDouble(timestamp);
                if (_sweepStartX == 0) _sweepStartX = currentX;
                if (currentX - _sweepStartX > sweepWidth)
                {
                    foreach (var s in _series.Values) s.Points.Clear();
                    foreach (var o in Model.Series)
                        if (o is LineSeries l) l.Points.Clear();
                    _sweepStartX = currentX;
                    series = _series[name]; // 重新获取引用
                    series.Points.Add(new DataPoint(currentX, value));
                }
            }

            // 限流 30Hz 刷新
            _dirty = true;
            var now = DateTime.Now;
            if ((now - _lastRefresh).TotalMilliseconds >= 33)
            {
                _lastRefresh = now;
                _dirty = false;
                if (DisplayMode == PlotDisplayMode.Scroll)
                    ApplyXAxisWindow();
                else
                    ApplySweepWindow();
                RecalcYAxis();
                Model.InvalidatePlot(true);
            }
        }

        /// <summary>恢复后重置节流计时器，确保首个数据点立即触发刷新</summary>
        public void OnResumeDrawing()
        {
            _lastRefresh = DateTime.MinValue;
        }

        /// <summary>扫描模式窗口：固定宽度，从 _sweepStartX 开始</summary>
        private void ApplySweepWindow()
        {
            if (_sweepStartX == 0) return;
            double sweepSec = MaxDataPoints * 0.05;
            double endX = DateTimeAxis.ToDouble(_xAxis.ConvertToDateTime(_sweepStartX).AddSeconds(sweepSec));
            _xAxis.Zoom(_sweepStartX, endX);
        }

        /// <summary>
        /// 滚动模式：点索引基线（稳定滚动），时间上下界防抖动 + 防暂停 gap 压扁。
        /// </summary>
        public void ApplyXAxisWindow()
        {
            double windowStart = double.MaxValue;
            double windowEnd = double.MinValue;
            int totalPoints = 0;

            foreach (var s in Model.Series)
            {
                if (!(s is LineSeries ls) || ls.Points.Count == 0) continue;
                totalPoints += ls.Points.Count;

                // 窗口起点：第 (Count - MaxDataPoints) 个点（原始逻辑，保证滚动稳定）
                int startIdx = ls.Points.Count - MaxDataPoints;
                if (startIdx < 0) startIdx = 0;
                double startX = ls.Points[startIdx].X;
                if (startX < windowStart) windowStart = startX;
                // 窗口终点：最后一个点
                double endX = ls.Points[ls.Points.Count - 1].X;
                if (endX > windowEnd) windowEnd = endX;
            }

            if (totalPoints > 0 && windowStart < windowEnd)
            {
                double windowRange = windowEnd - windowStart;
                // 至少 3 秒宽：防止暂停恢复后窗口只剩几个新点 → 波形平缓
                double minRange = 3.0 / SecPerDay;
                if (windowRange < minRange)
                {
                    windowStart = windowEnd - minRange;
                    windowRange = minRange;
                }
                // 最多 60 秒宽：防止含 gap 时窗口过宽 → 波形被压扁
                double maxRange = 60.0 / SecPerDay;
                if (windowRange > maxRange)
                {
                    windowStart = windowEnd - maxRange;
                    windowRange = maxRange;
                }
                double margin = windowRange * 0.02;
                _xAxis.Zoom(windowStart - margin, windowEnd + margin);
                Model.InvalidatePlot(true);
            }
        }

        /// <summary>用户手动设置 Y 轴范围（关掉自动时调用）</summary>
        public void SetYRange(double min, double max)
        {
            YMin = min;
            YMax = max;
            _yAxis.Zoom(min, max);
            Model.InvalidatePlot(true);
        }

        /// <summary>强制刷新（暂停/清除/切换模式时调用）</summary>
        public void Flush()
        {
            if (_dirty)
            {
                _dirty = false;
                _lastRefresh = DateTime.Now;
                ApplyXAxisWindow();
                RecalcYAxis();
                Model.InvalidatePlot(true);
            }
        }

        public void Clear()
        {
            _dirty = false;
            _series.Clear();
            Model.Series.Clear();
            LatestValues.Clear();
            _sweepStartX = 0;
            Model.InvalidatePlot(true);
        }

        public void ResetView()
        {
            _xAxis.Reset();

            // 扫描所有数据点计算 Y 轴范围
            double min = double.MaxValue, max = double.MinValue;
            foreach (var s in Model.Series)
            {
                if (!(s is LineSeries ls)) continue;
                foreach (var pt in ls.Points)
                {
                    if (pt.Y < min) min = pt.Y;
                    if (pt.Y > max) max = pt.Y;
                }
            }
            if (min < double.MaxValue)
            {
                double margin = (max - min) * 0.1;
                if (margin < 0.01) margin = 1;
                _yAxis.Zoom(min - margin, max + margin);
            }

            Model.InvalidatePlot(true);
        }

        public string ExportCsv()
        {
            var sb = new System.Text.StringBuilder();
            var names = _series.Keys.ToList();
            sb.Append("Timestamp");
            foreach (var name in names) sb.Append("," + name);
            sb.AppendLine();

            int maxCount = 0;
            foreach (var s in _series.Values)
                if (s.Points.Count > maxCount) maxCount = s.Points.Count;

            for (int i = 0; i < maxCount; i++)
            {
                string ts = "";
                var parts = new List<string>();
                foreach (var name in names)
                {
                    var s = _series[name];
                    if (i < s.Points.Count)
                    {
                        var pt = s.Points[i];
                        if (string.IsNullOrEmpty(ts))
                            ts = _xAxis.ConvertToDateTime(pt.X).ToString("yyyy-MM-dd HH:mm:ss.fff");
                        parts.Add(pt.Y.ToString("F4"));
                    }
                    else parts.Add("");
                }
                sb.AppendLine(ts + "," + string.Join(",", parts));
            }

            return sb.ToString();
        }

        public void TogglePause()
        {
            IsPaused = !IsPaused;
            if (IsPaused) Flush();  // 暂停时刷新残留数据
        }

        /// <summary>
        /// 获取某条曲线的原始数据点（用于统计分析）。
        /// </summary>
        public List<DataPoint> GetChannelData(string channelName)
        {
            if (_series.TryGetValue(channelName, out var ls))
                return new List<DataPoint>(ls.Points);
            // 也搜索未注册到 _series 的曲线
            foreach (var s in Model.Series)
            {
                if (s is LineSeries l && l.Title == channelName)
                    return new List<DataPoint>(l.Points);
            }
            return new List<DataPoint>();
        }

        /// <summary>
        /// 获取所有已注册曲线名。
        /// </summary>
        public List<string> GetChannelNames()
        {
            return _series.Keys.ToList();
        }

        /// <summary>
        /// 获取图例显示名（Title）。
        /// </summary>
        public string GetLegendTitle(string channelName)
        {
            return channelName;
        }

        /// <summary>
        /// 暂停/继续切换，同时返回新状态。
        /// Pause → 冻结波形；Resume → 清空冻结标记。
        /// </summary>
        public bool TogglePauseAndReturnState()
        {
            TogglePause();
            return IsPaused;
        }

        public void SetMarkers(bool show)
        {
            ShowMarkers = show;
            foreach (var s in _series.Values)
            {
                s.MarkerType = show ? MarkerType.Circle : MarkerType.None;
                s.MarkerSize = show ? 3 : 0;
            }
            Model.InvalidatePlot(true);
        }

        public void SetLines(bool show)
        {
            ShowLines = show;
            foreach (var s in _series.Values)
            {
                s.LineStyle = show ? LineStyle.Solid : LineStyle.None;
            }
            Model.InvalidatePlot(true);
        }

        private LineSeries CreateSeries(string name)
        {
            var colorIndex = _series.Count % 8;
            OxyColor color;
            switch (colorIndex)
            {
                case 0: color = OxyColor.FromRgb(0x0E, 0x63, 0x9C); break; // 蓝
                case 1: color = OxyColor.FromRgb(0xD4, 0x4E, 0x2A); break; // 橙红
                case 2: color = OxyColor.FromRgb(0x38, 0x9A, 0x38); break; // 绿
                case 3: color = OxyColor.FromRgb(0xC4, 0x7D, 0x2A); break; // 棕
                case 4: color = OxyColor.FromRgb(0x8E, 0x3B, 0x9E); break; // 紫
                case 5: color = OxyColor.FromRgb(0x2A, 0x9D, 0xC4); break; // 青
                case 6: color = OxyColor.FromRgb(0xC4, 0x2A, 0x7A); break; // 粉
                default: color = OxyColor.FromRgb(0xAA, 0xAA, 0xAA); break; // 灰
            }

            return new LineSeries
            {
                Title = name,
                StrokeThickness = 2,
                Color = color,
                MarkerType = ShowMarkers ? MarkerType.Circle : MarkerType.None,
                MarkerSize = ShowMarkers ? 3 : 0,
                MarkerFill = color,
                MarkerStroke = color,
                LineStyle = ShowLines ? LineStyle.Solid : LineStyle.None,
                CanTrackerInterpolatePoints = true,
            };
        }
    }
}
