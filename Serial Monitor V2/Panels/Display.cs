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
        private enum DrawTool { None, Select, Pencil, Line, Rect, Circle, Text, Eraser }

        /// <summary>
        /// 控点角色。Phase 3 用 TopLeft~Center；Endpoint1~Vertex2 预留给线端点/三角顶点拖拽。
        /// </summary>
        private enum HandleRole
        {
            TopLeft, TopRight, BottomLeft, BottomRight, Center,
            Endpoint1, Endpoint2,      // 预留：线端点独立拖拽
            Vertex0, Vertex1, Vertex2   // 预留：三角形顶点独立拖拽
        }

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

        // ——— #7 Phase 3：选择 + 控点调整 ———
        private UIElement _selectedShape = null;
        private int _selectedIndex = -1;
        private string _selectedShapeDrawType = null; // circle ↔ uniform scale
        private readonly List<FrameworkElement> _controlPoints = new List<FrameworkElement>();
        private HandleRole _activeHandle = HandleRole.Center;
        private Point _selectDragOrigin;
        private Rect _originalBounds;
        private object _originalShapeState; // 拖拽前快照，防累积误差
        private bool _hasMoved = false;

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
            _toolButtons.Add(btnToolSelect);
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
            DeselectShape();
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
            DeselectShape();
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

            DeselectShape();

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
            oledCanvas.Cursor = (tool == DrawTool.None || tool == DrawTool.Select) ? Cursors.Arrow : Cursors.Cross;

            // 橡皮擦光标
            if (tool == DrawTool.Eraser)
                ShowEraserCursor();
            else
                HideEraserCursor();

            // 切换工具时取消选中
            if (tool != DrawTool.Select && _selectedShape != null)
                DeselectShape();
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
        private void btnToolSelect_Click(object sender, RoutedEventArgs e)  { SelectTool(DrawTool.Select,  btnToolSelect); }

        // ── 锁定 / 解锁 ──
        private void btnLockCanvas_Click(object sender, RoutedEventArgs e)
        {
            _isLocked = !_isLocked;
            btnLockCanvas.Content = _isLocked ? "🔒" : "🔓";
            btnLockCanvas.ToolTip = _isLocked ? "解锁画布" : "锁定画布";
            if (_isLocked)
            {
                btnLockCanvas.Foreground = (Brush)FindResource("PrimaryBrush");
                DeselectShape();
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

            // ——— Select 模式：选中 / 控点拖拽 ———
            if (_drawTool == DrawTool.Select)
            {
                // ① 已选中 → 先检查控点
                if (_selectedShape != null)
                {
                    var hitHandle = HitTestControlPoint(pos);
                    if (hitHandle != null)
                    {
                        _activeHandle = (HandleRole)hitHandle.Tag;
                        _selectDragOrigin = pos;
                        _originalBounds = GetShapeBounds(_selectedShape);
                        CaptureOriginalState(_selectedShape, out _originalShapeState);
                        _isDrawing = true;
                        _hasMoved = false;
                        oledCanvas.CaptureMouse();
                        return;
                    }
                }

                // ② 点击图形（带线宽容差，倒序遍历优先上层）
                int hitIndex;
                var hitShape = HitTestShape(pos, out hitIndex);

                if (hitShape != null)
                {
                    if (hitShape == _selectedShape)
                    {
                        // 点中已选图形 → 准备平移
                        _activeHandle = HandleRole.Center;
                        _selectDragOrigin = pos;
                        _originalBounds = GetShapeBounds(_selectedShape);
                        CaptureOriginalState(_selectedShape, out _originalShapeState);
                        _isDrawing = true;
                        _hasMoved = false;
                        oledCanvas.CaptureMouse();
                    }
                    else
                    {
                        // 新图形 → 切换选中
                        DeselectShape();
                        SelectShape(hitShape, hitIndex);
                    }
                }
                else
                {
                    // ③ 点空白 → 取消选中
                    DeselectShape();
                }
                return;
            }

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

            // ——— Select 模式：拖拽控点 / 平移 ———
            if (_drawTool == DrawTool.Select && _selectedShape != null)
            {
                double dx = pos.X - _selectDragOrigin.X;
                double dy = pos.Y - _selectDragOrigin.Y;
                if (!_hasMoved && Math.Abs(dx) < 3 && Math.Abs(dy) < 3)
                    return;
                _hasMoved = true;

                if (_activeHandle == HandleRole.Center)
                {
                    double totalDx = pos.X - _selectDragOrigin.X;
                    double totalDy = pos.Y - _selectDragOrigin.Y;
                    TranslateShapeFromOriginal(_selectedShape, totalDx, totalDy);
                }
                else
                {
                    Rect newBounds = CalculateResizeBounds(_activeHandle, _originalBounds, pos);
                    if (newBounds.Width >= 3 && newBounds.Height >= 3)
                        FitShapeToBounds(_selectedShape, newBounds, _originalBounds);
                }
                UpdateControlPoints(GetShapeBounds(_selectedShape));
                return;
            }

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

            // ——— Select 模式：确认调整 ———
            if (_drawTool == DrawTool.Select && _selectedShape != null)
            {
                if (_hasMoved)
                {
                    FinalizeShapeModification();
                }
                return;
            }

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
            // Select 模式鼠标移出 → 从快照恢复原始位置
            if (_drawTool == DrawTool.Select && _selectedShape != null && _hasMoved)
            {
                RestoreOriginalState(_selectedShape, _originalShapeState);
                UpdateControlPoints(GetShapeBounds(_selectedShape));
                _hasMoved = false;
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
            SyncAllShapesToDevice();
        }

        /// <summary>全量同步画布到设备（clear + 全部 display + 全部 draw）</summary>
        private void SyncAllShapesToDevice()
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
        //  #7 Phase 3：选择 + 控点调整
        // ═══════════════════════════════════════

        // ── 选中 / 取消 ──

        private void SelectShape(UIElement shape, int index)
        {
            _selectedShape = shape;
            _selectedIndex = index;
            _selectedShapeDrawType = (index >= 0 && index < _displayVM.DrawCommands.Count)
                ? _displayVM.DrawCommands[index].Type : null;
            var bounds = GetShapeBounds(shape);
            CreateControlPoints(bounds);
        }

        private void DeselectShape()
        {
            RemoveControlPoints();
            _selectedShape = null;
            _selectedIndex = -1;
            _selectedShapeDrawType = null;
            _originalShapeState = null;
            _hasMoved = false;
        }

        // ── 包围盒 ──

        private static Rect GetShapeBounds(UIElement shape)
        {
            if (shape is Line line)
            {
                double x = Math.Min(line.X1, line.X2);
                double y = Math.Min(line.Y1, line.Y2);
                double w = Math.Abs(line.X2 - line.X1);
                double h = Math.Abs(line.Y2 - line.Y1);
                return new Rect(x, y, w, h);
            }
            if (shape is Polygon tri)
            {
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                foreach (var p in tri.Points)
                {
                    if (p.X < minX) minX = p.X; if (p.Y < minY) minY = p.Y;
                    if (p.X > maxX) maxX = p.X; if (p.Y > maxY) maxY = p.Y;
                }
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            // Rectangle / Ellipse（以及 1px point 矩形）
            double left = Canvas.GetLeft(shape);
            double top = Canvas.GetTop(shape);
            double width = (shape as FrameworkElement)?.ActualWidth ?? 0;
            double height = (shape as FrameworkElement)?.ActualHeight ?? 0;
            if (width == 0 && shape is Rectangle r) { width = r.Width; height = r.Height; }
            if (width == 0 && shape is Ellipse e) { width = e.Width; height = e.Height; }
            return new Rect(left, top, width, height);
        }

        // ── 控点创建 / 更新 / 移除 ──

        private void CreateControlPoints(Rect bounds)
        {
            RemoveControlPoints();
            double size = 8;
            var fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#42A5F5"));
            var stroke = Brushes.White;

            // 4 角控点
            var corners = new (double X, double Y, HandleRole Role)[]
            {
                (bounds.Left - size/2, bounds.Top - size/2, HandleRole.TopLeft),
                (bounds.Right - size/2, bounds.Top - size/2, HandleRole.TopRight),
                (bounds.Left - size/2, bounds.Bottom - size/2, HandleRole.BottomLeft),
                (bounds.Right - size/2, bounds.Bottom - size/2, HandleRole.BottomRight),
            };

            foreach (var c in corners)
            {
                var cursor = (c.Role == HandleRole.TopLeft || c.Role == HandleRole.BottomRight)
                    ? Cursors.SizeNWSE : Cursors.SizeNESW;
                var rect = new Rectangle
                {
                    Width = size, Height = size,
                    Fill = fill, Stroke = stroke, StrokeThickness = 1,
                    Tag = c.Role,
                    Cursor = cursor,
                };
                Canvas.SetLeft(rect, c.X);
                Canvas.SetTop(rect, c.Y);
                Canvas.SetZIndex(rect, 1000);
                oledCanvas.Children.Add(rect);
                _controlPoints.Add(rect);
            }

            // 选中虚线框
            var outline = new Rectangle
            {
                Width = bounds.Width, Height = bounds.Height,
                Stroke = fill, StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(outline, bounds.Left);
            Canvas.SetTop(outline, bounds.Top);
            Canvas.SetZIndex(outline, 999);
            oledCanvas.Children.Add(outline);
            _controlPoints.Add(outline);
        }

        private void UpdateControlPoints(Rect bounds)
        {
            double size = 8;
            // 4 角控点（索引 0-3）
            var corners = new (double X, double Y)[]
            {
                (bounds.Left - size/2, bounds.Top - size/2),
                (bounds.Right - size/2, bounds.Top - size/2),
                (bounds.Left - size/2, bounds.Bottom - size/2),
                (bounds.Right - size/2, bounds.Bottom - size/2),
            };
            for (int i = 0; i < 4 && i < _controlPoints.Count; i++)
            {
                Canvas.SetLeft(_controlPoints[i], corners[i].X);
                Canvas.SetTop(_controlPoints[i], corners[i].Y);
            }
            // 虚线框（索引 4）
            if (_controlPoints.Count >= 5 && _controlPoints[4] is Rectangle outline)
            {
                outline.Width = bounds.Width;
                outline.Height = bounds.Height;
                Canvas.SetLeft(outline, bounds.Left);
                Canvas.SetTop(outline, bounds.Top);
            }
        }

        private void RemoveControlPoints()
        {
            foreach (var cp in _controlPoints)
                oledCanvas.Children.Remove(cp);
            _controlPoints.Clear();
        }

        // ── 命中测试（带线宽容差）──

        private FrameworkElement HitTestControlPoint(Point pos)
        {
            foreach (var cp in _controlPoints)
            {
                if (cp is Rectangle rect && cp.Tag is HandleRole)
                {
                    double x = Canvas.GetLeft(rect), y = Canvas.GetTop(rect);
                    if (pos.X >= x - 3 && pos.X <= x + rect.Width + 3
                     && pos.Y >= y - 3 && pos.Y <= y + rect.Height + 3)
                        return cp;
                }
            }
            return null;
        }

        private UIElement HitTestShape(Point pos, out int index)
        {
            // 倒序遍历（上层优先）
            for (int i = _drawElements.Count - 1; i >= 0; i--)
            {
                var shape = _drawElements[i];
                Rect bounds = GetShapeBounds(shape);
                double tol = (shape is Line l && l.StrokeThickness < 6) ? 6 : 4;
                bounds.Inflate(tol, tol);

                if (!bounds.Contains(pos)) continue;

                // 细线：检查到线段的距离
                if (shape is Line line && line.StrokeThickness < tol)
                {
                    double dist = DistanceToLineSegment(pos,
                        new Point(line.X1, line.Y1),
                        new Point(line.X2, line.Y2));
                    if (dist < tol) { index = i; return shape; }
                    continue;
                }

                // 空心形状（矩形/椭圆/多边形）：在扩展包围盒内即命中
                index = i;
                return shape;
            }
            index = -1;
            return null;
        }

        private static double DistanceToLineSegment(Point p, Point a, Point b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 0.001)
                return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
            double t = Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq));
            double nearX = a.X + t * dx, nearY = a.Y + t * dy;
            return Math.Sqrt((p.X - nearX) * (p.X - nearX) + (p.Y - nearY) * (p.Y - nearY));
        }

        // ── 原始状态快照（防累积误差）──

        /// <summary>拖拽前保存图形原始参数，后续所有计算从原始值出，不累积。</summary>
        private static void CaptureOriginalState(UIElement shape, out object state)
        {
            if (shape is Line line)
                state = (line.X1, line.Y1, line.X2, line.Y2);
            else if (shape is Polygon tri)
                state = tri.Points.ToArray();
            else // Rectangle / Ellipse
                state = (Canvas.GetLeft(shape), Canvas.GetTop(shape),
                    ((FrameworkElement)shape).Width, ((FrameworkElement)shape).Height);
        }

        /// <summary>从快照恢复图形到拖拽前状态。</summary>
        private static void RestoreOriginalState(UIElement shape, object state)
        {
            if (state == null) return;
            if (state is (double x1, double y1, double x2, double y2) && shape is Line line)
            { line.X1 = x1; line.Y1 = y1; line.X2 = x2; line.Y2 = y2; }
            else if (state is Point[] pts && shape is Polygon tri)
            { tri.Points = new PointCollection(pts); }
            else if (state is (double l, double t, double w, double h))
            { Canvas.SetLeft(shape, l); Canvas.SetTop(shape, t); ((FrameworkElement)shape).Width = w; ((FrameworkElement)shape).Height = h; }
        }

        // ── 拖拽操作 ──

        private Rect CalculateResizeBounds(HandleRole corner, Rect orig, Point mousePos)
        {
            double left = orig.Left, top = orig.Top, right = orig.Right, bottom = orig.Bottom;

            switch (corner)
            {
                case HandleRole.TopLeft:
                    left = Math.Min(mousePos.X, right - 3);
                    top = Math.Min(mousePos.Y, bottom - 3);
                    break;
                case HandleRole.TopRight:
                    right = Math.Max(mousePos.X, left + 3);
                    top = Math.Min(mousePos.Y, bottom - 3);
                    break;
                case HandleRole.BottomLeft:
                    left = Math.Min(mousePos.X, right - 3);
                    bottom = Math.Max(mousePos.Y, top + 3);
                    break;
                case HandleRole.BottomRight:
                    right = Math.Max(mousePos.X, left + 3);
                    bottom = Math.Max(mousePos.Y, top + 3);
                    break;
            }
            return new Rect(left, top, Math.Max(3, right - left), Math.Max(3, bottom - top));
        }

        /// <summary>从原始状态 + 新旧包围盒比值 计算图形新位置（不从当前值累乘，防累积误差）。</summary>
        private void FitShapeToBounds(UIElement shape, Rect newBounds, Rect oldBounds)
        {
            if (_originalShapeState == null) return;
            double nw = Math.Max(3, newBounds.Width), nh = Math.Max(3, newBounds.Height);
            double ow = Math.Max(1, oldBounds.Width), oh = Math.Max(1, oldBounds.Height);
            double scaleX = nw / ow, scaleY = nh / oh;

            if (_originalShapeState is (double rx1, double ry1, double rx2, double ry2) && shape is Line line)
            {
                line.X1 = newBounds.Left + (rx1 - oldBounds.Left) * scaleX;
                line.Y1 = newBounds.Top  + (ry1 - oldBounds.Top)  * scaleY;
                line.X2 = newBounds.Left + (rx2 - oldBounds.Left) * scaleX;
                line.Y2 = newBounds.Top  + (ry2 - oldBounds.Top)  * scaleY;
            }
            else if (_originalShapeState is Point[] pts && shape is Polygon tri)
            {
                var newPts = new PointCollection();
                foreach (var p in pts)
                    newPts.Add(new Point(
                        newBounds.Left + (p.X - oldBounds.Left) * scaleX,
                        newBounds.Top  + (p.Y - oldBounds.Top)  * scaleY));
                tri.Points = newPts;
            }
            else if (_originalShapeState is (double ol, double ot, double ow2, double oh2) && _selectedShapeDrawType == "circle")
            {
                double cxNew = newBounds.Left + nw / 2, cyNew = newBounds.Top + nh / 2;
                double rNew = Math.Min(nw, nh) / 2;
                var fe = shape as FrameworkElement;
                fe.Width = rNew * 2; fe.Height = rNew * 2;
                Canvas.SetLeft(shape, cxNew - rNew); Canvas.SetTop(shape, cyNew - rNew);
            }
            else if (_originalShapeState is (double ol2, double ot2, double ow3, double oh3))
            {
                var fe = shape as FrameworkElement;
                fe.Width  = ow3 * scaleX;
                fe.Height = oh3 * scaleY;
                Canvas.SetLeft(shape, newBounds.Left + (ol2 - oldBounds.Left) * scaleX);
                Canvas.SetTop (shape, newBounds.Top  + (ot2 - oldBounds.Top)  * scaleY);
            }
        }

        /// <summary>从原始状态 + 累计偏移量 平移图形（每帧重置再应用，避免增量浮点累积）。</summary>
        private void TranslateShapeFromOriginal(UIElement shape, double totalDx, double totalDy)
        {
            RestoreOriginalState(shape, _originalShapeState);
            if (_originalShapeState is (double x1, double y1, double x2, double y2) && shape is Line line)
            { line.X1 = x1 + totalDx; line.Y1 = y1 + totalDy; line.X2 = x2 + totalDx; line.Y2 = y2 + totalDy; }
            else if (_originalShapeState is Point[] pts && shape is Polygon tri)
            {
                var newPts = new PointCollection();
                foreach (var p in pts) newPts.Add(new Point(p.X + totalDx, p.Y + totalDy));
                tri.Points = newPts;
            }
            else if (_originalShapeState is (double l, double t, double w, double h))
            { Canvas.SetLeft(shape, l + totalDx); Canvas.SetTop(shape, t + totalDy); }
        }

        // ── 修改确认：协议重编码 + 同步 ──

        private void FinalizeShapeModification()
        {
            if (_selectedShape == null || _selectedIndex < 0) return;
            if (_selectedIndex >= _displayVM.DrawCommands.Count) return;

            var oldCmd = _displayVM.DrawCommands[_selectedIndex];
            var newArgs = ShapeToProtocolArgs(_selectedShape, oldCmd);
            if (newArgs == null) return;

            // 就地更新 DrawCommand
            _displayVM.DrawCommands[_selectedIndex] = new DrawCommand
            {
                Type = oldCmd.Type,
                Args = newArgs,
                Color = oldCmd.Color,
            };

            // 重建控点（位置可能已变）
            RemoveControlPoints();
            var bounds = GetShapeBounds(_selectedShape);
            CreateControlPoints(bounds);

            // Point 2 预留接口 —— 当前全量刷新，将来可切增量
            SyncShapeAfterModification(_selectedIndex, newArgs);
        }

        /// <summary>
        /// Point 2 预留接口：形状修改后同步到设备。
        /// 当前实现：全量刷新（clear + 全部 draw）。将来可改为增量协议（[draw,remove,N] + [draw,insert,N,...] 或 ID 索引）。
        /// </summary>
        private void SyncShapeAfterModification(int index, List<string> newArgs)
        {
            SyncAllShapesToDevice();
        }

        /// <summary>
        /// 从当前图形状态重生成协议参数。
        /// 将来 Line/Triangle 覆写后可用多态分派。
        /// </summary>
        private static List<string> ShapeToProtocolArgs(UIElement shape, DrawCommand oldCmd)
        {
            string type = oldCmd.Type;
            string color = oldCmd.Color;

            switch (type)
            {
                case "point":
                    return new List<string> { "point",
                        ((int)Canvas.GetLeft(shape)).ToString(),
                        ((int)Canvas.GetTop(shape)).ToString(), color };

                case "line":
                    var line = (Line)shape;
                    var lineArgs = new List<string> { "line",
                        ((int)line.X1).ToString(), ((int)line.Y1).ToString(),
                        ((int)line.X2).ToString(), ((int)line.Y2).ToString(), color };
                    if (line.StrokeThickness != 1) lineArgs.Add(((int)line.StrokeThickness).ToString());
                    return lineArgs;

                case "rect":
                    var rect = (System.Windows.Shapes.Rectangle)shape;
                    var rectArgs = new List<string> { "rect",
                        ((int)Canvas.GetLeft(rect)).ToString(), ((int)Canvas.GetTop(rect)).ToString(),
                        ((int)rect.Width).ToString(), ((int)rect.Height).ToString(), color };
                    if (rect.StrokeThickness != 1) rectArgs.Add(((int)rect.StrokeThickness).ToString());
                    return rectArgs;

                case "fill":
                    var fillRect = (System.Windows.Shapes.Rectangle)shape;
                    return new List<string> { "fill",
                        ((int)Canvas.GetLeft(fillRect)).ToString(), ((int)Canvas.GetTop(fillRect)).ToString(),
                        ((int)fillRect.Width).ToString(), ((int)fillRect.Height).ToString(), color };

                case "circle":
                    var circle = (Ellipse)shape;
                    int cx = (int)(Canvas.GetLeft(circle) + circle.Width / 2);
                    int cy = (int)(Canvas.GetTop(circle) + circle.Height / 2);
                    int r = (int)(Math.Max(circle.Width, circle.Height) / 2);
                    var circArgs = new List<string> { "circle", cx.ToString(), cy.ToString(), r.ToString(), color };
                    if (circle.Stroke != null && circle.StrokeThickness != 1) circArgs.Add(((int)circle.StrokeThickness).ToString());
                    return circArgs;

                case "ellipse":
                    var ellipse = (Ellipse)shape;
                    int ex = (int)(Canvas.GetLeft(ellipse) + ellipse.Width / 2);
                    int ey = (int)(Canvas.GetTop(ellipse) + ellipse.Height / 2);
                    int a = (int)(ellipse.Width / 2);
                    int b = (int)(ellipse.Height / 2);
                    var ellArgs = new List<string> { "ellipse", ex.ToString(), ey.ToString(), a.ToString(), b.ToString(), color };
                    if (ellipse.Stroke != null && ellipse.StrokeThickness != 1) ellArgs.Add(((int)ellipse.StrokeThickness).ToString());
                    return ellArgs;

                case "triangle":
                    var tri = (Polygon)shape;
                    var triArgs = new List<string> { "triangle",
                        ((int)tri.Points[0].X).ToString(), ((int)tri.Points[0].Y).ToString(),
                        ((int)tri.Points[1].X).ToString(), ((int)tri.Points[1].Y).ToString(),
                        ((int)tri.Points[2].X).ToString(), ((int)tri.Points[2].Y).ToString(), color };
                    if (tri.StrokeThickness != 1) triArgs.Add(((int)tri.StrokeThickness).ToString());
                    return triArgs;
            }
            return null;
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
