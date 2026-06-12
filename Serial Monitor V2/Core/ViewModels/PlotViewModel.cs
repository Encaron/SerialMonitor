using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;

namespace 串口助手
{
    /// <summary>
    /// 波形图 ViewModel —— Phase 3 实现。
    /// 管理 OxyPlot PlotModel，响应 [plot,名称,数值] 协议消息。
    /// </summary>
    public class PlotViewModel
    {
        /// <summary>OxyPlot 绘图模型，直接绑定到 PlotView</summary>
        public PlotModel Model { get; private set; }

        /// <summary>是否暂停绘图（暂停时数据不追加到曲线）</summary>
        public bool IsPaused { get; set; }

        /// <summary>当前是否显示数值 HUD 叠加层</summary>
        public bool ShowValueHud { get; set; } = true;

        /// <summary>X 轴范围模式：true=自动，false=固定</summary>
        public bool XAxisAutoRange { get; set; } = true;

        /// <summary>Y 轴范围模式：true=自动，false=固定</summary>
        public bool YAxisAutoRange { get; set; } = true;

        /// <summary>固定 X 轴范围（分钟）</summary>
        public double XAxisFixedRange { get; set; } = 5.0;

        /// <summary>是否显示数据点标记（圆点标点）</summary>
        public bool ShowMarkers { get; set; }

        /// <summary>是否显示连线</summary>
        public bool ShowLines { get; set; } = true;

        /// <summary>每条曲线名 → 最新值（用于数值 HUD 显示）</summary>
        public Dictionary<string, double> LatestValues { get; } = new Dictionary<string, double>();

        private readonly Dictionary<string, LineSeries> _series = new Dictionary<string, LineSeries>();
        private const int MaxPoints = 5000;
        private readonly DateTimeAxis _xAxis;

        public PlotViewModel()
        {
            Model = new PlotModel
            {
                Title = null,
                PlotAreaBorderThickness = new OxyThickness(0),
                IsLegendVisible = true,
                TextColor = OxyColor.FromRgb(0x9D, 0x9D, 0x9D),        // 轴标签颜色
            };
            Model.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.LeftTop,
                LegendOrientation = LegendOrientation.Vertical,
                LegendTextColor = OxyColor.FromRgb(0xD4, 0xD4, 0xD4),
            });

            _xAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = null,
                StringFormat = "HH:mm:ss",
                TextColor = OxyColor.FromRgb(0x9D, 0x9D, 0x9D),
                TicklineColor = OxyColor.FromRgb(0x5A, 0x5A, 0x5A),
                MajorGridlineStyle = LineStyle.Dash,
                MajorGridlineColor = OxyColor.FromRgb(0x3E, 0x3E, 0x42),
                MinorGridlineStyle = LineStyle.None,
            };
            Model.Axes.Add(_xAxis);

            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = null,
                TextColor = OxyColor.FromRgb(0x9D, 0x9D, 0x9D),
                TicklineColor = OxyColor.FromRgb(0x5A, 0x5A, 0x5A),
                MajorGridlineStyle = LineStyle.Dash,
                MajorGridlineColor = OxyColor.FromRgb(0x3E, 0x3E, 0x42),
                MinorGridlineStyle = LineStyle.None,
            };
            Model.Axes.Add(yAxis);

            // ★ 画一根静态测试线，验证 OxyPlot 渲染正常
            AddTestLine();
        }

        /// <summary>
        /// 静态测试曲线——用于验证 OxyPlot 能在 WPF 中正常渲染。
        /// 确认显示正常后删掉此方法和调用。
        /// </summary>
        private void AddTestLine()
        {
            var testSeries = new LineSeries
            {
                Title = "测试曲线",
                StrokeThickness = 2,
                Color = OxyColor.FromRgb(0x0E, 0x63, 0x9C),
                MarkerType = MarkerType.None,
                LineStyle = LineStyle.Solid,
            };

            var now = DateTime.Now;
            for (int i = 0; i < 100; i++)
            {
                double t = i * 0.1;
                double val = Math.Sin(t) * 2.5;
                testSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(now.AddSeconds(t)), val));
            }

            Model.Series.Add(testSeries);
            Model.InvalidatePlot(true);
        }

        /// <summary>
        /// 收到 [plot,名称,数值] 协议消息时调用。
        /// 如果曲线名不存在则自动创建新系列；存在则追加数据点。
        /// </summary>
        /// <param name="name">曲线名称（自动成为图例）</param>
        /// <param name="value">数值</param>
        /// <param name="timestamp">时间戳</param>
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

            // 裁剪旧数据点
            if (series.Points.Count > MaxPoints)
            {
                int removeCount = series.Points.Count - MaxPoints;
                for (int i = 0; i < removeCount; i++)
                    series.Points.RemoveAt(0);
            }

            // 更新最新值
            LatestValues[name] = value;

            // 刷新图表
            Model.InvalidatePlot(true);
        }

        /// <summary>
        /// 清空所有曲线和数据点。
        /// </summary>
        public void Clear()
        {
            _series.Clear();
            Model.Series.Clear();
            LatestValues.Clear();
            Model.InvalidatePlot(true);
        }

        /// <summary>
        /// 重置视图：恢复默认缩放和平移。
        /// </summary>
        public void ResetView()
        {
            _xAxis.Reset();
            foreach (var axis in Model.Axes.OfType<LinearAxis>())
                axis.Reset();
            Model.InvalidatePlot(true);
        }

        /// <summary>
        /// 导出所有曲线数据为 CSV 字节（文件名在 UI 层处理）。
        /// </summary>
        public string ExportCsv()
        {
            var sb = new System.Text.StringBuilder();
            // 表头
            var names = _series.Keys.ToList();
            sb.Append("Timestamp");
            foreach (var name in names)
                sb.Append("," + name);
            sb.AppendLine();

            // 找到最大点数
            int maxCount = 0;
            foreach (var s in _series.Values)
                if (s.Points.Count > maxCount) maxCount = s.Points.Count;

            // 按行输出（简单按索引对齐，不等长时间戳用各系列自身时间）
            for (int i = 0; i < maxCount; i++)
            {
                var parts = new List<string>();
                string ts = "";
                foreach (var name in names)
                {
                    var s = _series[name];
                    if (i < s.Points.Count)
                    {
                        var pt = s.Points[i];
                        if (string.IsNullOrEmpty(ts))
                            ts = DateTimeAxis.ToDateTime(pt.X).ToString("yyyy-MM-dd HH:mm:ss.fff");
                        parts.Add(pt.Y.ToString("F4"));
                    }
                    else
                    {
                        parts.Add("");
                    }
                }
                sb.AppendLine(ts + "," + string.Join(",", parts));
            }

            return sb.ToString();
        }

        /// <summary>
        /// 切换暂停状态。
        /// </summary>
        public void TogglePause()
        {
            IsPaused = !IsPaused;
        }

        /// <summary>
        /// 切换数据点标记显隐。
        /// </summary>
        public void SetMarkers(bool show)
        {
            ShowMarkers = show;
            foreach (var series in _series.Values)
            {
                series.MarkerType = show ? MarkerType.Circle : MarkerType.None;
                series.MarkerSize = show ? 3 : 0;
            }
            Model.InvalidatePlot(true);
        }

        /// <summary>
        /// 切换连线显隐。
        /// </summary>
        public void SetLines(bool show)
        {
            ShowLines = show;
            foreach (var series in _series.Values)
            {
                series.LineStyle = show ? LineStyle.Solid : LineStyle.None;
            }
            Model.InvalidatePlot(true);
        }

        private LineSeries CreateSeries(string name)
        {
            // 颜色轮换：蓝色系优先，然后用调色板
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

            var series = new LineSeries
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

            return series;
        }
    }
}
