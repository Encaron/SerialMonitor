using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
        private readonly Dictionary<SensorCardViewModel, Border> _cardStripMap = new();
        private DispatcherTimer _sensorRefreshTimer;
        private SensorCardViewModel _selectedCard;
        private readonly Dictionary<SensorCardViewModel, Border> _cardEditWrapperMap = new();
        private readonly Dictionary<SensorCardViewModel, Button> _cardDeleteBtnMap = new();
        private bool _sidePanelDirty;
        private DispatcherTimer _sidePanelRefreshTimer;
        private readonly Dictionary<SensorCardViewModel, Slider> _cardSliderMap = new();
        private readonly Dictionary<SensorCardViewModel, TextBlock> _cardSliderMinLabelMap = new();
        private readonly Dictionary<SensorCardViewModel, TextBlock> _cardSliderMaxLabelMap = new();
        private readonly Dictionary<SensorCardViewModel, DateTime> _sliderLastSend = new();
        private bool _sliderProgrammaticUpdate;
        private SensorCardViewModel _detailCard; // null = 编辑侧栏显示行管理器，非 null = 显示该卡详情面板

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

            // F2 改名——窗口级事件，侧栏有焦点也能触发
            Application.Current.MainWindow.PreviewKeyDown += (s, e) =>
            {
                if (_sensorVM == null || !_sensorVM.IsEditMode || _selectedCard == null) return;
                if (_currentTab != "Sensors") return;
                if (e.Key == System.Windows.Input.Key.F2)
                {
                    StartRenameCard(_selectedCard);
                    e.Handled = true;
                }
            };

            // 正常模式侧栏：500ms dirty flag 刷新（减少 UI 抖动）
            _sidePanelRefreshTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(500), DispatcherPriority.Background,
                (s, e) =>
                {
                    if (_sidePanelDirty && _sensorVM != null
                        && !_sensorVM.IsEditMode && _currentTab == "Sensors")
                    {
                        _sidePanelDirty = false;
                        RefreshSensorSidePanel();
                    }
                }, Dispatcher);
            _sidePanelRefreshTimer.Start();

            // 离线超时检测：每 1s 检查所有 status 卡，超过 2s 无数据 → 标红 offline
            var offlineTimer = new DispatcherTimer(
                TimeSpan.FromSeconds(1), DispatcherPriority.Background,
                (s, e) =>
                {
                    if (_sensorVM == null) return;
                    var now = DateTime.Now;
                    foreach (var group in _sensorVM.Groups)
                    {
                        foreach (var card in group.Items.OfType<SensorCardViewModel>())
                        {
                            if (card.Type != "status") continue;
                            if (card.Status == "offline") continue;
                            if ((now - card.LastSeen).TotalSeconds > 2)
                            {
                                card.Status = "offline";
                                card.Value = "offline";
                                card.AlarmReason = null;
                                if (_sensorVM.IsActive)
                                    UpdateCardUI(card);
                            }
                        }
                    }
                }, Dispatcher);
            offlineTimer.Start();

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

            // 左竖条——左上左下圆角对齐卡片外框
            var strip = new Border
            {
                Width = 12,
                Background = accentBrush,
                CornerRadius = new CornerRadius(8, 0, 0, 8),
            };
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

            // 滑杆卡：± 按钮 + Slider + min/max 标尺
            FrameworkElement sliderSection = null;
            if (vm.Type == "slider")
            {
                sliderSection = CreateSliderControl(vm, accentBrush);
            }

            // 组装——波形/Slider 用 * 行自动填满剩余高度
            var contentGrid = new Grid { Margin = new Thickness(8, 6, 8, 6) };

            // 上半：文字区（Auto 行）
            var textStack = new StackPanel();
            textStack.Children.Add(title);
            // 滑杆卡的值在 sliderSection 里，其他卡在 textStack 里
            if (vm.Type != "slider")
            {
                textStack.Children.Add(valueText);
            }
            if (progressBar != null) { textStack.Children.Add(progressBar); }
            else if (miniPlot == null && vm.Type != "slider") { textStack.Children.Add(auxText); }
            if (toggleSection != null) { textStack.Children.Add(toggleSection); textStack.Children.Add(statusLabel); }

            if (miniPlot != null)
            {
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                if (progressBar == null) textStack.Children.Add(auxText); // 辅助行在波形上方
                Grid.SetRow(textStack, 0);
                Grid.SetRow(miniPlot, 1);
                if (miniPlot is Canvas cv)
                {
                    cv.Height = double.NaN;
                    cv.VerticalAlignment = VerticalAlignment.Stretch;
                }
                contentGrid.Children.Add(textStack);
                contentGrid.Children.Add(miniPlot);
            }
            else if (sliderSection != null)
            {
                // 滑杆卡：文字区（仅标题）+ 滑杆控件填满
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                ((Grid)sliderSection).Margin = new Thickness(0, 4, 0, 0);
                Grid.SetRow(textStack, 0);
                Grid.SetRow(sliderSection, 1);
                contentGrid.Children.Add(textStack);
                contentGrid.Children.Add(sliderSection);
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

            double cardWidth = vm.Type == "slider" ? 306 : 160;
            var card = new Border
            {
                Width = cardWidth, Height = 160,
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
                Background = Brushes.Transparent, // 必须设背景才能接收点击
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

            // 点击滑块 → 反转状态 → 发串口（编辑模式禁用）
            canvas.MouseLeftButtonDown += (s, e) =>
            {
                if (_sensorVM != null && _sensorVM.IsEditMode) return;
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

        // ═══ 滑杆卡控件 ═══

        /// <summary>
        /// 滑杆卡控件：± 按钮 + Slider + min/max 标尺。拖拽节流 200ms 发 [ctrl,slider,...]。
        /// </summary>
        private FrameworkElement CreateSliderControl(SensorCardViewModel vm, SolidColorBrush accentBrush)
        {
            var panel = new Grid();
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ——— ± 按钮行 ———
            var btnRow = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            double step = vm.SliderStep > 0 ? vm.SliderStep : 1;

            // [-] 按钮
            var minusBtn = new Button
            {
                Content = "−", Width = 24, Height = 24,
                FontSize = 16, FontWeight = FontWeights.Bold,
                Background = Brushes.Transparent,
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1),
                Foreground = accentBrush,
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
            };
            minusBtn.Click += (s, e) =>
            {
                if (_sensorVM != null && _sensorVM.IsEditMode) return;
                if (!double.TryParse(vm.Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double cur)) cur = vm.SliderMin;
                double newVal = Math.Max(vm.SliderMin, cur - step);
                SendSliderValueImmediate(vm, newVal);
            };
            Grid.SetColumn(minusBtn, 0);

            // 当前值（主显示）
            var valueText = new TextBlock
            {
                Text = GetDisplayValue(vm),
                FontSize = 22, FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            _cardValueMap[vm] = valueText;
            Grid.SetColumn(valueText, 1);

            // [+] 按钮
            var plusBtn = new Button
            {
                Content = "+", Width = 24, Height = 24,
                FontSize = 16, FontWeight = FontWeights.Bold,
                Background = Brushes.Transparent,
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1),
                Foreground = accentBrush,
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
            };
            plusBtn.Click += (s, e) =>
            {
                if (_sensorVM != null && _sensorVM.IsEditMode) return;
                if (!double.TryParse(vm.Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double cur)) cur = vm.SliderMin;
                double newVal = Math.Min(vm.SliderMax, cur + step);
                SendSliderValueImmediate(vm, newVal);
            };
            Grid.SetColumn(plusBtn, 2);

            btnRow.Children.Add(minusBtn);
            btnRow.Children.Add(valueText);
            btnRow.Children.Add(plusBtn);

            // ——— Slider ———
            double curValue = vm.SliderMin;
            if (double.TryParse(vm.Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double parsed))
                curValue = parsed;

            var slider = new Slider
            {
                Minimum = vm.SliderMin,
                Maximum = vm.SliderMax,
                Value = Math.Clamp(curValue, vm.SliderMin, vm.SliderMax),
                SmallChange = step,
                LargeChange = step * 10,
                Margin = new Thickness(0, 2, 0, 2),
                Foreground = accentBrush,
                IsSnapToTickEnabled = false,
            };
            _cardSliderMap[vm] = slider;

            // 拖拽开始/结束标记——松手立即发，拖中节流 200ms
            bool isDragging = false;
            slider.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (_sensorVM != null && _sensorVM.IsEditMode) return; // 编辑模式禁用
                isDragging = true;
            };
            slider.PreviewMouseLeftButtonUp += (s, e) =>
            {
                if (_sensorVM != null && _sensorVM.IsEditMode) return;
                isDragging = false;
                SendSliderValueImmediate(vm, slider.Value);
            };
            slider.PreviewMouseMove += (s, e) =>
            {
                if (!isDragging) return;
                if (e.LeftButton != MouseButtonState.Pressed) { isDragging = false; return; }
            };

            slider.ValueChanged += (s, e) =>
            {
                if (_sliderProgrammaticUpdate) return;
                if (_sensorVM != null && _sensorVM.IsEditMode) return;
                var newVal = e.NewValue;
                valueText.Text = $"{newVal:0.##}{vm.GetUnit()}";
                if (isDragging)
                    ThrottledSliderSend(vm, newVal);
                else
                    SendSliderValueImmediate(vm, newVal);
            };

            // ——— Min/Max 标尺 ———
            var labelRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            var minLabel = new TextBlock
            {
                Text = $"{vm.SliderMin:F0}",
                FontSize = 10,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            _cardSliderMinLabelMap[vm] = minLabel;
            var maxLabel = new TextBlock
            {
                Text = $"{vm.SliderMax:F0}",
                FontSize = 10,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            _cardSliderMaxLabelMap[vm] = maxLabel;
            labelRow.Children.Add(minLabel);
            labelRow.Children.Add(maxLabel);

            // Grid 三行布局
            Grid.SetRow(btnRow, 0);
            Grid.SetRow(slider, 1);
            Grid.SetRow(labelRow, 2);
            slider.VerticalAlignment = VerticalAlignment.Center;
            panel.Children.Add(btnRow);
            panel.Children.Add(slider);
            panel.Children.Add(labelRow);

            return panel;
        }

        private void ThrottledSliderSend(SensorCardViewModel vm, double value)
        {
            var now = DateTime.Now;
            if (_sliderLastSend.TryGetValue(vm, out var last)
                && (now - last).TotalMilliseconds < 200)
                return;
            _sliderLastSend[vm] = now;
            SendSliderValueImmediate(vm, value);
        }

        private void SendSliderValueImmediate(SensorCardViewModel vm, double value)
        {
            double clamped = Math.Round(Math.Clamp(value, vm.SliderMin, vm.SliderMax), 2);
            _sliderLastSend[vm] = DateTime.Now;
            vm.Value = $"{clamped:G}";
            UpdateCardUI(vm);
            string cmd = $"[ctrl,slider,{vm.Name},{clamped:G}]\r\n";
            SendRaw(cmd);
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
                strip.Background = accentBrush;

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

            // 滑杆卡：Slider 位置 + min/max 标签
            if (_cardSliderMap.TryGetValue(vm, out var slider))
            {
                if (double.TryParse(vm.Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double sv))
                {
                    _sliderProgrammaticUpdate = true;
                    slider.Value = Math.Clamp(sv, slider.Minimum, slider.Maximum);
                    _sliderProgrammaticUpdate = false;
                }
            }
            if (_cardSliderMinLabelMap.TryGetValue(vm, out var minLb))
                minLb.Text = $"{vm.SliderMin:F0}";
            if (_cardSliderMaxLabelMap.TryGetValue(vm, out var maxLb))
                maxLb.Text = $"{vm.SliderMax:F0}";

            // 正常模式侧栏 dirty flag
            _sidePanelDirty = true;
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
            _cardEditWrapperMap.Clear();
            _cardDeleteBtnMap.Clear();

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

                bool isEdit = _sensorVM.IsEditMode;

                if (!string.IsNullOrEmpty(group.Name) || isEdit)
                {
                    if (isEdit)
                    {
                        // 编辑模式：组名可点击 + [✏] 改名按钮
                        var nameHeader = new Grid { Margin = new Thickness(4, 0, 0, 4) };
                        nameHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        nameHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        var nameTb = new TextBlock
                        {
                            Text = string.IsNullOrEmpty(group.Name) ? "(未命名)" : group.Name,
                            FontSize = 11,
                            Foreground = (Brush)FindResource("TextMutedBrush"),
                            VerticalAlignment = VerticalAlignment.Center,
                            Cursor = System.Windows.Input.Cursors.Hand,
                        };
                        Grid.SetColumn(nameTb, 0);

                        var editBtn = new Button
                        {
                            Content = "✏",
                            Background = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            Foreground = (Brush)FindResource("TextMutedBrush"),
                            FontSize = 10,
                            Padding = new Thickness(4, 0, 0, 0),
                            Cursor = System.Windows.Input.Cursors.Hand,
                        };
                        Grid.SetColumn(editBtn, 1);

                        var captureGroup = group;
                        var captureTb = nameTb;
                        System.Windows.RoutedEventHandler startRename = (s, e) =>
                        {
                            // 组名变 TextBox
                            var parent2 = nameTb.Parent as Panel;
                            if (parent2 == null) return;
                            int idx2 = parent2.Children.IndexOf(nameHeader);
                            if (idx2 < 0) return;

                            var textBox2 = new TextBox
                            {
                                Text = captureGroup.Name ?? "",
                                FontSize = 11,
                                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                                Background = (Brush)FindResource("CardBgBrush"),
                                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                                BorderThickness = new Thickness(1),
                                Padding = new Thickness(4, 1, 4, 1),
                                MinWidth = 80,
                            };
                            textBox2.SelectAll();
                            parent2.Children.RemoveAt(idx2);
                            parent2.Children.Insert(idx2, textBox2);
                            textBox2.Focus();

                            textBox2.LostFocus += (s2, e2) =>
                            {
                                if (!parent2.Children.Contains(textBox2)) return;
                                captureGroup.Name = textBox2.Text.Trim();
                                parent2.Children.Remove(textBox2);
                                captureTb.Text = string.IsNullOrEmpty(captureGroup.Name) ? "(未命名)" : captureGroup.Name;
                                parent2.Children.Insert(idx2, nameHeader);
                                RefreshAllRows();
                                RefreshSensorSidePanel();
                                SaveSensorPrefs();
                            };
                            textBox2.KeyDown += (s2, e2) =>
                            {
                                if (e2.Key == System.Windows.Input.Key.Enter)
                                {
                                    if (!parent2.Children.Contains(textBox2)) return;
                                    captureGroup.Name = textBox2.Text.Trim();
                                    parent2.Children.Remove(textBox2);
                                    captureTb.Text = string.IsNullOrEmpty(captureGroup.Name) ? "(未命名)" : captureGroup.Name;
                                    parent2.Children.Insert(idx2, nameHeader);
                                    RefreshAllRows();
                                    RefreshSensorSidePanel();
                                    SaveSensorPrefs();
                                }
                                else if (e2.Key == System.Windows.Input.Key.Escape)
                                {
                                    if (!parent2.Children.Contains(textBox2)) return;
                                    parent2.Children.Remove(textBox2);
                                    parent2.Children.Insert(idx2, nameHeader);
                                }
                            };
                        };
                        nameTb.MouseLeftButtonDown += (s, e) => { startRename(s, e); e.Handled = true; };
                        editBtn.Click += (s, e) => startRename(s, e);

                        nameHeader.Children.Add(nameTb);
                        nameHeader.Children.Add(editBtn);
                        groupPanel.Children.Add(nameHeader);
                    }
                    else if (!string.IsNullOrEmpty(group.Name))
                    {
                        groupPanel.Children.Add(new TextBlock
                        {
                            Text = group.Name, FontSize = 11,
                            Foreground = (Brush)FindResource("TextMutedBrush"),
                            Margin = new Thickness(4, 0, 0, 4),
                        });
                    }
                }

                if (isEdit)
                {
                    // 编辑模式：WrapPanel 内卡片 + × 删除 + 间隙 [+] 热区
                    var itemsList = group.Items.ToList();
                    var currentWrap = new WrapPanel
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Top,
                    };
                    groupPanel.Children.Add(currentWrap);

                    for (int i = 0; i < itemsList.Count; i++)
                    {
                        if (itemsList[i] is string s && s == "---")
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

                        if (itemsList[i] is SensorCardViewModel cardVM)
                        {
                            var card = CreateSensorCard(cardVM);
                            var wrapper = CreateEditCardWrapper(cardVM, card);
                            currentWrap.Children.Add(wrapper);

                            // 间隙 [+] 热区（卡片后，除非下一个是换行或已到末尾）
                            int nextI = i + 1;
                            bool nextIsBreak = nextI < itemsList.Count
                                && itemsList[nextI] is string ns && ns == "---";
                            if (nextI < itemsList.Count && !nextIsBreak)
                            {
                                int insertIdx = -1;
                                for (int j = nextI; j < itemsList.Count; j++)
                                {
                                    if (itemsList[j] is SensorCardViewModel)
                                    {
                                        insertIdx = group.Items.IndexOf(itemsList[j]);
                                        break;
                                    }
                                }
                                if (insertIdx < 0) insertIdx = group.Items.Count;
                                currentWrap.Children.Add(CreateGapZone(group, insertIdx));
                            }
                        }
                    }
                }
                else
                {
                    // 正常模式：WrapPanel 水平排列
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
                }

                groupBorder.Child = groupPanel;
                sensorRowsPanel.Children.Add(groupBorder);
            }

            if (_sensorRefreshTimer != null && !_sensorRefreshTimer.IsEnabled
                && _sensorVM.IsActive && !_sensorVM.IsEditMode)
                _sensorRefreshTimer.Start();

            // 正常模式侧栏 dirty flag（新卡建卡或全量重建）
            _sidePanelDirty = true;

            // 详情面板指向的卡被删了→清空引用
            if (_detailCard != null && _sensorVM.FindByName(_detailCard.Name) == null)
                _detailCard = null;
        }

        // ═══ 持久化 ═══

        private void SaveSensorPrefs()
        {
            if (_prefsData == null || _sensorVM == null) return;
            _prefsData["sensors"] = _sensorVM.Serialize();
            _prefs.Save(_prefsData);
        }

        // ═══ 编辑按钮 ═══

        private void BtnSensorEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_sensorVM == null) return;

            _sensorVM.IsEditMode = !_sensorVM.IsEditMode;
            _selectedCard = null;
            _detailCard = null;

            if (_sensorVM.IsEditMode)
            {
                btnSensorEdit.Content = "完成";
                // 若当前在其他标签页（如设置），先切回传感面板——让 RefreshContentVisibility
                // 统一管理侧栏显隐，避免设置侧栏和卡片管理侧栏叠在一起
                if (_currentTab != "Sensors")
                    tabSensors.IsChecked = true;
                rightSensors.Visibility = Visibility.Visible;
                _sensorRefreshTimer?.Stop();
            }
            else
            {
                btnSensorEdit.Content = "编辑";
                rightSensors.Visibility = Visibility.Visible;
                if (_sensorRefreshTimer != null && !_sensorRefreshTimer.IsEnabled && _sensorVM.IsActive)
                    _sensorRefreshTimer.Start();
            }

            RefreshAllRows();
            RefreshSensorSidePanel();
            if (_sensorVM.IsEditMode)
                RefreshMiniPlots(); // 编辑模式：波形冻结前补一帧快照
            SaveSensorPrefs();
        }

        // ═══ 编辑模式——卡片选中 ═══

        private void SelectCard(SensorCardViewModel vm)
        {
            // 编辑模式→先确保切回传感标签页（即使点的是已选中卡片也切）
            if (vm != null && _sensorVM.IsEditMode && _currentTab != "Sensors")
                tabSensors.IsChecked = true;

            if (_selectedCard == vm)
            {
                // 同卡再点：只做侧栏恢复 + 定位
                if (vm != null && _sensorVM.IsEditMode && _currentTab == "Sensors")
                {
                    var container = rightSensors?.Content as StackPanel;
                    if (container == null || container.Children.Count == 0)
                        RefreshSensorSidePanel();
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                        new Action(() => ScrollSidebarToCard(vm)));
                }
                return;
            }

            // 取消旧选中高亮
            if (_selectedCard != null && _cardBorderMap.TryGetValue(_selectedCard, out var oldBorder))
            {
                oldBorder.BorderBrush = (Brush)FindResource("CardBorderBrush");
                oldBorder.BorderThickness = new Thickness(0.5);
            }

            _selectedCard = vm;

            // 新选中高亮——暗色模式用半透明蓝，不炸眼
            var selectBrush = isDarkTheme
                ? ParseColor("#42A5F5", 0.45)
                : (Brush)FindResource("PrimaryBrush");
            if (vm != null && _cardBorderMap.TryGetValue(vm, out var newBorder))
            {
                newBorder.BorderBrush = selectBrush;
                newBorder.BorderThickness = new Thickness(2);
            }

            // 编辑模式→侧栏恢复 + 滚动定位
            if (vm != null && _sensorVM.IsEditMode)
            {
                if (_currentTab == "Sensors")
                {
                    var container = rightSensors?.Content as StackPanel;
                    if (container == null || container.Children.Count == 0)
                        RefreshSensorSidePanel();
                }
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => ScrollSidebarToCard(vm)));
            }
        }

        /// <summary>清除卡片选中高亮（切页时调用）</summary>
        private void DeselectCard()
        {
            if (_selectedCard != null && _cardBorderMap.TryGetValue(_selectedCard, out var oldBorder))
            {
                oldBorder.BorderBrush = (Brush)FindResource("CardBorderBrush");
                oldBorder.BorderThickness = new Thickness(0.5);
            }
            _selectedCard = null;
        }

        private void ScrollSidebarToCard(SensorCardViewModel vm)
        {
            if (rightSensors == null || rightSensors.Content == null) return;
            // 递归查找 Tag==vm 的 FrameworkElement
            var root = rightSensors.Content as DependencyObject;
            if (root == null) return;
            var target = FindChildByTag(root, vm);
            if (target != null)
                target.BringIntoView();
        }

        private static FrameworkElement FindChildByTag(DependencyObject parent, object tag)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Tag == tag)
                    return fe;
                var found = FindChildByTag(child, tag);
                if (found != null) return found;
            }
            return null;
        }

        // ═══ 正常模式侧栏——卡片概览行 ═══

        /// <summary>
        /// 正常模式侧栏行：竖条色圆点 + 卡片名 + 当前值右对齐，高 28px。
        /// 点一下 → 主区滚动定位到该卡 + 脉冲高亮。
        /// </summary>
        private FrameworkElement CreateNormalSidebarRow(SensorCardViewModel card)
        {
            var row = new Grid { Height = 28, Margin = new Thickness(0, 1, 0, 1), Tag = card };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 竖条色圆点 10×10
            var dot = new Ellipse
            {
                Width = 10, Height = 10,
                Fill = ParseColor(card.GetAccentHex(isDarkTheme)),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(dot, 0);

            // 卡片名
            var nameBlock = new TextBlock
            {
                Text = card.Name,
                FontSize = 12,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(nameBlock, 1);

            // 当前值（右对齐）
            string displayValue = card.Type switch
            {
                "status" => card.Status ?? "--",
                "control" => card.Status == "on" ? "ON" : "OFF",
                _ => (card.Value ?? "--") + card.GetUnit(),
            };
            var valueBlock = new TextBlock
            {
                Text = displayValue,
                FontSize = 12,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(12, 0, 0, 0),
            };
            Grid.SetColumn(valueBlock, 2);

            row.Children.Add(dot);
            row.Children.Add(nameBlock);
            row.Children.Add(valueBlock);

            // 点击 → 主区滚动定位 + 脉冲高亮
            var captureCard = card;
            row.Cursor = Cursors.Hand;
            row.MouseLeftButtonDown += (s, e) =>
            {
                if (_cardBorderMap.TryGetValue(captureCard, out var border))
                {
                    border.BringIntoView();
                    PulseCardHighlight(captureCard);
                }
                e.Handled = true;
            };

            return row;
        }

        /// <summary>
        /// 卡片脉冲高亮：边框闪蓝 600ms 后恢复原样。
        /// 用于正常模式侧栏点击跳转后的视觉反馈。
        /// </summary>
        private void PulseCardHighlight(SensorCardViewModel card)
        {
            if (!_cardBorderMap.TryGetValue(card, out var border)) return;

            var pulseBrush = isDarkTheme
                ? ParseColor("#42A5F5", 0.6)
                : (Brush)FindResource("PrimaryBrush");
            var originalBrush = border.BorderBrush;
            var originalThickness = border.BorderThickness;

            border.BorderBrush = pulseBrush;
            border.BorderThickness = new Thickness(2);

            var timer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(600),
                DispatcherPriority.Background,
                (s, e) =>
                {
                    border.BorderBrush = originalBrush;
                    border.BorderThickness = originalThickness;
                    ((DispatcherTimer)s).Stop();
                },
                Dispatcher);
            timer.Start();
        }

        // ═══ 编辑模式——卡片外覆（× 删除按钮） ═══

        private Border CreateEditCardWrapper(SensorCardViewModel vm, Border card)
        {
            // 去掉卡片自带 Margin——由 wrapper 管理间距
            card.Margin = new Thickness(0);

            // × 删除按钮——卡片右上角
            var deleteBtn = new Button
            {
                Content = "×",
                Width = 20, Height = 20,
                FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 2, 0),
            };
            // 悬停变红
            deleteBtn.MouseEnter += (s, e) =>
                deleteBtn.Foreground = ParseColor(isDarkTheme ? "#EF5350" : "#F44336");
            deleteBtn.MouseLeave += (s, e) =>
                deleteBtn.Foreground = (Brush)FindResource("TextMutedBrush");
            var captureVm = vm;
            deleteBtn.Click += (s, e) =>
            {
                _sensorVM.RemoveCard(captureVm);
                if (_selectedCard == captureVm) _selectedCard = null;
                RefreshAllRows();
                RefreshSensorSidePanel();
                SaveSensorPrefs();
                e.Handled = true;
            };

            // 卡片本体可点击选中
            var cardBg = (Brush)FindResource("CardBgBrush");
            card.MouseLeftButtonDown += (s, e) =>
            {
                SelectCard(captureVm);
                e.Handled = true;
            };
            card.Cursor = System.Windows.Input.Cursors.Hand;

            // 选中态边框
            if (_selectedCard == vm)
            {
                var sb = isDarkTheme ? ParseColor("#42A5F5", 0.45) : (Brush)FindResource("PrimaryBrush");
                card.BorderBrush = sb;
                card.BorderThickness = new Thickness(2);
            }

            // 外覆 Grid：卡片垫底 + × 按钮叠在右上角
            var wrapper = new Grid();
            wrapper.Children.Add(card);
            wrapper.Children.Add(deleteBtn);

            var outer = new Border
            {
                Child = wrapper,
                Margin = new Thickness(0, 0, 0, 8),
            };
            _cardEditWrapperMap[vm] = outer;
            _cardDeleteBtnMap[vm] = deleteBtn;

            return outer;
        }

        // ═══ 编辑模式——间隙 [+] 热区 ═══

        private FrameworkElement CreateGapZone(SensorGroup group, int insertIndex)
        {
            var grid = new Grid
            {
                Width = 14, Height = 160,
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = System.Windows.Input.Cursors.Hand,
                ClipToBounds = false,
            };
            Panel.SetZIndex(grid, 1); // 溢出内容盖在相邻卡片之上

            // 竖条指示线——加粗，暗色模式也不消失
            var line = new Border
            {
                Width = 3, Height = 160,
                Background = (Brush)FindResource("TextMutedBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Opacity = 0.60,
            };
            grid.Children.Add(line);

            // 悬停展开的虚线框 + [+]——ScaleTransform 缩放，不挤卡片
            var expandGrid = new Grid
            {
                Width = 16, Height = 160,
                HorizontalAlignment = HorizontalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0, 1),
            };

            // 虚线框（Rectangle 支持 StrokeDashArray）
            var dashRect = new Rectangle
            {
                Width = 16, Height = 160,
                Stroke = (Brush)FindResource("PrimaryBrush"),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                RadiusX = 4, RadiusY = 4,
                Fill = (Brush)FindResource("SecondaryHoverBgBrush"),
            };
            expandGrid.Children.Add(dashRect);

            // + 号
            expandGrid.Children.Add(new TextBlock
            {
                Text = "+",
                FontSize = 16, FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("PrimaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });

            grid.Children.Add(expandGrid);

            var captureGroup = group;
            var captureIdx = insertIndex;
            var scale = (ScaleTransform)expandGrid.RenderTransform;

            grid.MouseEnter += (s, e) => { scale.ScaleX = 1; };
            grid.MouseLeave += (s, e) => { scale.ScaleX = 0; };
            grid.MouseLeftButtonDown += (s, e) =>
            {
                ShowAddCardPopup(grid, captureGroup, captureIdx);
                e.Handled = true;
            };

            return grid;
        }

        // ═══ 添加卡片 Popup ═══

        private void ShowAddCardPopup(UIElement placementTarget, SensorGroup group, int insertIndex,
            PlacementMode placement = PlacementMode.Right)
        {
            var popup = new Popup
            {
                PlacementTarget = placementTarget,
                Placement = placement,
                StaysOpen = true,  // 手动控制关闭，避免点一下消失
                AllowsTransparency = true,
            };

            var typeOptions = new[]
            {
                ("温度 (temp)", "temp"),
                ("湿度 (humidity)", "humidity"),
                ("气压 (pressure)", "pressure"),
                ("状态 (status)", "status"),
                ("开关 (control)", "control"),
                ("电机 (motor)", "motor"),
                ("滑杆 (slider)", "slider"),
                ("通用 (generic)", "generic"),
            };

            var typeCombo = new ComboBox
            {
                FontSize = 12, Height = 28,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                Margin = new Thickness(0, 0, 0, 8),
            };
            foreach (var (label, _) in typeOptions)
                typeCombo.Items.Add(label);
            typeCombo.SelectedIndex = 0;

            var nameBox = new TextBox
            {
                FontSize = 12, Height = 28,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 0, 8),
            };

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var addBtn = new Button
            {
                Content = "添加",
                Style = (Style)FindResource("PrimaryButtonStyle"),
                Height = 28, MinWidth = 0, Padding = new Thickness(14, 0, 14, 0),
                Margin = new Thickness(0, 0, 6, 0),
            };
            var cancelBtn = new Button
            {
                Content = "取消",
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Height = 28, MinWidth = 0, Padding = new Thickness(10, 0, 10, 0),
            };
            btnRow.Children.Add(addBtn);
            btnRow.Children.Add(cancelBtn);

            var stack = new StackPanel { Margin = new Thickness(12) };
            stack.Children.Add(new TextBlock
            {
                Text = "模板", FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 0, 0, 2),
            });
            stack.Children.Add(typeCombo);
            stack.Children.Add(new TextBlock
            {
                Text = "名字", FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 0, 0, 2),
            });
            stack.Children.Add(nameBox);
            stack.Children.Add(btnRow);

            var popupBorder = new Border
            {
                Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = stack,
                MinWidth = 220,
            };
            popup.Child = popupBorder;

            var captureGroup = group;
            var captureIdx = insertIndex;
            var capturePopup = popup;

            addBtn.Click += (s, e) =>
            {
                int sel = typeCombo.SelectedIndex;
                if (sel < 0 || sel >= typeOptions.Length) { capturePopup.IsOpen = false; return; }
                string typeKey = typeOptions[sel].Item2;
                string name = nameBox.Text.Trim();
                if (string.IsNullOrEmpty(name)) { capturePopup.IsOpen = false; return; }

                _sensorVM.DeletedNames.Remove(name); // 手动加卡→允许同名数据重新流入
                var card = new SensorCardViewModel { Type = typeKey, Name = name, SubType = typeKey };
                int idx = Math.Min(captureIdx, captureGroup.Items.Count);
                captureGroup.Items.Insert(idx, card);

                RefreshAllRows();
                RefreshSensorSidePanel();
                SaveSensorPrefs();
                capturePopup.IsOpen = false;
            };
            cancelBtn.Click += (s, e) => { capturePopup.IsOpen = false; };

            // 点 Popup 外部自动关闭
            MouseButtonEventHandler outsideClickHandler = null;
            outsideClickHandler = (s, e) =>
            {
                if (!capturePopup.IsOpen) return;
                var clicked = e.OriginalSource as DependencyObject;
                // 判断点击是否在 Popup 内部
                bool inside = false;
                var current = clicked;
                while (current != null)
                {
                    if (current == capturePopup.Child) { inside = true; break; }
                    current = VisualTreeHelper.GetParent(current);
                }
                if (!inside)
                {
                    capturePopup.IsOpen = false;
                    Application.Current.MainWindow.PreviewMouseLeftButtonDown -= outsideClickHandler;
                }
            };
            popup.Opened += (s, e) =>
            {
                // 延迟订阅，避免打开 Popup 的这次点击把自己关了
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() =>
                    {
                        Application.Current.MainWindow.PreviewMouseLeftButtonDown += outsideClickHandler;
                    }));
            };
            popup.Closed += (s, e) =>
            {
                Application.Current.MainWindow.PreviewMouseLeftButtonDown -= outsideClickHandler;
            };

            popup.IsOpen = true;
        }

        // ═══ 侧栏刷新（正常模式概览 / 编辑模式行管理器） ═══

        private void RefreshSensorSidePanel()
        {
            if (_sensorVM == null || rightSensors == null) return;

            var container = rightSensors.Content as StackPanel;
            if (container == null)
            {
                container = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 2, 0) };
                rightSensors.Content = container;
            }
            container.Children.Clear();

            _sidePanelDirty = false;
            bool isEdit = _sensorVM.IsEditMode;

            try
            {
                if (isEdit)
                {
                    // ═══ 编辑模式：行管理器 / 卡片详情 ═══
                    tbSidePanelTitle.Text = "卡片管理";

                    container.Children.Add(new TextBlock
                    {
                        Text = _detailCard != null ? "卡片详情" : "卡片管理",
                        Style = (Style)FindResource("CardHeaderStyle"),
                        Margin = new Thickness(0, 0, 0, 10),
                    });

                    if (_detailCard != null)
                    {
                        BuildCardDetailPanel(container, _detailCard);
                        // 跳过行管理器 + 底部按钮
                    }
                    else
                    {

                    for (int gi = 0; gi < _sensorVM.Groups.Count; gi++)
                    {
                        var group = _sensorVM.Groups[gi];
                        BuildGroupSection(container, group, gi);

                        if (gi < _sensorVM.Groups.Count - 1)
                        {
                            container.Children.Add(new Border
                            {
                                Height = 1,
                                Background = (Brush)FindResource("SeparatorBrush"),
                                Margin = new Thickness(0, 6, 0, 6),
                            });
                        }
                    }

                    var addGroupBtn = new Button
                    {
                        Content = "+ 添加组",
                        Style = (Style)FindResource("SecondaryButtonStyle"),
                        Height = 28, MinWidth = 0, Padding = new Thickness(10, 0, 10, 0),
                        Margin = new Thickness(0, 10, 0, 4),
                        HorizontalAlignment = HorizontalAlignment.Center,
                    };
                    addGroupBtn.Click += (s, e) =>
                    {
                        _sensorVM.AddGroup();
                        RefreshAllRows();
                        RefreshSensorSidePanel();
                    };
                    container.Children.Add(addGroupBtn);

                    var doneBtn = new Button
                    {
                        Content = "完成编辑",
                        Style = (Style)FindResource("PrimaryButtonStyle"),
                        Height = 28, MinWidth = 0, Padding = new Thickness(14, 0, 14, 0),
                        Margin = new Thickness(0, 4, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Center,
                    };
                    doneBtn.Click += (s, e) => BtnSensorEdit_Click(s, e);
                    container.Children.Add(doneBtn);
                    } // _detailCard == null → 行管理器
                }
                else
                {
                    // ═══ 正常模式：卡片概览 + 快速跳转 ═══
                    tbSidePanelTitle.Text = "传感面板";

                    bool anyVisible = false;
                    foreach (var group in _sensorVM.Groups)
                    {
                        // 组名标题
                        if (!string.IsNullOrEmpty(group.Name))
                        {
                            container.Children.Add(new TextBlock
                            {
                                Text = group.Name,
                                FontSize = 11,
                                Foreground = (Brush)FindResource("TextMutedBrush"),
                                Margin = new Thickness(0, anyVisible ? 8 : 0, 0, 3),
                                TextTrimming = TextTrimming.CharacterEllipsis,
                            });
                        }

                        foreach (var item in group.Items)
                        {
                            if (item is string) continue; // 跳过换行标记
                            if (item is SensorCardViewModel card)
                            {
                                container.Children.Add(CreateNormalSidebarRow(card));
                                anyVisible = true;
                            }
                        }
                    }

                    if (!anyVisible)
                    {
                        container.Children.Add(new TextBlock
                        {
                            Text = "暂无卡片",
                            FontSize = 12,
                            Foreground = (Brush)FindResource("TextMutedBrush"),
                            Margin = new Thickness(0, 20, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Center,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LogSystem($"传感侧栏刷新失败: {ex.Message}");
            }
        }

        private void BuildGroupSection(StackPanel container, SensorGroup group, int groupIndex)
        {
            // 组头折叠箭头 + 组名 + [✏] [×]
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var collapseBtn = new Button
            {
                Content = "▸",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                FontSize = 11, Padding = new Thickness(2, 0, 4, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            Grid.SetColumn(collapseBtn, 0);

            var nameBlock = new TextBlock
            {
                Text = string.IsNullOrEmpty(group.Name) ? "(未命名)" : group.Name,
                FontSize = 14, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(nameBlock, 1);

            // [✏] 改组名
            var renameBtn = CreateSmallIconBtn("✏", () =>
            {
                StartGroupRename(group, nameBlock, container);
            });
            Grid.SetColumn(renameBtn, 2);

            // [×] 删整组——内联二次确认
            var delGroupBtn = new Button
            {
                Content = "×",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = (Brush)FindResource("TextMutedBrush"),
                FontSize = 14, FontWeight = FontWeights.Bold,
                Padding = new Thickness(3, 0, 3, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = false, // false = 待确认
            };
            var captureGroup = group;
            System.Windows.Threading.DispatcherTimer resetTimer = null;
            delGroupBtn.Click += (s, e) =>
            {
                bool confirmed = delGroupBtn.Tag is bool b && b;
                if (!confirmed)
                {
                    // 第一次点击 → 确认态
                    delGroupBtn.Content = "确认删除?";
                    delGroupBtn.Foreground = ParseColor("#EF5350");
                    delGroupBtn.FontSize = 11;
                    delGroupBtn.FontWeight = FontWeights.SemiBold;
                    delGroupBtn.Tag = true;
                    resetTimer = new System.Windows.Threading.DispatcherTimer(
                        TimeSpan.FromSeconds(3),
                        System.Windows.Threading.DispatcherPriority.Background,
                        (_, _2) =>
                        {
                            delGroupBtn.Content = "×";
                            delGroupBtn.Foreground = (Brush)FindResource("TextMutedBrush");
                            delGroupBtn.FontSize = 14;
                            delGroupBtn.FontWeight = FontWeights.Bold;
                            delGroupBtn.Tag = false;
                            resetTimer?.Stop();
                        },
                        Dispatcher);
                    resetTimer.Start();
                }
                else
                {
                    // 第二次点击 → 执行删除
                    resetTimer?.Stop();
                    _sensorVM.RemoveGroup(captureGroup);
                    RefreshAllRows();
                    RefreshSensorSidePanel();
                    SaveSensorPrefs();
                }
            };
            Grid.SetColumn(delGroupBtn, 3);

            headerGrid.Children.Add(collapseBtn);
            headerGrid.Children.Add(nameBlock);
            headerGrid.Children.Add(renameBtn);
            headerGrid.Children.Add(delGroupBtn);

            // 组内项目面板
            var itemsPanel = new StackPanel { Margin = new Thickness(14, 0, 0, 4) };

            // 组外框——和主区组框同色底
            var groupSection = new StackPanel();
            groupSection.Children.Add(headerGrid);
            groupSection.Children.Add(itemsPanel);
            var groupFrame = new Border
            {
                Background = (Brush)FindResource("SecondaryHoverBgBrush"),
                BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 4, 6, 6),
                Margin = new Thickness(0, 0, 0, 6),
                Child = groupSection,
            };
            container.Children.Add(groupFrame);

            var itemsList = group.Items.ToList();
            var allCards = itemsList.OfType<SensorCardViewModel>().ToList();

            for (int i = 0; i < itemsList.Count; i++)
            {
                if (itemsList[i] is string s && s == "---")
                {
                    // 换行标记
                    var lbRow = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    lbRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    lbRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var lbLabel = new TextBlock
                    {
                        Text = "── 换行 ──", FontSize = 10,
                        Foreground = (Brush)FindResource("TextSecondaryBrush"),
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    Grid.SetColumn(lbLabel, 0);

                    var delLb = CreateSmallIconBtn("×", () =>
                    {
                        group.Items.Remove("---");
                        RefreshAllRows();
                        RefreshSensorSidePanel();
                        SaveSensorPrefs();
                    });
                    Grid.SetColumn(delLb, 1);

                    lbRow.Children.Add(lbLabel);
                    lbRow.Children.Add(delLb);
                    itemsPanel.Children.Add(lbRow);
                }
                else if (itemsList[i] is SensorCardViewModel card)
                {
                    BuildCardRow(itemsPanel, group, card, allCards);

                    // 两张卡片之间 → [+ 换行]（§7.9「侧栏两个卡片条目之间点 [+ 换行]」）
                    bool nextIsCard = (i + 1 < itemsList.Count)
                        && itemsList[i + 1] is SensorCardViewModel;
                    if (nextIsCard)
                    {
                        int insertAt = group.Items.IndexOf(itemsList[i + 1]);
                        var lbBetween = new TextBlock
                        {
                            Text = "————",
                            FontSize = 8,
                            Foreground = (Brush)FindResource("TextMutedBrush"),
                            Margin = new Thickness(0, -3, 0, -3),
                            Cursor = System.Windows.Input.Cursors.Hand,
                        };
                        var capGroup = group;
                        var capIdx = insertAt;
                        lbBetween.MouseEnter += (s2, e2) =>
                        {
                            lbBetween.Text = "＋ 换行";
                            lbBetween.Foreground = (Brush)FindResource("PrimaryBrush");
                            lbBetween.Margin = new Thickness(0, 0, 0, 0);
                        };
                        lbBetween.MouseLeave += (s2, e2) =>
                        {
                            lbBetween.Text = "————";
                            lbBetween.Foreground = (Brush)FindResource("TextMutedBrush");
                            lbBetween.Margin = new Thickness(0, -3, 0, -3);
                        };
                        lbBetween.MouseLeftButtonDown += (s2, e2) =>
                        {
                            capGroup.Items.Insert(capIdx, "---");
                            RefreshAllRows();
                            RefreshSensorSidePanel();
                            SaveSensorPrefs();
                            e2.Handled = true;
                        };
                        itemsPanel.Children.Add(lbBetween);
                    }
                }
            }

            // [+ 卡片]
            var addCardBtn = new Button
            {
                Content = "+ 卡片",
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Height = 22, MinWidth = 0, Padding = new Thickness(6, 0, 6, 0),
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            var capGroup3 = group;
            addCardBtn.Click += (s, e) =>
            {
                ShowAddCardPopup(addCardBtn, capGroup3, capGroup3.Items.Count, PlacementMode.Left);
            };
            itemsPanel.Children.Add(addCardBtn);

            // 折叠/展开
            collapseBtn.Click += (s, e) =>
            {
                bool collapsed = itemsPanel.Visibility == Visibility.Collapsed;
                itemsPanel.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
                collapseBtn.Content = collapsed ? "▸" : "▹";
            };
        }

        private void BuildCardRow(StackPanel container, SensorGroup group,
            SensorCardViewModel card, List<SensorCardViewModel> allCards)
        {
            int cardIdx = allCards.IndexOf(card);
            int cardCount = allCards.Count;

            var row = new Grid { Margin = new Thickness(0, 1, 0, 1), Tag = card };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });             // 0: dot
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 1: name
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });             // 2: ⚙
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });             // 3: ↑
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });             // 4: ↓
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });             // 5: ×

            // 竖条色圆点
            var dot = new Ellipse
            {
                Width = 10, Height = 10,
                Fill = ParseColor(card.GetAccentHex(isDarkTheme)),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(dot, 0);

            // 卡片名
            var nameBlock = new TextBlock
            {
                Text = card.Name, FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Cursor = System.Windows.Input.Cursors.IBeam,
            };
            var captureCard = card;
            nameBlock.MouseLeftButtonDown += (s, e) =>
            {
                // 侧栏原地改名：TextBlock → TextBox
                SelectCard(captureCard);
                var parent2 = nameBlock.Parent as Panel;
                if (parent2 == null) return;
                int idx2 = parent2.Children.IndexOf(nameBlock);
                if (idx2 < 0) return;

                var textBox2 = new TextBox
                {
                    Text = captureCard.Name,
                    FontSize = 11, FontWeight = nameBlock.FontWeight,
                    Foreground = (Brush)FindResource("TextPrimaryBrush"),
                    Background = (Brush)FindResource("CardBgBrush"),
                    BorderBrush = (Brush)FindResource("PrimaryBrush"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(2, 1, 2, 1),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                textBox2.SelectAll();
                Grid.SetColumn(textBox2, Grid.GetColumn(nameBlock));
                parent2.Children.RemoveAt(idx2);
                parent2.Children.Insert(idx2, textBox2);
                textBox2.Focus();

                var capturedNameBlock = nameBlock;
                textBox2.LostFocus += (s2, e2) =>
                {
                    if (!parent2.Children.Contains(textBox2)) return;
                    string nn = textBox2.Text.Trim();
                    if (!string.IsNullOrEmpty(nn)) { captureCard.Name = nn; capturedNameBlock.Text = nn; }
                    parent2.Children.Remove(textBox2);
                    parent2.Children.Insert(idx2, capturedNameBlock);
                    UpdateCardUI(captureCard);
                    SaveSensorPrefs();
                };
                textBox2.KeyDown += (s2, e2) =>
                {
                    if (e2.Key == System.Windows.Input.Key.Enter)
                    {
                        if (!parent2.Children.Contains(textBox2)) return;
                        string nn = textBox2.Text.Trim();
                        if (!string.IsNullOrEmpty(nn)) { captureCard.Name = nn; capturedNameBlock.Text = nn; }
                        parent2.Children.Remove(textBox2);
                        parent2.Children.Insert(idx2, capturedNameBlock);
                        UpdateCardUI(captureCard);
                        SaveSensorPrefs();
                    }
                    else if (e2.Key == System.Windows.Input.Key.Escape)
                    {
                        if (!parent2.Children.Contains(textBox2)) return;
                        parent2.Children.Remove(textBox2);
                        parent2.Children.Insert(idx2, capturedNameBlock);
                    }
                };
                e.Handled = true;
            };
            Grid.SetColumn(nameBlock, 1);

            // [⚙]——滑杆卡 / 通用卡 → 详情面板
            if (card.Type == "slider" || card.Type == "generic")
            {
                var cfgBtn = CreateSmallIconBtn("⚙", () =>
                {
                    _detailCard = card;
                    RefreshSensorSidePanel();
                });
                Grid.SetColumn(cfgBtn, 2);
                row.Children.Add(cfgBtn);
            }

            // [↑]——非首张
            if (cardIdx > 0)
            {
                var upBtn = CreateSmallIconBtn("↑", () =>
                {
                    MoveCard(group, card, -1);
                    RefreshAllRows();
                    RefreshSensorSidePanel();
                    SaveSensorPrefs();
                });
                Grid.SetColumn(upBtn, 3);
                row.Children.Add(upBtn);
            }

            // [↓]——非末张
            if (cardIdx < cardCount - 1)
            {
                var downBtn = CreateSmallIconBtn("↓", () =>
                {
                    MoveCard(group, card, 1);
                    RefreshAllRows();
                    RefreshSensorSidePanel();
                    SaveSensorPrefs();
                });
                Grid.SetColumn(downBtn, 4);
                row.Children.Add(downBtn);
            }

            // [×]
            var delBtn = CreateSmallIconBtn("×", () =>
            {
                if (_detailCard == card) _detailCard = null;
                _sensorVM.RemoveCard(card);
                if (_selectedCard == card) _selectedCard = null;
                RefreshAllRows();
                RefreshSensorSidePanel();
                SaveSensorPrefs();
            });
            Grid.SetColumn(delBtn, 5);

            row.Children.Add(dot);
            row.Children.Add(nameBlock);
            row.Children.Add(delBtn);
            container.Children.Add(row);
        }

        // ═══ 卡片详情面板（侧栏态3：滑杆卡配置 / 通用卡配置） ═══

        private void BuildCardDetailPanel(StackPanel container, SensorCardViewModel card)
        {
            // ← 返回行管理器
            var backBtn = new Button
            {
                Content = "← 返回",
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Height = 24, MinWidth = 0, Padding = new Thickness(6, 0, 6, 0),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 10),
            };
            backBtn.Click += (s, e) =>
            {
                _detailCard = null;
                RefreshSensorSidePanel();
            };
            container.Children.Add(backBtn);

            // 名称（所有卡通用）
            var nameLabel = new TextBlock
            {
                Text = "名称：", FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 0, 0, 2),
            };
            container.Children.Add(nameLabel);

            var nameBox = new TextBox
            {
                Text = card.Name,
                FontSize = 12, Height = 28,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 0, 8),
            };
            nameBox.TextChanged += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    card.Name = nameBox.Text.Trim();
                    UpdateCardUI(card);
                }
            };
            container.Children.Add(nameBox);

            if (card.Type == "slider")
            {
                // ——— 滑杆卡专属：Min / Max / Step ———
                AddDetailNumberField(container, "最小值：", card.SliderMin, v => {
                    card.SliderMin = v;
                    _sliderProgrammaticUpdate = true;
                    if (_cardSliderMap.TryGetValue(card, out var sl)) sl.Minimum = v;
                    _sliderProgrammaticUpdate = false;
                    UpdateCardUI(card);
                });
                AddDetailNumberField(container, "最大值：", card.SliderMax, v => {
                    card.SliderMax = v;
                    _sliderProgrammaticUpdate = true;
                    if (_cardSliderMap.TryGetValue(card, out var sl)) sl.Maximum = v;
                    _sliderProgrammaticUpdate = false;
                    UpdateCardUI(card);
                });
                AddDetailNumberField(container, "步长：", card.SliderStep, v => {
                    card.SliderStep = v;
                    _sliderProgrammaticUpdate = true;
                    if (_cardSliderMap.TryGetValue(card, out var sl)) { sl.SmallChange = v; sl.LargeChange = v * 10; }
                    _sliderProgrammaticUpdate = false;
                });
            }
            if (card.Type == "generic")
            {
                // ——— 通用卡专属：单位 / 颜色 / 波形 ———
                // 单位
                AddDetailTextField(container, "单位：（留空=无）", card.CustomUnit ?? "", v => {
                    card.CustomUnit = string.IsNullOrWhiteSpace(v) ? null : v.Trim();
                    UpdateCardUI(card);
                    SaveSensorPrefs();
                });

                // 颜色 12 色下拉
                BuildColorPicker(container, card);

                // 波形勾选
                var waveCheck = new CheckBox
                {
                    Content = "显示迷你波形",
                    IsChecked = card.ShowWaveform,
                    FontSize = 11,
                    Foreground = (Brush)FindResource("TextPrimaryBrush"),
                    Margin = new Thickness(0, 4, 0, 0),
                };
                waveCheck.Checked += (s, e) =>
                {
                    card.ShowWaveform = true;
                    RefreshAllRows();
                    SaveSensorPrefs();
                };
                waveCheck.Unchecked += (s, e) =>
                {
                    card.ShowWaveform = false;
                    RefreshAllRows();
                    SaveSensorPrefs();
                };
                container.Children.Add(waveCheck);
            }

            SaveSensorPrefs();
        }

        private void AddDetailNumberField(StackPanel container, string label, double currentValue,
            Action<double> onChanged)
        {
            var lb = new TextBlock
            {
                Text = label, FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 0, 0, 2),
            };
            container.Children.Add(lb);

            var box = new TextBox
            {
                Text = currentValue.ToString("F0"),
                FontSize = 12, Height = 28,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 0, 8),
            };
            box.LostFocus += (s, e) =>
            {
                if (double.TryParse(box.Text, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double v))
                    onChanged(v);
            };
            box.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    if (double.TryParse(box.Text, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double v))
                        onChanged(v);
                    // 焦点转移到下一个控件（如果有）
                    box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    e.Handled = true;
                }
            };
            container.Children.Add(box);
        }

        /// <summary>详情面板文本字段（单位等）</summary>
        private void AddDetailTextField(StackPanel container, string label, string currentValue,
            Action<string> onChanged)
        {
            var lb = new TextBlock
            {
                Text = label, FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 0, 0, 2),
            };
            container.Children.Add(lb);

            var box = new TextBox
            {
                Text = currentValue,
                FontSize = 12, Height = 28,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 0, 8),
            };
            box.LostFocus += (s, e) => onChanged(box.Text);
            box.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { onChanged(box.Text); e.Handled = true; }
            };
            container.Children.Add(box);
        }

        /// <summary>通用卡颜色选择：色块预览 + 40 色 Popup（复用按键页 ShowColorPickerPopup）</summary>
        private void BuildColorPicker(StackPanel container, SensorCardViewModel card)
        {
            var lb = new TextBlock
            {
                Text = "颜色：", FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 0, 0, 2),
            };
            container.Children.Add(lb);

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

            var currentHex = card.ColorHex ?? (isDarkTheme ? "#78909C" : "#607D8B");
            var swatch = new Border
            {
                Width = 28, Height = 28,
                CornerRadius = new CornerRadius(4),
                Background = ParseColor(currentHex),
                BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var pickBtn = new Button
            {
                Content = "选择颜色...",
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Height = 28, MinWidth = 0, Padding = new Thickness(10, 0, 10, 0),
                FontSize = 11,
            };
            pickBtn.Click += (s, e) =>
            {
                ShowColorPickerPopup(pickBtn, hex =>
                {
                    card.ColorHex = hex;
                    swatch.Background = ParseColor(hex);
                    UpdateCardUI(card);
                    SaveSensorPrefs();
                });
            };

            row.Children.Add(swatch);
            row.Children.Add(pickBtn);
            container.Children.Add(row);
        }

        private Button CreateSmallIconBtn(string text, Action onClick)
        {
            var btn = new Button
            {
                Content = text,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                FontSize = 11,
                Padding = new Thickness(2, 0, 2, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private void MoveCard(SensorGroup group, SensorCardViewModel card, int direction)
        {
            // 找到所有卡片在 Items 中的位置
            var cardIndices = group.Items
                .Select((item, idx) => (item, idx))
                .Where(x => x.item is SensorCardViewModel)
                .Select(x => x.idx)
                .ToList();

            int currentPos = cardIndices.IndexOf(group.Items.IndexOf(card));
            int newPos = currentPos + direction;
            if (newPos < 0 || newPos >= cardIndices.Count) return;

            int oldIdx = cardIndices[currentPos];
            int newIdx = cardIndices[newPos];

            // 交换 ObservableCollection 中两个位置
            var item1 = group.Items[oldIdx];
            var item2 = group.Items[newIdx];
            if (oldIdx < newIdx)
            {
                group.Items.RemoveAt(newIdx);
                group.Items.RemoveAt(oldIdx);
                group.Items.Insert(oldIdx, item2);
                group.Items.Insert(newIdx, item1);
            }
            else
            {
                group.Items.RemoveAt(oldIdx);
                group.Items.RemoveAt(newIdx);
                group.Items.Insert(newIdx, item1);
                group.Items.Insert(oldIdx, item2);
            }
        }

        // ═══ 组改名 ═══

        private void StartGroupRename(SensorGroup group, TextBlock nameBlock, StackPanel sidePanel)
        {
            var parent = nameBlock.Parent as Grid;
            if (parent == null) return;

            int nameCol = Grid.GetColumn(nameBlock);
            int nameRow = Grid.GetRow(nameBlock);

            // 用 TextBox 替换 TextBlock
            var textBox = new TextBox
            {
                Text = group.Name ?? "",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 1, 4, 1),
                VerticalAlignment = VerticalAlignment.Center,
            };
            textBox.SelectAll();
            Grid.SetColumn(textBox, nameCol);
            Grid.SetRow(textBox, nameRow);

            parent.Children.Remove(nameBlock);
            parent.Children.Add(textBox);
            textBox.Focus();

            textBox.LostFocus += (s, e) =>
            {
                if (!parent.Children.Contains(textBox)) return;
                group.Name = textBox.Text.Trim();
                parent.Children.Remove(textBox);
                nameBlock.Text = string.IsNullOrEmpty(group.Name) ? "(未命名)" : group.Name;
                parent.Children.Add(nameBlock);
                RefreshAllRows();
                RefreshSensorSidePanel();
                SaveSensorPrefs();
            };
            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    if (!parent.Children.Contains(textBox)) return;
                    group.Name = textBox.Text.Trim();
                    parent.Children.Remove(textBox);
                    nameBlock.Text = string.IsNullOrEmpty(group.Name) ? "(未命名)" : group.Name;
                    parent.Children.Add(nameBlock);
                    RefreshAllRows();
                    RefreshSensorSidePanel();
                    SaveSensorPrefs();
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    if (!parent.Children.Contains(textBox)) return;
                    parent.Children.Remove(textBox);
                    parent.Children.Add(nameBlock);
                }
            };
        }

        // ═══ 卡片改名 F2 ═══

        private void StartRenameCard(SensorCardViewModel vm)
        {
            if (!_cardTitleMap.TryGetValue(vm, out var titleBlock)) return;

            var parent = titleBlock.Parent as Panel;
            if (parent == null) return;

            // 找到标题在父容器中的位置
            int idx = parent.Children.IndexOf(titleBlock);
            if (idx < 0) return;

            // 隐藏 × 按钮，避免和 TextBox 重叠
            Button deleteBtn = null;
            if (_cardDeleteBtnMap.TryGetValue(vm, out deleteBtn))
                deleteBtn.Visibility = Visibility.Collapsed;

            var textBox = new TextBox
            {
                Text = vm.Name,
                FontSize = 15, FontWeight = FontWeights.Bold,
                Foreground = ParseColor(vm.GetAccentHex(isDarkTheme)),
                Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("PrimaryBrush"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2, 1, 2, 1),
                Margin = titleBlock.Margin,
            };
            textBox.SelectAll();

            parent.Children.RemoveAt(idx);
            parent.Children.Insert(idx, textBox);
            textBox.Focus();

            var captureVm = vm;
            textBox.LostFocus += (s, e) =>
            {
                // §7.11: 焦点移出 → 取消，恢复原名
                if (!parent.Children.Contains(textBox)) return;
                parent.Children.Remove(textBox);
                parent.Children.Insert(idx, titleBlock);
                if (deleteBtn != null) deleteBtn.Visibility = Visibility.Visible;
                panelSensors.Focus();
            };
            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    FinishRename(captureVm, textBox, parent, idx, titleBlock);
                    if (deleteBtn != null) deleteBtn.Visibility = Visibility.Visible;
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    // 取消：恢复原 TextBlock
                    parent.Children.Remove(textBox);
                    parent.Children.Insert(idx, titleBlock);
                    if (deleteBtn != null) deleteBtn.Visibility = Visibility.Visible;
                    // 恢复焦点到面板
                    panelSensors.Focus();
                }
            };
        }

        private void FinishRename(SensorCardViewModel vm, TextBox textBox,
            Panel parent, int idx, TextBlock titleBlock)
        {
            if (!parent.Children.Contains(textBox)) return;
            string newName = textBox.Text.Trim();
            if (!string.IsNullOrEmpty(newName) && newName != vm.Name)
            {
                vm.Name = newName;
                titleBlock.Text = newName;
            }
            parent.Children.Remove(textBox);
            parent.Children.Insert(idx, titleBlock);
            panelSensors.Focus();
            SaveSensorPrefs();
        }
    }
}
