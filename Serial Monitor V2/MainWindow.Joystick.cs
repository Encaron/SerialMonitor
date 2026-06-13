using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        private enum JoyStyle { Gamepad, Minimal, Classic }
        private JoyStyle _joyStyle = JoyStyle.Gamepad;
        private const string JoyStyleKey = "joystickStyle";

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

            AddJoyStyleItem(menu, "手柄风", "同心参考圆 + 8 方向标记 + 方向指示线 + 3D 拇指", style);
            AddJoyStyleItem(menu, "极简风", "圆角底座 + 网格点阵 + X 色条指示器 + 扁平拇指", style);
            AddJoyStyleItem(menu, "经典风", "原版结构 + 50% 虚线圆 + 阴影拇指 + X/Y 分行", style);

            menu.IsOpen = true;
        }

        private void AddJoyStyleItem(ContextMenu menu, string label, string tooltip, Style style)
        {
            bool isCurrent = label.Contains(_joyStyle switch
            {
                JoyStyle.Gamepad  => "手柄风",
                JoyStyle.Minimal  => "极简风",
                _                 => "经典风",
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
                _joyStyle = capturedLabel switch
                {
                    string l when l.Contains("手柄风") => JoyStyle.Gamepad,
                    string l when l.Contains("极简风") => JoyStyle.Minimal,
                    _ => JoyStyle.Classic,
                };
                btnJoystickStyle.Content = _joyStyle switch
                {
                    JoyStyle.Gamepad  => "手柄风",
                    JoyStyle.Minimal  => "极简风",
                    _                 => "经典风",
                };
                RefreshJoystickUI();
                SaveJoystickStyleInPrefs();
            };
            menu.Items.Add(item);
        }

        private void LoadJoystickStyleFromPrefs()
        {
            if (_prefsData != null && _prefsData.TryGetValue(JoyStyleKey, out var v) && v is string s)
            {
                _joyStyle = s switch
                {
                    "Minimal"  => JoyStyle.Minimal,
                    "Classic"  => JoyStyle.Classic,
                    _          => JoyStyle.Gamepad,
                };
            }
            btnJoystickStyle.Content = _joyStyle switch
            {
                JoyStyle.Gamepad  => "手柄风",
                JoyStyle.Minimal  => "极简风",
                _                 => "经典风",
            };
        }

        private void SaveJoystickStyleInPrefs()
        {
            if (_prefsData == null) return;
            _prefsData[JoyStyleKey] = _joyStyle.ToString();
            _prefs.Save(_prefsData);
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
                    default:                canvas = BuildClassicStyle(j);  break;
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

            var canvas = new Canvas { Width = pad, Height = pad, Background = Brushes.Transparent };
            if (j.Id != 1) canvas.Margin = new Thickness(32, 0, 0, 0);

            var borderBrush = (Brush)FindResource("CardBorderBrush");
            var mutedBrush  = (Brush)FindResource("TextMutedBrush");

            // 暗色圆形底板
            var baseCircle = new Ellipse {
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

            // 拇指：径向渐变模拟 3D 凸起（代码创建 RadialGradientBrush）
            var thumbGrad = new RadialGradientBrush(
                Color.FromRgb(0x40, 0xA0, 0xFF),  // 中心亮蓝
                Color.FromRgb(0x0E, 0x63, 0x9C))   // 边缘暗蓝
            { GradientOrigin = new Point(0.35, 0.35) };
            var thumb = new Ellipse {
                Width = thumbR * 2, Height = thumbR * 2,
                Fill = thumbGrad,
                Stroke = Brushes.White, StrokeThickness = 1.5,
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect {
                    Color = Colors.Black, BlurRadius = 6, ShadowDepth = 2, Opacity = 0.4,
                },
            };
            Canvas.SetLeft(thumb, tx - thumbR); Canvas.SetTop(thumb, ty - thumbR);
            canvas.Children.Add(thumb);

            // J1/J2 标签
            var label = new TextBlock {
                Text = "J" + j.Id, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = mutedBrush, Margin = new Thickness(8, 4, 0, 0),
            };
            Canvas.SetLeft(label, 0); Canvas.SetTop(label, 0);
            canvas.Children.Add(label);

            // 数值
            var posTb = MakePosTextBlock(j, pad, pad - 18, centerX: true, twoLine: false);
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

            var canvas = new Canvas { Width = pad, Height = pad, Background = Brushes.Transparent };
            if (j.Id != 1) canvas.Margin = new Thickness(24, 0, 0, 0);

            var cardBg  = (Brush)FindResource("CardBgBrush");
            var border  = (Brush)FindResource("CardBorderBrush");
            var muted   = (Brush)FindResource("TextMutedBrush");
            var primary = (Brush)FindResource("PrimaryBrush");
            var secondary = (Brush)FindResource("TextSecondaryBrush");

            // 圆角方形底座
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

            // 拇指：干净扁平圆形 + 阴影
            double tx = half + j.X * maxR;
            double ty = half - j.Y * maxR;
            var thumb = new Ellipse {
                Width = thumbR * 2, Height = thumbR * 2,
                Fill = primary,
                Stroke = Brushes.White, StrokeThickness = 2,
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect {
                    Color = Colors.Black, BlurRadius = 8, ShadowDepth = 1, Opacity = 0.3,
                },
            };
            Canvas.SetLeft(thumb, tx - thumbR); Canvas.SetTop(thumb, ty - thumbR);
            canvas.Children.Add(thumb);

            // 水平色条（底部）— X 偏移可视化
            var xBarBg = new System.Windows.Shapes.Rectangle {
                Width = pad - 40, Height = 4, RadiusX = 2, RadiusY = 2,
                Fill = (Brush)FindResource("SecondaryHoverBgBrush"),
                Stroke = border, StrokeThickness = 0.5,
            };
            Canvas.SetLeft(xBarBg, 20); Canvas.SetTop(xBarBg, pad - 22);
            canvas.Children.Add(xBarBg);

            // 色条上的当前位置标记
            double xIndicatorX = 20 + (pad - 40) * (j.X + 1) / 2; // map [-1,1] → [20, pad-20]
            var xIndicator = new Ellipse {
                Width = 6, Height = 6,
                Fill = primary, Stroke = Brushes.White, StrokeThickness = 1,
            };
            Canvas.SetLeft(xIndicator, xIndicatorX - 3); Canvas.SetTop(xIndicator, pad - 23);
            canvas.Children.Add(xIndicator);

            // 标签
            var label = new TextBlock {
                Text = "J" + j.Id, FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = secondary, Margin = new Thickness(0, 0, 0, 0),
            };
            Canvas.SetLeft(label, 12); Canvas.SetTop(label, 6);
            canvas.Children.Add(label);

            // 数值（两行）
            var posTb = MakePosTextBlock(j, pad, pad - 30, centerX: true, twoLine: true);
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

            var canvas = new Canvas { Width = pad, Height = pad, Background = Brushes.Transparent };
            if (j.Id != 1) canvas.Margin = new Thickness(32, 0, 0, 0);

            var borderBrush = (Brush)FindResource("CardBorderBrush");
            var mutedBrush  = (Brush)FindResource("TextMutedBrush");

            // 外圈（保留原版）
            var ring = new Ellipse {
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

            // 拇指：带阴影
            double tx = half + j.X * maxR;
            double ty = half - j.Y * maxR;
            var thumb = new Ellipse {
                Width = thumbR * 2, Height = thumbR * 2,
                Fill = (Brush)FindResource("PrimaryBrush"),
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
            var posTb = MakePosTextBlock(j, pad, pad - 20, centerX: true, twoLine: true);
            canvas.Children.Add(posTb);

            WireJoystickEvents(thumb, canvas, j);
            _joyElems[j.Id] = (thumb, ring, label, posTb, canvas);
            return canvas;
        }

        // ═══ 共用组件 ═══

        private TextBlock MakePosTextBlock(JoystickViewModel j, int width, double top, bool centerX, bool twoLine)
        {
            string text = twoLine
                ? string.Format("X {0:+0.00;-0.00; 0.00}\nY {1:+0.00;-0.00; 0.00}", j.X, j.Y)
                : string.Format("X:{0:+0.00;-0.00; 0.00}  Y:{1:+0.00;-0.00; 0.00}", j.X, j.Y);
            var tb = new TextBlock {
                Text = text, FontSize = 10,
                FontFamily = new FontFamily("Sarasa Mono SC, Consolas, Courier New"),
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                TextAlignment = centerX ? TextAlignment.Center : TextAlignment.Left,
            };
            Canvas.SetLeft(tb, centerX ? 0 : 4);
            Canvas.SetTop(tb, top);
            if (centerX) tb.Width = width;
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

            // 更新读数（主区 + 侧面板）
            elems.pos.Text = string.Format("X:{0:+0.00;-0.00; 0.00}  Y:{1:+0.00;-0.00; 0.00}", j.X, j.Y);
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
