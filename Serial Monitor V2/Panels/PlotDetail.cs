using OxyPlot;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace 串口助手
{
    public partial class MainWindow
    {
        // ==================================================================
        //  曲线详情状态
        // ==================================================================

        private bool _plotShowDetail;
        private string _plotDetailCurveName;
        private bool _plotTrackerDark;

        // ==================================================================
        //  详情面板切换
        // ==================================================================

        private void SwitchToPlotDetail(string curveName)
        {
            // 确保在波形标签页（用户可能从设置/按键/滑杆等其他页点曲线）
            if (_currentTab != "Plot")
            {
                if (tabPlot != null) tabPlot.IsChecked = true;
                _currentTab = "Plot";
                _previousContentTab = "Plot";
                EnsurePlotView();
            }
            _plotShowDetail = true;
            _plotDetailCurveName = curveName;
            RefreshPlotDetail(curveName);
            RefreshContentVisibility();
        }

        private void ReturnToPlotConfig()
        {
            _plotShowDetail = false;
            _plotDetailCurveName = null;
            RefreshContentVisibility();
        }

        private void TogglePlotPauseWithDetail()
        {
            if (!_plotVM.IsPaused)
            {
                _plotVM.TogglePause();
                if (plotView != null)
                    plotView.Controller = new PlotController(); // 默认控制器：单击追踪框、右键平移、滚轮缩放
                btnPlotPause.Content = "▶ 继续";
            }
            else
            {
                _plotVM.TogglePause();
                if (plotView != null)
                    plotView.Controller = null;
                btnPlotPause.Content = "⏸ 暂停";
                if (_plotShowDetail)
                    ReturnToPlotConfig();

                // 恢复时：① 重置节流时钟确保首个数据点立即刷新
                //          ② 延迟强制 PlotView 重绘防 WPF 渲染合并丢帧
                _plotVM.OnResumeDrawing();
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (plotView != null)
                    {
                        plotView.InvalidateMeasure();
                        plotView.InvalidateVisual();
                        _plotVM.Model.InvalidatePlot(false);
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        // ==================================================================
        //  详情面板数据填充
        // ==================================================================

        private void RefreshPlotDetail(string curveName)
        {
            var data = _plotVM.GetChannelData(curveName);
            var stats = PlotAnalyzer.ComputeStats(data);

            tbPlotDetailTitle.Text = $"📌 {curveName}";

            // 幅值
            tbDetailVpp.Text  = FormatVoltage(stats.Vpp);
            tbDetailVmax.Text = FormatVoltage(stats.Vmax);
            tbDetailVmin.Text = FormatVoltage(stats.Vmin);
            tbDetailAvg.Text  = FormatVoltage(stats.Average);
            tbDetailRms.Text  = FormatVoltage(stats.RMS);

            // 频率 / 时间
            if (stats.Frequency > 0.01)
            {
                tbDetailFreq.Text   = $"{stats.Frequency:F2} Hz";
                tbDetailPeriod.Text = $"{stats.Period:F2} ms";
            }
            else
            {
                tbDetailFreq.Text   = stats.FrequencyNote ?? "—";
                tbDetailPeriod.Text = "—";
            }

            bool isDualLevel = stats.DualLevelConfidence > 30;
            if (isDualLevel && stats.DutyCycle > 0)
            {
                tbDetailDuty.Text  = $"{stats.DutyCycle:F1} %";
                tbDetailHigh.Text  = stats.HighTime > 0 ? $"{stats.HighTime:F2} ms" : "—";
                tbDetailLow.Text   = stats.LowTime  > 0 ? $"{stats.LowTime:F2} ms" : "—";
                tbDetailRise.Text  = stats.RiseTime >= 0 ? $"{stats.RiseTime:F2} ms" : "≤ 采样间隔";
                tbDetailFall.Text  = stats.FallTime >= 0 ? $"{stats.FallTime:F2} ms" : "≤ 采样间隔";
            }
            else
            {
                tbDetailDuty.Text  = "—";
                tbDetailHigh.Text  = "—";
                tbDetailLow.Text   = "—";
                tbDetailRise.Text  = "—";
                tbDetailFall.Text  = "—";
            }
            lblDuty.Visibility  = isDualLevel ? Visibility.Visible : Visibility.Collapsed;
            tbDetailDuty.Visibility  = isDualLevel ? Visibility.Visible : Visibility.Collapsed;
            lblHigh.Visibility  = isDualLevel ? Visibility.Visible : Visibility.Collapsed;
            tbDetailHigh.Visibility  = isDualLevel ? Visibility.Visible : Visibility.Collapsed;
            lblLow.Visibility   = isDualLevel ? Visibility.Visible : Visibility.Collapsed;
            tbDetailLow.Visibility   = isDualLevel ? Visibility.Visible : Visibility.Collapsed;
            lblRise.Visibility  = isDualLevel ? Visibility.Visible : Visibility.Collapsed;
            tbDetailRise.Visibility  = isDualLevel ? Visibility.Visible : Visibility.Collapsed;
            lblFall.Visibility  = isDualLevel ? Visibility.Visible : Visibility.Collapsed;
            tbDetailFall.Visibility  = isDualLevel ? Visibility.Visible : Visibility.Collapsed;

            tbDetailConf.Text = $"{stats.DualLevelConfidence:F0} %";
            tbDetailSteep.Text = stats.EdgeSteepness > 0
                ? $"{stats.EdgeSteepness:F3} V/sample" : "—";
            tbDetailJitter.Text = stats.JitterSigma > 0.001
                ? $"{stats.JitterSigma:F3} ms" : "—";

            string signalType;
            double vppToRms = stats.RMS > 0.001 ? stats.Vpp / stats.RMS : 0;
            // 正弦 Vpp/RMS ≈ 2√2 ≈ 2.828，方波 ≈ 2.0
            // 正弦 CrestFactor ≈ 1.414，方波 ≈ 1.0
            bool isSineLike = vppToRms > 2.5 || stats.CrestFactor > 1.35;
            bool hasSharpEdge = stats.EdgeSteepness > 0.3;

            if (stats.Vpp < 0.001)
                signalType = "DC 电平";
            // 方波：双电平置信度高 + 不是正弦特征 + 有陡边
            else if (stats.DualLevelConfidence >= 85 && !isSineLike && hasSharpEdge)
                signalType = stats.DutyCycle > 45 && stats.DutyCycle < 55 ? "方波" : "矩形波";
            // 斜边方波：双电平有但不够锐利
            else if (stats.DualLevelConfidence >= 60 && !isSineLike)
                signalType = "斜边方波（滤波/容性）";
            else if (stats.CrestFactor > 1.8)
                signalType = "脉冲/尖峰";
            else if (isSineLike)
                signalType = "正弦波/三角波";
            else
                signalType = "未知 / 混合";
            tbDetailSignalType.Text = signalType;

            bool hasConf = stats.DualLevelConfidence > 10;
            lblConf.Visibility  = hasConf ? Visibility.Visible : Visibility.Collapsed;
            tbDetailConf.Visibility = hasConf ? Visibility.Visible : Visibility.Collapsed;
            bool hasSteep = stats.EdgeSteepness > 0;
            lblSteep.Visibility = hasSteep ? Visibility.Visible : Visibility.Collapsed;
            tbDetailSteep.Visibility = hasSteep ? Visibility.Visible : Visibility.Collapsed;
            bool hasJitter = stats.JitterSigma > 0.001;
            lblJitter.Visibility = hasJitter ? Visibility.Visible : Visibility.Collapsed;
            tbDetailJitter.Visibility = hasJitter ? Visibility.Visible : Visibility.Collapsed;

            tbDetailPoints.Text = stats.PointCount.ToString("N0");
            tbDetailSpan.Text = stats.TimeSpanMs > 1000
                ? $"{stats.TimeSpanMs / 1000:F2} s"
                : $"{stats.TimeSpanMs:F1} ms";
        }

        private static string FormatVoltage(double v)
        {
            if (Math.Abs(v) < 10) return $"{v:F3} V";
            if (Math.Abs(v) < 100) return $"{v:F2} V";
            return $"{v:F1} V";
        }

        // ==================================================================
        //  事件处理
        // ==================================================================

        private void btnPlotDetailBack_Click(object sender, RoutedEventArgs e) => ReturnToPlotConfig();

        // ==================================================================
        //  复制按钮
        // ==================================================================

        private void btnPlotDetailCopyData_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_plotDetailCurveName)) return;
            var data = _plotVM.GetChannelData(_plotDetailCurveName);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# {_plotDetailCurveName}");
            sb.AppendLine("Timestamp,Y");
            foreach (var pt in data)
                sb.AppendLine($"{pt.X:F6},{pt.Y:F6}");
            CopyWithFeedback(sb.ToString(), sender as Button);
        }

        private void btnPlotDetailCopyStats_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_plotDetailCurveName)) return;
            CopyWithFeedback(BuildStatsText(), sender as Button);
        }

        private void btnPlotDetailCopyAll_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_plotDetailCurveName)) return;
            var data = _plotVM.GetChannelData(_plotDetailCurveName);
            var sb = new System.Text.StringBuilder();
            // 统计
            sb.AppendLine(BuildStatsText());
            sb.AppendLine();
            // 原始数据
            sb.AppendLine("=== 原始数据 ===");
            sb.AppendLine("Timestamp,Y");
            foreach (var pt in data)
                sb.AppendLine($"{pt.X:F6},{pt.Y:F6}");
            CopyWithFeedback(sb.ToString(), sender as Button);
        }

        private string BuildStatsText()
        {
            var data = _plotVM.GetChannelData(_plotDetailCurveName);
            var stats = PlotAnalyzer.ComputeStats(data);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# {_plotDetailCurveName} — 波形分析");
            sb.AppendLine($"# 点数:{stats.PointCount}  跨度:{stats.TimeSpanMs:F1}ms  采样间隔:{stats.SampleIntervalMs:F2}ms");
            sb.AppendLine();
            sb.AppendLine("=== 幅值 ===");
            sb.AppendLine($"Vpp          {stats.Vpp:F4} V");
            sb.AppendLine($"Vmax         {stats.Vmax:F4} V");
            sb.AppendLine($"Vmin         {stats.Vmin:F4} V");
            sb.AppendLine($"平均值 Avg   {stats.Average:F4} V");
            sb.AppendLine($"有效值 RMS   {stats.RMS:F4} V");
            sb.AppendLine();
            sb.AppendLine("=== 频率 / 时间 ===");
            if (stats.Frequency > 0.01)
            {
                sb.AppendLine($"频率         {stats.Frequency:F2} Hz");
                sb.AppendLine($"周期         {stats.Period:F2} ms");
            }
            else sb.AppendLine($"周期检测     {stats.FrequencyNote ?? "—"}");
            if (stats.DualLevelConfidence > 30 && stats.DutyCycle > 0)
            {
                sb.AppendLine($"占空比       {stats.DutyCycle:F1} %");
                sb.AppendLine($"高电平 Th    {stats.HighTime:F2} ms");
                sb.AppendLine($"低电平 Tl    {stats.LowTime:F2} ms");
                sb.AppendLine($"上升时间 Tr  {(stats.RiseTime >= 0 ? $"{stats.RiseTime:F2} ms" : "≤ 采样间隔")}");
                sb.AppendLine($"下降时间 Tf  {(stats.FallTime >= 0 ? $"{stats.FallTime:F2} ms" : "≤ 采样间隔")}");
            }
            sb.AppendLine();
            sb.AppendLine("=== 波形特征 ===");
            sb.AppendLine($"信号类型     {tbDetailSignalType.Text}");
            sb.AppendLine($"双电平置信度 {stats.DualLevelConfidence:F0} %");
            if (stats.EdgeSteepness > 0)
                sb.AppendLine($"边沿陡度     {stats.EdgeSteepness:F3} V/sample");
            if (stats.JitterSigma > 0.001)
                sb.AppendLine($"抖动 σ       {stats.JitterSigma:F3} ms");
            return sb.ToString();
        }

        // ==================================================================
        //  「📊 详细」按钮 —— 弹出曲线列表，点选查看详情
        // ==================================================================

        private void btnPlotDetail_Click(object sender, RoutedEventArgs e)
        {
            if (_plotVM == null) return;

            // 未暂停 → 提示
            if (!_plotVM.IsPaused)
            {
                LogSystem("📊 详细：请先暂停（点击 ⏸ 暂停 或单击波形图）");
                return;
            }

            // #10 FFT 频域模式：直接显示频域指标（不需要选曲线）
            if (_isFreqDomain)
            {
                SwitchToFreqDetail();
                return;
            }

            var names = _plotVM.GetChannelNames();
            if (names.Count == 0)
            {
                LogSystem("📊 详细：暂无曲线数据");
                return;
            }

            var style = (Style)FindResource("ContextMenuMenuItemStyle");
            var menu = new ContextMenu
            {
                PlacementTarget = sender as UIElement,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
            };
            foreach (var name in names)
            {
                var item = new MenuItem { Header = $"📈 {name}", Style = style };
                var capturedName = name;
                item.Click += (s2, e2) => SwitchToPlotDetail(capturedName);
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        }

        /// <summary>#10 FFT：直接切到频域详情面板</summary>
        private void SwitchToFreqDetail()
        {
            _plotShowDetail = true;
            _plotDetailCurveName = null;
            RefreshFreqDetail();
            RefreshContentVisibility();
        }

        /// <summary>#10 FFT：刷新频域详情面板（委托统一方法）</summary>
        private void RefreshFreqDetail()
        {
            UpdateFreqSideInfo();
        }

        // ==================================================================
        //  PlotView 鼠标交互：单击暂停 / 暂停后单击曲线切详情 / 暂停后 OxyPlot 控制器正常 tracking
        // ==================================================================

        /// <summary>
        /// MouseLeftButtonDown 统一入口：
        ///   运行中 → 暂停 + 启用控制器（缩放/平移/追踪框）
        ///   暂停中 → 先 HitTest 找曲线，命中→切详情，未命中→OxyPlot 默认 tracking
        /// </summary>
        private void PlotView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (plotView == null) return;

            if (!_plotVM.IsPaused)
            {
                // 运行中：暂停 + 启用 OxyPlot 默认控制器（缩放/平移/追踪框）
                TogglePlotPauseWithDetail();
                return;
            }

            // 暂停中：HitTest 查找曲线
            var pos = e.GetPosition(plotView);
            try
            {
                var hitArgs = new HitTestArguments(new ScreenPoint(pos.X, pos.Y), 30);
                var hits = plotView.ActualModel?.HitTest(hitArgs);
                if (hits != null)
                {
                    foreach (var hit in hits)
                    {
                        if (hit.Element is LineSeries ls && !string.IsNullOrEmpty(ls.Title))
                        {
                            SwitchToPlotDetail(ls.Title);
                            e.Handled = true; // 阻止 OxyPlot 同时弹出追踪框
                            return;
                        }
                    }
                }
                // 未命中曲线 → OxyPlot 默认控制器处理（追踪框等）
                // 延迟 80ms 扫描追踪框颜色（等 OxyPlot 创建 TrackerControl 并挂到可视化树）
                ScheduleTrackerColorFix();
            }
            catch { /* HitTest 异常静默，让 OxyPlot 正常处理 */ }
        }

        // ==================================================================
        //  追踪框颜色修复
        //  暗色模式：设置 plotView 的 TextElement.Foreground 继承属性 = 黑字
        //  追踪框的 TextBlock 是 plotView 子树里唯一的 WPF TextBlock（其他都是 OxyPlot 自绘）
        //  继承属性自动生效，OxyPlot 任何时候创建/更新追踪框都不会覆盖
        // ==================================================================

        public void FixPlotTrackerColors(bool isDark)
        {
            _plotTrackerDark = isDark;
            if (plotView != null)
            {
                TextElement.SetForeground(plotView,
                    isDark ? new SolidColorBrush(Colors.Black)
                           : new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)));
            }
        }

        /// <summary>暂停后在波形空白处点鼠标时，保险再设一次（OxyPlot 可能在 MouseDown 里重置）</summary>
        private void ScheduleTrackerColorFix()
        {
            if (plotView == null) return;
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            var dark = _plotTrackerDark;
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                if (plotView != null)
                    TextElement.SetForeground(plotView,
                        dark ? new SolidColorBrush(Colors.Black)
                             : new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)));
            };
            timer.Start();
        }
    }
}
