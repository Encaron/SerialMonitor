using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace 串口助手
{
    public partial class MainWindow
    {
        // ——— 摇杆面板 ———
        private JoystickPanelViewModel _joyVM;
        private Dictionary<int, (FrameworkElement thumb, Ellipse ring, TextBlock label, TextBlock pos, Canvas pad)> _joyElems
            = new Dictionary<int, (FrameworkElement, Ellipse, TextBlock, TextBlock, Canvas)>();
        private int _draggingJoyId;
        private Point _dragOrigin;
        private DateTime _lastJoySent;

        // 底板和拇指独立风格（用字符串：内置="手柄风"/"极简风"/"经典风"，自定义=任意名）
        private string _padStyle = "手柄风";
        private string _thumbStyle = "手柄风";
        private string _customPadStyle = "";
        private string _customThumbStyle = "";
        private const string JoyPadStyleKey = "joystickPadStyle";
        private const string JoyThumbStyleKey = "joystickThumbStyle";
        private const string JoyCustomPadKey = "joystickCustomPad";
        private const string JoyCustomThumbKey = "joystickCustomThumb";

        private static readonly HashSet<string> BuiltInJoyStyles = new HashSet<string> { "手柄风", "极简风", "经典风" };

        private void SwitchSidePanelToJoystick() {
            if (_currentTab != "Joystick") { tabJoystick.IsChecked = true; }
        }

        // ——— 初始化 ———
        private void InitJoystickPanel()
        {
            if (_joyVM != null) return;
            _joyVM = new JoystickPanelViewModel();

            if (_prefsData != null && _prefsData.TryGetValue("joysticks", out var joyObj)
                && joyObj is List<object> rawList && rawList.Count > 0)
            {
                var list = new List<Dictionary<string, object>>();
                foreach (var item in rawList)
                    if (item is Dictionary<string, object> d) list.Add(d);
                if (list.Count > 0) _joyVM.DeserializeJoysticks(list);
            }

            LoadJoystickStyleFromPrefs();
            RefreshJoystickUI();
        }

        // ═══════════════════════════════════════
        //  摇杆 UI 构建
        // ═══════════════════════════════════════

        // ═══ 风格切换（下拉菜单，参照键盘布局菜单模式） ═══

        // ——— 底板 / 拇指独立下拉菜单 ———

        private void btnJoystickPadStyle_Click(object sender, RoutedEventArgs e) => ShowJoyStyleMenu(sender, isPad: true);
        private void btnJoystickThumbStyle_Click(object sender, RoutedEventArgs e) => ShowJoyStyleMenu(sender, isPad: false);

        private void ShowJoyStyleMenu(object sender, bool isPad)
        {
            var button = sender as Button;
            if (button == null) return;

            var menu = new ContextMenu
            {
                PlacementTarget = button,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
            };
            button.ContextMenu = menu;

            var itemStyle = (Style)FindResource("ContextMenuMenuItemStyle");
            var sepStyle = (Style)FindResource("ContextMenuSeparatorStyle");

            string current = isPad ? (_padStyle) : (_thumbStyle);
            if (!BuiltInJoyStyles.Contains(current)) current = _customPadStyle; // custom

            AddJoyStyleMenuItem(menu, "手柄风", "同心参考圆 + 8 方向标记 + 方向指示线", itemStyle, current == "手柄风", isPad);
            AddJoyStyleMenuItem(menu, "极简风", "圆角底座 + 网格点阵 + X 色条", itemStyle, current == "极简风", isPad);
            AddJoyStyleMenuItem(menu, "经典风", "50% 虚线圆 + 阴影 + X/Y 分行", itemStyle, current == "经典风", isPad);

            var customs = isPad ? ScanCustomPadStyles() : ScanCustomThumbStyles();
            if (customs.Count > 0)
            {
                menu.Items.Add(new Separator { Style = sepStyle });
                foreach (var name in customs)
                    AddJoyStyleMenuItem(menu, name, "图片自定义", itemStyle, current == name, isPad);
            }

            menu.IsOpen = true;
        }

        private void AddJoyStyleMenuItem(ContextMenu menu, string label, string desc, Style style, bool isCurrent, bool isPad)
        {
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(new TextBlock
            {
                Text = (isCurrent ? "✓ " : "   ") + label,
                FontSize = 12, FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = "  " + desc, FontSize = 10,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            });
            var item = new MenuItem { Header = headerPanel, Style = style, Tag = label };
            item.Click += (s2, e2) =>
            {
                string chosen = (string)((MenuItem)s2).Tag;
                if (BuiltInJoyStyles.Contains(chosen))
                {
                    if (isPad) { _padStyle = chosen; _customPadStyle = ""; }
                    else { _thumbStyle = chosen; _customThumbStyle = ""; }
                }
                else
                {
                    if (isPad) { _padStyle = "自定义"; _customPadStyle = chosen; }
                    else { _thumbStyle = "自定义"; _customThumbStyle = chosen; }
                }
                btnJoystickPadStyle.Content = JoyPadLabel() + " ▾";
                btnJoystickThumbStyle.Content = JoyThumbLabel() + " ▾";
                RefreshJoystickUI();
                SaveJoyStylePrefs();
            };
            menu.Items.Add(item);
        }

        private string JoyPadLabel() => BuiltInJoyStyles.Contains(_padStyle) ? _padStyle : _customPadStyle;
        private string JoyThumbLabel() => BuiltInJoyStyles.Contains(_thumbStyle) ? _thumbStyle : _customThumbStyle;

        private void LoadJoystickStyleFromPrefs()
        {
            if (_prefsData == null) return;
            // 兼容旧格式
            if (_prefsData.TryGetValue("joystickStyle", out var oldStyle) && oldStyle is string oldStr)
            {
                switch (oldStr) {
                    case "Minimal": _padStyle = "极简风"; _thumbStyle = "极简风"; break;
                    case "Classic": _padStyle = "经典风"; _thumbStyle = "经典风"; break;
                    case "Custom": if (_prefsData.TryGetValue("joystickCustomStyle", out var cs) && cs is string csStr)
                        { _padStyle = "自定义"; _thumbStyle = "自定义"; _customPadStyle = csStr; _customThumbStyle = csStr; }
                        break;
                    default: _padStyle = "手柄风"; _thumbStyle = "手柄风"; break;
                }
            }
            // 新格式覆盖
            if (_prefsData.TryGetValue(JoyPadStyleKey, out var ps) && ps is string psStr) _padStyle = psStr;
            if (_prefsData.TryGetValue(JoyThumbStyleKey, out var ts) && ts is string tsStr) _thumbStyle = tsStr;
            if (_prefsData.TryGetValue(JoyCustomPadKey, out var cp) && cp is string cpStr) _customPadStyle = cpStr;
            if (_prefsData.TryGetValue(JoyCustomThumbKey, out var ct) && ct is string ctStr) _customThumbStyle = ctStr;
            btnJoystickPadStyle.Content = JoyPadLabel() + " ▾";
            btnJoystickThumbStyle.Content = JoyThumbLabel() + " ▾";
        }

        private void SaveJoyStylePrefs()
        {
            if (_prefsData == null) return;
            _prefsData[JoyPadStyleKey] = _padStyle;
            _prefsData[JoyThumbStyleKey] = _thumbStyle;
            if (!string.IsNullOrEmpty(_customPadStyle)) _prefsData[JoyCustomPadKey] = _customPadStyle;
            if (!string.IsNullOrEmpty(_customThumbStyle)) _prefsData[JoyCustomThumbKey] = _customThumbStyle;
            _prefs.Save(_prefsData);
        }

        // ═══ 图片加载（优先文件系统 → 回退 WPF 资源 → 代码绘制） ═══

        /// <summary>
        /// 加载摇杆图片：① 文件系统（用户直接放 PNG，不注册 csproj）→ ② WPF 资源（csproj 注册）→ ③ null（回退代码绘制）
        /// </summary>
        private static ImageBrush TryLoadImageBrush(string relativePath)
        {
            // 1) 文件系统（优先——用户只需放 PNG 到 Icons/joystick/）
            try
            {
                var exeDir = AppContext.BaseDirectory;
                var filePath = System.IO.Path.Combine(exeDir, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
                if (File.Exists(filePath))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(filePath);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return new ImageBrush(bmp) { Stretch = Stretch.Uniform };
                }
            }
            catch { }

            // 2) WPF 资源（csproj <Resource Include> 注册方式）
            try
            {
                var uri = new Uri(relativePath, UriKind.Relative);
                var streamInfo = Application.GetResourceStream(uri);
                if (streamInfo != null)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = streamInfo.Stream;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return new ImageBrush(bmp) { Stretch = Stretch.Uniform };
                }
            }
            catch { }

            return null;
        }

        private static (ImageBrush pad, ImageBrush thumb) LoadJoyImages(string styleName)
        {
            return (
                TryLoadImageBrush($"Icons/joystick/pad_{styleName}.png"),
                TryLoadImageBrush($"Icons/joystick/thumb_{styleName}.png")
            );
        }

        private static List<string> ScanCustomPadStyles()
            => ScanJoyFiles("pad_*.png", "pad_");

        private static List<string> ScanCustomThumbStyles()
            => ScanJoyFiles("thumb_*.png", "thumb_");

        private static List<string> ScanJoyFiles(string pattern, string prefix)
        {
            var result = new List<string>();
            try
            {
                var exeDir = AppContext.BaseDirectory;
                var joyDir = System.IO.Path.Combine(exeDir, "Icons", "joystick");
                if (!Directory.Exists(joyDir)) return result;

                foreach (var file in Directory.GetFiles(joyDir, pattern))
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

        // ═══ UI 构建 — 入口 ═══

        private void RefreshJoystickUI()
        {
            if (_joyVM == null) return;
            joystickPanel.Children.Clear(); _joyElems.Clear();

            // 同风格内置 → 走原路径（性能最优，完整装饰）
            bool sameBuiltIn = BuiltInJoyStyles.Contains(_padStyle)
                            && _padStyle == _thumbStyle;

            foreach (var j in _joyVM.Joysticks)
            {
                Canvas canvas;
                if (sameBuiltIn)
                {
                    canvas = _padStyle switch
                    {
                        "极简风" => BuildMinimalStyle(j),
                        "经典风" => BuildClassicStyle(j),
                        _        => BuildGamepadStyle(j),
                    };
                }
                else
                {
                    canvas = BuildHybridJoyStyle(j);
                }
                joystickPanel.Children.Add(canvas);
            }
            // 初始化侧栏为空（等待首次发送后更新）
        }

        // ═══════════════════════════════════════════════
        //  风格 A — 游戏手柄风
        // ═══════════════════════════════════════════════

        private Canvas BuildGamepadStyle(JoystickViewModel j)
        {
            int pad = j.PadSize;
            double half = pad / 2.0, thumbR = 16, maxR = half - thumbR;

            var canvas = new Canvas { Width = pad, Height = pad + 24, Background = Brushes.Transparent };
            if (j.Id != 1) canvas.Margin = new Thickness(32, 0, 0, 0);

            var borderBrush = (Brush)FindResource("CardBorderBrush");
            var mutedBrush  = (Brush)FindResource("TextMutedBrush");

            // 尝试加载图片，找不到则回退代码绘制
            var (padImg, thumbImg) = LoadJoyImages("gamepad");
            Ellipse baseCircle;

            if (padImg != null)
            {
                // 图片底座（圆形裁切）
                baseCircle = new Ellipse {
                    Width = pad, Height = pad,
                    Fill = padImg,
                    Stroke = borderBrush, StrokeThickness = 2,
                };
                canvas.Children.Add(baseCircle);
            }
            else
            {
                // 代码绘制：暗色圆形底板
                baseCircle = new Ellipse {
                    Width = pad, Height = pad,
                    Fill = (Brush)FindResource("SecondaryHoverBgBrush"),
                    Stroke = borderBrush, StrokeThickness = 2,
                };
                canvas.Children.Add(baseCircle);

                // 三圈同心参考圆（25% / 50% / 75%）
                double[] radii = { 0.25, 0.50, 0.75 };
                double[] opacities = { 0.25, 0.35, 0.25 };
                for (int r = 0; r < radii.Length; r++)
                {
                    double d = pad * radii[r];
                    var ring = new Ellipse {
                        Width = d, Height = d,
                        Stroke = borderBrush, StrokeThickness = 0.8,
                        Opacity = opacities[r],
                    };
                    Canvas.SetLeft(ring, half - d/2); Canvas.SetTop(ring, half - d/2);
                    canvas.Children.Add(ring);
                }

                // 8 方向小标记（外圈边缘）
                for (int a = 0; a < 8; a++)
                {
                    double angle = a * Math.PI / 4 - Math.PI / 2;
                    double cx = half + Math.Cos(angle) * (half - 8);
                    double cy = half + Math.Sin(angle) * (half - 8);
                    var dot = new Ellipse {
                        Width = 4, Height = 4,
                        Fill = mutedBrush, Opacity = 0.5,
                    };
                    Canvas.SetLeft(dot, cx - 2); Canvas.SetTop(dot, cy - 2);
                    canvas.Children.Add(dot);
                }

                // 十字参考线（半透明）
                foreach (var (x1,y1,x2,y2) in new[] {
                    (10.0, half, pad-10.0, half),
                    (half, 10.0, half, pad-10.0) })
                {
                    var line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                        Stroke = borderBrush, StrokeThickness = 0.5, Opacity = 0.4 };
                    canvas.Children.Add(line);
                }

                // 方向指示线（中心 → 拇指）
                double tx = half + j.X * maxR;
                double ty = half - j.Y * maxR;
                var guide = new Line {
                    X1 = half, Y1 = half, X2 = tx, Y2 = ty,
                    Stroke = (Brush)FindResource("PrimaryBrush"),
                    StrokeThickness = 1.5, Opacity = 0.35,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Triangle,
                };
                canvas.Children.Add(guide);
            }

            // 拇指位置
            double thumbX = half + j.X * maxR;
            double thumbY = half - j.Y * maxR;

            // 拇指：图片优先，回退径向渐变
            var thumbFill = thumbImg ?? (Brush)new RadialGradientBrush(
                Color.FromRgb(0x40, 0xA0, 0xFF),
                Color.FromRgb(0x0E, 0x63, 0x9C))
            { GradientOrigin = new Point(0.35, 0.35) };
            var thumb = new Ellipse {
                Width = thumbR * 2, Height = thumbR * 2,
                Fill = thumbFill,
                Stroke = Brushes.White, StrokeThickness = 1.5,
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect {
                    Color = Colors.Black, BlurRadius = 6, ShadowDepth = 2, Opacity = 0.4,
                },
            };
            Canvas.SetLeft(thumb, thumbX - thumbR); Canvas.SetTop(thumb, thumbY - thumbR);
            canvas.Children.Add(thumb);

            // J1/J2 标签
            var label = new TextBlock {
                Text = "J" + j.Id, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = mutedBrush, Margin = new Thickness(8, 4, 0, 0),
            };
            Canvas.SetLeft(label, 0); Canvas.SetTop(label, 0);
            canvas.Children.Add(label);

            // 数值
            var posTb = MakePosTextBlock(j, pad, twoLine: false);
            canvas.Children.Add(posTb);

            WireJoystickEvents(thumb, canvas, j);
            _joyElems[j.Id] = (thumb, baseCircle, label, posTb, canvas);
            return canvas;
        }

        // ═══════════════════════════════════════════════
        //  风格 B — 现代极简风
        // ═══════════════════════════════════════════════

        private Canvas BuildMinimalStyle(JoystickViewModel j)
        {
            int pad = j.PadSize;
            double half = pad / 2.0, thumbR = 14, maxR = half - thumbR;

            var canvas = new Canvas { Width = pad, Height = pad + 24, Background = Brushes.Transparent };
            if (j.Id != 1) canvas.Margin = new Thickness(24, 0, 0, 0);

            var border  = (Brush)FindResource("CardBorderBrush");
            var muted   = (Brush)FindResource("TextMutedBrush");
            var primary = (Brush)FindResource("PrimaryBrush");
            var secondary = (Brush)FindResource("TextSecondaryBrush");

            // 尝试加载图片
            var (padImg, thumbImg) = LoadJoyImages("minimal");

            if (padImg != null)
            {
                // 图片底座
                var baseRect = new System.Windows.Shapes.Rectangle {
                    Width = pad, Height = pad, RadiusX = 16, RadiusY = 16,
                    Fill = padImg,
                    Stroke = border, StrokeThickness = 1,
                };
                canvas.Children.Add(baseRect);
            }
            else
            {
                // 代码绘制：圆角方形底座
                var baseRect = new System.Windows.Shapes.Rectangle {
                    Width = pad, Height = pad, RadiusX = 16, RadiusY = 16,
                    Fill = (Brush)FindResource("SecondaryHoverBgBrush"),
                    Stroke = border, StrokeThickness = 1,
                };
                canvas.Children.Add(baseRect);

                // 2px 网格点阵（淡色参考网格）
                for (int gx = 0; gx <= 4; gx++)
                for (int gy = 0; gy <= 4; gy++)
                {
                    double px = half + (gx - 2) * (half * 0.4);
                    double py = half + (gy - 2) * (half * 0.4);
                    var dot = new Ellipse { Width = 2, Height = 2, Fill = muted, Opacity = 0.3 };
                    Canvas.SetLeft(dot, px - 1); Canvas.SetTop(dot, py - 1);
                    canvas.Children.Add(dot);
                }

                // 水平色条（底部）— X 偏移可视化
                var xBarBg = new System.Windows.Shapes.Rectangle {
                    Width = pad - 40, Height = 4, RadiusX = 2, RadiusY = 2,
                    Fill = (Brush)FindResource("SecondaryHoverBgBrush"),
                    Stroke = border, StrokeThickness = 0.5,
                };
                Canvas.SetLeft(xBarBg, 20); Canvas.SetTop(xBarBg, pad - 22);
                canvas.Children.Add(xBarBg);

                // 色条上的当前位置标记
                double xIndicatorX = 20 + (pad - 40) * (j.X + 1) / 2;
                var xIndicator = new Ellipse {
                    Width = 6, Height = 6,
                    Fill = primary, Stroke = Brushes.White, StrokeThickness = 1,
                };
                Canvas.SetLeft(xIndicator, xIndicatorX - 3); Canvas.SetTop(xIndicator, pad - 23);
                canvas.Children.Add(xIndicator);
            }

            // 拇指位置
            double tx = half + j.X * maxR;
            double ty = half - j.Y * maxR;

            // 拇指：图片优先，回退纯色
            var thumbFill = thumbImg ?? primary;
            var thumb = new Ellipse {
                Width = thumbR * 2, Height = thumbR * 2,
                Fill = thumbFill,
                Stroke = Brushes.White, StrokeThickness = 2,
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect {
                    Color = Colors.Black, BlurRadius = 8, ShadowDepth = 1, Opacity = 0.3,
                },
            };
            Canvas.SetLeft(thumb, tx - thumbR); Canvas.SetTop(thumb, ty - thumbR);
            canvas.Children.Add(thumb);

            // 标签
            var label = new TextBlock {
                Text = "J" + j.Id, FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = secondary, Margin = new Thickness(0, 0, 0, 0),
            };
            Canvas.SetLeft(label, 12); Canvas.SetTop(label, 6);
            canvas.Children.Add(label);

            // 数值（两行）
            var posTb = MakePosTextBlock(j, pad, twoLine: true);
            canvas.Children.Add(posTb);

            WireJoystickEvents(thumb, canvas, j);
            _joyElems[j.Id] = (thumb, null!, label, posTb, canvas);
            return canvas;
        }

        // ═══════════════════════════════════════════════
        //  风格 C — 经典打磨（原版结构 + 层次提升）
        // ═══════════════════════════════════════════════

        private Canvas BuildClassicStyle(JoystickViewModel j)
        {
            int pad = j.PadSize;
            double half = pad / 2.0, thumbR = 16, maxR = half - thumbR;

            var canvas = new Canvas { Width = pad, Height = pad + 24, Background = Brushes.Transparent };
            if (j.Id != 1) canvas.Margin = new Thickness(32, 0, 0, 0);

            var borderBrush = (Brush)FindResource("CardBorderBrush");
            var mutedBrush  = (Brush)FindResource("TextMutedBrush");

            // 尝试加载图片
            var (padImg, thumbImg) = LoadJoyImages("classic");
            Ellipse ring;

            if (padImg != null)
            {
                // 图片底座（圆形裁切）
                ring = new Ellipse {
                    Width = pad, Height = pad,
                    Fill = padImg,
                    Stroke = borderBrush, StrokeThickness = 2,
                };
                canvas.Children.Add(ring);
            }
            else
            {
                // 代码绘制：外圈
                ring = new Ellipse {
                    Width = pad, Height = pad,
                    Stroke = borderBrush, StrokeThickness = 2,
                    Fill = (Brush)FindResource("SecondaryHoverBgBrush"),
                };
                canvas.Children.Add(ring);

                // 暗色中心圆底（50% 范围）
                var innerBg = new Ellipse {
                    Width = pad * 0.5, Height = pad * 0.5,
                    Fill = (Brush)FindResource("CardBgBrush"),
                    Stroke = borderBrush, StrokeThickness = 0.8, Opacity = 0.6,
                };
                Canvas.SetLeft(innerBg, half - pad*0.25); Canvas.SetTop(innerBg, half - pad*0.25);
                canvas.Children.Add(innerBg);

                // 50% 虚线参考圆
                var dashCircle = new Ellipse {
                    Width = pad * 0.5, Height = pad * 0.5,
                    Stroke = mutedBrush, StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 4 }, Opacity = 0.4,
                };
                Canvas.SetLeft(dashCircle, half - pad*0.25); Canvas.SetTop(dashCircle, half - pad*0.25);
                canvas.Children.Add(dashCircle);

                // 十字线（比原版更淡）
                foreach (var (x1,y1,x2,y2) in new[] {
                    (10.0, half, pad-10.0, half),
                    (half, 10.0, half, pad-10.0) })
                {
                    var line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                        Stroke = borderBrush, StrokeThickness = 0.5, Opacity = 0.35 };
                    canvas.Children.Add(line);
                }
            }

            // 拇指位置
            double tx = half + j.X * maxR;
            double ty = half - j.Y * maxR;

            // 拇指：图片优先，回退主题色
            var thumbFill = thumbImg ?? (Brush)FindResource("PrimaryBrush");
            var thumb = new Ellipse {
                Width = thumbR * 2, Height = thumbR * 2,
                Fill = thumbFill,
                Stroke = Brushes.White, StrokeThickness = 2,
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect {
                    Color = Colors.Black, BlurRadius = 6, ShadowDepth = 2, Opacity = 0.35,
                },
            };
            Canvas.SetLeft(thumb, tx - thumbR); Canvas.SetTop(thumb, ty - thumbR);
            canvas.Children.Add(thumb);

            // 标签
            var label = new TextBlock {
                Text = "J" + j.Id, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = mutedBrush, Margin = new Thickness(8, 4, 0, 0),
            };
            Canvas.SetLeft(label, 0); Canvas.SetTop(label, 0);
            canvas.Children.Add(label);

            // 数值（两行分列）
            var posTb = MakePosTextBlock(j, pad, twoLine: true);
            canvas.Children.Add(posTb);

            WireJoystickEvents(thumb, canvas, j);
            _joyElems[j.Id] = (thumb, ring, label, posTb, canvas);
            return canvas;
        }

        // ═══ 混搭风格（底板和拇指独立选择，含自定义图片） ═══

        private Canvas BuildHybridJoyStyle(JoystickViewModel j)
        {
            int pad = j.PadSize;
            double half = pad / 2.0, thumbR = 16, maxR = half - thumbR;

            var canvas = new Canvas { Width = pad, Height = pad + 24, Background = Brushes.Transparent };
            if (j.Id != 1) canvas.Margin = new Thickness(32, 0, 0, 0);

            var borderBrush = (Brush)FindResource("CardBorderBrush");
            var mutedBrush  = (Brush)FindResource("TextMutedBrush");

            // —— 底板 ——
            string padName = BuiltInJoyStyles.Contains(_padStyle) ? _padStyle : _customPadStyle;
            var (padImg, _) = LoadJoyImages(padName);
            Ellipse ring = null;

            if (padImg != null)
            {
                ring = new Ellipse { Width = pad, Height = pad, Fill = padImg, Stroke = borderBrush, StrokeThickness = 2 };
                canvas.Children.Add(ring);
            }
            else if (_padStyle == "经典风")
            {
                ring = new Ellipse { Width = pad, Height = pad, Fill = (Brush)FindResource("SecondaryHoverBgBrush"), Stroke = borderBrush, StrokeThickness = 2 };
                canvas.Children.Add(ring);
                var dashRing = new Ellipse { Width = pad * 0.5, Height = pad * 0.5, Stroke = borderBrush, StrokeThickness = 1.2, StrokeDashArray = new DoubleCollection { 4, 4 }, Opacity = 0.6 };
                Canvas.SetLeft(dashRing, half - pad * 0.25); Canvas.SetTop(dashRing, half - pad * 0.25);
                canvas.Children.Add(dashRing);
            }
            else if (_padStyle == "极简风")
            {
                var baseRect = new System.Windows.Shapes.Rectangle { Width = pad, Height = pad, RadiusX = 12, RadiusY = 12, Fill = (Brush)FindResource("SecondaryHoverBgBrush"), Stroke = borderBrush, StrokeThickness = 1.5 };
                canvas.Children.Add(baseRect);
                ring = null;
                // 5×5 网格点
                for (int gx = 0; gx < 5; gx++)
                for (int gy = 0; gy < 5; gy++)
                {
                    double px = (pad * 0.1) + gx * (pad * 0.2);
                    double py = (pad * 0.1) + gy * (pad * 0.2);
                    var dot = new Ellipse { Width = 2, Height = 2, Fill = mutedBrush, Opacity = 0.5 };
                    Canvas.SetLeft(dot, px); Canvas.SetTop(dot, py);
                    canvas.Children.Add(dot);
                }
            }
            else // 默认 / 手柄风
            {
                ring = new Ellipse { Width = pad, Height = pad, Fill = (Brush)FindResource("SecondaryHoverBgBrush"), Stroke = borderBrush, StrokeThickness = 2 };
                canvas.Children.Add(ring);
                // 十字参考线
                var hLine = new System.Windows.Shapes.Line { X1 = 0, X2 = pad, Y1 = half, Y2 = half, Stroke = borderBrush, StrokeThickness = 0.5, Opacity = 0.3 };
                var vLine = new System.Windows.Shapes.Line { X1 = half, X2 = half, Y1 = 0, Y2 = pad, Stroke = borderBrush, StrokeThickness = 0.5, Opacity = 0.3 };
                canvas.Children.Add(hLine); canvas.Children.Add(vLine);
            }

            // —— 拇指 ——
            string thumbName = BuiltInJoyStyles.Contains(_thumbStyle) ? _thumbStyle : _customThumbStyle;
            var (_, thumbImg) = LoadJoyImages(thumbName);
            Brush thumbFill;
            if (thumbImg != null)
            {
                thumbFill = thumbImg;
            }
            else if (_thumbStyle == "极简风")
            {
                thumbFill = new SolidColorBrush(isDarkTheme ? Color.FromRgb(0x60, 0x60, 0x70) : Color.FromRgb(0x80, 0x80, 0x90));
            }
            else
            {
                thumbFill = new RadialGradientBrush(
                    Color.FromRgb(0x40, 0xA0, 0xFF),
                    Color.FromRgb(0x0E, 0x63, 0x9C))
                { GradientOrigin = new Point(0.35, 0.35) };
            }

            double tx = half + j.X * maxR;
            double ty = half - j.Y * maxR;
            var thumb = new Ellipse {
                Width = thumbR * 2, Height = thumbR * 2,
                Fill = thumbFill,
                Stroke = Brushes.White, StrokeThickness = 1.5,
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect {
                    Color = Colors.Black, BlurRadius = 6, ShadowDepth = 2, Opacity = 0.4,
                },
            };
            Canvas.SetLeft(thumb, tx - thumbR); Canvas.SetTop(thumb, ty - thumbR);
            canvas.Children.Add(thumb);

            // —— 标签 + 数值 ——
            var label = new TextBlock { Text = "J" + j.Id, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = mutedBrush };
            Canvas.SetLeft(label, 8); Canvas.SetTop(label, 4);
            canvas.Children.Add(label);

            var posTb = MakePosTextBlock(j, pad, twoLine: false);
            canvas.Children.Add(posTb);

            WireJoystickEvents(thumb, canvas, j);
            _joyElems[j.Id] = (thumb, ring, label, posTb, canvas);
            return canvas;
        }

        // ═══ 共用组件 ═══

        private static readonly Brush JoyGreenBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // Material Green 500

        /// <summary>
        /// 生成彩色 X/Y 文字块。X 用主题蓝（PrimaryBrush），Y 用绿色。
        /// 放在大圆盘下方 pad+4 位置。
        /// </summary>
        /// <summary>只更新 X/Y 数值 Run，不破坏彩色格式</summary>
        private static void UpdatePosTextValues(TextBlock tb, JoystickViewModel j)
        {
            var inlines = tb.Inlines;
            if (inlines.Count < 4) return;
            // Inlines 结构：[0] "X " [1] X值 [2] "  "/LB [3] "Y " [4] Y值
            if (inlines.ElementAt(1) is Run xRun)
                xRun.Text = string.Format("{0:+0.00;-0.00; 0.00}", j.X);
            if (inlines.ElementAt(inlines.Count - 1) is Run yRun)
                yRun.Text = string.Format("{0:+0.00;-0.00; 0.00}", j.Y);
        }

        private TextBlock MakePosTextBlock(JoystickViewModel j, int padWidth, bool twoLine)
        {
            var primary = (Brush)FindResource("PrimaryBrush");
            var textBrush = (Brush)FindResource("TextSecondaryBrush");

            var tb = new TextBlock {
                FontSize = 13,
                FontFamily = new FontFamily("Sarasa Mono SC, Consolas, Courier New"),
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Width = padWidth,
            };
            Canvas.SetTop(tb, padWidth + 4);
            Canvas.SetLeft(tb, 0);

            // X 行
            var xLabel = new Run("X ") { Foreground = textBrush, FontSize = 11, FontWeight = FontWeights.Normal };
            var xVal  = new Run(string.Format("{0:+0.00;-0.00; 0.00}", j.X)) { Foreground = primary };
            tb.Inlines.Add(xLabel); tb.Inlines.Add(xVal);

            if (twoLine)
            {
                tb.Inlines.Add(new LineBreak());
            }
            else
            {
                tb.Inlines.Add(new Run("  ") { Foreground = textBrush });
            }

            // Y 行
            var yLabel = new Run("Y ") { Foreground = textBrush, FontSize = 11, FontWeight = FontWeights.Normal };
            var yVal   = new Run(string.Format("{0:+0.00;-0.00; 0.00}", j.Y)) { Foreground = JoyGreenBrush };
            tb.Inlines.Add(yLabel); tb.Inlines.Add(yVal);

            return tb;
        }

        private void WireJoystickEvents(FrameworkElement thumb, Canvas canvas, JoystickViewModel j)
        {
            int capturedId = j.Id;
            thumb.MouseLeftButtonDown += (s, e) => {
                SpringPress(s as FrameworkElement);
                if (e.ClickCount == 2) {
                    j.X = 0; j.Y = 0; SendJoystickValue(j); RefreshJoystickUI(); e.Handled = true; return;
                }
                _draggingJoyId = capturedId;
                thumb.CaptureMouse();
                _dragOrigin = e.GetPosition(canvas);
                e.Handled = true;
            };
            thumb.MouseMove += JoystickThumb_MouseMove;
            thumb.MouseLeftButtonUp += JoystickThumb_MouseUp;
        }

        /// <summary>
        /// 侧栏摇杆反馈——由 SendJoystickValue 传入已构造的协议消息，分行显示便于阅读。
        /// </summary>
        private void RefreshJoystickSideFeedback(string msg)
        {
            // 协议 [joystick,1,x1,y1,x2,y2] →
            //   [joystick,1,
            //    0.350,-0.420,   ← J1
            //    0.000, 0.000]   ← J2
            var match = System.Text.RegularExpressions.Regex.Match(msg,
                @"\[joystick,1,([^,]+),([^,]+),([^,]+),([^\]]+)\]");
            if (match.Success)
            {
                tbJoy1Feedback.Text = string.Format(
                    "[joystick,1,\n {0}, {1},\n {2},  {3}]",
                    match.Groups[1].Value, match.Groups[2].Value,
                    match.Groups[3].Value, match.Groups[4].Value);
            }
            else
            {
                tbJoy1Feedback.Text = msg;
            }
        }

        // ——— 拖拽逻辑 ———
        private void JoystickThumb_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingJoyId == 0) return;
            SwitchSidePanelToJoystick();
            var j = _joyVM.GetJoystick(_draggingJoyId); if (j == null) return;
            if (!_joyElems.TryGetValue(_draggingJoyId, out var elems)) return;

            var canvas = elems.pad;
            var pos = e.GetPosition(canvas);
            double half = canvas.Width / 2.0;
            double thumbR = 16;
            double maxR = half - thumbR;

            // 限制在圆形区域内
            double dx = pos.X - half, dy = half - pos.Y; // Y 轴反转
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist > maxR) { dx = dx / dist * maxR; dy = dy / dist * maxR; }

            j.X = Math.Round(dx / maxR, 3);
            j.Y = Math.Round(dy / maxR, 3);

            // 更新圆钮位置
            double tx = half + j.X * maxR, ty = half - j.Y * maxR;
            Canvas.SetLeft(elems.thumb, tx - thumbR); Canvas.SetTop(elems.thumb, ty - thumbR);
            // 更新方向线 / X 色条（全量重建更可靠，但为了流畅只移拇指）

            // 更新读数（不改 Inline 结构，只替换数值 Run）
            UpdatePosTextValues(elems.pos, j);

            // 节流发送
            var now = DateTime.Now;
            if ((now - _lastJoySent).TotalMilliseconds >= j.SendIntervalMs)
            {
                _lastJoySent = now;
                SendJoystickValue(j);
            }
        }

        private void JoystickThumb_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggingJoyId == 0) return;
            SpringRelease(sender as FrameworkElement);
            (sender as UIElement)?.ReleaseMouseCapture();
            var j = _joyVM.GetJoystick(_draggingJoyId);
            if (j != null) SendJoystickValue(j); // 松手发送最终值
            _draggingJoyId = 0;
        }

        private void SendJoystickValue(JoystickViewModel j)
        {
            if (_session == null || !_session.IsOpen) return;
            var j2 = _joyVM.GetJoystick(j.Id == 1 ? 2 : 1);
            double x2 = j2?.X ?? 0, y2 = j2?.Y ?? 0;
            string msg;
            if (j.Id == 1)
                msg = string.Format("[joystick,1,{0:F3},{1:F3},{2:F3},{3:F3}]", j.X, j.Y, x2, y2);
            else
                msg = string.Format("[joystick,1,{0:F3},{1:F3},{2:F3},{3:F3}]", x2, y2, j.X, j.Y);
            SendRaw(msg, appendLineEnding: true);
            // 侧栏协议反馈（直接传已构造的消息，不重复构造）
            Dispatcher.InvokeAsync(() => RefreshJoystickSideFeedback(msg));
        }

        private void btnJoystickCenter_Click(object sender, RoutedEventArgs e)
        {
            if (_joyVM == null) return;
            foreach (var j in _joyVM.Joysticks) { j.X = 0; j.Y = 0; SendJoystickValue(j); }
            RefreshJoystickUI(); SaveJoystickPrefs();
        }

        // ——— 设置 ———
        private void tbJoystickInterval_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_joyVM == null) return;
            if (int.TryParse(tbJoystickInterval.Text, out int v) && v >= 20)
            {
                foreach (var j in _joyVM.Joysticks) j.SendIntervalMs = v;
                SaveJoystickPrefs();
            }
            else tbJoystickInterval.Text = _joyVM.Joysticks.FirstOrDefault()?.SendIntervalMs.ToString() ?? "100";
        }

        // ═══════════════════════════════════════
        //  协议处理 & 持久化
        // ═══════════════════════════════════════

        private void HandleJoystickMessage(int id, double x, double y)
        {
            InitJoystickPanel();
            _joyVM.SetJoystickValues(id, x, y);
            Dispatcher.InvokeAsync(() => { RefreshJoystickUI(); });
        }

        private void SaveJoystickPrefs()
        {
            if (_joyVM == null || _prefsData == null) return;
            var arr = new System.Collections.ArrayList();
            foreach (var d in _joyVM.SerializeJoysticks()) arr.Add(d);
            _prefsData["joysticks"] = arr;
            _prefs.Save(_prefsData);
        }
    }
}
