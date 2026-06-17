using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace 串口助手
{
    public partial class MainWindow
    {
        // ——— 传感面板 ———
        private SensorPanelViewModel _sensorVM;
        private readonly Dictionary<SensorCardViewModel, (Polygon polygon, Polyline polyline)> _miniPlotMap = new();
        private readonly Dictionary<SensorCardViewModel, Border> _cardBorderMap = new();
        private readonly Dictionary<SensorCardViewModel, TextBlock> _cardTitleMap = new();
        private readonly Dictionary<SensorCardViewModel, TextBlock> _cardValueMap = new();
        private readonly Dictionary<SensorCardViewModel, TextBlock> _cardAuxMap = new();
        private readonly Dictionary<SensorCardViewModel, Border> _cardProgressFillMap = new();
        private readonly Dictionary<SensorCardViewModel, TextBlock> _cardProgressPctMap = new();
        private readonly Dictionary<SensorCardViewModel, Border> _cardToggleTrackMap = new();
        private readonly Dictionary<SensorCardViewModel, Ellipse> _cardToggleThumbMap = new();
        private readonly Dictionary<SensorCardViewModel, TextBlock> _cardStatusLabelMap = new();
        private readonly Dictionary<SensorCardViewModel, Rectangle> _cardStripMap = new();
        private DispatcherTimer _sensorRefreshTimer;

        // ——— 初始化 ———
        private void InitSensorPanel()
        {
            if (_sensorVM != null) return;
            _sensorVM = new SensorPanelViewModel();

            if (_prefsData != null && _prefsData.TryGetValue("sensors", out var sensorsObj)
                && sensorsObj is System.Collections.IList arr && arr.Count > 0)
            {
                var list = new List<object>();
                foreach (var item in arr) list.Add(item);
                _sensorVM.Deserialize(list);
            }

            if (_sensorVM.Groups.Count == 0)
                _sensorVM.AddGroup();

            if (_sensorRefreshTimer == null)
            {
                _sensorRefreshTimer = new DispatcherTimer(
                    TimeSpan.FromMilliseconds(33), DispatcherPriority.Background,
                    (s, e) => RefreshMiniPlots(), Dispatcher);
            }

            RefreshAllRows();
        }

        // ═══ 工具 ═══

        private static SolidColorBrush ParseColor(string hex, double alpha = 1.0)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length < 7)
                return new SolidColorBrush(Colors.Gray);
            try
            {
                byte r = Convert.ToByte(hex.Substring(1, 2), 16);
                byte g = Convert.ToByte(hex.Substring(3, 2), 16);
                byte b = Convert.ToByte(hex.Substring(5, 2), 16);
                byte a = (byte)(Math.Clamp(alpha, 0, 1) * 255);
                return new SolidColorBrush(Color.FromArgb(a, r, g, b));
            }
            catch { return new SolidColorBrush(Colors.Gray); }
        }

        private static string GetDisplayValue(SensorCardViewModel vm)
        {
            if (vm.Type == "status") return vm.Status ?? "--";
            if (vm.Type == "control") return (vm.Status == "on") ? "ON" : "OFF";
            return (vm.Value ?? "--") + vm.GetUnit();
        }

        private static double CalcProgressPct(SensorCardViewModel vm)
        {
            if (vm.Type == "pressure")
                return double.TryParse(vm.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double hpa)
                    ? Math.Clamp(hpa / 1013.25 * 100, 0, 100) : 0;
            return double.TryParse(vm.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double hum)
                ? Math.Clamp(hum, 0, 100) : 0;
        }

        // ═══ 八卡工厂 ═══

        private Border CreateSensorCard(SensorCardViewModel vm)
        {
            var isDark = isDarkTheme;
            var accentHex = vm.GetAccentHex(isDark);
            var accentBrush = ParseColor(accentHex);
            var accentFill = ParseColor(accentHex, alpha: 0.35);

            // 左竖条
            var strip = new Rectangle { Width = 12, Fill = accentBrush };
            _cardStripMap[vm] = strip;

            // 标题——最大文字，和竖条同色
            var title = new TextBlock
            {
                Text = vm.Name, FontSize = 15, FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                Margin = new Thickness(0, 0, 0, 2),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            _cardTitleMap[vm] = title;

            // 主值
            var valueText = new TextBlock
            {
                Text = GetDisplayValue(vm), FontSize = 22, FontWeight = FontWeights.Bold,
                Foreground = accentBrush, Margin = new Thickness(0, 1, 0, 1),
            };
            _cardValueMap[vm] = valueText;

            // 辅助参数——大一点
            var auxText = new TextBlock
            {
                Text = vm.AuxText ?? "", FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Visibility = string.IsNullOrEmpty(vm.AuxText) ? Visibility.Collapsed : Visibility.Visible,
            };
            _cardAuxMap[vm] = auxText;

            // 进度条（湿度/气压）——紧凑
            FrameworkElement progressBar = null;
            if (vm.Type == "humidity" || vm.Type == "pressure")
            {
                double pct = CalcProgressPct(vm);
                var (pgBar, pgFill, pgPct) = CreateProgressBar(pct, accentBrush);
                _cardProgressFillMap[vm] = pgFill;
                _cardProgressPctMap[vm] = pgPct;
                progressBar = pgBar;
            }

            // 迷你波形——占剩余空间的大头
            FrameworkElement miniPlot = null;
            bool hasWaveform = vm.Type != "status" && vm.Type != "control" && vm.Type != "slider";
            if (vm.Type == "generic") hasWaveform = vm.ShowWaveform;
            if (hasWaveform)
                miniPlot = CreateMiniPlot(vm, accentBrush, accentFill);

            // 开关卡：Canvas 定位胶囊滑块
            FrameworkElement toggleSection = null;
            TextBlock statusLabel = null;
            if (vm.Type == "control")
            {
                var (section, track, thumb) = CreateToggleSwitch(vm);
                toggleSection = section;
                _cardToggleTrackMap[vm] = track;
                _cardToggleThumbMap[vm] = thumb;
                statusLabel = new TextBlock
                {
                    Text = vm.Status == "on" ? "状态：已开启" : "状态：已关闭",
                    FontSize = 11, Foreground = (Brush)FindResource("TextSecondaryBrush"),
                    Margin = new Thickness(0, 4, 0, 0),
                };
                _cardStatusLabelMap[vm] = statusLabel;
            }

            // 组装——波形用 * 行自动填满剩余高度
            var contentGrid = new Grid { Margin = new Thickness(8, 6, 8, 6) };

            // 上半：文字区（Auto 行）
            var textStack = new StackPanel();
            textStack.Children.Add(title);
            textStack.Children.Add(valueText);
            if (progressBar != null) { textStack.Children.Add(progressBar); }
            else if (miniPlot == null) { textStack.Children.Add(auxText); }
            if (toggleSection != null) { textStack.Children.Add(toggleSection); textStack.Children.Add(statusLabel); }

            if (miniPlot != null)
            {
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                if (progressBar == null) textStack.Children.Add(auxText); // 辅助行在波形上方
                Grid.SetRow(textStack, 0);
                Grid.SetRow(miniPlot, 1);
                // Canvas 高度走 VerticalAlignment=Stretch + 实际可用高度
                if (miniPlot is Canvas cv)
                {
                    cv.Height = double.NaN; // 由所在行的 Stretch 决定，初始给 1 防报错
                    cv.VerticalAlignment = VerticalAlignment.Stretch;
                }
                contentGrid.Children.Add(textStack);
                contentGrid.Children.Add(miniPlot);
            }
            else
            {
                // 无波形卡——纯文字，StackPanel 撑到满
                contentGrid.Children.Add(textStack);
            }

            // 外层 Grid（左竖条 + 内容）
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(strip, 0);
            Grid.SetColumn(contentGrid, 1);
            grid.Children.Add(strip);
            grid.Children.Add(contentGrid);

            double cardWidth = vm.Type == "slider" ? 280 : 160;
            var card = new Border
            {
                Width = cardWidth, Height = 200,
                Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(0.5),
                CornerRadius = new CornerRadius(8),
                Child = grid,
                Margin = new Thickness(0, 0, 8, 8),
            };
            _cardBorderMap[vm] = card;

            return card;
        }

        // ═══ 进度条——条在左，数字在右 ═══

        // 返回：(整行容器 Grid, 填充条 Border, 百分比 TextBlock)
        private (FrameworkElement row, Border fill, TextBlock pct) CreateProgressBar(double percent, Brush fillBrush)
        {
            // 彩色填充条
            var fillBorder = new Border
            {
                Background = fillBrush, CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = Math.Max(percent / 100.0 * 95, percent > 0 ? 4 : 0),
                Height = 12,
            };

            // 灰底轨道——填充条放里面
            var track = new Border
            {
                Background = ParseColor(isDarkTheme ? "#3A3A3D" : "#E8E8E8"),
                CornerRadius = new CornerRadius(3),
                Height = 12,
                Child = fillBorder,
            };

            // 百分比数字——在条右边
            var pctText = new TextBlock
            {
                Text = $"{percent:F1}%", FontSize = 9,
                Foreground = fillBrush, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
            };

            // 自适应：条占 *（填满剩余宽度），数字占 Auto
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(track, 0);
            Grid.SetColumn(pctText, 1);
            row.Children.Add(track);
            row.Children.Add(pctText);

            return (row, fillBorder, pctText);
        }

        // ═══ 胶囊滑块——Canvas 绝对定位 ═══

        private (Border container, Border track, Ellipse thumb) CreateToggleSwitch(SensorCardViewModel vm)
        {
            bool isOn = vm.Status == "on";
            var isDark = isDarkTheme;
            var accentHex = vm.GetAccentHex(isDark);

            var track = new Border
            {
                Width = 60, Height = 30,
                CornerRadius = new CornerRadius(15),
                Background = isOn ? ParseColor(accentHex) : ParseColor(isDark ? "#555555" : "#BBBBBB"),
            };

            var canvas = new Canvas
            {
                Width = 60, Height = 30,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            var thumb = new Ellipse
            {
                Width = 20, Height = 20,
                Fill = isOn ? Brushes.White : ParseColor(isDark ? "#AAAAAA" : "#E0E0E0"),
            };
            Canvas.SetLeft(thumb, isOn ? 35 : 5);
            Canvas.SetTop(thumb, 5);
            canvas.Children.Add(thumb);

            // 点击滑块 → 反转状态 → 发串口
            canvas.MouseLeftButtonDown += (s, e) =>
            {
                string newState = vm.Status == "on" ? "off" : "on";
                vm.Status = newState;
                vm.Value = newState;
                string cmd = $"[ctrl,{vm.SubType},{vm.Name},{newState}]\r\n";
                SendRaw(cmd);
                UpdateCardUI(vm);
                e.Handled = true;
            };

            var container = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            container.Children.Add(track);
            container.Children.Add(canvas);

            return (new Border { Child = container }, track, thumb);
        }

        // ═══ 迷你波形 ═══

        private Canvas CreateMiniPlot(SensorCardViewModel vm, Brush lineBrush, Brush fillBrush)
        {
            var canvas = new Canvas
            {
                // 宽高由 Grid * 行/列自动分配，不设固定值
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                MinHeight = 40,
                Margin = new Thickness(0, 2, 0, 0),
            };
            var polygon = new Polygon { Fill = fillBrush, Stroke = null };
            var polyline = new Polyline
            {
                Stroke = lineBrush, StrokeThickness = 1.0, StrokeLineJoin = PenLineJoin.Round,
            };
            canvas.Children.Add(polygon);
            canvas.Children.Add(polyline);
            _miniPlotMap[vm] = (polygon, polyline);
            return canvas;
        }

        private void RefreshMiniPlots()
        {
            if (_sensorVM == null || !_sensorVM.IsActive) return;

            foreach (var (vm, (polygon, polyline)) in _miniPlotMap)
            {
                var data = vm.History.GetSnapshot();
                polygon.Points.Clear();
                polyline.Points.Clear();
                if (data.Count < 2) continue;

                // 读 Canvas 实际宽高（由 Grid * 行分配）
                var canvas = polyline.Parent as Canvas;
                double cw = (canvas != null && canvas.ActualWidth > 0) ? canvas.ActualWidth : 130;
                double ch = (canvas != null && canvas.ActualHeight > 0) ? canvas.ActualHeight : 60;

                double yMin = data.Min(), yMax = data.Max();
                bool isFlat = (yMax - yMin) < 0.01;
                double yRange = isFlat ? 1.0 : (yMax - yMin);

                var linePoints = new PointCollection();
                for (int i = 0; i < data.Count; i++)
                {
                    double x = cw * i / (data.Count - 1);
                    double y = isFlat
                        ? ch * 0.30
                        : ch * (1.0 - (data[i] - yMin) / yRange * 0.85);
                    linePoints.Add(new Point(x, y));
                }
                polyline.Points = linePoints;

                var areaPoints = new PointCollection(linePoints);
                areaPoints.Add(new Point(cw, ch));
                areaPoints.Add(new Point(0, ch));
                polygon.Points = areaPoints;
            }
        }

        // ═══ 增量更新 ═══

        private void UpdateCardUI(SensorCardViewModel vm)
        {
            if (vm == null) return;
            var isDark = isDarkTheme;
            var accentHex = vm.GetAccentHex(isDark);
            var accentBrush = ParseColor(accentHex);

            // 左竖条
            if (_cardStripMap.TryGetValue(vm, out var strip))
                strip.Fill = accentBrush;

            // 标题（开关卡 ON→OFF 时文字色跟变）
            if (_cardTitleMap.TryGetValue(vm, out var titleTb))
                titleTb.Foreground = accentBrush;

            // 主值
            if (_cardValueMap.TryGetValue(vm, out var valueTb))
            {
                valueTb.Text = GetDisplayValue(vm);
                valueTb.Foreground = accentBrush;
            }

            // 辅助
            if (_cardAuxMap.TryGetValue(vm, out var auxTb))
            {
                auxTb.Text = vm.AuxText ?? "";
                auxTb.Visibility = string.IsNullOrEmpty(vm.AuxText)
                    ? Visibility.Collapsed : Visibility.Visible;
            }

            // 进度条
            if (_cardProgressFillMap.TryGetValue(vm, out var fillBar)
                && _cardProgressPctMap.TryGetValue(vm, out var pctTb))
            {
                double pct = CalcProgressPct(vm);
                double trackW = (fillBar.Parent is Border tb && tb.ActualWidth > 0) ? tb.ActualWidth : 95;
                fillBar.Width = Math.Max(pct / 100.0 * trackW, pct > 0 ? 4 : 0);
                fillBar.Background = accentBrush;
                pctTb.Text = $"{pct:F1}%";
                pctTb.Foreground = accentBrush;
            }

            // 开关卡：Canvas 定位滑块
            if (_cardToggleTrackMap.TryGetValue(vm, out var track)
                && _cardToggleThumbMap.TryGetValue(vm, out var thumb))
            {
                bool isOn = vm.Status == "on";
                track.Background = isOn ? ParseColor(accentHex)
                                        : ParseColor(isDark ? "#555555" : "#BBBBBB");
                thumb.Fill = isOn ? Brushes.White
                                  : ParseColor(isDark ? "#AAAAAA" : "#E0E0E0");
                Canvas.SetLeft(thumb, isOn ? 35 : 5);
            }
            if (_cardStatusLabelMap.TryGetValue(vm, out var slTb))
            {
                slTb.Text = (vm.Status == "on") ? "状态：已开启" : "状态：已关闭";
            }
        }

        // ═══ 全量重建 ═══

        private void RefreshAllRows()
        {
            if (_sensorVM == null || sensorRowsPanel == null) return;

            sensorRowsPanel.Children.Clear();
            _miniPlotMap.Clear();
            _cardBorderMap.Clear();
            _cardTitleMap.Clear();
            _cardValueMap.Clear();
            _cardAuxMap.Clear();
            _cardProgressFillMap.Clear();
            _cardProgressPctMap.Clear();
            _cardToggleTrackMap.Clear();
            _cardToggleThumbMap.Clear();
            _cardStatusLabelMap.Clear();
            _cardStripMap.Clear();

            Brush groupBg = isDarkTheme
                ? ParseColor("#1C1C1E")       // 深于卡片 #252526 → 卡片浮在组框上
                : ParseColor("#E6E6EB");       // 深于卡片 #FFFFFF → 卡片浮在组框上

            foreach (var group in _sensorVM.Groups)
            {
                var groupBorder = new Border
                {
                    Background = groupBg,
                    BorderBrush = (Brush)FindResource("CardBorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 6, 8, 8),
                    Margin = new Thickness(0, 0, 0, 12),
                };

                var groupPanel = new StackPanel();

                if (!string.IsNullOrEmpty(group.Name))
                {
                    groupPanel.Children.Add(new TextBlock
                    {
                        Text = group.Name, FontSize = 11,
                        Foreground = (Brush)FindResource("TextMutedBrush"),
                        Margin = new Thickness(4, 0, 0, 4),
                    });
                }

                var currentWrap = new WrapPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Top,
                };
                groupPanel.Children.Add(currentWrap);

                foreach (var item in group.Items)
                {
                    if (item is string s && s == "---")
                    {
                        currentWrap = new WrapPanel
                        {
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Top,
                            Margin = new Thickness(0, 4, 0, 0),
                        };
                        groupPanel.Children.Add(currentWrap);
                        continue;
                    }

                    if (item is SensorCardViewModel cardVM)
                        currentWrap.Children.Add(CreateSensorCard(cardVM));
                }

                groupBorder.Child = groupPanel;
                sensorRowsPanel.Children.Add(groupBorder);
            }

            if (_sensorRefreshTimer != null && !_sensorRefreshTimer.IsEnabled && _sensorVM.IsActive)
                _sensorRefreshTimer.Start();
        }

        // ═══ 持久化 ═══

        private void SaveSensorPrefs()
        {
            if (_prefsData == null || _sensorVM == null) return;
            _prefsData["sensors"] = _sensorVM.Serialize();
            _prefs.Save(_prefsData);
        }

        // ═══ 编辑按钮（Phase A 占位） ═══

        private void BtnSensorEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_sensorVM == null) return;
            LogSystem("传感面板编辑模式将在 Phase B 实现");
        }
    }
}
