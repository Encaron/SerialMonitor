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
        private Dictionary<int, (Ellipse thumb, Ellipse ring, TextBlock label, TextBlock pos, Canvas pad)> _joyElems
            = new Dictionary<int, (Ellipse, Ellipse, TextBlock, TextBlock, Canvas)>();
        private int _draggingJoyId;
        private Point _dragOrigin;
        private DateTime _lastJoySent;

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

            RefreshJoystickUI();
        }

        // ═══════════════════════════════════════
        //  摇杆 UI 构建
        // ═══════════════════════════════════════

        private void RefreshJoystickUI()
        {
            if (_joyVM == null) return;
            joystickPanel.Children.Clear(); _joyElems.Clear();

            foreach (var j in _joyVM.Joysticks)
            {
                int padSize = j.PadSize;
                double half = padSize / 2.0;
                double thumbR = 16;

                var canvas = new Canvas {
                    Width = padSize, Height = padSize,
                    Margin = new Thickness(j.Id == 1 ? 0 : 32, 0, 0, 0),
                    Background = Brushes.Transparent,
                };

                // 外圈
                var ring = new Ellipse {
                    Width = padSize, Height = padSize,
                    Stroke = (Brush)FindResource("CardBorderBrush"), StrokeThickness = 2,
                    Fill = (Brush)FindResource("SecondaryHoverBgBrush"),
                };
                Canvas.SetLeft(ring, 0); Canvas.SetTop(ring, 0);
                canvas.Children.Add(ring);

                // 十字线
                var hLine = new Line {
                    X1 = 10, Y1 = half, X2 = padSize - 10, Y2 = half,
                    Stroke = (Brush)FindResource("CardBorderBrush"), StrokeThickness = 0.5,
                };
                canvas.Children.Add(hLine);
                var vLine = new Line {
                    X1 = half, Y1 = 10, X2 = half, Y2 = padSize - 10,
                    Stroke = (Brush)FindResource("CardBorderBrush"), StrokeThickness = 0.5,
                };
                canvas.Children.Add(vLine);

                // 拖拽圆钮
                double tx = half + j.X * (half - thumbR);
                double ty = half - j.Y * (half - thumbR); // Y 轴向上为正
                var thumb = new Ellipse {
                    Width = thumbR * 2, Height = thumbR * 2,
                    Fill = (Brush)FindResource("PrimaryBrush"),
                    Stroke = Brushes.White, StrokeThickness = 2,
                    Cursor = Cursors.Hand,
                };
                Canvas.SetLeft(thumb, tx - thumbR); Canvas.SetTop(thumb, ty - thumbR);
                canvas.Children.Add(thumb);

                // 鼠标事件
                int capturedId = j.Id;
                thumb.MouseLeftButtonDown += (s, e) => {
                    SpringPress(s as FrameworkElement);
                    if (e.ClickCount == 2) {
                        // 双击回中
                        j.X = 0; j.Y = 0; SendJoystickValue(j); RefreshJoystickUI(); e.Handled = true; return;
                    }
                    _draggingJoyId = capturedId;
                    var t = s as Ellipse; t?.CaptureMouse();
                    _dragOrigin = e.GetPosition(canvas);
                    e.Handled = true;
                };
                thumb.MouseMove += JoystickThumb_MouseMove;
                thumb.MouseLeftButtonUp += JoystickThumb_MouseUp;

                // 编号标签
                var label = new TextBlock {
                    Text = "J" + j.Id, FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("TextMutedBrush"),
                    Margin = new Thickness(8, 4, 0, 0),
                };
                Canvas.SetLeft(label, 0); Canvas.SetTop(label, 0);
                canvas.Children.Add(label);

                // 位置读数
                var posText = string.Format("X:{0:+0.00;-0.00; 0.00}  Y:{1:+0.00;-0.00; 0.00}", j.X, j.Y);
                var posTb = new TextBlock {
                    Text = posText, FontSize = 10,
                    FontFamily = new FontFamily("Sarasa Mono SC, Consolas, Courier New"),
                    Foreground = (Brush)FindResource("TextSecondaryBrush"),
                    TextAlignment = TextAlignment.Center,
                };
                Canvas.SetLeft(posTb, 0); Canvas.SetTop(posTb, padSize - 20);
                posTb.Width = padSize;
                canvas.Children.Add(posTb);

                _joyElems[j.Id] = (thumb, ring, label, posTb, canvas);
                joystickPanel.Children.Add(canvas);
            }
            RefreshJoystickSideValues();
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
            var t = sender as Ellipse; t?.ReleaseMouseCapture();
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
