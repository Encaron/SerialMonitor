using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace 串口助手
{
    public partial class MainWindow
    {
        // ——— 滑杆面板 ———
        private SliderPanelViewModel _sliderVM;
        private List<SliderViewModel> _selectedSliders = new List<SliderViewModel>();
        private Dictionary<string, DateTime> _sliderLastSent = new Dictionary<string, DateTime>();
        // 缓存模板部件：避免 ValueChanged 每次 FindName+ApplyTemplate 造成拖拽卡顿
        private readonly Dictionary<Slider, (Border trackFill, Ellipse thumbDot, TextBlock valueTb)> _sliderPartsCache = new();
        // 侧栏活跃滑杆：上次用户拖拽的滑杆，初始 null → fallback 第一个
        private string _lastActiveSlider = null;

        // ——— 风格系统 ———

        private class SliderStyleParams
        {
            public string Name;           // Display name
            public double TrackH;         // Track background height
            public double TrackR;         // Track corner radius
            public double ThumbW;         // Thumb width
            public double ThumbH;         // Thumb height
            public double StrokeThick;    // Thumb stroke thickness (0 when using image)
            public bool IsSquare;         // True = rectangle thumb, false = round
            public string TrackImg;       // Relative path to track image (null = code-drawn track)
            public string ThumbImg;       // Relative path to thumb image (null = code-drawn thumb)
        }

        private static readonly SliderStyleParams DefaultStyleParams = new SliderStyleParams
        {
            Name = "default", TrackH = 4, TrackR = 2, ThumbW = 16, ThumbH = 16,
            StrokeThick = 2
        };

        private static readonly SliderStyleParams MinimalStyleParams = new SliderStyleParams
        {
            Name = "minimal", TrackH = 2, TrackR = 1, ThumbW = 12, ThumbH = 12,
            StrokeThick = 1
        };

        private static readonly SliderStyleParams SquareStyleParams = new SliderStyleParams
        {
            Name = "方块", TrackH = 4, TrackR = 2, ThumbW = 14, ThumbH = 14,
            StrokeThick = 0, IsSquare = true
        };

        // ——— 初始化 ———
        private void InitSliderPanel(bool refreshUI = true)
        {
            if (_sliderVM == null)
            {
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
            }

            if (refreshUI)
            {
                InitSlidersColorPanel();
                RefreshSlidersUI();
            }
        }

        // ——— 颜色面板 ———
        private bool _slidersColorPanelInited;
        private void InitSlidersColorPanel()
        {
            if (_slidersColorPanelInited) return; _slidersColorPanelInited = true;
            string[] colors = LogicValueMaps.ColorKeys;
            foreach (var colorName in colors)
                slidersColorPanel.Children.Add(CreateSlidersColorChip(colorName, c => {
                    if (_selectedSliders.Count == 1) { _selectedSliders[0].Color = c; RefreshSlidersUI(); RefreshSlidersSidePanel(); }
                    UpdateSlidersColorChipSelection(c);
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
                Cursor = Cursors.Hand, ToolTip = LogicValueMaps.DisplayColor(colorName), Tag = colorName,
            };
            border.MouseLeftButtonDown += (s, e) => onClick((string)((Border)s).Tag);
            return border;
        }

        /// <summary>更新滑杆色块选中态</summary>
        private void UpdateSlidersColorChipSelection(string currentColor)
        {
            foreach (Border chip in slidersColorPanel.Children)
            {
                bool isSelected = chip.Tag != null && (string)chip.Tag == currentColor;
                chip.BorderBrush = isSelected ? (Brush)FindResource("PrimaryBrush")
                                              : (Brush)FindResource("CardBorderBrush");
                chip.BorderThickness = new Thickness(isSelected ? 2 : 1);
            }
        }

        // ——— "自定义颜色" 按钮 ———
        private void btnSlidersCustomColor_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSliders.Count != 1) return;
            var slider = _selectedSliders[0];
            ShowColorPickerPopup(btnSlidersCustomColor, hex => {
                slider.Color = hex;
                RefreshSlidersUI();
                RefreshSlidersSidePanel();
                UpdateSlidersColorChipSelection(hex);
            });
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

        // ——— 快速预设按钮 ———
        private void btnSlidersPresetZero_Click(object sender, RoutedEventArgs e) {
            SetAllSlidersTo(vm => vm.MinValue);
        }
        private void btnSlidersPresetMid_Click(object sender, RoutedEventArgs e) {
            SetAllSlidersTo(vm => (vm.MinValue + vm.MaxValue) / 2.0);
        }
        private void btnSlidersPresetMax_Click(object sender, RoutedEventArgs e) {
            SetAllSlidersTo(vm => vm.MaxValue);
        }
        /// <summary>
        /// 全部滑杆设为指定值，更新 UI 控件并发送（串口打开时）。
        /// </summary>
        private void SetAllSlidersTo(Func<SliderViewModel, double> getValue)
        {
            if (_sliderVM == null || _sliderVM.Sliders.Count == 0) return;
            if (_sliderVM.IsEditMode) return;

            foreach (var vm in _sliderVM.Sliders)
            {
                double val = getValue(vm);
                val = Math.Round(val / vm.Step) * vm.Step;
                val = Math.Max(vm.MinValue, Math.Min(vm.MaxValue, val));
                vm.Value = val;
                // 同步 UI 控件（不改结构，只改 Slider.Value）
                foreach (Border card in slidersPanel.Children)
                {
                    if (card.Tag is SliderViewModel tag && tag.Name == vm.Name && card.Child is Grid grid)
                    {
                        foreach (var child in grid.Children)
                        {
                            if (child is Slider sl && sl.Tag is SliderViewModel svm2 && svm2.Name == vm.Name)
                                sl.Value = vm.Value;
                            if (child is TextBlock tb && Grid.GetRow(tb) == 1 && Grid.GetColumn(tb) == 1)
                                tb.Text = vm.DisplayValue;
                        }
                    }
                }
                // 串口打开时发送
                if (_session != null && _session.IsOpen)
                    SendSliderValue(vm);
            }

            // 显示第一个被预设的滑杆详情
            if (_sliderVM.Sliders.Count > 0)
            {
                _lastActiveSlider = _sliderVM.Sliders[0].Name;
                UpdateSliderSideDetail(_sliderVM.Sliders[0]);
            }
        }

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
        //  风格系统（每个滑杆独立风格）
        // ═══════════════════════════════════════

        private SliderStyleParams ResolveTrackParams(string styleName)
        {
            if (styleName == "minimal") return MinimalStyleParams;
            if (styleName == "default" || string.IsNullOrEmpty(styleName)) return DefaultStyleParams;
            return BuildTrackParams(styleName);
        }

        private SliderStyleParams ResolveThumbParams(string styleName)
        {
            if (styleName == "minimal") return MinimalStyleParams;
            if (styleName == "方块") return SquareStyleParams;
            if (styleName == "default" || string.IsNullOrEmpty(styleName)) return DefaultStyleParams;
            return BuildThumbParams(styleName);
        }

        private SliderStyleParams BuildTrackParams(string styleName)
        {
            var path = $"Icons/sliders/track_{styleName}.png";
            double h = 4, r = 2;
            try
            {
                var brush = TryLoadImageBrush(path);
                if (brush?.ImageSource is BitmapSource bmp)
                {
                    h = Math.Max(1, Math.Min(16, bmp.PixelHeight));
                    r = Math.Max(0, Math.Min(8, h / 2.0));
                    return new SliderStyleParams { Name = styleName, TrackH = h, TrackR = r, TrackImg = path };
                }
            }
            catch { }
            return new SliderStyleParams { Name = styleName, TrackH = h, TrackR = r };
        }

        private SliderStyleParams BuildThumbParams(string styleName)
        {
            var path = $"Icons/sliders/thumb_{styleName}.png";
            double w = 16, h = 16;
            try
            {
                var brush = TryLoadImageBrush(path);
                if (brush?.ImageSource is BitmapSource bmp)
                {
                    w = Math.Max(8, Math.Min(48, bmp.PixelWidth));
                    h = Math.Max(8, Math.Min(48, bmp.PixelHeight));
                    return new SliderStyleParams { Name = styleName, ThumbW = w, ThumbH = h, StrokeThick = 0, ThumbImg = path };
                }
            }
            catch { }
            return new SliderStyleParams { Name = styleName, ThumbW = w, ThumbH = h, StrokeThick = 2 };
        }

        /// <summary>将轨道和拇指各自独立的风格应用到已加载的 Slider 控件</summary>
        private void ApplySliderStyleToControl(Slider slider, string trackStyle, string thumbStyle)
        {
            var tp = ResolveTrackParams(trackStyle ?? "default");
            var thp = ResolveThumbParams(thumbStyle ?? "default");
            if (tp == null || thp == null) return;
            var p = new SliderStyleParams
            {
                TrackH = tp.TrackH, TrackR = tp.TrackR, TrackImg = tp.TrackImg,
                ThumbW = thp.ThumbW, ThumbH = thp.ThumbH, StrokeThick = thp.StrokeThick,
                IsSquare = thp.IsSquare, ThumbImg = thp.ThumbImg,
            };
            slider.ApplyTemplate();

            // —— Slider 控件 + 内部 Grid 高度（跟随拇指大小自适应）——
            double sliderH = Math.Max(28, p.ThumbH + 12);
            slider.Height = sliderH;
            var root = slider.Template?.FindName("sliderRoot", slider) as Grid;
            if (root != null) root.Height = sliderH;

            // —— 轨道：有图用图，无图代码绘制 ——
            var trackBg = slider.Template?.FindName("trackBg", slider) as Border;
            var trackImage = slider.Template?.FindName("trackImage", slider) as Image;
            bool hasTrackImg = !string.IsNullOrEmpty(p.TrackImg);

            if (trackBg != null)
            {
                trackBg.Height = p.TrackH;
                trackBg.CornerRadius = new CornerRadius(p.TrackR);
                trackBg.Visibility = hasTrackImg ? Visibility.Collapsed : Visibility.Visible;
            }
            if (trackImage != null)
            {
                if (hasTrackImg)
                {
                    var brush = TryLoadImageBrush(p.TrackImg);
                    if (brush != null)
                    {
                        trackImage.Source = brush.ImageSource;
                        trackImage.Height = p.TrackH;
                        trackImage.Stretch = Stretch.Fill;
                        trackImage.VerticalAlignment = VerticalAlignment.Center;
                        trackImage.Visibility = Visibility.Visible;
                    }
                    else { trackImage.Visibility = Visibility.Collapsed; if (trackBg != null) trackBg.Visibility = Visibility.Visible; }
                }
                else trackImage.Visibility = Visibility.Collapsed;
            }

            // —— 彩色填充条尺寸 ——
            var trackFill = slider.Template?.FindName("trackFill", slider) as Border;
            if (trackFill != null)
            {
                trackFill.Height = p.TrackH;
                trackFill.CornerRadius = new CornerRadius(p.TrackR);
            }

            // —— 拇指：有图用图，无图代码绘制 ——
            var thumb = slider.Template?.FindName("Thumb", slider) as Thumb;
            if (thumb != null)
            {
                thumb.ApplyTemplate();
                var thumbDot = thumb.Template?.FindName("thumbDot", thumb) as Ellipse;
                var thumbRect = thumb.Template?.FindName("thumbRect", thumb) as Rectangle;
                var thumbImage = thumb.Template?.FindName("thumbImage", thumb) as Image;
                bool hasThumbImg = !string.IsNullOrEmpty(p.ThumbImg);

                // 圆形拇指
                if (thumbDot != null)
                {
                    thumbDot.Width = p.ThumbW;
                    thumbDot.Height = p.ThumbH;
                    thumbDot.StrokeThickness = p.StrokeThick;
                    thumbDot.Visibility = (hasThumbImg || p.IsSquare) ? Visibility.Collapsed : Visibility.Visible;
                }
                // 方形拇指
                if (thumbRect != null)
                {
                    thumbRect.Width = p.ThumbW;
                    thumbRect.Height = p.ThumbH;
                    thumbRect.Visibility = (!hasThumbImg && p.IsSquare) ? Visibility.Visible : Visibility.Collapsed;
                }
                if (thumbImage != null)
                {
                    if (hasThumbImg)
                    {
                        var brush = TryLoadImageBrush(p.ThumbImg);
                        if (brush != null)
                        {
                            thumbImage.Source = brush.ImageSource;
                            thumbImage.Width = p.ThumbW;
                            thumbImage.Height = p.ThumbH;
                            thumbImage.Stretch = Stretch.Uniform;
                            thumbImage.Visibility = Visibility.Visible;
                        }
                        else { thumbImage.Visibility = Visibility.Collapsed; if (thumbDot != null) thumbDot.Visibility = Visibility.Visible; }
                    }
                    else thumbImage.Visibility = Visibility.Collapsed;
                }
            }
        }

        // ——— 侧面板轨道 / 拇指独立下拉菜单 ———

        private void btnSliderTrackStyle_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSliders.Count != 1) return;
            var svm = _selectedSliders[0];
            ShowStyleMenu(sender, svm, isTrack: true);
        }

        private void btnSliderThumbStyle_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSliders.Count != 1) return;
            var svm = _selectedSliders[0];
            ShowStyleMenu(sender, svm, isTrack: false);
        }

        private void ShowStyleMenu(object sender, SliderViewModel svm, bool isTrack)
        {
            var button = sender as Button;
            if (button == null) return;

            var menu = new ContextMenu
            {
                PlacementTarget = button,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
            };
            var itemStyle = (Style)FindResource("ContextMenuMenuItemStyle");
            var sepStyle = (Style)FindResource("ContextMenuSeparatorStyle");

            string current = isTrack ? svm.TrackStyle : svm.ThumbStyle;
            string desc = isTrack ? "4px / 2px 轨道高度" : "16px / 12px 圆形拖钮";

            // 把菜单绑定到按钮上（确保能正确打开）
            button.ContextMenu = menu;

            AddStyleMenuItem(menu, "default", desc, itemStyle, current == "default", svm, isTrack);
            AddStyleMenuItem(menu, "minimal", desc, itemStyle, current == "minimal", svm, isTrack);
            if (!isTrack) AddStyleMenuItem(menu, "方块", "14×14 方角矩形 · 无描边", itemStyle, current == "方块", svm, isTrack);

            var customs = isTrack ? ScanCustomTrackStyles() : ScanCustomSliderThumbStyles();
            if (customs.Count > 0)
            {
                menu.Items.Add(new Separator { Style = sepStyle });
                foreach (var name in customs)
                    AddStyleMenuItem(menu, name, "图片自定义", itemStyle, current == name, svm, isTrack);
            }

            menu.IsOpen = true;
        }

        private void AddStyleMenuItem(ContextMenu menu, string name, string desc, Style style, bool isCurrent,
            SliderViewModel svm, bool isTrack)
        {
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(new TextBlock
            {
                Text = (isCurrent ? "✓ " : "   ") + LogicValueMaps.DisplaySliderStyle(name),
                FontSize = 12, FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = "  " + desc, FontSize = 10,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            });
            var item = new MenuItem { Header = headerPanel, Style = style, Tag = name };
            item.Click += (s2, e2) =>
            {
                if (isTrack) svm.TrackStyle = (string)((MenuItem)s2).Tag;
                else svm.ThumbStyle = (string)((MenuItem)s2).Tag;
                RefreshSlidersUI();
                RefreshSlidersSidePanel();
                SaveSlidersPrefs();
            };
            menu.Items.Add(item);
        }

        private static List<string> ScanCustomTrackStyles()
        {
            return ScanStyleFiles("track_*.png", "track_");
        }

        private static List<string> ScanCustomSliderThumbStyles()
        {
            return ScanStyleFiles("thumb_*.png", "thumb_");
        }

        private static List<string> ScanStyleFiles(string pattern, string prefix)
        {
            var result = new List<string>();
            try
            {
                var exeDir = AppContext.BaseDirectory;
                var slidersDir = System.IO.Path.Combine(exeDir, "Icons", "sliders");
                if (!Directory.Exists(slidersDir)) return result;

                foreach (var file in Directory.GetFiles(slidersDir, pattern))
                {
                    var name = System.IO.Path.GetFileNameWithoutExtension(file);
                    if (name.StartsWith(prefix) && name.Length > prefix.Length)
                        name = name.Substring(prefix.Length);
                    if (!string.IsNullOrEmpty(name) && !result.Contains(name))
                        result.Add(name);
                }
            }
            catch { }
            return result;
        }

        // ═══════════════════════════════════════
        //  滑杆 UI 刷新
        // ═══════════════════════════════════════

        private void RefreshSlidersUI()
        {
            if (_sliderVM == null) return;
            _sliderPartsCache.Clear();
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
                if (svm.Color != "default" && !string.IsNullOrEmpty(svm.Color))
                {
                    string hex = SliderPanelViewModel.GetColorHex(svm.Color, isDarkTheme);
                    if (hex != null) {
                        card.BorderThickness = new Thickness(4, 1, 1, 1);
                        card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                    }
                }

                // 编辑模式选中高亮
                if (isEdit && _selectedSliders.Contains(svm)) {
                    bool hasCustomColor = svm.Color != "default" && !string.IsNullOrEmpty(svm.Color);
                    if (!hasCustomColor) {
                        var accent = isDarkTheme ? Color.FromRgb(0x0E, 0x63, 0x9C) : Color.FromRgb(0x00, 0x78, 0xD4);
                        card.BorderBrush = new SolidColorBrush(accent); card.BorderThickness = new Thickness(2);
                    } else {
                        card.Background = new SolidColorBrush(isDarkTheme
                            ? Color.FromRgb(0x2A, 0x2E, 0x3A) : Color.FromRgb(0xE6, 0xF0, 0xFA));
                        card.BorderThickness = new Thickness(4, 1, 1, 1);
                    }
                }

                {
                    // ── 名字 + 滑杆 + 数值（编辑和正常模式都显示滑杆预览）──
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    var nameLabel = new TextBlock {
                        Text = isEdit ? "✎ " + svm.Name : svm.Name,
                        FontSize = 12, FontWeight = FontWeights.SemiBold,
                        Foreground = isEdit ? (Brush)FindResource("PrimaryBrush") : (Brush)FindResource("TextPrimaryBrush"),
                        Margin = new Thickness(0, 0, 0, 6),
                        Cursor = isEdit ? Cursors.Hand : Cursors.Arrow,
                    };
                    if (isEdit)
                    {
                        nameLabel.MouseLeftButtonDown += (s2, e2) => {
                            _selectedSliders.Clear(); _selectedSliders.Add(svm);
                            SwitchSidePanelToSliders();
                            RefreshSlidersUI(); RefreshSlidersSidePanel();
                        };
                    }
                    Grid.SetRow(nameLabel, 0); Grid.SetColumn(nameLabel, 0); Grid.SetColumnSpan(nameLabel, 2);
                    grid.Children.Add(nameLabel);

                    var slider = new Slider {
                        Minimum = svm.MinValue, Maximum = svm.MaxValue, Value = svm.Value,
                        SmallChange = svm.Step, LargeChange = svm.Step * 10,
                        VerticalAlignment = VerticalAlignment.Center,
                        Style = (Style)FindResource("ColoredSliderStyle"),
                        Tag = svm,
                    };
                    // Q 弹：只作用于滑钮（Thumb），不缩放整条滑杆
                    slider.PreviewMouseLeftButtonDown += (s, e) => {
                        if (_sliderVM.IsEditMode) return;
                        var sl = s as Slider; sl.ApplyTemplate();
                        var thumb = sl.Template.FindName("Thumb", sl) as FrameworkElement;
                        if (thumb != null) SpringPress(thumb);
                    };
                    // 拖拽过程中持续更新数值显示 + 节流发送（使用缓存引用避免每次 FindName）
                    slider.ValueChanged += (s, e) => {
                        if (_sliderVM.IsEditMode) return;
                        var sl = s as Slider; var vm = sl?.Tag as SliderViewModel; if (vm == null) return;
                        double rounded = Math.Round(sl.Value / vm.Step) * vm.Step;
                        rounded = Math.Max(vm.MinValue, Math.Min(vm.MaxValue, rounded));
                        vm.Value = rounded;
                        if (_sliderPartsCache.TryGetValue(sl, out var parts)) {
                            if (parts.valueTb != null) parts.valueTb.Text = vm.DisplayValue;
                            UpdateSliderProgressBarCached(sl, vm, parts.trackFill, parts.thumbDot);
                        }
                        // 侧栏活跃滑杆详情（直接更新 TextBlock，不调 RefreshSlidersUI）
                        _lastActiveSlider = vm.Name;
                        UpdateSliderSideDetail(vm);
                        var now = DateTime.Now;
                        if (!_sliderLastSent.TryGetValue(vm.Name, out var last)
                            || (now - last).TotalMilliseconds >= vm.SendIntervalMs)
                        {
                            _sliderLastSent[vm.Name] = now;
                            SendSliderValue(vm);
                        }
                    };
                    // 松手：Q 弹回弹（只作用于 Thumb）+ 取整 + 发送
                    slider.PreviewMouseLeftButtonUp += (s, e) => {
                        if (_sliderVM.IsEditMode) return;
                        var sl = s as Slider; sl.ApplyTemplate();
                        var thumb = sl.Template.FindName("Thumb", sl) as FrameworkElement;
                        if (thumb != null) SpringRelease(thumb);
                        var vm = sl?.Tag as SliderViewModel; if (vm == null) return;
                        double rounded = Math.Round(sl.Value / vm.Step) * vm.Step;
                        rounded = Math.Max(vm.MinValue, Math.Min(vm.MaxValue, rounded));
                        sl.Value = rounded; vm.Value = rounded;
                        if (_sliderPartsCache.TryGetValue(sl, out var parts)) {
                            if (parts.valueTb != null) parts.valueTb.Text = vm.DisplayValue;
                            UpdateSliderProgressBarCached(sl, vm, parts.trackFill, parts.thumbDot);
                        }
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

                    // 等布局完成后缓存模板部件 + 刷新轨道彩色填充
                    slider.Loaded += (s2, e2) => {
                        var sl2 = s2 as Slider;
                        var vm2 = sl2?.Tag as SliderViewModel;
                        if (vm2 == null) return;
                        ApplySliderStyleToControl(sl2, vm2.TrackStyle ?? "default", vm2.ThumbStyle ?? "default");
                        var tFill = sl2.Template?.FindName("trackFill", sl2) as Border;
                        var thumb2 = sl2.Template?.FindName("Thumb", sl2) as Thumb;
                        Ellipse tDot = null;
                        if (thumb2 != null) { thumb2.ApplyTemplate(); tDot = thumb2.Template?.FindName("thumbDot", thumb2) as Ellipse; }
                        var grid2 = sl2.Parent as Grid;
                        TextBlock vTb = null;
                        if (grid2 != null)
                            foreach (var child in grid2.Children)
                                if (child is TextBlock tb && Grid.GetRow(tb) == 1 && Grid.GetColumn(tb) == 1)
                                    { vTb = tb; break; }
                        _sliderPartsCache[sl2] = (tFill, tDot, vTb);
                        UpdateSliderProgressBarCached(sl2, vm2, tFill, tDot);
                    };

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

        /// <summary>使用缓存引用更新进度条和滑钮颜色，避免每次拖拽 FindName/ApplyTemplate</summary>
        private void UpdateSliderProgressBarCached(Slider slider, SliderViewModel svm, Border trackFill, Ellipse thumbDot)
        {
            string hex = SliderPanelViewModel.GetColorHex(svm.Color, isDarkTheme);
            Color baseColor;
            if (hex != null)
                baseColor = (Color)ColorConverter.ConvertFromString(hex);
            else
                baseColor = ((SolidColorBrush)FindResource("PrimaryBrush")).Color;

            if (trackFill != null)
            {
                double pct = (svm.Value - svm.MinValue) / (svm.MaxValue - svm.MinValue);
                trackFill.Width = Math.Max(0, slider.ActualWidth * pct);
                trackFill.Background = new SolidColorBrush(baseColor);
            }

            if (thumbDot != null)
            {
                byte r = (byte)(baseColor.R * 0.60);
                byte g = (byte)(baseColor.G * 0.60);
                byte b = (byte)(baseColor.B * 0.60);
                thumbDot.Fill = new SolidColorBrush(Color.FromRgb(r, g, b));
            }

            // 方形拇指也同步颜色
            var thumb = slider.Template?.FindName("Thumb", slider) as Thumb;
            if (thumb != null)
            {
                thumb.ApplyTemplate();
                var thumbRect = thumb.Template?.FindName("thumbRect", thumb) as Rectangle;
                if (thumbRect != null && thumbRect.Visibility == Visibility.Visible)
                {
                    byte r = (byte)(baseColor.R * 0.60);
                    byte g = (byte)(baseColor.G * 0.60);
                    byte b = (byte)(baseColor.B * 0.60);
                    thumbRect.Fill = new SolidColorBrush(Color.FromRgb(r, g, b));
                }
            }
        }

        private void ShowSliderFeedback(SliderViewModel svm)
        {
            tbSliderFeedbackName.Text = svm.Name;
            tbSliderFeedbackValue.Text = string.Format("[slider,{0},{1}]", svm.Name, svm.DisplayValue);
            UpdateSliderProtocolPreview(svm);
        }

        private void UpdateSliderProtocolPreview(SliderViewModel svm)
        {
            if (tbSliderProtocolPreview == null || svm == null) return;
            tbSliderProtocolPreview.Text = string.Format("[slider,{0},{1}]", svm.Name, svm.DisplayValue);
        }

        private void btnSliderProtocolCopy_Click(object sender, RoutedEventArgs e)
        {
            if (tbSliderProtocolPreview == null || string.IsNullOrEmpty(tbSliderProtocolPreview.Text)) return;
            SafeSetClipboard(tbSliderProtocolPreview.Text);
            if (sender is Button btn) ShowCopyToastAndShake(btn);
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

            if (!isEdit)
            {
                rightSlidersFeedback.Visibility = Visibility.Visible;
                // 正常模式：显示活跃滑杆详情
                var active = GetActiveSlider();
                if (active != null) UpdateSliderSideDetail(active);
                return;
            }
            if (count == 0) { rightSlidersNoSelection.Visibility = Visibility.Visible; return; }

            // 单选
            var svm = _selectedSliders[0];
            rightSlidersSingleSelect.Visibility = Visibility.Visible;
            tbSliderName.Text = svm.Name;
            btnSliderTrackStyle.Content = LogicValueMaps.DisplaySliderStyle(svm.TrackStyle ?? "default") + " ▾";
            btnSliderThumbStyle.Content = LogicValueMaps.DisplaySliderStyle(svm.ThumbStyle ?? "default") + " ▾";
            tbSliderMin.Text = svm.MinValue.ToString();
            tbSliderMax.Text = svm.MaxValue.ToString();
            tbSliderStep.Text = svm.Step.ToString();
            tbSliderInterval.Text = svm.SendIntervalMs.ToString();
            UpdateSlidersColorChipSelection(svm.Color);
        }

        /// <summary>
        /// 获取侧栏应显示的活跃滑杆：用户最后拖拽的 → fallback 第一个。
        /// </summary>
        private SliderViewModel GetActiveSlider()
        {
            if (_sliderVM == null || _sliderVM.Sliders.Count == 0) return null;
            if (!string.IsNullOrEmpty(_lastActiveSlider))
            {
                var s = _sliderVM.Sliders.FirstOrDefault(x => x.Name == _lastActiveSlider);
                if (s != null) return s;
            }
            return _sliderVM.Sliders[0];
        }

        /// <summary>
        /// 拖拽滑杆时直接更新侧栏详情字段（不调 RefreshSlidersUI——会销毁正在拖的控件）。
        /// </summary>
        private void UpdateSliderSideDetail(SliderViewModel vm)
        {
            if (vm == null) return;
            // 只在正常模式（非编辑）下更新
            if (_sliderVM.IsEditMode) return;
            tbSliderFeedbackName.Text = vm.Name;
            tbSliderCurrentValue.Text = vm.DisplayValue;
            tbSliderDetailRange.Text = $"{vm.MinValue} ~ {vm.MaxValue}";
            tbSliderDetailStep.Text = vm.Step.ToString();
            UpdateSliderProtocolPreview(vm);
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
        //  主题切换
        // ═══════════════════════════════════════

        internal void UpdateSlidersTheme()
        {
            if (_sliderVM == null || slidersPanel.Children.Count == 0) return;
            var cardBg = (Brush)FindResource("CardBgBrush");
            var cardBorder = (Brush)FindResource("CardBorderBrush");
            var textBrush = (Brush)FindResource("TextPrimaryBrush");
            var primaryBrush = (Brush)FindResource("PrimaryBrush");
            bool isEdit = _sliderVM.IsEditMode;

            foreach (var child in slidersPanel.Children)
            {
                if (child is not Border card) continue;
                if (card.Tag is not SliderViewModel svm) continue;

                bool hasCustomColor = svm.Color != "default" && !string.IsNullOrEmpty(svm.Color);
                card.Background = cardBg;
                if (!hasCustomColor)
                    card.BorderBrush = cardBorder;

                if (card.Child is Grid grid)
                    UpdateSlidersGridTheme(grid, isEdit, textBrush, primaryBrush);
            }

            // 更新进度条 trackFill（仅默认颜色滑块——跟随 PrimaryColor）
            foreach (var kv in _sliderPartsCache)
            {
                var sl = kv.Key;
                var parts = kv.Value;
                if (parts.trackFill == null) continue;
                var svm = sl.Tag as SliderViewModel;
                if (svm != null && (svm.Color == "default" || string.IsNullOrEmpty(svm.Color)))
                    parts.trackFill.Background = primaryBrush;
            }
        }

        private static void UpdateSlidersGridTheme(Grid grid, bool isEdit, Brush textBrush, Brush primaryBrush)
        {
            bool firstTextBlock = true; // Row 0 的 nameLabel
            foreach (var child in grid.Children)
            {
                if (child is TextBlock tb)
                {
                    tb.Foreground = firstTextBlock && isEdit ? primaryBrush : textBrush;
                    firstTextBlock = false;
                }
                if (child is Panel subPanel)
                    UpdateSlidersGridTheme_Recursive(subPanel, textBrush);
            }
        }

        private static void UpdateSlidersGridTheme_Recursive(Panel panel, Brush textBrush)
        {
            foreach (var child in panel.Children)
            {
                if (child is TextBlock tb) tb.Foreground = textBrush;
                if (child is Panel subPanel) UpdateSlidersGridTheme_Recursive(subPanel, textBrush);
            }
        }

        // ═══════════════════════════════════════
        //  协议处理 & 持久化
        // ═══════════════════════════════════════

        private void HandleSliderMessage(string name, string valStr) {
            InitSliderPanel(refreshUI: false);
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
