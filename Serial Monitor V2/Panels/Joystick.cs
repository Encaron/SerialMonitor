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

        private enum JoyStyle { Gamepad, Minimal, Classic, Custom }
        private JoyStyle _joyStyle = JoyStyle.Gamepad;
        private string _customJoyStyle = "";
        private const string JoyStyleKey = "joystickStyle";
        private const string JoyCustomStyleKey = "joystickCustomStyle";

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

        private void btnJoystickStyle_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu
            {
                PlacementTarget = sender as UIElement,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
            };
            var style = (Style)FindResource("ContextMenuMenuItemStyle");
            var sepStyle = (Style)FindResource("ContextMenuSeparatorStyle");

            // —— 三个内置风格（始终存在）——
            AddBuiltinJoyItem(menu, "手柄风", "同心参考圆 + 8 方向标记 + 方向指示线 + 3D 拇指", style);
            AddBuiltinJoyItem(menu, "极简风", "圆角底座 + 网格点阵 + X 色条指示器 + 扁平拇指", style);
            AddBuiltinJoyItem(menu, "经典风", "原版结构 + 50% 虚线圆 + 阴影拇指 + X/Y 分行", style);

            // —— 自动发现自定义风格（Icons/joystick/ 下 pad_xxx.png）——
            var customs = ScanCustomJoyStyles();
            if (customs.Count > 0)
            {
                menu.Items.Add(new Separator { Style = sepStyle });
                foreach (var name in customs)
                    AddCustomJoyItem(menu, name, style);
            }

            menu.IsOpen = true;
        }

        private void AddBuiltinJoyItem(ContextMenu menu, string label, string tooltip, Style style)
        {
            bool isCurrent = _joyStyle != JoyStyle.Custom && label.Contains(_joyStyle switch
            {
                JoyStyle.Gamepad => "手柄风", JoyStyle.Minimal => "极简风", _ => "经典风",
            });
            var item = new MenuItem
            {
                Header = (isCurrent ? "✓ " : "    ") + label,
                ToolTip = tooltip,
                Style = style,
            };
            string capturedLabel = label;
            item.Click += (s, e) =>
            {
                _joyStyle = capturedLabel.Contains("手柄风") ? JoyStyle.Gamepad
                          : capturedLabel.Contains("极简风") ? JoyStyle.Minimal
                          : JoyStyle.Classic;
                _customJoyStyle = "";
                btnJoystickStyle.Content = CurrentJoyStyleLabel();
                RefreshJoystickUI();
                SaveJoystickStyleInPrefs();
            };
            menu.Items.Add(item);
        }

        private void AddCustomJoyItem(ContextMenu menu, string name, Style style)
        {
            bool isCurrent = _joyStyle == JoyStyle.Custom && _customJoyStyle == name;
            var item = new MenuItem
            {
                Header = (isCurrent ? "✓ " : "    ") + name,
                ToolTip = $"自定义：Icons/joystick/pad_{name}.png + thumb_{name}.png",
                Style = style,
            };
            string captured = name;
            item.Click += (s, e) =>
            {
                _joyStyle = JoyStyle.Custom;
                _customJoyStyle = captured;
                btnJoystickStyle.Content = CurrentJoyStyleLabel();
                RefreshJoystickUI();
                SaveJoystickStyleInPrefs();
            };
            menu.Items.Add(item);
        }

        private string CurrentJoyStyleLabel()
        {
            if (_joyStyle == JoyStyle.Custom) return _customJoyStyle;
            return _joyStyle switch { JoyStyle.Gamepad => "手柄风", JoyStyle.Minimal => "极简风", _ => "经典风" };
        }

        private void LoadJoystickStyleFromPrefs()
        {
            if (_prefsData != null && _prefsData.TryGetValue(JoyStyleKey, out var v) && v is string s)
            {
                switch (s)
                {
                    case "Custom":
                        _joyStyle = JoyStyle.Custom;
                        if (_prefsData.TryGetValue(JoyCustomStyleKey, out var cs) && cs is string csStr)
                            _customJoyStyle = csStr;
                        break;
                    case "Minimal": _joyStyle = JoyStyle.Minimal; break;
                    case "Classic": _joyStyle = JoyStyle.Classic; break;
                    default:        _joyStyle = JoyStyle.Gamepad; break;
                }
            }
            btnJoystickStyle.Content = CurrentJoyStyleLabel();
        }

        private void SaveJoystickStyleInPrefs()
        {
            if (_prefsData == null) return;
            _prefsData[JoyStyleKey] = _joyStyle.ToString();
            if (_joyStyle == JoyStyle.Custom)
                _prefsData[JoyCustomStyleKey] = _customJoyStyle;
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

        /// <summary>
        /// 扫描 Icons/joystick/ 下 pad_*.png，提取自定义风格名（排除内置的 gamepad/minimal/classic）。
        /// 以后用户只需放 pad_xxx.png + thumb_xxx.png，下拉菜单自动出现。
        /// </summary>
        private static List<string> ScanCustomJoyStyles()
        {
            var result = new List<string>();
            try
            {
                var exeDir = AppContext.BaseDirectory;
                var joyDir = System.IO.Path.Combine(exeDir, "Icons", "joystick");
                if (!Directory.Exists(joyDir)) return result;

                var builtIn = new HashSet<string> { "gamepad", "minimal", "classic" };
                foreach (var file in Directory.GetFiles(joyDir, "pad_*.png"))
                {
                    var name = System.IO.Path.GetFileNameWithoutExtension(file); // "pad_xxx"
                    if (name.StartsWith("pad_") && name.Length > 4)
                        name = name.Substring(4); // "xxx"
                    if (!builtIn.Contains(name) && !result.Contains(name))
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
            foreach (var j in _joyVM.Joysticks)
            {
                Canvas canvas;
                switch (_joyStyle)
                {
                    case JoyStyle.Gamepad:  canvas = BuildGamepadStyle(j);  break;
                    case JoyStyle.Minimal:  canvas = BuildMinimalStyle(j);  break;
                    case JoyStyle.Classic:  canvas = BuildClassicStyle(j);  break;
                    default:                canvas = BuildCustomStyle(j);   break;
                }
                joystickPanel.Children.Add(canvas);
            }
            RefreshJoystickSideValues();
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

        // ═══════════════════════════════════════════════
        //  风格 D — 自定义（图片为底，零装饰）
        // ═══════════════════════════════════════════════

        private Canvas BuildCustomStyle(JoystickViewModel j)
        {
            int pad = j.PadSize;
            double half = pad / 2.0, thumbR = 16, maxR = half - thumbR;

            var canvas = new Canvas { Width = pad, Height = pad + 24, Background = Brushes.Transparent };
            if (j.Id != 1) canvas.Margin = new Thickness(32, 0, 0, 0);

            var borderBrush = (Brush)FindResource("CardBorderBrush");
            var mutedBrush  = (Brush)FindResource("TextMutedBrush");

            var (padImg, thumbImg) = LoadJoyImages(_customJoyStyle);

            // 底座：有图用图，没图回退 Gamepad 风格（安全默认）
            Ellipse ring;
            if (padImg != null)
            {
                ring = new Ellipse { Width = pad, Height = pad, Fill = padImg, Stroke = borderBrush, StrokeThickness = 2 };
                canvas.Children.Add(ring);
            }
            else
            {
                // 图片都没了（用户删了文件）→ 回退 Gamepad 码绘
                return BuildGamepadStyle(j);
            }

            // 拇指位置
            double tx = half + j.X * maxR;
            double ty = half - j.Y * maxR;

            // 拇指：有图用图，没图用 Gamepad 径向渐变
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
            Canvas.SetLeft(thumb, tx - thumbR); Canvas.SetTop(thumb, ty - thumbR);
            canvas.Children.Add(thumb);

            // 标签
            var label = new TextBlock {
                Text = "J" + j.Id, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = mutedBrush,
            };
            Canvas.SetLeft(label, 8); Canvas.SetTop(label, 4);
            canvas.Children.Add(label);

            // 数值
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

        private void RefreshJoystickSideValues()
        {
            if (_joyVM == null) return;
            var j1 = _joyVM.GetJoystick(1);
            var j2 = _joyVM.GetJoystick(2);
            if (j1 != null) tbJoy1Value.Text = string.Format("X: {0:+0.00;-0.00; 0.00}  Y: {1:+0.00;-0.00; 0.00}", j1.X, j1.Y);
            if (j2 != null) tbJoy2Value.Text = string.Format("X: {0:+0.00;-0.00; 0.00}  Y: {1:+0.00;-0.00; 0.00}", j2.X, j2.Y);
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
            RefreshJoystickSideValues();

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
