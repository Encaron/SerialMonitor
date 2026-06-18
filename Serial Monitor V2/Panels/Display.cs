using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace 串口助手
{
    public partial class MainWindow
    {
        // ——— OLED 面板 ———
        private DisplayPanelViewModel _displayVM;
        private Dictionary<string, TextBlock> _oledTexts = new Dictionary<string, TextBlock>();
        private List<UIElement> _drawElements = new List<UIElement>();

        // ——— #7 PC 鼠标画板 ———
        private enum DrawTool { None, Pencil, Line, Rect, Circle, Text, Eraser }
        private DrawTool _drawTool = DrawTool.None;
        private string _drawColor = "#FFFFFF";
        private int _drawLineWidth = 1;
        private bool _isLocked = true;
        private bool _isDrawing = false;
        private Ellipse _eraserCursor = null;
        private Point _drawStart;
        private UIElement _previewShape = null;
        private Point _lastSamplePoint;
        private DateTime _lastSampleTime;
        private readonly List<Button> _toolButtons = new List<Button>();

        // ——— 初始化 ———
        private void InitOLEDPanel()
        {
            if (_displayVM != null) return;
            _displayVM = new DisplayPanelViewModel();

            // 恢复尺寸
            if (_prefsData != null)
            {
                if (_prefsData.TryGetValue("oledWidth", out var wObj) && int.TryParse(wObj?.ToString(), out int pw))
                    _displayVM.CanvasWidth = pw;
                if (_prefsData.TryGetValue("oledHeight", out var hObj) && int.TryParse(hObj?.ToString(), out int ph))
                    _displayVM.CanvasHeight = ph;
            }

            // 初始化预设尺寸下拉
            cbOLEDPreset.Items.Clear();
            string[] presets = { "128×64", "128×128", "128×160", "240×240", "256×64", "256×128", "320×240", "480×320", "640×320", "800×480" };
            string[] tags    = { "128,64","128,128","128,160","240,240","256,64","256,128","320,240","480,320","640,320","800,480" };
            for (int i = 0; i < presets.Length; i++)
                cbOLEDPreset.Items.Add(new ComboBoxItem { Content = presets[i], Tag = tags[i] });
            string curTag = _displayVM.CanvasWidth + "," + _displayVM.CanvasHeight;
            cbOLEDPreset.SelectedItem = null;
            for (int i = 0; i < tags.Length; i++)
                if (tags[i] == curTag) { cbOLEDPreset.SelectedIndex = i; break; }

            // 初始化尺寸输入框
            tbOLEDWidth.Text = _displayVM.CanvasWidth.ToString();
            tbOLEDHeight.Text = _displayVM.CanvasHeight.ToString();

            RefreshOLEDUI();

            // ——— #7 工具栏初始化 ———
            // 线宽下拉
            cbLineWidth.Items.Clear();
            int[] widths = { 1, 2, 3, 5, 8 };
            foreach (var lw in widths)
                cbLineWidth.Items.Add(new ComboBoxItem { Content = lw.ToString(), Tag = lw });
            cbLineWidth.SelectedIndex = 0;

            // 工具按钮列表
            _toolButtons.Clear();
            _toolButtons.Add(btnToolPencil);
            _toolButtons.Add(btnToolLine);
            _toolButtons.Add(btnToolRect);
            _toolButtons.Add(btnToolCircle);
            _toolButtons.Add(btnToolText);
            _toolButtons.Add(btnToolEraser);

            // 画布鼠标事件（只挂一次）
            oledCanvas.MouseLeftButtonDown += OledCanvas_MouseLeftButtonDown;
            oledCanvas.MouseMove += OledCanvas_MouseMove;
            oledCanvas.MouseLeftButtonUp += OledCanvas_MouseLeftButtonUp;
            oledCanvas.MouseLeave += (s, e) => { if (_isDrawing) CancelDrawing(); };
        }

        // ═══════════════════════════════════════
        //  OLED 渲染
        // ═══════════════════════════════════════

        private void RefreshOLEDUI()
        {
            if (_displayVM == null) return;
            int w = _displayVM.CanvasWidth, h = _displayVM.CanvasHeight;
            oledCanvas.Width = w; oledCanvas.Height = h;
            oledCanvas.Children.Clear(); _oledTexts.Clear();

            bool hasItems = _displayVM.Items.Count > 0;
            oledEmptyHint.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;

            // ── 坐标轴刻度 ──
            var axisBrush = (Brush)FindResource("TextMutedBrush");
            var axisFont = new FontFamily("Sarasa Mono SC, Consolas, Courier New");

            oledXAxis.Children.Clear();
            oledXAxis.ColumnDefinitions.Clear();
            oledXAxis.ColumnDefinitions.Add(new ColumnDefinition());
            {
                var x0 = new TextBlock { Text = "0", FontSize = 9, Foreground = axisBrush, FontFamily = axisFont, HorizontalAlignment = HorizontalAlignment.Left };
                oledXAxis.Children.Add(x0);
                var xMid = new TextBlock { Text = (w / 2).ToString(), FontSize = 9, Foreground = axisBrush, FontFamily = axisFont, HorizontalAlignment = HorizontalAlignment.Center };
                oledXAxis.Children.Add(xMid);
                var xW = new TextBlock { Text = w.ToString(), FontSize = 9, Foreground = axisBrush, FontFamily = axisFont, HorizontalAlignment = HorizontalAlignment.Right };
                oledXAxis.Children.Add(xW);
            }

            oledYAxis.Children.Clear();
            oledYAxis.RowDefinitions.Clear();
            oledYAxis.RowDefinitions.Add(new RowDefinition());
            {
                var y0 = new TextBlock { Text = "0", FontSize = 9, Foreground = axisBrush, FontFamily = axisFont, VerticalAlignment = VerticalAlignment.Top };
                oledYAxis.Children.Add(y0);
                var yMid = new TextBlock { Text = (h / 2).ToString(), FontSize = 9, Foreground = axisBrush, FontFamily = axisFont, VerticalAlignment = VerticalAlignment.Center };
                oledYAxis.Children.Add(yMid);
                var yH = new TextBlock { Text = h.ToString(), FontSize = 9, Foreground = axisBrush, FontFamily = axisFont, VerticalAlignment = VerticalAlignment.Bottom };
                oledYAxis.Children.Add(yH);
            }

            // ── 用户数据文本 ──
            foreach (var item in _displayVM.Items)
            {
                Brush textBrush = ParseColorBrush(item.Color) ?? Brushes.White;
                var tb = new TextBlock {
                    Text = item.Text, FontSize = item.FontSize,
                    FontFamily = axisFont, Foreground = textBrush,
                };
                Canvas.SetLeft(tb, item.X);
                Canvas.SetTop(tb, item.Y);
                oledCanvas.Children.Add(tb);
                _oledTexts[item.X + "," + item.Y] = tb;
            }

            // ── 重绘图形（放在文本之上）──
            foreach (var shape in _drawElements)
                oledCanvas.Children.Add(shape);

            // ── 恢复背景色 ──
            SetCanvasBackground(_displayVM.CanvasBackground);
        }

        private static Brush ParseColorBrush(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return null;
            try
            {
                if (!hex.StartsWith("#")) hex = "#" + hex;
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }
            catch { return null; }
        }

        // ——— 操作 ———
        private void btnOLEDClear_Click(object sender, RoutedEventArgs e)
        {
            if (_displayVM == null) return;
            _displayVM.ClearAll();
            _drawElements.Clear();
            RefreshOLEDUI();
            if (_session != null && _session.IsOpen)
                SendRaw("[display-clear]", appendLineEnding: true);
        }

        private void cbOLEDPreset_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_displayVM == null || cbOLEDPreset.SelectedItem == null) return;
            var tag = (cbOLEDPreset.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (tag == null) return;
            var parts = tag.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
            {
                tbOLEDWidth.Text = w.ToString(); tbOLEDHeight.Text = h.ToString();
                ApplyOLEDSize();
            }
        }

        private void tbOLEDSize_LostFocus(object sender, RoutedEventArgs e) { ApplyOLEDSize(); }
        private void btnOLEDSizeApply_Click(object sender, RoutedEventArgs e) { ApplyOLEDSize(); }

        private void ApplyOLEDSize()
        {
            if (_displayVM == null) { InitOLEDPanel(); if (_displayVM == null) return; }
            if (!int.TryParse(tbOLEDWidth.Text, out int w) || w < 64 || w > 2000)
            { tbOLEDWidth.Text = _displayVM.CanvasWidth.ToString(); w = _displayVM.CanvasWidth; }
            if (!int.TryParse(tbOLEDHeight.Text, out int h) || h < 32 || h > 2000)
            { tbOLEDHeight.Text = _displayVM.CanvasHeight.ToString(); h = _displayVM.CanvasHeight; }
            _displayVM.CanvasWidth = w; _displayVM.CanvasHeight = h;
            oledCanvas.Width = w; oledCanvas.Height = h;
            RefreshOLEDUI(); SaveOLEDPrefs();
        }

        // ═══════════════════════════════════════
        //  协议处理 & 持久化
        // ═══════════════════════════════════════

        private void HandleDisplayMessage(int x, int y, string text, int fontSize, string color = null)
        {
            InitOLEDPanel();
            _displayVM.SetText(x, y, text, fontSize, color);
            Dispatcher.InvokeAsync(() => { RefreshOLEDUI(); });
        }

        private void HandleDisplayClear()
        {
            InitOLEDPanel();
            _displayVM.ClearAll();
            _drawElements.Clear();
            Dispatcher.InvokeAsync(() => { RefreshOLEDUI(); });
        }

        // ═══════════════════════════════════════
        //  #6 OLED 绘图指令 [draw,...]
        // ═══════════════════════════════════════

        /// <summary>路由绘图指令到对应 handler</summary>
        private void HandleDrawMessage(List<string> args)
        {
            if (args == null || args.Count == 0) return;
            InitOLEDPanel();

            string subType = args[0];
            Dispatcher.InvokeAsync(() =>
            {
                switch (subType)
                {
                    case "point":    HandleDrawPoint(args);    break;
                    case "line":     HandleDrawLine(args);     break;
                    case "rect":     HandleDrawRect(args);     break;
                    case "fill":     HandleDrawFill(args);     break;
                    case "circle":   HandleDrawCircle(args);   break;
                    case "ellipse":  HandleDrawEllipse(args);  break;
                    case "triangle": HandleDrawTriangle(args); break;
                    case "clear":    HandleDrawClearCmd(args); break;
                }
            });
        }

        // ── 画点 [draw,point,x,y,#RRGGBB] ──
        private void HandleDrawPoint(List<string> args)
        {
            if (args.Count < 3) return;
            if (!int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y)) return;
            string color = args.Count >= 4 ? args[3] : "#FFFFFF";

            var brush = ParseColorBrush(color) ?? Brushes.White;
            var pt = new Rectangle { Width = 1, Height = 1, Fill = brush };
            Canvas.SetLeft(pt, x);
            Canvas.SetTop(pt, y);

            oledCanvas.Children.Add(pt);
            _drawElements.Add(pt);
            _displayVM.DrawCommands.Add(new DrawCommand { Type = "point", Args = args, Color = color });
            oledEmptyHint.Visibility = Visibility.Collapsed;
        }

        // ── 画线 [draw,line,x1,y1,x2,y2,#RRGGBB,w] ──
        private void HandleDrawLine(List<string> args)
        {
            if (args.Count < 5) return;
            if (!int.TryParse(args[1], out int x1) || !int.TryParse(args[2], out int y1)
             || !int.TryParse(args[3], out int x2) || !int.TryParse(args[4], out int y2)) return;
            string color = args.Count >= 6 ? args[5] : "#FFFFFF";
            int lw = (args.Count >= 7 && int.TryParse(args[6], out int w)) ? w : 1;

            var brush = ParseColorBrush(color) ?? Brushes.White;
            var line = new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = brush, StrokeThickness = lw,
            };

            oledCanvas.Children.Add(line);
            _drawElements.Add(line);
            _displayVM.DrawCommands.Add(new DrawCommand { Type = "line", Args = args, Color = color });
            oledEmptyHint.Visibility = Visibility.Collapsed;
        }

        // ── 空心矩形 [draw,rect,x,y,w,h,#RRGGBB,w] ──
        private void HandleDrawRect(List<string> args)
        {
            if (args.Count < 5) return;
            if (!int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y)
             || !int.TryParse(args[3], out int w) || !int.TryParse(args[4], out int h)) return;
            string color = args.Count >= 6 ? args[5] : "#FFFFFF";
            int lw = (args.Count >= 7 && int.TryParse(args[6], out int sw)) ? sw : 1;

            var brush = ParseColorBrush(color) ?? Brushes.White;
            var rect = new Rectangle
            {
                Width = w, Height = h,
                Stroke = brush, StrokeThickness = lw,
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);

            oledCanvas.Children.Add(rect);
            _drawElements.Add(rect);
            _displayVM.DrawCommands.Add(new DrawCommand { Type = "rect", Args = args, Color = color });
            oledEmptyHint.Visibility = Visibility.Collapsed;
        }

        // ── 实心填充 [draw,fill,x,y,w,h,#RRGGBB] ──
        private void HandleDrawFill(List<string> args)
        {
            if (args.Count < 5) return;
            if (!int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y)
             || !int.TryParse(args[3], out int w) || !int.TryParse(args[4], out int h)) return;
            string color = args.Count >= 6 ? args[5] : "#FFFFFF";

            var brush = ParseColorBrush(color) ?? Brushes.White;
            var rect = new Rectangle
            {
                Width = w, Height = h,
                Fill = brush,
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);

            oledCanvas.Children.Add(rect);
            _drawElements.Add(rect);
            _displayVM.DrawCommands.Add(new DrawCommand { Type = "fill", Args = args, Color = color });
            oledEmptyHint.Visibility = Visibility.Collapsed;
        }

        // ── 空心圆 [draw,circle,cx,cy,r,#RRGGBB,w] ──
        private void HandleDrawCircle(List<string> args)
        {
            if (args.Count < 4) return;
            if (!int.TryParse(args[1], out int cx) || !int.TryParse(args[2], out int cy)
             || !int.TryParse(args[3], out int r)) return;
            string color = args.Count >= 5 ? args[4] : "#FFFFFF";
            int lw = (args.Count >= 6 && int.TryParse(args[5], out int sw)) ? sw : 1;

            var brush = ParseColorBrush(color) ?? Brushes.White;
            var circle = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Stroke = brush, StrokeThickness = lw,
            };
            Canvas.SetLeft(circle, cx - r);
            Canvas.SetTop(circle, cy - r);

            oledCanvas.Children.Add(circle);
            _drawElements.Add(circle);
            _displayVM.DrawCommands.Add(new DrawCommand { Type = "circle", Args = args, Color = color });
            oledEmptyHint.Visibility = Visibility.Collapsed;
        }

        // ── 空心椭圆 [draw,ellipse,x,y,a,b,#RRGGBB,w] ──
        private void HandleDrawEllipse(List<string> args)
        {
            if (args.Count < 5) return;
            if (!int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y)
             || !int.TryParse(args[3], out int a) || !int.TryParse(args[4], out int b)) return;
            string color = args.Count >= 6 ? args[5] : "#FFFFFF";
            int lw = (args.Count >= 7 && int.TryParse(args[6], out int sw)) ? sw : 1;

            var brush = ParseColorBrush(color) ?? Brushes.White;
            var ellipse = new Ellipse
            {
                Width = a * 2, Height = b * 2,
                Stroke = brush, StrokeThickness = lw,
            };
            Canvas.SetLeft(ellipse, x - a);
            Canvas.SetTop(ellipse, y - b);

            oledCanvas.Children.Add(ellipse);
            _drawElements.Add(ellipse);
            _displayVM.DrawCommands.Add(new DrawCommand { Type = "ellipse", Args = args, Color = color });
            oledEmptyHint.Visibility = Visibility.Collapsed;
        }

        // ── 空心三角形 [draw,triangle,x0,y0,x1,y1,x2,y2,#RRGGBB,w] ──
        private void HandleDrawTriangle(List<string> args)
        {
            if (args.Count < 7) return;
            if (!int.TryParse(args[1], out int x0) || !int.TryParse(args[2], out int y0)
             || !int.TryParse(args[3], out int x1) || !int.TryParse(args[4], out int y1)
             || !int.TryParse(args[5], out int x2) || !int.TryParse(args[6], out int y2)) return;
            string color = args.Count >= 8 ? args[7] : "#FFFFFF";
            int lw = (args.Count >= 9 && int.TryParse(args[8], out int sw)) ? sw : 1;

            var brush = ParseColorBrush(color) ?? Brushes.White;
            var triangle = new Polygon
            {
                Points = new PointCollection { new Point(x0, y0), new Point(x1, y1), new Point(x2, y2) },
                Stroke = brush, StrokeThickness = lw,
            };

            oledCanvas.Children.Add(triangle);
            _drawElements.Add(triangle);
            _displayVM.DrawCommands.Add(new DrawCommand { Type = "triangle", Args = args, Color = color });
            oledEmptyHint.Visibility = Visibility.Collapsed;
        }

        // ── 清屏 [draw,clear] 或 [draw,clear,#RRGGBB] ──
        private void HandleDrawClearCmd(List<string> args)
        {
            string color = args.Count >= 2 ? args[1] : "#111111";

            // 移除所有图形
            foreach (var shape in _drawElements)
                oledCanvas.Children.Remove(shape);
            _drawElements.Clear();

            // 清 VM
            _displayVM.ClearAll();

            // 更新背景色
            _displayVM.CanvasBackground = color;
            SetCanvasBackground(color);

            // 重建文本（RefreshOLEDUI 会重绘所有内容，但 drawElements 已空所以只重建文本）
            RefreshOLEDUI();
        }

        /// <summary>设置画布背景色</summary>
        private void SetCanvasBackground(string hex)
        {
            var brush = ParseColorBrush(hex);
            if (brush != null)
                oledCanvasBorder.Background = brush;
        }

        private void SaveOLEDPrefs()
        {
            if (_displayVM == null || _prefsData == null) return;
            _prefsData["oledWidth"] = _displayVM.CanvasWidth;
            _prefsData["oledHeight"] = _displayVM.CanvasHeight;
            _prefs.Save(_prefsData);
        }

        // ═══════════════════════════════════════
        //  #7 PC 鼠标画板 —— 工具栏 + 绘制交互
        // ═══════════════════════════════════════

        // ── 工具切换 ──
        private void SelectTool(DrawTool tool, Button activeBtn)
        {
            if (_isLocked) return;
            _drawTool = tool;
            foreach (var b in _toolButtons)
                b.Style = (Style)FindResource("SecondaryButtonStyle");
            if (activeBtn != null)
                activeBtn.Style = (Style)FindResource("PrimaryButtonStyle");
            oledCanvas.Cursor = tool == DrawTool.None ? Cursors.Arrow : Cursors.Cross;

            // 橡皮擦光标
            if (tool == DrawTool.Eraser)
                ShowEraserCursor();
            else
                HideEraserCursor();
        }

        private void ShowEraserCursor()
        {
            if (_eraserCursor != null) return;
            int r = _drawLineWidth * 4 + 2;
            _eraserCursor = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Stroke = (Brush)FindResource("TextPrimaryBrush"),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Fill = new SolidColorBrush(Colors.Transparent),
                IsHitTestVisible = false,
            };
            oledCanvas.Children.Add(_eraserCursor);
            Canvas.SetZIndex(_eraserCursor, 999);
        }

        private void HideEraserCursor()
        {
            if (_eraserCursor == null) return;
            oledCanvas.Children.Remove(_eraserCursor);
            _eraserCursor = null;
        }

        private void UpdateEraserCursor(Point pos)
        {
            if (_eraserCursor == null) return;
            int r = _drawLineWidth * 4 + 2;
            _eraserCursor.Width = r * 2;
            _eraserCursor.Height = r * 2;
            Canvas.SetLeft(_eraserCursor, pos.X - r);
            Canvas.SetTop(_eraserCursor, pos.Y - r);
        }

        private void btnToolPencil_Click(object sender, RoutedEventArgs e)  { SelectTool(DrawTool.Pencil,  btnToolPencil); }
        private void btnToolLine_Click(object sender, RoutedEventArgs e)    { SelectTool(DrawTool.Line,    btnToolLine); }
        private void btnToolRect_Click(object sender, RoutedEventArgs e)    { SelectTool(DrawTool.Rect,    btnToolRect); }
        private void btnToolCircle_Click(object sender, RoutedEventArgs e)  { SelectTool(DrawTool.Circle,  btnToolCircle); }
        private void btnToolText_Click(object sender, RoutedEventArgs e)    { SelectTool(DrawTool.Text,    btnToolText); }
        private void btnToolEraser_Click(object sender, RoutedEventArgs e)  { SelectTool(DrawTool.Eraser,  btnToolEraser); }

        // ── 锁定 / 解锁 ──
        private void btnLockCanvas_Click(object sender, RoutedEventArgs e)
        {
            _isLocked = !_isLocked;
            btnLockCanvas.Content = _isLocked ? "🔒" : "🔓";
            btnLockCanvas.ToolTip = _isLocked ? "解锁画布" : "锁定画布";
            if (_isLocked)
            {
                btnLockCanvas.Foreground = (Brush)FindResource("PrimaryBrush");
                SelectTool(DrawTool.None, null);
                oledCanvas.Cursor = Cursors.Arrow;
            }
            else
            {
                btnLockCanvas.ClearValue(Control.ForegroundProperty);
                oledCanvas.Cursor = Cursors.Cross;
            }
        }

        // ── 线宽 ──
        private void cbLineWidth_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (cbLineWidth.SelectedItem is ComboBoxItem item && item.Tag is int w)
            {
                _drawLineWidth = w;
                // 橡皮擦光标同步大小
                if (_eraserCursor != null)
                {
                    int r = w * 4 + 2;
                    _eraserCursor.Width = r * 2;
                    _eraserCursor.Height = r * 2;
                }
            }
        }

        // ── 颜色 ──
        private void btnDrawColor_Click(object sender, RoutedEventArgs e)
        {
            ShowDrawColorPicker(btnDrawColor, hex =>
            {
                _drawColor = hex;
                var brush = ParseColorBrush(hex);
                if (brush != null) colorSwatch.Background = brush;
            });
        }

        /// <summary>画板专用颜色选择——40色块点即关，hex手输走确认</summary>
        private void ShowDrawColorPicker(FrameworkElement placementTarget, Action<string> onColorPicked)
        {
            string[] palette = {
                "#F44336","#E91E63","#9C27B0","#673AB7","#3F51B5","#2196F3","#03A9F4","#00BCD4",
                "#009688","#4CAF50","#8BC34A","#CDDC39","#FFEB3B","#FFC107","#FF9800","#FF5722",
                "#795548","#9E9E9E","#607D8B","#555555","#FFFFFF","#FF4081","#7C4DFF","#536DFE",
                "#448AFF","#40C4FF","#18FFFF","#64FFDA","#69F0AE","#B2FF59","#EEFF41","#FFD740",
                "#FFAB40","#FF6E40","#FF8A80","#EA80FC","#B388FF","#8C9EFF","#80D8FF","#A7FFEB",
            };

            string currentHex = _drawColor;
            var fg = (Brush)FindResource("TextPrimaryBrush");
            var popup = new Popup();

            var border = new Border {
                Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10), MaxWidth = 244,
            };
            var stack = new StackPanel();

            // 40 色块
            var colorGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 8, Margin = new Thickness(0, 0, 0, 8) };
            foreach (var hex in palette)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var swatch = new Border {
                    Width = 24, Height = 24, CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(color),
                    BorderBrush = (Brush)FindResource("CardBorderBrush"),
                    BorderThickness = new Thickness(1), Margin = new Thickness(1),
                    Cursor = Cursors.Hand, Tag = hex,
                };
                swatch.MouseLeftButtonDown += (s, ev) =>
                {
                    popup.IsOpen = false;
                    onColorPicked(hex);
                    ev.Handled = true;
                };
                colorGrid.Children.Add(swatch);
            }
            stack.Children.Add(colorGrid);

            // Hex 输入行
            var hexRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var previewSwatch = new Border {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(currentHex)),
                BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left,
            };
            Grid.SetColumn(previewSwatch, 0);
            hexRow.Children.Add(previewSwatch);
            var hexBox = new TextBox {
                Text = currentHex, FontFamily = new FontFamily("Consolas"),
                FontSize = 12, Height = 24, Padding = new Thickness(6, 2, 6, 2),
                Foreground = fg, Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(hexBox, 1);
            hexRow.Children.Add(hexBox);
            hexBox.TextChanged += (_, __) =>
            {
                currentHex = hexBox.Text;
                try { previewSwatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(currentHex)); }
                catch { }
            };
            stack.Children.Add(hexRow);

            // 确认 / 取消
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancelBtn = new Button { Content = "取消", Style = (Style)FindResource("SecondaryButtonStyle"),
                Height = 26, MinWidth = 52, FontSize = 12, Padding = new Thickness(8, 0, 8, 0) };
            var confirmBtn = new Button { Content = "确认", Style = (Style)FindResource("PrimaryButtonStyle"),
                Height = 26, MinWidth = 52, FontSize = 12, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(8, 0, 0, 0) };
            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(confirmBtn);
            stack.Children.Add(btnRow);

            border.Child = stack;
            popup.Child = border;
            popup.AllowsTransparency = true;
            popup.PlacementTarget = placementTarget;
            popup.Placement = PlacementMode.Right;
            popup.StaysOpen = false;
            popup.PopupAnimation = PopupAnimation.None;

            cancelBtn.Click += (_, __) => popup.IsOpen = false;
            confirmBtn.Click += (_, __) => { popup.IsOpen = false; onColorPicked(currentHex); };
            popup.IsOpen = true;
            hexBox.Focus();
            hexBox.SelectAll();
        }

        // ═══════════════════════════════════════
        //  鼠标事件
        // ═══════════════════════════════════════

        private void OledCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isLocked || _drawTool == DrawTool.None) return;
            InitOLEDPanel();
            var pos = e.GetPosition(oledCanvas);
            _drawStart = pos;
            _isDrawing = true;
            oledCanvas.CaptureMouse();

            switch (_drawTool)
            {
                case DrawTool.Pencil:
                case DrawTool.Eraser:
                {
                    // 自由绘制：首点
                    string color = _drawTool == DrawTool.Eraser ? _displayVM.CanvasBackground : _drawColor;
                    _lastSamplePoint = pos;
                    _lastSampleTime = DateTime.Now;
                    var ptArgs = new List<string> { "point",
                        ((int)pos.X).ToString(), ((int)pos.Y).ToString(), color };
                    HandleDrawPoint(ptArgs);       // 本地渲染
                    SendDrawCmd("point", ptArgs);  // 发串口
                    break;
                }

                case DrawTool.Line:
                case DrawTool.Rect:
                case DrawTool.Circle:
                    // 创建虚线预览
                    _previewShape = CreatePreviewShape(_drawTool, pos, pos);
                    if (_previewShape != null) oledCanvas.Children.Add(_previewShape);
                    break;

                case DrawTool.Text:
                    ShowTextInputPopup(pos);
                    _isDrawing = false;
                    break;
            }
        }

        private void OledCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pos = e.GetPosition(oledCanvas);

            // 橡皮擦光标跟随（非绘制中）
            if (_drawTool == DrawTool.Eraser && !_isDrawing)
                UpdateEraserCursor(pos);

            if (!_isDrawing) return;

            switch (_drawTool)
            {
                case DrawTool.Pencil:
                case DrawTool.Eraser:
                {
                    var now = DateTime.Now;
                    double dx = pos.X - _lastSamplePoint.X;
                    double dy = pos.Y - _lastSamplePoint.Y;
                    if ((now - _lastSampleTime).TotalMilliseconds < 30 || (dx * dx + dy * dy) < 4)
                        return; // 节流：30ms 且位移 ≥ 2px

                    string color = _drawTool == DrawTool.Eraser ? _displayVM.CanvasBackground : _drawColor;
                    var lineArgs = new List<string> { "line",
                        ((int)_lastSamplePoint.X).ToString(), ((int)_lastSamplePoint.Y).ToString(),
                        ((int)pos.X).ToString(), ((int)pos.Y).ToString(), color };
                    if (_drawLineWidth != 1) lineArgs.Add(_drawLineWidth.ToString());
                    HandleDrawLine(lineArgs);       // 本地渲染
                    SendDrawCmd("line", lineArgs);  // 发串口

                    _lastSamplePoint = pos;
                    _lastSampleTime = now;
                    break;
                }

                case DrawTool.Line:
                case DrawTool.Rect:
                case DrawTool.Circle:
                    UpdatePreviewShape(_previewShape, _drawTool, _drawStart, pos);
                    break;
            }
        }

        private void OledCanvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isDrawing) return;
            _isDrawing = false;
            oledCanvas.ReleaseMouseCapture();

            var pos = e.GetPosition(oledCanvas);

            switch (_drawTool)
            {
                case DrawTool.Pencil:
                case DrawTool.Eraser:
                    // 自由绘制：收尾点
                    break;

                case DrawTool.Line:
                case DrawTool.Rect:
                case DrawTool.Circle:
                    // 删预览 → 建正式图形 → 发协议
                    if (_previewShape != null)
                    {
                        oledCanvas.Children.Remove(_previewShape);
                        _previewShape = null;
                    }
                    // 最小尺寸过滤（拖太短算取消）
                    double w = Math.Abs(pos.X - _drawStart.X);
                    double h = Math.Abs(pos.Y - _drawStart.Y);
                    if (_drawTool == DrawTool.Line && Math.Sqrt(w * w + h * h) < 3) return;
                    if (_drawTool != DrawTool.Line && (w < 3 || h < 3)) return;
                    FinalizeShape(_drawTool, _drawStart, pos);
                    break;
            }
        }

        // ── 取消绘制（鼠标移出画布）──
        private void CancelDrawing()
        {
            if (!_isDrawing) return;
            _isDrawing = false;
            oledCanvas.ReleaseMouseCapture();
            if (_previewShape != null)
            {
                oledCanvas.Children.Remove(_previewShape);
                _previewShape = null;
            }
        }

        // ═══════════════════════════════════════
        //  预览虚线
        // ═══════════════════════════════════════

        private UIElement CreatePreviewShape(DrawTool tool, Point p1, Point p2)
        {
            var brush = ParseColorBrush(_drawColor) ?? Brushes.White;
            brush = brush.Clone(); brush.Opacity = 0.45;

            double x = Math.Min(p1.X, p2.X), y = Math.Min(p1.Y, p2.Y);
            double w = Math.Abs(p2.X - p1.X), h = Math.Abs(p2.Y - p1.Y);

            Shape shape = null;
            switch (tool)
            {
                case DrawTool.Line:
                    shape = new Line { X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
                        Stroke = brush, StrokeThickness = _drawLineWidth,
                        StrokeDashArray = new DoubleCollection { 4, 3 } };
                    break;
                case DrawTool.Rect:
                    shape = new Rectangle { Width = w, Height = h,
                        Stroke = brush, StrokeThickness = _drawLineWidth,
                        StrokeDashArray = new DoubleCollection { 4, 3 } };
                    Canvas.SetLeft(shape, x); Canvas.SetTop(shape, y);
                    break;
                case DrawTool.Circle:
                {
                    double cx = (p1.X + p2.X) / 2, cy = (p1.Y + p2.Y) / 2;
                    double rx = Math.Abs(p2.X - p1.X) / 2, ry = Math.Abs(p2.Y - p1.Y) / 2;
                    shape = new Ellipse { Width = rx * 2, Height = ry * 2,
                        Stroke = brush, StrokeThickness = _drawLineWidth,
                        StrokeDashArray = new DoubleCollection { 4, 3 } };
                    Canvas.SetLeft(shape, cx - rx); Canvas.SetTop(shape, cy - ry);
                    break;
                }
            }
            return shape;
        }

        private void UpdatePreviewShape(UIElement element, DrawTool tool, Point p1, Point p2)
        {
            if (element == null) return;
            double x = Math.Min(p1.X, p2.X), y = Math.Min(p1.Y, p2.Y);
            double w = Math.Abs(p2.X - p1.X), h = Math.Abs(p2.Y - p1.Y);

            switch (tool)
            {
                case DrawTool.Line when element is Line line:
                    line.X1 = p1.X; line.Y1 = p1.Y; line.X2 = p2.X; line.Y2 = p2.Y;
                    break;
                case DrawTool.Rect when element is Rectangle rect:
                    rect.Width = w; rect.Height = h;
                    Canvas.SetLeft(rect, x); Canvas.SetTop(rect, y);
                    break;
                case DrawTool.Circle when element is Ellipse ellipse:
                {
                    double cx = (p1.X + p2.X) / 2, cy = (p1.Y + p2.Y) / 2;
                    double rx = Math.Abs(p2.X - p1.X) / 2, ry = Math.Abs(p2.Y - p1.Y) / 2;
                    ellipse.Width = rx * 2; ellipse.Height = ry * 2;
                    Canvas.SetLeft(ellipse, cx - rx); Canvas.SetTop(ellipse, cy - ry);
                    break;
                }
            }
        }

        // ═══════════════════════════════════════
        //  定形 → 编码发送
        // ═══════════════════════════════════════

        private void FinalizeShape(DrawTool tool, Point p1, Point p2)
        {
            string color = _drawColor;
            string type; List<string> args;

            switch (tool)
            {
                case DrawTool.Line:
                    type = "line";
                    args = new List<string> { "line",
                        ((int)p1.X).ToString(), ((int)p1.Y).ToString(),
                        ((int)p2.X).ToString(), ((int)p2.Y).ToString(), color };
                    break;
                case DrawTool.Rect:
                {
                    int x = (int)Math.Min(p1.X, p2.X), y = (int)Math.Min(p1.Y, p2.Y);
                    int w = (int)Math.Abs(p2.X - p1.X), h = (int)Math.Abs(p2.Y - p1.Y);
                    type = "rect";
                    args = new List<string> { "rect", x.ToString(), y.ToString(), w.ToString(), h.ToString(), color };
                    break;
                }
                case DrawTool.Circle:
                {
                    int cx = (int)((p1.X + p2.X) / 2), cy = (int)((p1.Y + p2.Y) / 2);
                    int r = (int)Math.Max(Math.Abs(p2.X - p1.X), Math.Abs(p2.Y - p1.Y)) / 2;
                    type = "circle";
                    args = new List<string> { "circle", cx.ToString(), cy.ToString(), r.ToString(), color };
                    break;
                }
                default: return;
            }

            // 线宽写入协议
            if (_drawLineWidth != 1) args.Add(_drawLineWidth.ToString());

            // 渲染到画布
            switch (tool)
            {
                case DrawTool.Line:    HandleDrawLine(args);    break;
                case DrawTool.Rect:    HandleDrawRect(args);    break;
                case DrawTool.Circle:  HandleDrawCircle(args);  break;
            }
            // 发串口
            SendDrawCmd(type, args);
        }

        /// <summary>向串口发送 [draw,...] 协议</summary>
        private void SendDrawCmd(string type, List<string> args)
        {
            if (_session == null || !_session.IsOpen) return;
            string payload = "[draw," + string.Join(",", args) + "]";
            SendRaw(payload, appendLineEnding: true);
        }

        // ═══════════════════════════════════════
        //  文字输入 Popup
        // ═══════════════════════════════════════

        private void ShowTextInputPopup(Point canvasPos)
        {
            var popup = new Popup
            {
                PlacementTarget = oledCanvas,
                Placement = PlacementMode.RelativePoint,
                StaysOpen = true,
                AllowsTransparency = true,
            };
            popup.HorizontalOffset = canvasPos.X + 8;
            popup.VerticalOffset = canvasPos.Y;

            var bg = (Brush)FindResource("CardBgBrush");
            var fg = (Brush)FindResource("TextPrimaryBrush");
            var borderBrush = (Brush)FindResource("CardBorderBrush");

            var panel = new Border
            {
                Background = bg, BorderBrush = borderBrush, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(10),
                MinWidth = 200,
            };
            var stack = new StackPanel();
            var title = new TextBlock { Text = "输入文字", FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = fg, Margin = new Thickness(0, 0, 0, 6) };
            stack.Children.Add(title);

            var textBox = new TextBox { Height = 28, Margin = new Thickness(0, 0, 0, 6) };
            stack.Children.Add(textBox);

            var sizeStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            sizeStack.Children.Add(new TextBlock { Text = "字号:", FontSize = 12, Foreground = fg,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            var sizeBox = new TextBox { Text = "14", Width = 50, Height = 28 };
            sizeStack.Children.Add(sizeBox);
            stack.Children.Add(sizeStack);

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancelBtn = new Button { Content = "取消", Width = 60, Height = 26,
                Style = (Style)FindResource("SecondaryButtonStyle") };
            var okBtn = new Button { Content = "确定", Width = 60, Height = 26,
                Style = (Style)FindResource("PrimaryButtonStyle"), Margin = new Thickness(6, 0, 0, 0) };
            btnStack.Children.Add(cancelBtn);
            btnStack.Children.Add(okBtn);
            stack.Children.Add(btnStack);

            panel.Child = stack;
            popup.Child = panel;
            popup.IsOpen = true;

            textBox.Focus();

            // 关闭
            void Close() { popup.IsOpen = false; }

            okBtn.Click += (s, e) =>
            {
                string text = textBox.Text;
                if (string.IsNullOrWhiteSpace(text)) { Close(); return; }
                if (!int.TryParse(sizeBox.Text, out int fontSize) || fontSize < 8) fontSize = 14;
                string color = _drawColor;

                var displayArgs = new List<string> { ((int)canvasPos.X).ToString(),
                    ((int)canvasPos.Y).ToString(), text, fontSize.ToString(), color };
                Dispatcher.InvokeAsync(() => HandleDisplayMessage(
                    (int)canvasPos.X, (int)canvasPos.Y, text, fontSize, color));

                if (_session != null && _session.IsOpen)
                {
                    string cmd = string.Format("[display,{0},{1},{2},{3},{4}]",
                        (int)canvasPos.X, (int)canvasPos.Y, text, fontSize, color);
                    SendRaw(cmd, appendLineEnding: true);
                }
                Close();
            };
            cancelBtn.Click += (s, e) => Close();
        }

        // ═══════════════════════════════════════
        //  发送画布（全部图形重发）
        // ═══════════════════════════════════════

        private void btnSendCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null || !_session.IsOpen) return;
            if (_displayVM == null) return;

            // 先清屏
            SendRaw("[draw,clear," + _displayVM.CanvasBackground + "]", appendLineEnding: true);

            // 重发文本
            foreach (var item in _displayVM.Items)
            {
                string cmd = string.Format("[display,{0},{1},{2},{3},{4}]",
                    item.X, item.Y, item.Text, item.FontSize, item.Color ?? "#FFFFFF");
                SendRaw(cmd, appendLineEnding: true);
            }

            // 重发图形
            foreach (var cmd in _displayVM.DrawCommands)
            {
                string payload = "[draw," + string.Join(",", cmd.Args) + "]";
                SendRaw(payload, appendLineEnding: true);
            }
        }

        // ═══════════════════════════════════════
        //  导出 C 数组
        // ═══════════════════════════════════════

        private void btnExportC_Click(object sender, RoutedEventArgs e)
        {
            if (_displayVM == null) return;
            int w = _displayVM.CanvasWidth, h = _displayVM.CanvasHeight;

            // 光栅化画布
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(oledCanvas);

            int stride = w * 4;
            byte[] pixels = new byte[stride * h];
            rtb.CopyPixels(pixels, stride, 0);

            // 转为灰度 → 二值化 → 打包为 8-bit 字节
            var bits = new List<byte>();
            for (int row = 0; row < h; row += 8)
            {
                for (int col = 0; col < w; col++)
                {
                    byte b = 0;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        int py = row + bit;
                        if (py >= h) break;
                        int idx = py * stride + col * 4;
                        // 亮度阈值：>128 算亮
                        byte gray = (byte)((pixels[idx+1] + pixels[idx+2] + pixels[idx+3]) / 3);
                        if (gray > 128) b |= (byte)(1 << bit);
                    }
                    bits.Add(b);
                }
            }

            // 格式化为 C 数组字符串
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"// OLED 画布导出 — {w}×{h} 单色");
            sb.AppendLine($"const uint8_t logo[{bits.Count}] = {{");
            for (int i = 0; i < bits.Count; i++)
            {
                if (i % 16 == 0) sb.Append("    ");
                sb.Append($"0x{bits[i]:X2}");
                if (i < bits.Count - 1) sb.Append(",");
                if ((i + 1) % 16 == 0) sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("};");

            Clipboard.SetText(sb.ToString());
            LogSystem($"✓ 已导出 {w}×{h} 单色 C 数组到剪贴板（{bits.Count} 字节）");
        }
    }
}
