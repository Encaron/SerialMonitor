using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace 串口助手
{
    public partial class MainWindow
    {
        // ——— 滑杆面板 ———
        private SliderPanelViewModel _sliderVM;
        private List<SliderViewModel> _selectedSliders = new List<SliderViewModel>();
        private Dictionary<string, DateTime> _sliderLastSent = new Dictionary<string, DateTime>();

        // ——— 初始化 ———
        private void InitSliderPanel()
        {
            if (_sliderVM != null) return;
            _sliderVM = new SliderPanelViewModel();

            // 恢复滑杆
            if (_prefsData != null && _prefsData.TryGetValue("sliders", out var slidersObj)
                && slidersObj is List<object> rawList && rawList.Count > 0)
            {
                var list = new List<Dictionary<string, object>>();
                foreach (var item in rawList)
                    if (item is Dictionary<string, object> d) list.Add(d);
                if (list.Count > 0) _sliderVM.DeserializeSliders(list);
            }

            InitSlidersColorPanel();
            RefreshSlidersUI();
                   }

        // ——— 颜色面板 ———
        private bool _slidersColorPanelInited;
        private void InitSlidersColorPanel()
        {
            if (_slidersColorPanelInited) return; _slidersColorPanelInited = true;
            string[] colors = { "默认", "红色", "绿色", "蓝色", "黄色", "白色", "灰色" };
            foreach (var colorName in colors)
                slidersColorPanel.Children.Add(CreateSlidersColorChip(colorName, c => {
                    if (_selectedSliders.Count == 1) { _selectedSliders[0].Color = c; RefreshSlidersUI(); RefreshSlidersSidePanel(); }
                }));
        }

        private Border CreateSlidersColorChip(string colorName, Action<string> onClick)
        {
            var isDark = isDarkTheme;
            string hex = SliderPanelViewModel.GetColorHex(colorName, isDark);
            Brush fillBrush = hex == null ? (Brush)FindResource("CardBgBrush")
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            var border = new Border {
                Width = 24, Height = 24, CornerRadius = new CornerRadius(4),
                Background = fillBrush, BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 6, 6),
                Cursor = Cursors.Hand, ToolTip = colorName, Tag = colorName,
            };
            border.MouseLeftButtonDown += (s, e) => onClick((string)((Border)s).Tag);
            return border;
        }

        // ——— 编辑模式切换 ———
        private void SwitchSidePanelToSliders() {
            if (_currentTab != "Sliders") { tabSliders.IsChecked = true; }
        }

        private void btnSlidersEdit_Click(object sender, RoutedEventArgs e) {
            InitSliderPanel(); _sliderVM.IsEditMode = true; _selectedSliders.Clear();
            slidersToolbarNormal.Visibility = Visibility.Collapsed; slidersToolbarEdit.Visibility = Visibility.Visible;
            RefreshSlidersUI(); RefreshSlidersSidePanel();
        }
        private void btnSlidersDone_Click(object sender, RoutedEventArgs e) {
            CancelSlidersConfirm();
            _sliderVM.IsEditMode = false; _selectedSliders.Clear();
            slidersToolbarNormal.Visibility = Visibility.Visible; slidersToolbarEdit.Visibility = Visibility.Collapsed;
            RefreshSlidersUI(); RefreshSlidersSidePanel(); SaveSlidersPrefs();        }

        // ——— 添加 / 清空 / 删除 ———
        private void btnSlidersAdd_Click(object sender, RoutedEventArgs e) {
            InitSliderPanel();
            SwitchSidePanelToSliders();
            string name = "Slider" + (_sliderVM.Sliders.Count + 1);
            var s = _sliderVM.AddSlider(name);
            _selectedSliders.Clear(); _selectedSliders.Add(s);
            RefreshSlidersUI(); RefreshSlidersSidePanel();        }
        private void btnSlidersClearAll_Click(object sender, RoutedEventArgs e) {
            if (_sliderVM == null || _sliderVM.Sliders.Count == 0) return;
            if (_slidersConfirmButton == btnSlidersClearAll) {
                _sliderVM.ClearAll(); _selectedSliders.Clear();
                CancelSlidersConfirm(); RefreshSlidersUI(); RefreshSlidersSidePanel();            } else { StartSlidersConfirm(btnSlidersClearAll, "⚠ 确认清空"); }
        }
        private void btnSliderDelete_Click(object sender, RoutedEventArgs e) {
            if (_selectedSliders.Count == 0) return;
            foreach (var s in _selectedSliders.ToList()) _sliderVM.RemoveSlider(s);
            _selectedSliders.Clear(); RefreshSlidersUI(); RefreshSlidersSidePanel();        }

        // ——— 二次确认 ———
        private Button _slidersConfirmButton;
        private string _slidersConfirmOriginalText;
        private DispatcherTimer _slidersConfirmTimer;
        private void StartSlidersConfirm(Button btn, string confirmText) {
            CancelSlidersConfirm();
            _slidersConfirmButton = btn;
            _slidersConfirmOriginalText = btn.Content?.ToString() ?? "";
            btn.Content = confirmText;
            _slidersConfirmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _slidersConfirmTimer.Tick += (s2, e2) => CancelSlidersConfirm();
            _slidersConfirmTimer.Start();
        }
        private void CancelSlidersConfirm() {
            if (_slidersConfirmButton != null) _slidersConfirmButton.Content = _slidersConfirmOriginalText;
            _slidersConfirmTimer?.Stop(); _slidersConfirmTimer = null;
            _slidersConfirmButton = null; _slidersConfirmOriginalText = null;
        }

        // ═══════════════════════════════════════
        //  滑杆 UI 刷新
        // ═══════════════════════════════════════

        private void RefreshSlidersUI()
        {
            if (_sliderVM == null) return;
            slidersPanel.Children.Clear();
            bool hasSliders = _sliderVM.Sliders.Count > 0;
            bool isEdit = _sliderVM.IsEditMode;
            slidersEmptyHintNormal.Visibility = (!hasSliders && !isEdit) ? Visibility.Visible : Visibility.Collapsed;
            slidersEmptyHintEdit.Visibility   = (!hasSliders && isEdit)   ? Visibility.Visible : Visibility.Collapsed;
            if (!hasSliders) return;

            foreach (var svm in _sliderVM.Sliders)
            {
                var card = new Border {
                    Background = (Brush)FindResource("CardBgBrush"),
                    BorderBrush = (Brush)FindResource("CardBorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = isEdit ? new Thickness(12, 6, 12, 6) : new Thickness(12, 8, 12, 12),
                    Margin = new Thickness(0, 0, 0, isEdit ? 4 : 8),
                    Tag = svm,
                };

                // 颜色条
                if (svm.Color != "默认" && !string.IsNullOrEmpty(svm.Color))
                {
                    string hex = SliderPanelViewModel.GetColorHex(svm.Color, isDarkTheme);
                    if (hex != null) {
                        card.BorderThickness = new Thickness(4, 1, 1, 1);
                        card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                    }
                }

                // 编辑模式选中高亮
                if (isEdit && _selectedSliders.Contains(svm)) {
                    bool hasCustomColor = svm.Color != "默认" && !string.IsNullOrEmpty(svm.Color);
                    if (!hasCustomColor) {
                        var accent = isDarkTheme ? Color.FromRgb(0x0E, 0x63, 0x9C) : Color.FromRgb(0x00, 0x78, 0xD4);
                        card.BorderBrush = new SolidColorBrush(accent); card.BorderThickness = new Thickness(2);
                    } else {
                        card.Background = new SolidColorBrush(isDarkTheme
                            ? Color.FromRgb(0x2A, 0x2E, 0x3A) : Color.FromRgb(0xE6, 0xF0, 0xFA));
                        card.BorderThickness = new Thickness(4, 1, 1, 1);
                    }
                }

                if (isEdit)
                {
                    // ── 编辑模式：紧凑单行 ──
                    var editRow = new Grid { Height = 28 };
                    editRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    editRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var nameLabel = new TextBlock {
                        Text = "✎ " + svm.Name, FontSize = 12, FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)FindResource("PrimaryBrush"),
                        VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand,
                    };
                    nameLabel.MouseLeftButtonDown += (s2, e2) => {
                        _selectedSliders.Clear(); _selectedSliders.Add(svm);
                        SwitchSidePanelToSliders();
                        RefreshSlidersUI(); RefreshSlidersSidePanel();
                    };
                    Grid.SetColumn(nameLabel, 0); editRow.Children.Add(nameLabel);

                    var valTb = new TextBlock {
                        Text = string.Format("{0} — {1} · {2}", svm.DisplayValue,
                            svm.MinValue + "~" + svm.MaxValue,
                            "步长" + svm.Step),
                        FontSize = 10, Foreground = (Brush)FindResource("TextMutedBrush"),
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    Grid.SetColumn(valTb, 1); editRow.Children.Add(valTb);

                    card.Child = editRow;
                }
                else
                {
                    // ── 正常模式：名字 + 滑杆 + 数值 ──
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    var nameLabel = new TextBlock {
                        Text = svm.Name, FontSize = 12, FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)FindResource("TextPrimaryBrush"),
                        Margin = new Thickness(0, 0, 0, 6),
                    };
                    Grid.SetRow(nameLabel, 0); Grid.SetColumn(nameLabel, 0); Grid.SetColumnSpan(nameLabel, 2);
                    grid.Children.Add(nameLabel);

                    var slider = new Slider {
                        Minimum = svm.MinValue, Maximum = svm.MaxValue, Value = svm.Value,
                        SmallChange = svm.Step, LargeChange = svm.Step * 10,
                        // 不用 IsSnapToTickEnabled——.NET 8 下拖拽会卡在单格内
                        Height = 28, VerticalAlignment = VerticalAlignment.Center,
                        Tag = svm,
                    };
                    // 拖拽过程中持续更新数值显示 + 节流发送
                    slider.ValueChanged += (s, e) => {
                        if (_sliderVM.IsEditMode) return;
                        var sl = s as Slider; var vm = sl?.Tag as SliderViewModel; if (vm == null) return;
                        // 按步长取整
                        double rounded = Math.Round(sl.Value / vm.Step) * vm.Step;
                        rounded = Math.Max(vm.MinValue, Math.Min(vm.MaxValue, rounded));
                        vm.Value = rounded;
                        UpdateSliderValueDisplay(sl, vm);
                        // 节流发送（200ms 默认间隔）
                        var now = DateTime.Now;
                        if (!_sliderLastSent.TryGetValue(vm.Name, out var last)
                            || (now - last).TotalMilliseconds >= vm.SendIntervalMs)
                        {
                            _sliderLastSent[vm.Name] = now;
                            SendSliderValue(vm);
                        }
                    };
                    // 松手时取整 + 确保最终值发送
                    slider.PreviewMouseLeftButtonUp += (s, e) => {
                        if (_sliderVM.IsEditMode) return;
                        var sl = s as Slider; var vm = sl?.Tag as SliderViewModel; if (vm == null) return;
                        double rounded = Math.Round(sl.Value / vm.Step) * vm.Step;
                        rounded = Math.Max(vm.MinValue, Math.Min(vm.MaxValue, rounded));
                        sl.Value = rounded; vm.Value = rounded;
                        UpdateSliderValueDisplay(sl, vm);
                        SendSliderValue(vm);
                    };
                    Grid.SetRow(slider, 1); Grid.SetColumn(slider, 0);
                    grid.Children.Add(slider);

                    var valTb = new TextBlock {
                        Text = svm.DisplayValue, FontSize = 12, FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)FindResource("PrimaryBrush"),
                        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0),
                        MinWidth = 44, TextAlignment = TextAlignment.Right,
                    };
                    Grid.SetRow(valTb, 1); Grid.SetColumn(valTb, 1);
                    grid.Children.Add(valTb);

                    card.Child = grid;
                }

                slidersPanel.Children.Add(card);
            }
        }

        private void SendSliderValue(SliderViewModel svm)
        {
            if (_session == null || !_session.IsOpen) return;
            _sliderLastSent[svm.Name] = DateTime.Now;
            string valStr = svm.Value.ToString("F" + svm.DecimalPlaces);
            SendRaw(string.Format("[slider,{0},{1}]", svm.Name, valStr), appendLineEnding: true);
            ShowSliderFeedback(svm);
        }

        private void UpdateSliderValueDisplay(Slider slider, SliderViewModel svm)
        {
            // 找到同 Grid 中的数值 TextBlock
            var grid = slider.Parent as Grid; if (grid == null) return;
            foreach (var child in grid.Children)
                if (child is TextBlock tb && Grid.GetRow(tb) == 1 && Grid.GetColumn(tb) == 1)
                    { tb.Text = svm.DisplayValue; break; }
        }

        private void ShowSliderFeedback(SliderViewModel svm)
        {
            tbSliderFeedbackName.Text = svm.Name;
            tbSliderFeedbackValue.Text = string.Format("[slider,{0},{1}]", svm.Name, svm.DisplayValue);
        }

        // ═══════════════════════════════════════
        //  侧面板刷新
        // ═══════════════════════════════════════

        private void RefreshSlidersSidePanel()
        {
            if (_sliderVM == null) return;
            bool isEdit = _sliderVM.IsEditMode;
            int count = _selectedSliders.Count;

            rightSlidersFeedback.Visibility  = Visibility.Collapsed;
            rightSlidersNoSelection.Visibility  = Visibility.Collapsed;
            rightSlidersSingleSelect.Visibility = Visibility.Collapsed;

            if (!isEdit) { rightSlidersFeedback.Visibility = Visibility.Visible; return; }
            if (count == 0) { rightSlidersNoSelection.Visibility = Visibility.Visible; return; }

            // 单选
            var svm = _selectedSliders[0];
            rightSlidersSingleSelect.Visibility = Visibility.Visible;
            tbSliderName.Text = svm.Name;
            tbSliderMin.Text = svm.MinValue.ToString();
            tbSliderMax.Text = svm.MaxValue.ToString();
            tbSliderStep.Text = svm.Step.ToString();
            tbSliderInterval.Text = svm.SendIntervalMs.ToString();
        }

        // ——— 单选属性编辑 ———
        private void tbSliderName_LostFocus(object sender, RoutedEventArgs e) {
            if (_selectedSliders.Count == 1) { _selectedSliders[0].Name = tbSliderName.Text; RefreshSlidersUI(); RefreshSlidersSidePanel(); }
        }
        private void tbSliderMin_LostFocus(object sender, RoutedEventArgs e) {
            if (_selectedSliders.Count == 1 && double.TryParse(tbSliderMin.Text, out double v)) {
                _selectedSliders[0].MinValue = v;
                if (_selectedSliders[0].Value < v) _selectedSliders[0].Value = v;
                RefreshSlidersUI();
            } else if (_selectedSliders.Count == 1) tbSliderMin.Text = _selectedSliders[0].MinValue.ToString();
        }
        private void tbSliderMax_LostFocus(object sender, RoutedEventArgs e) {
            if (_selectedSliders.Count == 1 && double.TryParse(tbSliderMax.Text, out double v)) {
                _selectedSliders[0].MaxValue = v;
                if (_selectedSliders[0].Value > v) _selectedSliders[0].Value = v;
                RefreshSlidersUI();
            } else if (_selectedSliders.Count == 1) tbSliderMax.Text = _selectedSliders[0].MaxValue.ToString();
        }
        private void tbSliderStep_LostFocus(object sender, RoutedEventArgs e) {
            if (_selectedSliders.Count == 1 && double.TryParse(tbSliderStep.Text, out double v) && v > 0) {
                _selectedSliders[0].Step = v; RefreshSlidersUI();
            } else if (_selectedSliders.Count == 1) tbSliderStep.Text = _selectedSliders[0].Step.ToString();
        }
        private void tbSliderInterval_LostFocus(object sender, RoutedEventArgs e) {
            if (_selectedSliders.Count == 1 && int.TryParse(tbSliderInterval.Text, out int v) && v >= 20) {
                _selectedSliders[0].SendIntervalMs = v;
            } else if (_selectedSliders.Count == 1) tbSliderInterval.Text = _selectedSliders[0].SendIntervalMs.ToString();
        }

        // ═══════════════════════════════════════
        //  协议处理 & 持久化
        // ═══════════════════════════════════════

        private void HandleSliderMessage(string name, string valStr) {
            InitSliderPanel();
            if (double.TryParse(valStr, out double val)) {
                _sliderVM.SetSliderValue(name, val);
                // ⚠️ 不要调 RefreshSlidersUI()——会销毁当前正在拖拽的 Slider 控件！
                // 改为仅更新对应 Slider 的数值显示
                var svm = _sliderVM.Sliders.FirstOrDefault(s => s.Name == name);
                if (svm == null) return;
                Dispatcher.InvokeAsync(() => {
                    // 找到该滑杆的 Slider 控件并同步显示
                    foreach (Border card in slidersPanel.Children) {
                        if (card.Tag is SliderViewModel tag && tag.Name == name) {
                            if (!_sliderVM.IsEditMode && card.Child is Grid grid) {
                                foreach (var child in grid.Children) {
                                    if (child is Slider slider2 && slider2.Tag is SliderViewModel svm2 && svm2.Name == name)
                                        slider2.Value = svm.Value;
                                    if (child is TextBlock tb2 && Grid.GetRow(tb2) == 1 && Grid.GetColumn(tb2) == 1)
                                        tb2.Text = svm.DisplayValue;
                                }
                            }
                            break;
                        }
                    }
                });
            }
        }

        private void SaveSlidersPrefs() {
            if (_sliderVM == null || _prefsData == null) return;
            var arr = new System.Collections.ArrayList();
            foreach (var d in _sliderVM.SerializeSliders()) arr.Add(d);
            _prefsData["sliders"] = arr;
            _prefs.Save(_prefsData);
        }
    }
}
