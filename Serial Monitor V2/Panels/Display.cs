using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        private enum DrawTool { None, Select, Pencil, Line, Rect, RoundedRect, Circle, Ellipse, Triangle, Arc, Text, Eraser }

        /// <summary>
        /// 控点角色。Phase 3 用 TopLeft~Center；Endpoint1~Vertex2 预留给线端点/三角顶点拖拽。
        /// </summary>
        private enum HandleRole
        {
            TopLeft, TopRight, BottomLeft, BottomRight, Center,
            Endpoint1, Endpoint2,      // F2 线端点独立拖拽
            Vertex0, Vertex1, Vertex2,  // F3 三角形顶点独立拖拽
            RotateHandle               // F4 旋转控点
        }

        private DrawTool _drawTool = DrawTool.None;
        private DrawTool _rectSubTool = DrawTool.Rect;        // 矩形按钮当前子工具
        private DrawTool _circleSubTool = DrawTool.Circle;     // 圆形按钮当前子工具
        private string _drawColor = "#FFFFFF";
        private int _drawLineWidth = 1;
        private int _roundedRectRadius = 8;  // 圆角矩形默认圆角
        private int _arcStartAngle = 0;      // 弧线默认起始角（OLED坐标系：右=0°，顺时针为正）
        private int _arcEndAngle = 180;      // 弧线默认终止角
        private bool _isLocked = true;
        private bool _isDrawing = false;
        private Ellipse _eraserCursor = null;
        private Point _drawStart;
        private UIElement _previewShape = null;
        private Point _lastSamplePoint;
        private DateTime _lastSampleTime;
        private readonly List<Button> _toolButtons = new List<Button>();
        private readonly HashSet<int> _lockedShapeIndices = new HashSet<int>(); // 单元素锁定

        // ——— 文字编辑（侧栏模式） ———
        private bool _isEditingText = false;
        private Point _textEditPos;
        private string _textEditColor = "#FFFFFF";
        private Border _textPreview = null;
        private string _editingTextOriginalKey = null; // 通过选择编辑已有文字时的原始 key

        // ——— #7 Phase 3：选择 + 控点调整 ———
        private UIElement _selectedShape = null;
        private int _selectedIndex = -1;
        private string _selectedShapeDrawType = null; // circle ↔ uniform scale
        private readonly List<FrameworkElement> _controlPoints = new List<FrameworkElement>();
        private HandleRole _activeHandle = HandleRole.Center;
        private Point _selectDragOrigin;
        private Rect _originalBounds;
        private object _originalShapeState; // 拖拽前快照，防累积误差
        private double _shapeRotationAngle = 0;   // F4 旋转：当前角度（度）
        private double _rotationStartAngle = 0;   // F4 旋转开始时的初始角度
        private bool _hasMoved = false;

        // ——— F1 形状属性面板 ———
        private bool _populatingShapeEditor = false;
        private string _shapeEditColor = "#FFFFFF";
        private readonly List<TextBox> _shapeFieldTextBoxes = new List<TextBox>(); // 动态字段 TextBox 列表

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
            // 工具按钮列表
            _toolButtons.Clear();
            _toolButtons.Add(btnToolSelect);
            _toolButtons.Add(btnToolPencil);
            _toolButtons.Add(btnToolLine);
            _toolButtons.Add(btnToolRect);
            _toolButtons.Add(btnToolCircle);
            _toolButtons.Add(btnToolTriangle);
            _toolButtons.Add(btnToolArc);
            _toolButtons.Add(btnToolText);
            _toolButtons.Add(btnToolEraser);

            // 锁屏按钮初始颜色（默认锁定=红色）
            btnLockCanvas.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53935"));

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

            // 文字预览强保护：先摘下来，Children.Clear 后再挂回去
            if (_isEditingText && _textPreview != null)
                oledCanvas.Children.Remove(_textPreview);

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

            // ── 收集 _drawElements 中已有的文字位置 ──
            var drawTextKeys = new HashSet<string>();
            foreach (var shape in _drawElements)
            {
                if (shape is TextBlock dtb)
                {
                    int dx = (int)Canvas.GetLeft(dtb), dy = (int)Canvas.GetTop(dtb);
                    drawTextKeys.Add($"{dx},{dy}");
                }
            }

            // ── 用户数据文本（跳过 _drawElements 中已有的文字）──
            foreach (var item in _displayVM.Items)
            {
                string key = $"{item.X},{item.Y}";
                if (drawTextKeys.Contains(key)) continue; // 已由 _drawElements 管理

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

            // ── 重绘图形（含新格式文字 TextBlock）──
            foreach (var shape in _drawElements)
                oledCanvas.Children.Add(shape);

            // ── 恢复文字预览（如果正在编辑）──
            if (_isEditingText && _textPreview != null)
                oledCanvas.Children.Add(_textPreview);

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

        private void tbOLEDSize_LostFocus(object sender, RoutedEventArgs e)
        {
            // 文字编辑期间设置面板已隐藏，不响应隐藏控件的失焦
            if (rightOLEDSettings.Visibility != Visibility.Visible) return;
            ApplyOLEDSize();
        }
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
            // 若同位置已有文字 → 从 _drawElements 移除旧项
            var oldKey = $"{x},{y}";
            for (int i = _drawElements.Count - 1; i >= 0; i--)
            {
                if (_drawElements[i] is TextBlock tb2 && (int)Canvas.GetLeft(tb2) == x && (int)Canvas.GetTop(tb2) == y)
                {
                    oledCanvas.Children.Remove(tb2);
                    _drawElements.RemoveAt(i);
                    if (i < _displayVM.DrawCommands.Count) _displayVM.DrawCommands.RemoveAt(i);
                    break;
                }
            }
            _oledTexts.Remove(oldKey);
            _displayVM.SetText(x, y, text, fontSize, color); // 保持 Items 同步（向后兼容）
            // 同时走 [draw,text] 新格式
            var args = new List<string> { "text", x.ToString(), y.ToString(), text, fontSize.ToString(), color ?? "#FFFFFF" };
            Dispatcher.InvokeAsync(() => { HandleDrawText(args); });
        }

        private void HandleDisplayClear()
        {
            InitOLEDPanel();
            _displayVM.ClearAll();
            _drawElements.Clear();
            _oledTexts.Clear();
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
                    case "text":     HandleDrawText(args);     break;
                    case "point":    HandleDrawPoint(args);    break;
                    case "line":     HandleDrawLine(args);     break;
                    case "rect":     HandleDrawRect(args);     break;
                    case "fill":
                        // 兼容旧协议 [draw,fill,x,y,w,h,#color] → 转为新格式 [draw,rect,x,y,w,h,#color,1,fill]
                        args[0] = "rect";
                        if (args.Count < 6) break;
                        args.Add("fill");
                        HandleDrawRect(args);
                        break;
                    case "circle":   HandleDrawCircle(args);      break;
                    case "ellipse":  HandleDrawEllipse(args);     break;
                    case "rrect":    HandleDrawRoundedRect(args);  break;
                    case "arc":      HandleDrawArc(args);          break;
                    case "triangle": HandleDrawTriangle(args);    break;
                    case "clear":    HandleDrawClearCmd(args);    break;
                }
            });
        }

        /// <summary>像素对齐渲染：防 1px 描边被 ClipToBounds 切掉半边</summary>
        private static void SnapShapeToPixel(Shape shape)
        {
            // 所有图形统一：SnapsToDevicePixels 对齐像素中心，保留抗锯齿（丝滑）
            // D15 原方案加过 EdgeMode.Aliased，但那会让斜线/曲边出现锯齿断裂
            shape.SnapsToDevicePixels = true;
        }

        // ── 文字 [draw,text,x,y,content,fontSize,#RRGGBB] ──
        private void HandleDrawText(List<string> args)
        {
            if (args.Count < 5) return;
            if (!int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y)) return;
            string content = args[3];
            if (!int.TryParse(args[4], out int fontSize)) return;
            string color = args.Count >= 6 ? args[5] : "#FFFFFF";

            var brush = ParseColorBrush(color) ?? Brushes.White;
            var tb = new TextBlock
            {
                Text = content,
                FontSize = fontSize,
                FontFamily = new FontFamily("Sarasa Mono SC, Consolas, Courier New"),
                Foreground = brush,
            };
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);

            oledCanvas.Children.Add(tb);
            _drawElements.Add(tb);
            _displayVM.DrawCommands.Add(new DrawCommand { Type = "text", Args = args, Color = color });
            oledEmptyHint.Visibility = Visibility.Collapsed;
        }

        // ── 画点 [draw,point,x,y,#RRGGBB] ──
        private void HandleDrawPoint(List<string> args)
        {
            if (args.Count < 3) return;
            if (!int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y)) return;
            string color = args.Count >= 4 ? args[3] : "#FFFFFF";

            var brush = ParseColorBrush(color) ?? Brushes.White;
            var pt = new Rectangle { Width = 1, Height = 1, Fill = brush };
            SnapShapeToPixel(pt);
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
            SnapShapeToPixel(line);

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
            bool isFilled = args.Count >= 7 && args[args.Count - 1] == "fill";

            var brush = ParseColorBrush(color) ?? Brushes.White;
            var rect = new Rectangle
            {
                Width = w, Height = h,
            };

            if (isFilled)
            {
                rect.Fill = brush;
            }
            else
            {
                int lw = (args.Count >= 7 && int.TryParse(args[6], out int sw) && sw >= 1) ? sw : 1;
                rect.Stroke = brush;
                rect.StrokeThickness = lw;
            }

            SnapShapeToPixel(rect);
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);

            oledCanvas.Children.Add(rect);
            _drawElements.Add(rect);
            _displayVM.DrawCommands.Add(new DrawCommand { Type = "rect", Args = args, Color = color });
            oledEmptyHint.Visibility = Visibility.Collapsed;
        }

        // ── 实心填充 [draw,fill,x,y,w,h,#RRGGBB] ──
        // ── 圆 [draw,circle,cx,cy,r,#RRGGBB,w] 或 [draw,circle,cx,cy,r,#RRGGBB,w,fill] ──
        private void HandleDrawCircle(List<string> args)
        {
            if (args.Count < 4) return;
            if (!int.TryParse(args[1], out int cx) || !int.TryParse(args[2], out int cy)
             || !int.TryParse(args[3], out int r)) return;
            string color = args.Count >= 5 ? args[4] : "#FFFFFF";
            bool isFilled = args.Count >= 6 && args[args.Count - 1] == "fill";

            var brush = ParseColorBrush(color) ?? Brushes.White;
            var circle = new Ellipse
            {
                Width = r * 2, Height = r * 2,
            };

            if (isFilled)
            {
                circle.Fill = brush;
            }
            else
            {
                int lw = (args.Count >= 6 && int.TryParse(args[5], out int sw) && sw >= 1) ? sw : 1;
                circle.Stroke = brush;
                circle.StrokeThickness = lw;
            }

            SnapShapeToPixel(circle);
            Canvas.SetLeft(circle, cx - r);
            Canvas.SetTop(circle, cy - r);

            oledCanvas.Children.Add(circle);
            _drawElements.Add(circle);
            _displayVM.DrawCommands.Add(new DrawCommand { Type = "circle", Args = args, Color = color });
            oledEmptyHint.Visibility = Visibility.Collapsed;
        }

        // ── 椭圆 [draw,ellipse,cx,cy,a,b,#RRGGBB,w] 或 [draw,ellipse,cx,cy,a,b,#RRGGBB,w,fill] ──
        private void HandleDrawEllipse(List<string> args)
        {
            if (args.Count < 5) return;
            if (!int.TryParse(args[1], out int cx) || !int.TryParse(args[2], out int cy)
             || !int.TryParse(args[3], out int a) || !int.TryParse(args[4], out int b)) return;
            string color = args.Count >= 6 ? args[5] : "#FFFFFF";
            bool isFilled = args.Count >= 7 && args[args.Count - 1] == "fill";

            var brush = ParseColorBrush(color) ?? Brushes.White;
            var ellipse = new Ellipse
            {
                Width = a * 2, Height = b * 2,
            };

            if (isFilled)
            {
                ellipse.Fill = brush;
            }
            else
            {
                int lw = (args.Count >= 7 && int.TryParse(args[6], out int sw) && sw >= 1) ? sw : 1;
                ellipse.Stroke = brush;
                ellipse.StrokeThickness = lw;
            }

            SnapShapeToPixel(ellipse);
            Canvas.SetLeft(ellipse, cx - a);
            Canvas.SetTop(ellipse, cy - b);

            oledCanvas.Children.Add(ellipse);
            _drawElements.Add(ellipse);
            _displayVM.DrawCommands.Add(new DrawCommand { Type = "ellipse", Args = args, Color = color });
            oledEmptyHint.Visibility = Visibility.Collapsed;
        }

        // ── 圆角矩形 [draw,rrect,x,y,w,h,#RRGGBB,w,radius] 或 [draw,rrect,x,y,w,h,#RRGGBB,w,radius,fill] ──
        private void HandleDrawRoundedRect(List<string> args)
        {
            if (args.Count < 5) return;
            if (!int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y)
             || !int.TryParse(args[3], out int w) || !int.TryParse(args[4], out int h)) return;
            string color = args.Count >= 6 ? args[5] : "#FFFFFF";
            int radius = 8;
            int lw = 1;
            bool isFilled = args.Count >= 8 && args[args.Count - 1] == "fill";

            // 解析可选参数：先拿线宽（索引6），再拿圆角半径（索引7）
            if (args.Count >= 7 && int.TryParse(args[6], out int sw) && sw >= 1) lw = sw;
            if (args.Count >= 8)
            {
                // 倒数第一个可能是 "fill"，倒数第二个可能是 radius
                int radiusIdx = isFilled ? args.Count - 2 : args.Count - 1;
                if (radiusIdx >= 7 && int.TryParse(args[radiusIdx], out int rr) && rr >= 0)
                    radius = rr;
            }

            var brush = ParseColorBrush(color) ?? Brushes.White;
            var rrect = new Rectangle
            {
                Width = w, Height = h,
                RadiusX = radius, RadiusY = radius,
            };

            if (isFilled)
            {
                rrect.Fill = brush;
            }
            else
            {
                rrect.Stroke = brush;
                rrect.StrokeThickness = lw;
            }

            SnapShapeToPixel(rrect);
            Canvas.SetLeft(rrect, x);
            Canvas.SetTop(rrect, y);

            oledCanvas.Children.Add(rrect);
            _drawElements.Add(rrect);
            _displayVM.DrawCommands.Add(new DrawCommand { Type = "rrect", Args = args, Color = color });
            oledEmptyHint.Visibility = Visibility.Collapsed;
        }

        // ── 弧线 [draw,arc,cx,cy,r,startAngle,endAngle,#RRGGBB,w] 或 [draw,arc,cx,cy,r,startAngle,endAngle,#RRGGBB,w,fill] ──
        private void HandleDrawArc(List<string> args)
        {
            if (args.Count < 6) return;
            if (!int.TryParse(args[1], out int cx) || !int.TryParse(args[2], out int cy)
             || !int.TryParse(args[3], out int r)) return;
            if (!int.TryParse(args[4], out int startAngle) || !int.TryParse(args[5], out int endAngle)) return;
            string color = args.Count >= 7 ? args[6] : "#FFFFFF";
            bool isFilled = args.Count >= 8 && args[args.Count - 1] == "fill";
            int lw = 1;
            if (!isFilled && args.Count >= 8 && int.TryParse(args[7], out int sw) && sw >= 1) lw = sw;

            var brush = ParseColorBrush(color) ?? Brushes.White;
            var arcPath = BuildArcPath(cx, cy, r, startAngle, endAngle, brush, lw, isFilled, preview: false);
            SnapShapeToPixel(arcPath);
            // Path 定位：Data 几何含绝对坐标，Canvas 原点设 (0,0)
            Canvas.SetLeft(arcPath, 0);
            Canvas.SetTop(arcPath, 0);

            oledCanvas.Children.Add(arcPath);
            _drawElements.Add(arcPath);
            _displayVM.DrawCommands.Add(new DrawCommand { Type = "arc", Args = args, Color = color });
            oledEmptyHint.Visibility = Visibility.Collapsed;
        }

        /// <summary>构建弧线 Path（OLED 坐标系：右=0°，顺时针为正，Y向下⟶WPF 坐标相同）</summary>
        private static System.Windows.Shapes.Path BuildArcPath(double cx, double cy, double r,
            double startAngle, double endAngle, Brush brush, double lw, bool filled, bool preview)
        {
            // OLED/WPF 共用坐标系：右=0°，顺时针为正（下=90°，左=±180°）
            double sweep = ((endAngle - startAngle) % 360 + 360) % 360;
            bool isLargeArc = sweep > 180;

            double startRad = startAngle * Math.PI / 180.0;
            double endRad = endAngle * Math.PI / 180.0;
            double sx = cx + r * Math.Cos(startRad);
            double sy = cy + r * Math.Sin(startRad);
            double ex = cx + r * Math.Cos(endRad);
            double ey = cy + r * Math.Sin(endRad);

            var sweepDir = SweepDirection.Clockwise;

            var geometry = new PathGeometry();

            if (filled)
            {
                // 扇形：圆心 → 起始弧点 → 弧线到终止弧点 → 闭合回圆心
                var fig = new PathFigure { StartPoint = new Point(cx, cy) };
                fig.Segments.Add(new LineSegment(new Point(sx, sy), true));
                fig.Segments.Add(new ArcSegment(new Point(ex, ey),
                    new Size(r, r), 0, isLargeArc, sweepDir, true));
                fig.IsClosed = true;
                geometry.Figures.Add(fig);
            }
            else
            {
                // 弧线段
                var fig = new PathFigure { StartPoint = new Point(sx, sy) };
                fig.Segments.Add(new ArcSegment(new Point(ex, ey),
                    new Size(r, r), 0, isLargeArc, sweepDir, true));
                geometry.Figures.Add(fig);
            }

            var path = new System.Windows.Shapes.Path
            {
                Data = geometry,
                Stroke = preview ? brush : null,
                StrokeThickness = lw,
                StrokeDashArray = preview ? new DoubleCollection { 4, 3 } : null,
            };

            if (!preview)
            {
                if (filled)
                    path.Fill = brush;
                else
                    path.Stroke = brush;
            }

            return path;
        }

        // ── 空心三角形 [draw,triangle,x0,y0,x1,y1,x2,y2,#RRGGBB,w] ──
        private void HandleDrawTriangle(List<string> args)
        {
            if (args.Count < 7) return;
            if (!int.TryParse(args[1], out int x0) || !int.TryParse(args[2], out int y0)
             || !int.TryParse(args[3], out int x1) || !int.TryParse(args[4], out int y1)
             || !int.TryParse(args[5], out int x2) || !int.TryParse(args[6], out int y2)) return;
            string color = args.Count >= 8 ? args[7] : "#FFFFFF";
            bool isFilled = args.Count >= 9 && args[args.Count - 1] == "fill";

            var brush = ParseColorBrush(color) ?? Brushes.White;
            var triangle = new Polygon
            {
                Points = new PointCollection { new Point(x0, y0), new Point(x1, y1), new Point(x2, y2) },
            };

            if (isFilled)
            {
                triangle.Fill = brush;
            }
            else
            {
                int lw = (args.Count >= 9 && int.TryParse(args[8], out int sw) && sw >= 1) ? sw : 1;
                triangle.Stroke = brush;
                triangle.StrokeThickness = lw;
            }

            SnapShapeToPixel(triangle);

            oledCanvas.Children.Add(triangle);
            _drawElements.Add(triangle);
            _displayVM.DrawCommands.Add(new DrawCommand { Type = "triangle", Args = args, Color = color });
            oledEmptyHint.Visibility = Visibility.Collapsed;
        }

        // ── 清屏 [draw,clear] 或 [draw,clear,#RRGGBB] ──
        private void HandleDrawClearCmd(List<string> args)
        {
            string color = args.Count >= 2 ? args[1] : "#111111";

            // 静默清理选中态（不保存、不同步——设备已清屏，回推图形会矛盾）
            RemoveControlPoints();
            _selectedShape = null;
            _selectedIndex = -1;
            _selectedShapeDrawType = null;
            _originalShapeState = null;
            _hasMoved = false;
            // 恢复侧栏
            if (rightOLEDShapeEditor.Visibility == Visibility.Visible)
            {
                rightOLEDShapeEditor.Visibility = Visibility.Collapsed;
                rightOLEDSettings.Visibility = Visibility.Visible;
                tbSidePanelTitle.Text = "OLED 设置";
            }

            // 移除所有图形
            foreach (var shape in _drawElements)
                oledCanvas.Children.Remove(shape);
            _drawElements.Clear();

            // 清锁定索引
            _lockedShapeIndices.Clear();

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

        /// <summary>纯抖动反馈（不带 toast），用于锁按钮等非复制场景</summary>
        private static void ShakeButton(Button btn)
        {
            btn.RenderTransformOrigin = new Point(0.5, 0.5);
            var st = new ScaleTransform(1, 1);
            btn.RenderTransform = st;
            var anim = new DoubleAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.FromMilliseconds(150)) };
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0,  KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(33))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0.90, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(75))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.03, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0,  KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
            btn.Dispatcher.BeginInvoke(new Action(() =>
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            }));
        }

        private void SelectTool(DrawTool tool, Button activeBtn)
        {
            if (_isLocked)
            {
                // 抖动锁按钮 → 视觉反馈（不带 toast）
                if (btnLockCanvas != null)
                    ShakeButton(btnLockCanvas);
                return;
            }
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
            // 离开 Text 工具时退出文字编辑
            if (tool != DrawTool.Text && _isEditingText)
                ExitTextEditMode(save: _editingTextOriginalKey != null); // 编辑已有文字 → 保存；新文字 → 丢弃
        }

        /// <summary>橡皮擦实际线宽（比绘制线宽粗，等于视觉光标半径）</summary>
        private int EraserLineWidth => Math.Max(5, _drawLineWidth * 2 + 2);

        private void ShowEraserCursor()
        {
            if (_eraserCursor != null) return;
            int r = EraserLineWidth / 2 + 1;
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
            int r = EraserLineWidth / 2 + 1;
            _eraserCursor.Width = r * 2;
            _eraserCursor.Height = r * 2;
            Canvas.SetLeft(_eraserCursor, pos.X - r);
            Canvas.SetTop(_eraserCursor, pos.Y - r);
        }

        private void btnToolPencil_Click(object sender, RoutedEventArgs e)  { SelectTool(DrawTool.Pencil,  btnToolPencil); }
        private void btnToolLine_Click(object sender, RoutedEventArgs e)    { SelectTool(DrawTool.Line,    btnToolLine); }
        private void btnToolRect_Click(object sender, RoutedEventArgs e)
        {
            if (_isLocked) { if (btnLockCanvas != null) ShakeButton(btnLockCanvas); return; }
            // 左键：已激活→弹出下拉换类型，未激活→选中当前子工具
            if (_drawTool == DrawTool.Rect || _drawTool == DrawTool.RoundedRect)
            {
                // 已激活 → 弹出下拉菜单
                if (sender is Button btn && btn.ContextMenu != null)
                {
                    btn.ContextMenu.PlacementTarget = btn;
                    btn.ContextMenu.Placement = PlacementMode.Bottom;
                    btn.ContextMenu.IsOpen = true;
                }
            }
            else
            {
                SelectTool(_rectSubTool, btnToolRect);
            }
        }
        private void btnToolCircle_Click(object sender, RoutedEventArgs e)
        {
            if (_isLocked) { if (btnLockCanvas != null) ShakeButton(btnLockCanvas); return; }
            if (_drawTool == DrawTool.Circle || _drawTool == DrawTool.Ellipse)
            {
                if (sender is Button btn && btn.ContextMenu != null)
                {
                    btn.ContextMenu.PlacementTarget = btn;
                    btn.ContextMenu.Placement = PlacementMode.Bottom;
                    btn.ContextMenu.IsOpen = true;
                }
            }
            else
            {
                SelectTool(_circleSubTool, btnToolCircle);
            }
        }
        private void btnToolTriangle_Click(object sender, RoutedEventArgs e){ SelectTool(DrawTool.Triangle,btnToolTriangle); }
        private void btnToolArc_Click(object sender, RoutedEventArgs e)      { SelectTool(DrawTool.Arc,     btnToolArc); }
        private void btnToolText_Click(object sender, RoutedEventArgs e)    { SelectTool(DrawTool.Text,    btnToolText); }
        private void btnToolEraser_Click(object sender, RoutedEventArgs e)  { SelectTool(DrawTool.Eraser,  btnToolEraser); }
        private void btnToolSelect_Click(object sender, RoutedEventArgs e)  { SelectTool(DrawTool.Select,  btnToolSelect); }

        // ── 矩形/圆形 子工具下拉菜单 ──
        private void SubToolRect_Click(object sender, RoutedEventArgs e)
        {
            _rectSubTool = DrawTool.Rect;
            btnToolRect.Content = "▭▾";
            SelectTool(DrawTool.Rect, btnToolRect);
        }
        private void SubToolRoundedRect_Click(object sender, RoutedEventArgs e)
        {
            _rectSubTool = DrawTool.RoundedRect;
            btnToolRect.Content = "▢▾";
            SelectTool(DrawTool.RoundedRect, btnToolRect);
        }
        private void SubToolCircle_Click(object sender, RoutedEventArgs e)
        {
            _circleSubTool = DrawTool.Circle;
            btnToolCircle.Content = "◯▾";
            SelectTool(DrawTool.Circle, btnToolCircle);
        }
        private void SubToolEllipse_Click(object sender, RoutedEventArgs e)
        {
            _circleSubTool = DrawTool.Ellipse;
            btnToolCircle.Content = "◭▾";
            SelectTool(DrawTool.Ellipse, btnToolCircle);
        }

        // ── 锁定 / 解锁 ──
        private void btnLockCanvas_Click(object sender, RoutedEventArgs e)
        {
            _isLocked = !_isLocked;
            btnLockCanvas.Content = _isLocked ? "🔒" : "🔓";
            btnLockCanvas.ToolTip = _isLocked ? "解锁画布" : "锁定画布";
            if (_isLocked)
            {
                btnLockCanvas.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53935"));
                DeselectShape();
                // 重置所有工具栏按钮为未选中样式（不走 SelectTool——它被 _isLocked 拦截）
                _drawTool = DrawTool.None;
                foreach (var b in _toolButtons)
                    b.Style = (Style)FindResource("SecondaryButtonStyle");
                oledCanvas.Cursor = Cursors.Arrow;
            }
            else
            {
                btnLockCanvas.Foreground = (Brush)FindResource("PrimaryBrush");
                oledCanvas.Cursor = Cursors.Cross;
            }
        }

        // ── 颜色 ──
        // 颜色选择已移至侧栏（文字编辑模式 + 将来图形属性面板）

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
                    // text 只支持平移和旋转，不响应角控点 resize
                    bool isText = _selectedShape is TextBlock;
                    if (hitHandle != null && (!isText || hitHandle.Tag is HandleRole r && r == HandleRole.RotateHandle))
                    {
                        _activeHandle = (HandleRole)hitHandle.Tag;
                        _selectDragOrigin = pos;
                        _originalBounds = GetShapeBounds(_selectedShape);
                        CaptureOriginalState(_selectedShape, out _originalShapeState);
                        // F4 旋转：保存初始角度（弧线用圆心，其他用包围盒中心）
                        if (_activeHandle == HandleRole.RotateHandle)
                        {
                            double refCx = _originalBounds.Left + _originalBounds.Width / 2;
                            double refCy = _originalBounds.Top + _originalBounds.Height / 2;
                            if (_selectedShapeDrawType == "arc" && _selectedIndex >= 0 && _selectedIndex < _displayVM.DrawCommands.Count)
                            {
                                var arcCmd = _displayVM.DrawCommands[_selectedIndex];
                                if (arcCmd.Args != null && arcCmd.Args.Count >= 3)
                                {
                                    refCx = double.Parse(arcCmd.Args[1]);
                                    refCy = double.Parse(arcCmd.Args[2]);
                                }
                            }
                            _rotationStartAngle = Math.Atan2(pos.Y - refCy, pos.X - refCx) * 180.0 / Math.PI;
                        }
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
                    // 文字编辑中？点空白保存退出
                    if (_isEditingText && _editingTextOriginalKey == null)
                        ExitTextEditMode(save: false);
                    else if (_isEditingText)
                        ExitTextEditMode(save: true);
                }
                return;
            }

            switch (_drawTool)
            {
                case DrawTool.Pencil:
                case DrawTool.Eraser:
                    _isDrawing = true;
                    oledCanvas.CaptureMouse();
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
                case DrawTool.RoundedRect:
                case DrawTool.Circle:
                case DrawTool.Ellipse:
                case DrawTool.Triangle:
                case DrawTool.Arc:
                    _isDrawing = true;
                    oledCanvas.CaptureMouse();
                    // 创建虚线预览
                    _previewShape = CreatePreviewShape(_drawTool, pos, pos);
                    if (_previewShape != null) oledCanvas.Children.Add(_previewShape);
                    break;

                case DrawTool.Text:
                    if (_isEditingText)
                        ExitTextEditMode(save: false); // 先退出上一位置
                    EnterTextEditMode(pos);
                    _isDrawing = false;
                    break;
            }
        }

        private void OledCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pos = e.GetPosition(oledCanvas);

            // 橡皮擦光标跟随（绘制中也跟，视觉反馈不中断）
            if (_drawTool == DrawTool.Eraser)
                UpdateEraserCursor(pos);

            if (!_isDrawing) return;

            // ——— Select 模式：拖拽控点 / 平移 ———
            if (_drawTool == DrawTool.Select && _selectedShape != null)
            {
                if (IsElementLocked()) return; // 锁定元素禁止拖拽
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
                else if (_activeHandle == HandleRole.RotateHandle)
                {
                    // F4 旋转：算角度增量（弧线用圆心，其他用包围盒中心）
                    double refCx = _originalBounds.Left + _originalBounds.Width / 2;
                    double refCy = _originalBounds.Top + _originalBounds.Height / 2;
                    if (_selectedShapeDrawType == "arc" && _selectedIndex >= 0 && _selectedIndex < _displayVM.DrawCommands.Count)
                    {
                        var arcCmd = _displayVM.DrawCommands[_selectedIndex];
                        if (arcCmd.Args != null && arcCmd.Args.Count >= 3)
                        {
                            refCx = double.Parse(arcCmd.Args[1]);
                            refCy = double.Parse(arcCmd.Args[2]);
                        }
                    }
                    double currentAngle = Math.Atan2(pos.Y - refCy, pos.X - refCx) * 180.0 / Math.PI;
                    double deltaAngle = currentAngle - _rotationStartAngle;
                    // 吸附：Shift 键时 15° 步进
                    if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        deltaAngle = Math.Round(deltaAngle / 15.0) * 15.0;
                    _shapeRotationAngle = deltaAngle;

                    // 弧线：RotateTransform 绕圆心（Canvas=0 时本地=画布坐标，RotateTransform(cx,cy) 正好）
                    if (_selectedShapeDrawType == "arc" && _selectedShape is System.Windows.Shapes.Path arcP5
                        && _selectedIndex >= 0 && _selectedIndex < _displayVM.DrawCommands.Count)
                    {
                        var arcCmd = _displayVM.DrawCommands[_selectedIndex];
                        if (arcCmd.Args != null && arcCmd.Args.Count >= 3)
                        {
                            double arcCx = double.Parse(arcCmd.Args[1]);
                            double arcCy = double.Parse(arcCmd.Args[2]);
                            _selectedShape.RenderTransform = new RotateTransform(deltaAngle, arcCx, arcCy);
                        }
                    }
                    else
                    {
                        _selectedShape.RenderTransformOrigin = new Point(0.5, 0.5);
                        _selectedShape.RenderTransform = new RotateTransform(deltaAngle);
                    }
                }
                else if (_activeHandle == HandleRole.Endpoint1 && _selectedShape is Line line1)
                {
                    double totalDx = pos.X - _selectDragOrigin.X;
                    double totalDy = pos.Y - _selectDragOrigin.Y;
                    RestoreOriginalState(_selectedShape, _originalShapeState);
                    if (_originalShapeState is (double x1, double y1, double x2, double y2))
                    { line1.X1 = x1 + totalDx; line1.Y1 = y1 + totalDy; }
                }
                else if (_activeHandle == HandleRole.Endpoint2 && _selectedShape is Line line2)
                {
                    double totalDx = pos.X - _selectDragOrigin.X;
                    double totalDy = pos.Y - _selectDragOrigin.Y;
                    RestoreOriginalState(_selectedShape, _originalShapeState);
                    if (_originalShapeState is (double x1, double y1, double x2, double y2))
                    { line2.X2 = x2 + totalDx; line2.Y2 = y2 + totalDy; }
                }
                else if (_activeHandle == HandleRole.Vertex0 && _selectedShape is Polygon tri0)
                {
                    double totalDx = pos.X - _selectDragOrigin.X;
                    double totalDy = pos.Y - _selectDragOrigin.Y;
                    RestoreOriginalState(_selectedShape, _originalShapeState);
                    if (_originalShapeState is Point[] orig)
                    { tri0.Points = new PointCollection { new Point(orig[0].X + totalDx, orig[0].Y + totalDy), orig[1], orig[2] }; }
                }
                else if (_activeHandle == HandleRole.Vertex1 && _selectedShape is Polygon tri1)
                {
                    double totalDx = pos.X - _selectDragOrigin.X;
                    double totalDy = pos.Y - _selectDragOrigin.Y;
                    RestoreOriginalState(_selectedShape, _originalShapeState);
                    if (_originalShapeState is Point[] orig)
                    { tri1.Points = new PointCollection { orig[0], new Point(orig[1].X + totalDx, orig[1].Y + totalDy), orig[2] }; }
                }
                else if (_activeHandle == HandleRole.Vertex2 && _selectedShape is Polygon tri2)
                {
                    double totalDx = pos.X - _selectDragOrigin.X;
                    double totalDy = pos.Y - _selectDragOrigin.Y;
                    RestoreOriginalState(_selectedShape, _originalShapeState);
                    if (_originalShapeState is Point[] orig)
                    { tri2.Points = new PointCollection { orig[0], orig[1], new Point(orig[2].X + totalDx, orig[2].Y + totalDy) }; }
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
                    int effectiveLw = _drawTool == DrawTool.Eraser ? EraserLineWidth : _drawLineWidth;
                    if (effectiveLw != 1) lineArgs.Add(effectiveLw.ToString());
                    HandleDrawLine(lineArgs);       // 本地渲染
                    SendDrawCmd("line", lineArgs);  // 发串口

                    _lastSamplePoint = pos;
                    _lastSampleTime = now;
                    break;
                }

                case DrawTool.Line:
                case DrawTool.Rect:
                case DrawTool.RoundedRect:
                case DrawTool.Circle:
                case DrawTool.Ellipse:
                case DrawTool.Triangle:
                case DrawTool.Arc:
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
                case DrawTool.RoundedRect:
                case DrawTool.Circle:
                case DrawTool.Ellipse:
                case DrawTool.Triangle:
                case DrawTool.Arc:
                    // 删预览 → 建正式图形 → 发协议
                    if (_previewShape != null)
                    {
                        oledCanvas.Children.Remove(_previewShape);
                        _previewShape = null;
                    }
                    // 最小尺寸过滤
                    double w = Math.Abs(pos.X - _drawStart.X);
                    double h = Math.Abs(pos.Y - _drawStart.Y);
                    // 弧线：半径过小取消
                    if (_drawTool == DrawTool.Arc && Math.Max(w, h) < 5) return;
                    // 直线：长度过短取消
                    if (_drawTool == DrawTool.Line && Math.Sqrt(w * w + h * h) < 3) return;
                    // 其他：拖太短取消
                    if (_drawTool != DrawTool.Line && _drawTool != DrawTool.Arc && (w < 3 || h < 3)) return;
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
                    SnapShapeToPixel(shape);
                    break;
                case DrawTool.Rect:
                    shape = new Rectangle { Width = w, Height = h,
                        Stroke = brush, StrokeThickness = _drawLineWidth,
                        StrokeDashArray = new DoubleCollection { 4, 3 } };
                    SnapShapeToPixel(shape);
                    Canvas.SetLeft(shape, x); Canvas.SetTop(shape, y);
                    break;
                case DrawTool.Circle:
                {
                    double cx = (p1.X + p2.X) / 2, cy = (p1.Y + p2.Y) / 2;
                    double rx = Math.Abs(p2.X - p1.X) / 2, ry = Math.Abs(p2.Y - p1.Y) / 2;
                    shape = new Ellipse { Width = rx * 2, Height = ry * 2,
                        Stroke = brush, StrokeThickness = _drawLineWidth,
                        StrokeDashArray = new DoubleCollection { 4, 3 } };
                    SnapShapeToPixel(shape);
                    Canvas.SetLeft(shape, cx - rx); Canvas.SetTop(shape, cy - ry);
                    break;
                }
                case DrawTool.Triangle:
                {
                    // 等腰三角：顶点在包围盒上边中点，底边在下
                    var pts = new PointCollection
                    {
                        new Point(x + w / 2, y),           // 顶点
                        new Point(x, y + h),               // 左下
                        new Point(x + w, y + h),           // 右下
                    };
                    shape = new Polygon { Points = pts,
                        Stroke = brush, StrokeThickness = _drawLineWidth,
                        StrokeDashArray = new DoubleCollection { 4, 3 } };
                    SnapShapeToPixel(shape);
                    break;
                }
                case DrawTool.RoundedRect:
                    shape = new Rectangle { Width = w, Height = h,
                        RadiusX = _roundedRectRadius, RadiusY = _roundedRectRadius,
                        Stroke = brush, StrokeThickness = _drawLineWidth,
                        StrokeDashArray = new DoubleCollection { 4, 3 } };
                    SnapShapeToPixel(shape);
                    Canvas.SetLeft(shape, x); Canvas.SetTop(shape, y);
                    break;
                case DrawTool.Ellipse:
                {
                    double ecx = (p1.X + p2.X) / 2, ecy = (p1.Y + p2.Y) / 2;
                    double erx = Math.Abs(p2.X - p1.X) / 2, ery = Math.Abs(p2.Y - p1.Y) / 2;
                    shape = new Ellipse { Width = erx * 2, Height = ery * 2,
                        Stroke = brush, StrokeThickness = _drawLineWidth,
                        StrokeDashArray = new DoubleCollection { 4, 3 } };
                    SnapShapeToPixel(shape);
                    Canvas.SetLeft(shape, ecx - erx); Canvas.SetTop(shape, ecy - ery);
                    break;
                }
                case DrawTool.Arc:
                    // 预览：半透明虚线弧 + 圆心十字
                    double ar = Math.Max(w, h) / 2;
                    double acx = (p1.X + p2.X) / 2, acy = (p1.Y + p2.Y) / 2;
                    shape = BuildArcPath(acx, acy, ar, _arcStartAngle, _arcEndAngle,
                        brush, _drawLineWidth, filled: false, preview: true);
                    SnapShapeToPixel(shape);
                    break;
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
                case DrawTool.Triangle when element is Polygon poly:
                {
                    poly.Points = new PointCollection
                    {
                        new Point(x + w / 2, y),
                        new Point(x, y + h),
                        new Point(x + w, y + h),
                    };
                    break;
                }
                case DrawTool.RoundedRect when element is Rectangle rrect2:
                    rrect2.Width = w; rrect2.Height = h;
                    Canvas.SetLeft(rrect2, x); Canvas.SetTop(rrect2, y);
                    break;
                case DrawTool.Ellipse when element is Ellipse ell2:
                {
                    double ecx = (p1.X + p2.X) / 2, ecy = (p1.Y + p2.Y) / 2;
                    double erx = Math.Abs(p2.X - p1.X) / 2, ery = Math.Abs(p2.Y - p1.Y) / 2;
                    ell2.Width = erx * 2; ell2.Height = ery * 2;
                    Canvas.SetLeft(ell2, ecx - erx); Canvas.SetTop(ell2, ecy - ery);
                    break;
                }
                case DrawTool.Arc when element is Path arcPath:
                {
                    double ar = Math.Max(w, h) / 2;
                    double acx = (p1.X + p2.X) / 2, acy = (p1.Y + p2.Y) / 2;
                    var ab = ParseColorBrush(_drawColor) ?? Brushes.White;
                    ab = ab.Clone(); ab.Opacity = 0.45;
                    var newPath = BuildArcPath(acx, acy, ar, _arcStartAngle, _arcEndAngle,
                        ab, _drawLineWidth, filled: false, preview: true);
                    arcPath.Data = newPath.Data;
                    arcPath.Stroke = newPath.Stroke;
                    arcPath.StrokeThickness = newPath.StrokeThickness;
                    arcPath.StrokeDashArray = newPath.StrokeDashArray;
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
                case DrawTool.Triangle:
                {
                    int x = (int)Math.Min(p1.X, p2.X), y = (int)Math.Min(p1.Y, p2.Y);
                    int w = (int)Math.Abs(p2.X - p1.X), h = (int)Math.Abs(p2.Y - p1.Y);
                    type = "triangle";
                    args = new List<string> { "triangle",
                        (x + w / 2).ToString(), y.ToString(),               // 顶点
                        x.ToString(), (y + h).ToString(),                    // 左下
                        (x + w).ToString(), (y + h).ToString(), color };     // 右下
                    break;
                }
                case DrawTool.RoundedRect:
                {
                    int x = (int)Math.Min(p1.X, p2.X), y = (int)Math.Min(p1.Y, p2.Y);
                    int w = (int)Math.Abs(p2.X - p1.X), h = (int)Math.Abs(p2.Y - p1.Y);
                    type = "rrect";
                    args = new List<string> { "rrect", x.ToString(), y.ToString(), w.ToString(), h.ToString(), color };
                    break;
                }
                case DrawTool.Ellipse:
                {
                    int ecx = (int)((p1.X + p2.X) / 2), ecy = (int)((p1.Y + p2.Y) / 2);
                    int a = (int)Math.Abs(p2.X - p1.X) / 2, b = (int)Math.Abs(p2.Y - p1.Y) / 2;
                    type = "ellipse";
                    args = new List<string> { "ellipse", ecx.ToString(), ecy.ToString(), a.ToString(), b.ToString(), color };
                    break;
                }
                case DrawTool.Arc:
                {
                    int acx = (int)((p1.X + p2.X) / 2), acy = (int)((p1.Y + p2.Y) / 2);
                    int ar = (int)(Math.Max(Math.Abs(p2.X - p1.X), Math.Abs(p2.Y - p1.Y)) / 2);
                    type = "arc";
                    args = new List<string> { "arc", acx.ToString(), acy.ToString(), ar.ToString(),
                        _arcStartAngle.ToString(), _arcEndAngle.ToString(), color };
                    break;
                }
                default: return;
            }

            // 线宽写入协议
            if (_drawLineWidth != 1 && tool != DrawTool.RoundedRect) args.Add(_drawLineWidth.ToString());
            // 圆角矩形：始终输出 lw + radius（保证协议固定格式，避免 radius 被误读为 lw）
            if (tool == DrawTool.RoundedRect)
            {
                args.Add(_drawLineWidth.ToString());
                args.Add(_roundedRectRadius.ToString());
            }

            // 渲染到画布
            switch (tool)
            {
                case DrawTool.Line:        HandleDrawLine(args);        break;
                case DrawTool.Rect:        HandleDrawRect(args);        break;
                case DrawTool.RoundedRect: HandleDrawRoundedRect(args); break;
                case DrawTool.Circle:      HandleDrawCircle(args);      break;
                case DrawTool.Ellipse:     HandleDrawEllipse(args);     break;
                case DrawTool.Triangle:    HandleDrawTriangle(args);    break;
                case DrawTool.Arc:         HandleDrawArc(args);         break;
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
        //  文字编辑（侧栏模式）
        // ═══════════════════════════════════════

        /// <summary>Select 模式下点击已有文字 → 进入编辑</summary>
        private void SelectText(string key, TextBlock tb, Point pos)
        {
            // 查找对应的 DisplayItem
            var parts = key.Split(',');
            if (parts.Length != 2) return;
            if (!int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y)) return;

            DisplayItem existingItem = null;
            foreach (var item in _displayVM.Items)
            {
                if (item.X == x && item.Y == y) { existingItem = item; break; }
            }
            if (existingItem == null) return;

            // 记住原始 key（若编辑后位置改变，需要清理旧项）
            _editingTextOriginalKey = key;

            // 进入编辑模式，预填内容
            _isEditingText = true;
            _textEditPos = new Point(x, y);
            _textEditColor = existingItem.Color ?? "#FFFFFF";

            // 初始化字号下拉
            if (cbTextEditFontSize.Items.Count == 0)
            {
                int[] sizes = { 8, 10, 12, 14, 16, 18, 20, 24, 28, 32 };
                foreach (var s in sizes)
                    cbTextEditFontSize.Items.Add(new ComboBoxItem { Content = s.ToString(), Tag = s });
            }

            // 选中已有字号
            for (int i = 0; i < cbTextEditFontSize.Items.Count; i++)
            {
                if (cbTextEditFontSize.Items[i] is ComboBoxItem ci && ci.Tag is int fs && fs == existingItem.FontSize)
                { cbTextEditFontSize.SelectedIndex = i; break; }
            }

            tbTextEditX.Text = x.ToString();
            tbTextEditY.Text = y.ToString();
            tbTextEditContent.Text = existingItem.Text;
            textEditColorSwatch.Background = ParseColorBrush(_textEditColor) ?? Brushes.White;

            // 从画布移除旧 TextBlock（预览会替代它）
            oledCanvas.Children.Remove(tb);
            _oledTexts.Remove(key);

            // 切侧栏到文字编辑
            rightOLEDSettings.Visibility = Visibility.Collapsed;
            rightOLEDShapeEditor.Visibility = Visibility.Collapsed;
            rightOLEDTextEditor.Visibility = Visibility.Visible;
            tbSidePanelTitle.Text = "文字编辑";

            oledCanvas.Cursor = Cursors.Arrow;
            CreateTextPreview();

            tbTextEditContent.Focus();
            Keyboard.Focus(tbTextEditContent);
        }

        /// <summary>切回 OLED 标签页时重置侧栏到设置视图</summary>
        private void ResetOLEDSidePanel()
        {
            if (_isEditingText)
                ExitTextEditMode(save: _editingTextOriginalKey != null);
            if (_selectedShape != null)
                ExitShapeEditMode(save: true);
            rightOLEDSettings.Visibility = Visibility.Visible;
            rightOLEDTextEditor.Visibility = Visibility.Collapsed;
            rightOLEDShapeEditor.Visibility = Visibility.Collapsed;
            tbSidePanelTitle.Text = "OLED 设置";
        }

        private void EnterTextEditMode(Point canvasPos)
        {
            _isEditingText = true;
            _textEditPos = canvasPos;
            _textEditColor = _drawColor;
            _editingTextOriginalKey = null; // 新文字，非编辑已有

            // 初始填充字号下拉
            if (cbTextEditFontSize.Items.Count == 0)
            {
                int[] sizes = { 8, 10, 12, 14, 16, 18, 20, 24, 28, 32 };
                foreach (var s in sizes)
                    cbTextEditFontSize.Items.Add(new ComboBoxItem { Content = s.ToString(), Tag = s });
                cbTextEditFontSize.SelectedIndex = 3; // 14
            }
            else if (cbTextEditFontSize.SelectedIndex < 0)
            {
                cbTextEditFontSize.SelectedIndex = 3;
            }

            tbTextEditX.Text = ((int)canvasPos.X).ToString();
            tbTextEditY.Text = ((int)canvasPos.Y).ToString();
            tbTextEditContent.Text = "";
            textEditColorSwatch.Background = ParseColorBrush(_textEditColor) ?? Brushes.White;

            // 新建文字 → 显示确定/取消，隐藏协议预览 + 锁按钮
            textEditConfirmRow.Visibility = Visibility.Visible;
            sharedProtocolPreview.Visibility = Visibility.Collapsed;
            if (btnTextLock != null) btnTextLock.Visibility = Visibility.Collapsed;

            // 切侧栏到编辑模式
            rightOLEDSettings.Visibility = Visibility.Collapsed;
            rightOLEDShapeEditor.Visibility = Visibility.Collapsed;
            rightOLEDTextEditor.Visibility = Visibility.Visible;
            tbSidePanelTitle.Text = "文字编辑";

            // 画布上创建预览框 + 切箭头光标
            oledCanvas.Cursor = Cursors.Arrow;
            CreateTextPreview();

            tbTextEditContent.Focus();
            Keyboard.Focus(tbTextEditContent);
        }

        private void ExitTextEditMode(bool save)
        {
            if (!_isEditingText) return;

            if (save)
            {
                string text = tbTextEditContent.Text;
                _drawColor = _textEditColor;
                int fontSize = cbTextEditFontSize.SelectedItem is ComboBoxItem item && item.Tag is int fs ? fs : 14;
                string color = _textEditColor;

                if (_editingTextOriginalKey != null)
                {
                    // 编辑已有文字 → 直接更新画布 TextBlock + DrawCommand
                    if (_selectedShape is TextBlock tb)
                    {
                        int newX = int.TryParse(tbTextEditX.Text, out int px) ? px : (int)Canvas.GetLeft(tb);
                        int newY = int.TryParse(tbTextEditY.Text, out int py) ? py : (int)Canvas.GetTop(tb);
                        tb.Text = text;
                        tb.FontSize = fontSize;
                        tb.Foreground = ParseColorBrush(color) ?? Brushes.White;
                        Canvas.SetLeft(tb, newX); Canvas.SetTop(tb, newY);

                        // 更新 DrawCommand
                        if (_selectedIndex >= 0 && _selectedIndex < _displayVM.DrawCommands.Count)
                        {
                            var newArgs = new List<string> { "text", newX.ToString(), newY.ToString(), text, fontSize.ToString(), color };
                            _displayVM.DrawCommands[_selectedIndex] = new DrawCommand
                                { Type = "text", Args = newArgs, Color = color };
                            SyncShapeAfterModification(_selectedIndex, newArgs);
                        }
                    }
                    // 清理旧的 _oledTexts / Items（向后兼容）
                    var oldParts = _editingTextOriginalKey.Split(',');
                    if (oldParts.Length == 2 && int.TryParse(oldParts[0], out int ox) && int.TryParse(oldParts[1], out int oy))
                    {
                        _oledTexts.Remove(_editingTextOriginalKey);
                        _displayVM.Items.RemoveAll(it => it.X == ox && it.Y == oy);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(text))
                {
                    // 新建文字 → 走 [draw,text,...]
                    int newX = int.TryParse(tbTextEditX.Text, out int px) ? px : (int)_textEditPos.X;
                    int newY = int.TryParse(tbTextEditY.Text, out int py) ? py : (int)_textEditPos.Y;
                    var args = new List<string> { "text", newX.ToString(), newY.ToString(), text, fontSize.ToString(), color };
                    HandleDrawText(args);
                    if (_session != null && _session.IsOpen)
                        SendDrawCmd("text", args);
                }
            }
            else
            {
                // 取消编辑已有文字 → 从 _originalShapeState 恢复（TextBlock 一直在画布上）
                if (_editingTextOriginalKey != null && _originalShapeState != null)
                {
                    RestoreOriginalState(_selectedShape, _originalShapeState);
                }
            }

            RemoveTextPreview();
            _isEditingText = false;
            _editingTextOriginalKey = null;

            // 恢复画布光标
            oledCanvas.Cursor = (_drawTool == DrawTool.None || _drawTool == DrawTool.Select)
                ? Cursors.Arrow : Cursors.Cross;

            // 恢复侧栏
            rightOLEDSettings.Visibility = Visibility.Visible;
            rightOLEDTextEditor.Visibility = Visibility.Collapsed;
            sharedProtocolPreview.Visibility = Visibility.Collapsed;
            tbSidePanelTitle.Text = "OLED 设置";
        }

        private void CreateTextPreview()
        {
            RemoveTextPreview();
            string text = tbTextEditContent?.Text ?? "";
            int fontSize = cbTextEditFontSize?.SelectedItem is ComboBoxItem item && item.Tag is int fs ? fs : 14;
            var colorBrush = ParseColorBrush(_textEditColor) ?? Brushes.White;

            var tb = new TextBlock
            {
                Text = string.IsNullOrEmpty(text) ? " " : text,
                FontSize = fontSize,
                Foreground = colorBrush,
                FontFamily = new FontFamily("Sarasa Mono SC, Consolas, Courier New"),
            };

            var border = new Border
            {
                Child = tb,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#42A5F5")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 2, 4, 2),
                Background = new SolidColorBrush(Color.FromArgb(0x18, 0x42, 0xA5, 0xF5)),
                IsHitTestVisible = false, // 不拦截点击，鼠标穿透到画布
            };

            Canvas.SetLeft(border, _textEditPos.X);
            Canvas.SetTop(border, _textEditPos.Y);
            Canvas.SetZIndex(border, 998);
            oledCanvas.Children.Add(border);
            _textPreview = border;
        }

        private void UpdateTextPreview()
        {
            if (_textPreview == null) return;
            string text = tbTextEditContent?.Text ?? "";
            int fontSize = cbTextEditFontSize?.SelectedItem is ComboBoxItem item && item.Tag is int fs ? fs : 14;
            var colorBrush = ParseColorBrush(_textEditColor) ?? Brushes.White;

            var tb = _textPreview.Child as TextBlock;
            if (tb != null)
            {
                tb.Text = string.IsNullOrEmpty(text) ? " " : text;
                tb.FontSize = fontSize;
                tb.Foreground = colorBrush;
            }
        }

        private void RemoveTextPreview()
        {
            if (_textPreview != null)
            {
                oledCanvas.Children.Remove(_textPreview);
                _textPreview = null;
            }
        }

        // ── 侧栏事件 ──

        private void tbTextEditContent_Changed(object sender, TextChangedEventArgs e)
        {
            if (!_isEditingText) return;
            if (_editingTextOriginalKey != null)
                ApplyTextEditToCanvas(); // 编辑已有 → 直接改原文
            else
                UpdateTextPreview();
        }

        private void cbTextEditFontSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isEditingText) return;
            if (_editingTextOriginalKey != null)
                ApplyTextEditToCanvas();
            else
                UpdateTextPreview();
        }

        private void TextEditField_Changed(object sender, TextChangedEventArgs e)
        {
            if (!_isEditingText) return;
            if (_editingTextOriginalKey != null)
                ApplyTextEditToCanvas();
        }

        /// <summary>编辑已有文字时，侧栏字段 → 直接更新画布 TextBlock</summary>
        private void ApplyTextEditToCanvas()
        {
            if (_selectedShape is TextBlock tb)
            {
                int x = int.TryParse(tbTextEditX.Text, out int px) ? px : (int)Canvas.GetLeft(tb);
                int y = int.TryParse(tbTextEditY.Text, out int py) ? py : (int)Canvas.GetTop(tb);
                Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y);
                tb.Text = tbTextEditContent.Text;
                int fontSize = cbTextEditFontSize.SelectedItem is ComboBoxItem item && item.Tag is int fs ? fs : 14;
                tb.FontSize = fontSize;
                tb.Foreground = ParseColorBrush(_textEditColor) ?? Brushes.White;

                // 更新控点
                RemoveControlPoints();
                var bounds = GetShapeBounds(tb);
                CreateControlPoints(bounds);

                // 更新协议预览
                UpdateSharedProtocolPreview();
            }
        }

        private void btnCopyTextContent_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbTextEditContent.Text)) return;
            SafeSetClipboard(tbTextEditContent.Text);
            if (sender is Button btn) ShowCopyToastAndShake(btn);
        }

        /// <summary>更新共享协议预览（图形+文字通用）</summary>
        private void UpdateSharedProtocolPreview()
        {
            if (_selectedShape == null || _selectedIndex < 0) { tbProtocolPreview.Text = "—"; return; }

            if (_selectedShape is TextBlock tb)
            {
                int x = (int)Canvas.GetLeft(tb), y = (int)Canvas.GetTop(tb);
                string color = "#FFFFFF";
                if (tb.Foreground is SolidColorBrush scb)
                    color = $"#{scb.Color.R:X2}{scb.Color.G:X2}{scb.Color.B:X2}";
                tbProtocolPreview.Text = $"[draw,text,{x},{y},{tb.Text},{(int)tb.FontSize},{color}]";
                return;
            }

            if (_selectedIndex < _displayVM.DrawCommands.Count)
            {
                var args = ShapeToProtocolArgs(_selectedShape, _displayVM.DrawCommands[_selectedIndex]);
                if (args != null)
                    tbProtocolPreview.Text = "[draw," + string.Join(",", args) + "]";
                else
                    tbProtocolPreview.Text = "—";
            }
        }

        private void textEditColorSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (!_isEditingText) return;
            ShowDrawColorPicker(textEditColorSwatch, hex =>
            {
                _textEditColor = hex;
                var brush = ParseColorBrush(hex);
                if (brush != null) textEditColorSwatch.Background = brush;
                if (_editingTextOriginalKey != null)
                    ApplyTextEditToCanvas();
                else
                    UpdateTextPreview();
            });
        }

        private void btnTextEditConfirm_Click(object sender, RoutedEventArgs e)
        {
            ExitTextEditMode(save: true);
        }

        private void btnTextEditCancel_Click(object sender, RoutedEventArgs e)
        {
            ExitTextEditMode(save: false);
        }

        // ═══════════════════════════════════════
        //  发送画布（全部图形重发）
        // ═══════════════════════════════════════

        private void btnSendCanvas_Click(object sender, RoutedEventArgs e)
        {
            SyncAllShapesToDevice();
        }

        /// <summary>全量同步画布到设备（clear + 全部 display + 全部 draw）</summary>
        // F4：旋转矩形拆 2 填充三角，MCU 零改动
        private void SendRotatedRectAsTriangles(Shape shape, DrawCommand cmd)
        {
            // 取旋转后的 4 个角点
            double x = Canvas.GetLeft(shape), y = Canvas.GetTop(shape);
            double w = shape is Rectangle r ? r.Width : ((FrameworkElement)shape).Width;
            double h = shape is Rectangle r2 ? r2.Height : ((FrameworkElement)shape).Height;
            var corners = new (double X, double Y)[]
            {
                (x, y), (x + w, y), (x + w, y + h), (x, y + h)
            };
            var rt = shape.RenderTransform as RotateTransform;
            double angle = rt?.Angle ?? 0;
            double cx = x + w / 2, cy = y + h / 2;
            var rotated = corners.Select(c => RotatePoint(c.X, c.Y, cx, cy, angle)).ToArray();

            // 2 填充三角: A-C-B, A-D-C
            string color = cmd.Color ?? "#FFFFFF";
            int lw = (cmd.Args.Count >= 7 && int.TryParse(cmd.Args[6], out int sw) && sw >= 1) ? sw : 1;

            var tri1 = $"[draw,triangle,{(int)rotated[0].Item1},{(int)rotated[0].Item2},{(int)rotated[2].Item1},{(int)rotated[2].Item2},{(int)rotated[1].Item1},{(int)rotated[1].Item2},{color},{lw},fill]";
            var tri2 = $"[draw,triangle,{(int)rotated[0].Item1},{(int)rotated[0].Item2},{(int)rotated[3].Item1},{(int)rotated[3].Item2},{(int)rotated[2].Item1},{(int)rotated[2].Item2},{color},{lw},fill]";
            SendRaw(tri1, appendLineEnding: true);
            SendRaw(tri2, appendLineEnding: true);
        }

        private void SyncAllShapesToDevice()
        {
            if (_session == null || !_session.IsOpen) return;
            if (_displayVM == null) return;

            // 先清屏
            SendRaw("[draw,clear," + _displayVM.CanvasBackground + "]", appendLineEnding: true);

            // 重发文本（向后兼容——同时发 [display] 旧格式和 [draw,text] 新格式）
            var syncedTextKeys = new HashSet<string>();
            foreach (var item in _displayVM.Items)
            {
                string cmd = string.Format("[display,{0},{1},{2},{3},{4}]",
                    item.X, item.Y, item.Text, item.FontSize, item.Color ?? "#FFFFFF");
                SendRaw(cmd, appendLineEnding: true);
                syncedTextKeys.Add($"{item.X},{item.Y}");
            }

            // 重发图形 + 新格式文字
            for (int i = 0; i < _displayVM.DrawCommands.Count; i++)
            {
                var cmd = _displayVM.DrawCommands[i];
                // F4 旋转：矩形/圆角矩形 → 拆 2 填充三角发给 MCU
                if (i < _drawElements.Count && _drawElements[i] is Shape shape
                    && shape.RenderTransform is RotateTransform rt && rt.Angle != 0
                    && (cmd.Type == "rect" || cmd.Type == "rrect"))
                {
                    SendRotatedRectAsTriangles(shape, cmd);
                }
                else
                {
                    string payload = "[draw," + string.Join(",", cmd.Args) + "]";
                    SendRaw(payload, appendLineEnding: true);
                }
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
            EnterShapeEditMode();
        }

        private void DeselectShape()
        {
            if (_selectedShape != null)
                ExitShapeEditMode(save: true);
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
            // Rectangle / Ellipse / Path（以及 1px point 矩形）
            double left = Canvas.GetLeft(shape);
            double top = Canvas.GetTop(shape);
            double width = (shape as FrameworkElement)?.ActualWidth ?? 0;
            double height = (shape as FrameworkElement)?.ActualHeight ?? 0;

            // Path 优先：几何含绝对坐标，必须从 Data.Bounds 推算（ActualWidth 非零会导致跳过）
            if (shape is System.Windows.Shapes.Path arcPath2 && arcPath2.Data?.Bounds != null)
            {
                var geoBounds = arcPath2.Data.Bounds;
                left = double.IsNaN(left) ? geoBounds.Left : left + geoBounds.Left;
                top = double.IsNaN(top) ? geoBounds.Top : top + geoBounds.Top;
                width = geoBounds.Width;
                height = geoBounds.Height;
            }
            else if (width == 0 && shape is Rectangle r) { width = r.Width; height = r.Height; }
            else if (width == 0 && shape is Ellipse e) { width = e.Width; height = e.Height; }
            else if (width == 0 && shape is TextBlock tb) { width = 20; height = tb.FontSize + 4; }
            if (width <= 0) { width = 40; height = 40; }
            var bounds = new Rect(left, top, width, height);

            // F4 旋转：矩形/椭圆保留 RotateTransform，包围盒需扩展为旋转后的 AABB
            if (shape is FrameworkElement fe && fe.RenderTransform is RotateTransform rt && Math.Abs(rt.Angle) > 0.01)
            {
                double cx = bounds.Left + bounds.Width / 2, cy = bounds.Top + bounds.Height / 2;
                double rad = rt.Angle * Math.PI / 180.0;
                double cos = Math.Cos(rad), sin = Math.Sin(rad);
                var corners = new[] {
                    (bounds.Left, bounds.Top), (bounds.Right, bounds.Top),
                    (bounds.Right, bounds.Bottom), (bounds.Left, bounds.Bottom)
                };
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                foreach (var (px, py) in corners)
                {
                    double dx = px - cx, dy = py - cy;
                    double rx = cx + dx * cos - dy * sin;
                    double ry = cy + dx * sin + dy * cos;
                    if (rx < minX) minX = rx; if (ry < minY) minY = ry;
                    if (rx > maxX) maxX = rx; if (ry > maxY) maxY = ry;
                }
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            return bounds;
        }

        // ── F4 旋转：判断当前选中图形是否支持旋转 ──
        private bool IsRotationSupported()
        {
            if (_selectedShapeDrawType == "circle" || _selectedShapeDrawType == "point") return false;
            return true;
        }

        // F4 在包围盒上方添加旋转控点（小圆 + 连接线）
        private void AddRotationControlPoint(Rect bounds, Brush fill, Brush stroke)
        {
            if (!IsRotationSupported()) return;
            double rotSize = 10;
            double rotCx = bounds.Left + bounds.Width / 2;
            double rotCy = bounds.Top - 20;
            // 连接线
            var connLine = new Line
            {
                X1 = rotCx, Y1 = bounds.Top,
                X2 = rotCx, Y2 = rotCy + rotSize / 2,
                Stroke = fill, StrokeThickness = 1,
                IsHitTestVisible = false,
            };
            Canvas.SetZIndex(connLine, 999);
            oledCanvas.Children.Add(connLine);
            _controlPoints.Add(connLine);
            // 旋转圆点（用 Rectangle——HitTestControlPoint 只查 Rectangle）
            var rotDot = new Rectangle
            {
                Width = rotSize, Height = rotSize,
                Fill = fill, Stroke = stroke, StrokeThickness = 1,
                Tag = HandleRole.RotateHandle,
                Cursor = Cursors.Hand,
            };
            Canvas.SetLeft(rotDot, rotCx - rotSize / 2);
            Canvas.SetTop(rotDot, rotCy);
            Canvas.SetZIndex(rotDot, 1000);
            oledCanvas.Children.Add(rotDot);
            _controlPoints.Add(rotDot);
        }

        // 更新旋转控点位置
        private void UpdateRotationControlPoint(Rect bounds)
        {
            // 找到旋转控点（Tag=RotateHandle）和连接线（前一个元素）
            for (int i = 0; i < _controlPoints.Count; i++)
            {
                if (_controlPoints[i] is Rectangle rotDot && rotDot.Tag is HandleRole r && r == HandleRole.RotateHandle)
                {
                    double rotCx = bounds.Left + bounds.Width / 2;
                    double rotCy = bounds.Top - 20;
                    Canvas.SetLeft(rotDot, rotCx - 5);
                    Canvas.SetTop(rotDot, rotCy);
                    // 连接线在 i-1
                    if (i > 0 && _controlPoints[i - 1] is Line connLine)
                    {
                        connLine.X1 = rotCx; connLine.Y1 = bounds.Top;
                        connLine.X2 = rotCx; connLine.Y2 = rotCy + 5;
                    }
                    return;
                }
            }
        }

        // ── 控点创建 / 更新 / 移除 ──

        private void CreateControlPoints(Rect bounds)
        {
            RemoveControlPoints();
            double size = 8;
            bool locked = IsElementLocked();
            var fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(locked ? "#888888" : "#42A5F5"));
            var stroke = locked ? Brushes.Gray : Brushes.White;

            // ── 直线：2 端点控点 + 中心控点 + 虚线框（F2 线端点独立拖拽） ──
            if (_selectedShape is Line line)
            {
                var handles = new (double X, double Y, HandleRole Role, Cursor Cursor)[]
                {
                    (line.X1 - size/2, line.Y1 - size/2, HandleRole.Endpoint1, Cursors.Hand),
                    (line.X2 - size/2, line.Y2 - size/2, HandleRole.Endpoint2, Cursors.Hand),
                    ((line.X1 + line.X2)/2 - size/2, (line.Y1 + line.Y2)/2 - size/2, HandleRole.Center, Cursors.SizeAll),
                };
                foreach (var h in handles)
                {
                    var rect = new Rectangle
                    {
                        Width = size, Height = size,
                        Fill = fill, Stroke = stroke, StrokeThickness = 1,
                        Tag = h.Role, Cursor = h.Cursor,
                    };
                    Canvas.SetLeft(rect, h.X); Canvas.SetTop(rect, h.Y);
                    Canvas.SetZIndex(rect, 1000);
                    oledCanvas.Children.Add(rect);
                    _controlPoints.Add(rect);
                }
                // 虚线框
                var outline = new Rectangle
                {
                    Width = bounds.Width, Height = bounds.Height,
                    Stroke = fill, StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(outline, bounds.Left); Canvas.SetTop(outline, bounds.Top);
                Canvas.SetZIndex(outline, 999);
                oledCanvas.Children.Add(outline);
                _controlPoints.Add(outline);
                AddRotationControlPoint(bounds, fill, stroke);
                return;
            }

            // ── 三角形：3 顶点控点 + 中心控点 + 虚线框（F3 三角形顶点独立拖拽） ──
            if (_selectedShape is Polygon tri)
            {
                var pts = tri.Points;
                double cx = (pts[0].X + pts[1].X + pts[2].X) / 3;
                double cy = (pts[0].Y + pts[1].Y + pts[2].Y) / 3;
                var handles = new (double X, double Y, HandleRole Role, Cursor Cursor)[]
                {
                    (pts[0].X - size/2, pts[0].Y - size/2, HandleRole.Vertex0, Cursors.Hand),
                    (pts[1].X - size/2, pts[1].Y - size/2, HandleRole.Vertex1, Cursors.Hand),
                    (pts[2].X - size/2, pts[2].Y - size/2, HandleRole.Vertex2, Cursors.Hand),
                    (cx - size/2, cy - size/2, HandleRole.Center, Cursors.SizeAll),
                };
                foreach (var h in handles)
                {
                    var rect = new Rectangle
                    {
                        Width = size, Height = size,
                        Fill = fill, Stroke = stroke, StrokeThickness = 1,
                        Tag = h.Role, Cursor = h.Cursor,
                    };
                    Canvas.SetLeft(rect, h.X); Canvas.SetTop(rect, h.Y);
                    Canvas.SetZIndex(rect, 1000);
                    oledCanvas.Children.Add(rect);
                    _controlPoints.Add(rect);
                }
                var triOutline = new Rectangle
                {
                    Width = bounds.Width, Height = bounds.Height,
                    Stroke = fill, StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(triOutline, bounds.Left); Canvas.SetTop(triOutline, bounds.Top);
                Canvas.SetZIndex(triOutline, 999);
                oledCanvas.Children.Add(triOutline);
                _controlPoints.Add(triOutline);
                AddRotationControlPoint(bounds, fill, stroke);
                return;
            }

            // ── 其他图形：4 角控点 ──
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
            var outline2 = new Rectangle
            {
                Width = bounds.Width, Height = bounds.Height,
                Stroke = fill, StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(outline2, bounds.Left);
            Canvas.SetTop(outline2, bounds.Top);
            Canvas.SetZIndex(outline2, 999);
            oledCanvas.Children.Add(outline2);
            _controlPoints.Add(outline2);
            AddRotationControlPoint(bounds, fill, stroke);
        }

        private void UpdateControlPoints(Rect bounds)
        {
            double size = 8;

            // ── 直线：更新端点 + 中心 + 虚线框 ──
            if (_selectedShape is Line line && _controlPoints.Count >= 4)
            {
                Canvas.SetLeft(_controlPoints[0], line.X1 - size/2);
                Canvas.SetTop (_controlPoints[0], line.Y1 - size/2);
                Canvas.SetLeft(_controlPoints[1], line.X2 - size/2);
                Canvas.SetTop (_controlPoints[1], line.Y2 - size/2);
                Canvas.SetLeft(_controlPoints[2], (line.X1 + line.X2)/2 - size/2);
                Canvas.SetTop (_controlPoints[2], (line.Y1 + line.Y2)/2 - size/2);
                if (_controlPoints[3] is Rectangle lineOutline)
                {
                    lineOutline.Width = bounds.Width; lineOutline.Height = bounds.Height;
                    Canvas.SetLeft(lineOutline, bounds.Left); Canvas.SetTop(lineOutline, bounds.Top);
                }
                UpdateRotationControlPoint(bounds);
                return;
            }

            // ── 三角形：更新 3 顶点 + 中心 + 虚线框 ──
            if (_selectedShape is Polygon tri && _controlPoints.Count >= 5)
            {
                var pts = tri.Points;
                Canvas.SetLeft(_controlPoints[0], pts[0].X - size/2);
                Canvas.SetTop (_controlPoints[0], pts[0].Y - size/2);
                Canvas.SetLeft(_controlPoints[1], pts[1].X - size/2);
                Canvas.SetTop (_controlPoints[1], pts[1].Y - size/2);
                Canvas.SetLeft(_controlPoints[2], pts[2].X - size/2);
                Canvas.SetTop (_controlPoints[2], pts[2].Y - size/2);
                double cx = (pts[0].X + pts[1].X + pts[2].X) / 3;
                double cy = (pts[0].Y + pts[1].Y + pts[2].Y) / 3;
                Canvas.SetLeft(_controlPoints[3], cx - size/2);
                Canvas.SetTop (_controlPoints[3], cy - size/2);
                if (_controlPoints[4] is Rectangle triOutline)
                {
                    triOutline.Width = bounds.Width; triOutline.Height = bounds.Height;
                    Canvas.SetLeft(triOutline, bounds.Left); Canvas.SetTop(triOutline, bounds.Top);
                }
                UpdateRotationControlPoint(bounds);
                return;
            }

            // ── 其他图形：4 角控点（索引 0-3）──
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
            UpdateRotationControlPoint(bounds);
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

        /// <summary>命中测试文字（Select 模式下点击文字 → 进入编辑）</summary>
        private TextBlock HitTestText(Point pos, out string key)
        {
            // 倒序遍历（后添加的在上面）
            var keys = _oledTexts.Keys.ToArray();
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                key = keys[i];
                var tb = _oledTexts[key];
                double left = Canvas.GetLeft(tb);
                double top = Canvas.GetTop(tb);
                double w = tb.ActualWidth;
                double h = tb.ActualHeight;
                if (w <= 0) w = 20; // 空文本或未布局，给个最小宽度
                if (h <= 0) h = 14;
                var bounds = new Rect(left - 2, top - 2, w + 4, h + 4);
                if (bounds.Contains(pos))
                    return tb;
            }
            key = null;
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
        private void CaptureOriginalState(UIElement shape, out object state)
        {
            if (shape is Line line)
                state = (line.X1, line.Y1, line.X2, line.Y2);
            else if (shape is Polygon tri)
                state = tri.Points.ToArray();
            else if (shape is System.Windows.Shapes.Path arcPath)
            {
                // 弧线：保存圆心+半径（非宽高！弧线不填满包围盒，圆心不在包围盒中心）
                string arcColor = (arcPath.Stroke as SolidColorBrush ?? arcPath.Fill as SolidColorBrush)
                    ?.Color.ToString() ?? "#FFFFFF";
                if (!arcColor.StartsWith("#")) arcColor = "#" + arcColor;
                // 从 DrawCommand 读圆心/半径（不从几何反推——ArcSegment 解析极难）
                double acx = 0, acy = 0, ar = 40;
                if (_selectedIndex >= 0 && _selectedIndex < _displayVM.DrawCommands.Count)
                {
                    var cmd = _displayVM.DrawCommands[_selectedIndex];
                    if (cmd.Args != null && cmd.Args.Count >= 6)
                    {
                        double.TryParse(cmd.Args[1], out acx);
                        double.TryParse(cmd.Args[2], out acy);
                        double.TryParse(cmd.Args[3], out ar);
                    }
                }
                // 存储画布空间坐标（几何中心 + Canvas 偏移）
                double canvasCx = acx + Canvas.GetLeft(arcPath);
                double canvasCy = acy + Canvas.GetTop(arcPath);
                state = (Canvas.GetLeft(arcPath), Canvas.GetTop(arcPath),
                    canvasCx, canvasCy, ar, arcColor, arcPath.StrokeThickness);
            }
            else if (shape is TextBlock tb)
                state = (Canvas.GetLeft(tb), Canvas.GetTop(tb), tb.Text, tb.FontSize,
                    (tb.Foreground as SolidColorBrush)?.Color.ToString() ?? "#FFFFFF");
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
            else if (state is ValueTuple<double, double, string, double, string> textState && shape is TextBlock tb)
            {
                var (tl, tt, txt, fs, clr) = textState;
                Canvas.SetLeft(tb, tl); Canvas.SetTop(tb, tt);
                tb.Text = txt; tb.FontSize = fs;
                try { tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(clr)); } catch { }
            }
            else if (state is (double al, double at, double acx, double acy, double ar, string arcClr, double arcLw) && shape is System.Windows.Shapes.Path arcPath2)
            {
                Canvas.SetLeft(arcPath2, al); Canvas.SetTop(arcPath2, at);
                try
                {
                    var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(arcClr));
                    if (arcPath2.Fill != null) arcPath2.Fill = b;
                    else arcPath2.Stroke = b;
                    arcPath2.StrokeThickness = arcLw;
                }
                catch { }
            }
            else if (state is (double sl, double st, double sw, double sh))
            { Canvas.SetLeft(shape, sl); Canvas.SetTop(shape, st); ((FrameworkElement)shape).Width = sw; ((FrameworkElement)shape).Height = sh; }
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
            else if (_originalShapeState is (double al, double at, double acx, double acy, double ar, string arcClr, double arcLw) && shape is System.Windows.Shapes.Path arcP4)
            {
                // 弧线 resize：圆心在包围盒内按比例位移 + 半径等比缩放
                //   (弧线不填满包围盒，不能把圆心硬塞到新包围盒中心——否则弧线视觉飞走)
                int newR = (int)(ar * Math.Max(scaleX, scaleY));
                if (newR < 3) newR = 3;
                int newCx = (int)(newBounds.Left + (acx - oldBounds.Left) * scaleX);
                int newCy = (int)(newBounds.Top  + (acy - oldBounds.Top)  * scaleY);

                // 从 DrawCommand 取角度（resize 不改角度）
                int sa = _arcStartAngle, ea = _arcEndAngle;
                if (_selectedIndex >= 0 && _selectedIndex < _displayVM.DrawCommands.Count)
                {
                    var cmd = _displayVM.DrawCommands[_selectedIndex];
                    if (cmd.Args != null && cmd.Args.Count >= 6)
                    {
                        int.TryParse(cmd.Args[4], out sa);
                        int.TryParse(cmd.Args[5], out ea);
                    }
                }

                bool filledArc = arcP4.Fill != null && arcP4.Stroke == null;
                try
                {
                    var nb = new SolidColorBrush((Color)ColorConverter.ConvertFromString(arcClr));
                    var rebuilt = BuildArcPath(newCx, newCy, newR, sa, ea, nb, (int)arcLw, filledArc, preview: false);
                    arcP4.Data = rebuilt.Data;
                    if (filledArc) { arcP4.Fill = nb; arcP4.Stroke = null; }
                    else { arcP4.Stroke = nb; arcP4.StrokeThickness = arcLw; arcP4.Fill = null; }
                    Canvas.SetLeft(arcP4, 0);
                    Canvas.SetTop(arcP4, 0);
                    // 同步更新 DrawCommand（避免 FinalizeShapeModification 从几何反推圆心出错）
                    if (_selectedIndex >= 0 && _selectedIndex < _displayVM.DrawCommands.Count)
                    {
                        var cmd2 = _displayVM.DrawCommands[_selectedIndex];
                        if (cmd2.Args != null && cmd2.Args.Count >= 6)
                        {
                            cmd2.Args[1] = newCx.ToString();
                            cmd2.Args[2] = newCy.ToString();
                            cmd2.Args[3] = newR.ToString();
                        }
                    }
                }
                catch { }
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
            else if (_originalShapeState is (double al, double at, double acx, double acy, double ar, string _, double _) && shape is System.Windows.Shapes.Path)
            { Canvas.SetLeft(shape, al + totalDx); Canvas.SetTop(shape, at + totalDy); }
            else if (_originalShapeState is (double l, double t, double w, double h))
            { Canvas.SetLeft(shape, l + totalDx); Canvas.SetTop(shape, t + totalDy); }
            else if (_originalShapeState is ValueTuple<double, double, string, double, string> txtState && shape is TextBlock)
            {
                Canvas.SetLeft(shape, txtState.Item1 + totalDx);
                Canvas.SetTop(shape, txtState.Item2 + totalDy);
            }
        }

        // ── 修改确认：协议重编码 + 同步 ──

        // F4：将旋转角度"拍平"到坐标中（线/三角）或保留 Transform（矩/椭/文字）
        private void BakeRotationIntoShape()
        {
            if (_selectedShape == null) return;
            double a = _shapeRotationAngle;
            // 获取旋转中心（原始包围盒中心）
            double cx = _originalBounds.Left + _originalBounds.Width / 2;
            double cy = _originalBounds.Top + _originalBounds.Height / 2;

            if (_selectedShapeDrawType == "line" && _selectedShape is Line line)
            {
                // 旋转端点
                (double rx1, double ry1) = RotatePoint(line.X1, line.Y1, cx, cy, a);
                (double rx2, double ry2) = RotatePoint(line.X2, line.Y2, cx, cy, a);
                line.X1 = rx1; line.Y1 = ry1; line.X2 = rx2; line.Y2 = ry2;
                line.RenderTransform = Transform.Identity;
            }
            else if (_selectedShapeDrawType == "triangle" && _selectedShape is Polygon tri)
            {
                var newPts = new PointCollection();
                foreach (var p in tri.Points)
                {
                    var (rx, ry) = RotatePoint(p.X, p.Y, cx, cy, a);
                    newPts.Add(new Point(rx, ry));
                }
                tri.Points = newPts;
                tri.RenderTransform = Transform.Identity;
            }
            else if (_selectedShapeDrawType == "arc" && _selectedShape is System.Windows.Shapes.Path arcP
                && _selectedIndex >= 0 && _selectedIndex < _displayVM.DrawCommands.Count)
            {
                // 弧线旋转 = 改 startAngle/endAngle，松手时烘焙到 DrawCommand
                var arcCmd = _displayVM.DrawCommands[_selectedIndex];
                if (arcCmd.Args != null && arcCmd.Args.Count >= 6)
                {
                    int sa = int.Parse(arcCmd.Args[4]) + (int)a;
                    int ea = int.Parse(arcCmd.Args[5]) + (int)a;
                    arcCmd.Args[4] = ((sa % 360) + 360) % 360 + "";
                    arcCmd.Args[5] = ((ea % 360) + 360) % 360 + "";
                    // 重建几何（清除 RotateTransform）
                    int acx = int.Parse(arcCmd.Args[1]), acy = int.Parse(arcCmd.Args[2]), arR = int.Parse(arcCmd.Args[3]);
                    bool filled = arcP.Fill != null && arcP.Stroke == null;
                    int lw = (int)arcP.StrokeThickness;
                    var brush = arcP.Stroke ?? arcP.Fill ?? Brushes.White;
                    var rebuilt = BuildArcPath(acx, acy, arR, sa, ea, brush, lw, filled, preview: false);
                    arcP.Data = rebuilt.Data;
                    if (filled) { arcP.Fill = brush; arcP.Stroke = null; }
                    else { arcP.Stroke = brush; arcP.StrokeThickness = lw; arcP.Fill = null; }
                    Canvas.SetLeft(arcP, 0); Canvas.SetTop(arcP, 0);
                    arcP.RenderTransform = Transform.Identity;
                }
            }
            else if (_selectedShapeDrawType == "circle" || _selectedShapeDrawType == "point")
            {
                // 不支持旋转，重置
                _selectedShape.RenderTransform = Transform.Identity;
            }
            // rect / rrect / ellipse / text：保留 RotateTransform，不烘焙
            _shapeRotationAngle = 0;
        }

        // 点 (x,y) 绕 (cx,cy) 旋转 a 度
        private static (double, double) RotatePoint(double x, double y, double cx, double cy, double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            double cos = Math.Cos(rad), sin = Math.Sin(rad);
            double dx = x - cx, dy = y - cy;
            return (cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
        }

        private void FinalizeShapeModification()
        {
            if (_selectedShape == null || _selectedIndex < 0) return;
            if (_selectedIndex >= _displayVM.DrawCommands.Count) return;

            var oldCmd = _displayVM.DrawCommands[_selectedIndex];

            // F4 旋转烘焙：线/三角/弧 → 拍平坐标；矩形/椭/文字 → 保留 RotateTransform
            if (_activeHandle == HandleRole.RotateHandle && _shapeRotationAngle != 0)
            {
                BakeRotationIntoShape();
            }

            // 弧线：平移后 Canvas 偏移 → 累加到 cmd.Args 圆心坐标
            //   (resize 已由 FitShapeToBounds 直接更新 cmd.Args，此处只补平移的 Canvas 偏移)
            if (_selectedShapeDrawType == "arc" && _selectedShape is System.Windows.Shapes.Path arcP2
                && oldCmd.Args != null && oldCmd.Args.Count >= 6)
            {
                int canvasDx = (int)Canvas.GetLeft(arcP2);
                int canvasDy = (int)Canvas.GetTop(arcP2);
                if (canvasDx != 0 || canvasDy != 0)
                {
                    oldCmd.Args[1] = (int.Parse(oldCmd.Args[1]) + canvasDx).ToString();
                    oldCmd.Args[2] = (int.Parse(oldCmd.Args[2]) + canvasDy).ToString();
                }
            }

            var newArgs = ShapeToProtocolArgs(_selectedShape, oldCmd);
            if (newArgs == null) return;

            // 就地更新 DrawCommand
            _displayVM.DrawCommands[_selectedIndex] = new DrawCommand
            {
                Type = oldCmd.Type,
                Args = newArgs,
                Color = _shapeEditColor, // 使用编辑器中的实际颜色
            };

            // 弧线：用新协议参数重建几何，Canvas 归零（防下次拖拽累积误差）
            if (_selectedShapeDrawType == "arc" && _selectedShape is System.Windows.Shapes.Path arcP3
                && newArgs.Count >= 6)
            {
                int newCx = int.Parse(newArgs[1]), newCy = int.Parse(newArgs[2]);
                int newR = int.Parse(newArgs[3]), newSa = int.Parse(newArgs[4]), newEa = int.Parse(newArgs[5]);
                bool newFilled = newArgs.Count > 6 && newArgs[newArgs.Count - 1] == "fill";
                int newLw = GetShapeEditorLineWidth();
                var rebuilt = BuildArcPath(newCx, newCy, newR, newSa, newEa,
                    ParseColorBrush(_shapeEditColor) ?? Brushes.White, newLw, newFilled, preview: false);
                arcP3.Data = rebuilt.Data;
                if (newFilled) { arcP3.Fill = ParseColorBrush(_shapeEditColor) ?? Brushes.White; arcP3.Stroke = null; }
                else { arcP3.Stroke = ParseColorBrush(_shapeEditColor) ?? Brushes.White; arcP3.StrokeThickness = newLw; arcP3.Fill = null; }
                Canvas.SetLeft(arcP3, 0); Canvas.SetTop(arcP3, 0);
            }

            // 重建控点（位置可能已变）
            RemoveControlPoints();
            var bounds = GetShapeBounds(_selectedShape);
            CreateControlPoints(bounds);

            // 刷新侧栏字段（拖拽后数字同步）
            if (rightOLEDShapeEditor.Visibility == Visibility.Visible)
                PopulateShapeEditorFields();
            else if (rightOLEDTextEditor.Visibility == Visibility.Visible && _selectedShape is TextBlock tb)
            {
                tbTextEditX.Text = ((int)Canvas.GetLeft(tb)).ToString();
                tbTextEditY.Text = ((int)Canvas.GetTop(tb)).ToString();
                UpdateSharedProtocolPreview();
            }

            // Point 2 预留接口 —— 当前全量刷新，将来可切增量
            SyncShapeAfterModification(_selectedIndex, newArgs);

            // 更新快照，防止后续取消回退到拖拽前状态
            CaptureOriginalState(_selectedShape, out _originalShapeState);
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

            // 从实际画刷取颜色（不依赖 oldCmd.Color）
            string color = oldCmd.Color ?? "#FFFFFF";
            if (shape is Shape sh)
            {
                bool filled = sh.Fill != null && sh.Stroke == null;
                var brush = (filled ? sh.Fill : sh.Stroke) as SolidColorBrush;
                if (brush != null)
                    color = $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
            }
            else if (shape is TextBlock tb && tb.Foreground is SolidColorBrush tbr)
            {
                color = $"#{tbr.Color.R:X2}{tbr.Color.G:X2}{tbr.Color.B:X2}";
            }

            switch (type)
            {
                case "text":
                    var textTb = (TextBlock)shape;
                    return new List<string> { "text",
                        ((int)Canvas.GetLeft(textTb)).ToString(),
                        ((int)Canvas.GetTop(textTb)).ToString(),
                        textTb.Text,
                        ((int)textTb.FontSize).ToString(), color };

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
                    bool rectFilled = rect.Fill != null && rect.Stroke == null;
                    var rectArgs = new List<string> { "rect",
                        ((int)Canvas.GetLeft(rect)).ToString(), ((int)Canvas.GetTop(rect)).ToString(),
                        ((int)rect.Width).ToString(), ((int)rect.Height).ToString(), color };
                    if (!rectFilled && rect.StrokeThickness != 1) rectArgs.Add(((int)rect.StrokeThickness).ToString());
                    if (rectFilled) rectArgs.Add("fill");
                    return rectArgs;

                case "fill":
                    // 兼容旧 DrawCommand.Type=="fill"（旧协议升级后统一转 rect+fill）
                    var oldFillRect = (System.Windows.Shapes.Rectangle)shape;
                    return new List<string> { "rect",
                        ((int)Canvas.GetLeft(oldFillRect)).ToString(), ((int)Canvas.GetTop(oldFillRect)).ToString(),
                        ((int)oldFillRect.Width).ToString(), ((int)oldFillRect.Height).ToString(), color, "fill" };

                case "circle":
                    var circle = (Ellipse)shape;
                    bool circFilled = circle.Fill != null && circle.Stroke == null;
                    int cx = (int)(Canvas.GetLeft(circle) + circle.Width / 2);
                    int cy = (int)(Canvas.GetTop(circle) + circle.Height / 2);
                    int r = (int)(Math.Max(circle.Width, circle.Height) / 2);
                    var circArgs = new List<string> { "circle", cx.ToString(), cy.ToString(), r.ToString(), color };
                    if (!circFilled && circle.Stroke != null && circle.StrokeThickness != 1) circArgs.Add(((int)circle.StrokeThickness).ToString());
                    if (circFilled) circArgs.Add("fill");
                    return circArgs;

                case "ellipse":
                    var ellipse = (Ellipse)shape;
                    bool ellFilled = ellipse.Fill != null && ellipse.Stroke == null;
                    int ex = (int)(Canvas.GetLeft(ellipse) + ellipse.Width / 2);
                    int ey = (int)(Canvas.GetTop(ellipse) + ellipse.Height / 2);
                    int a = (int)(ellipse.Width / 2);
                    int b = (int)(ellipse.Height / 2);
                    var ellArgs = new List<string> { "ellipse", ex.ToString(), ey.ToString(), a.ToString(), b.ToString(), color };
                    if (!ellFilled && ellipse.Stroke != null && ellipse.StrokeThickness != 1) ellArgs.Add(((int)ellipse.StrokeThickness).ToString());
                    if (ellFilled) ellArgs.Add("fill");
                    return ellArgs;

                case "triangle":
                    var tri = (Polygon)shape;
                    bool triFilled = tri.Fill != null && tri.Stroke == null;
                    var triArgs = new List<string> { "triangle",
                        ((int)tri.Points[0].X).ToString(), ((int)tri.Points[0].Y).ToString(),
                        ((int)tri.Points[1].X).ToString(), ((int)tri.Points[1].Y).ToString(),
                        ((int)tri.Points[2].X).ToString(), ((int)tri.Points[2].Y).ToString(), color };
                    if (!triFilled && tri.StrokeThickness != 1) triArgs.Add(((int)tri.StrokeThickness).ToString());
                    if (triFilled) triArgs.Add("fill");
                    return triArgs;

                case "rrect":
                    var rrect2 = (System.Windows.Shapes.Rectangle)shape;
                    bool rrFilled = rrect2.Fill != null && rrect2.Stroke == null;
                    int rrLw = (int)rrect2.StrokeThickness;
                    var rrArgs = new List<string> { "rrect",
                        ((int)Canvas.GetLeft(rrect2)).ToString(), ((int)Canvas.GetTop(rrect2)).ToString(),
                        ((int)rrect2.Width).ToString(), ((int)rrect2.Height).ToString(), color,
                        rrLw.ToString(), ((int)rrect2.RadiusX).ToString() };
                    if (rrFilled) rrArgs.Add("fill");
                    return rrArgs;

                case "arc":
                    if (shape is System.Windows.Shapes.Path arcPath)
                    {
                        // 从 DrawCommand.Args 读取圆心/半径/角度（Path 难以反向解析几何参数）
                        if (oldCmd.Args != null && oldCmd.Args.Count >= 6)
                        {
                            var arcArgs = new List<string> { "arc",
                                oldCmd.Args[1], oldCmd.Args[2], oldCmd.Args[3],
                                oldCmd.Args[4], oldCmd.Args[5], color };
                            bool arcFilled = arcPath.Fill != null && arcPath.Stroke == null;
                            if (!arcFilled && arcPath.Stroke is SolidColorBrush asb2 && arcPath.StrokeThickness != 1)
                                arcArgs.Add(((int)arcPath.StrokeThickness).ToString());
                            if (arcFilled) arcArgs.Add("fill");
                            return arcArgs;
                        }
                    }
                    return null;
            }
            return null;
        }

        // ═══════════════════════════════════════
        //  F1 形状属性面板（侧栏编辑）
        // ═══════════════════════════════════════

        private void EnterShapeEditMode()
        {
            if (_selectedShape == null || _selectedIndex < 0) return;
            // 锁定时禁止进入编辑模式
            if (_isLocked) return;

            // 保存原始状态（取消时恢复）
            CaptureOriginalState(_selectedShape, out _originalShapeState);

            // text 类型 → 走文字编辑面板（保留原文在画布，可拖拽移动）
            if (_selectedShapeDrawType == "text" && _selectedShape is TextBlock tb)
            {
                _isEditingText = true;
                _textEditPos = new Point(Canvas.GetLeft(tb), Canvas.GetTop(tb));
                _textEditColor = (tb.Foreground as SolidColorBrush)?.Color.ToString() ?? "#FFFFFF";
                if (!_textEditColor.StartsWith("#")) _textEditColor = "#" + _textEditColor;
                _editingTextOriginalKey = $"{(int)_textEditPos.X},{(int)_textEditPos.Y}";

                // 初始化字号下拉
                if (cbTextEditFontSize.Items.Count == 0)
                {
                    int[] sizes = { 8, 10, 12, 14, 16, 18, 20, 24, 28, 32 };
                    foreach (var s in sizes)
                        cbTextEditFontSize.Items.Add(new ComboBoxItem { Content = s.ToString(), Tag = s });
                }
                for (int i = 0; i < cbTextEditFontSize.Items.Count; i++)
                {
                    if (cbTextEditFontSize.Items[i] is ComboBoxItem ci && ci.Tag is int fs && fs == (int)tb.FontSize)
                    { cbTextEditFontSize.SelectedIndex = i; break; }
                }

                tbTextEditX.Text = ((int)_textEditPos.X).ToString();
                tbTextEditY.Text = ((int)_textEditPos.Y).ToString();
                tbTextEditContent.Text = tb.Text;
                textEditColorSwatch.Background = ParseColorBrush(_textEditColor) ?? Brushes.White;

                // 编辑已有文字 → 隐藏确定/取消，显示协议预览
                textEditConfirmRow.Visibility = Visibility.Collapsed;
                sharedProtocolPreview.Visibility = Visibility.Visible;
                UpdateSharedProtocolPreview();

                // 切换侧栏到文字编辑
                rightOLEDSettings.Visibility = Visibility.Collapsed;
                rightOLEDShapeEditor.Visibility = Visibility.Collapsed;
                rightOLEDTextEditor.Visibility = Visibility.Visible;
                tbSidePanelTitle.Text = "文字编辑";

                // 锁定状态同步
                bool txtLocked = IsElementLocked();
                if (btnTextLock != null) btnTextLock.Visibility = Visibility.Visible;
                btnTextLock.Content = txtLocked ? "🔒 解锁" : "🔓 锁定";
                tbTextEditX.IsEnabled = !txtLocked;
                tbTextEditY.IsEnabled = !txtLocked;
                tbTextEditContent.IsEnabled = !txtLocked;
                cbTextEditFontSize.IsEnabled = !txtLocked;

                // 不建预览，保留原文 + 控点（可拖拽中心移动）
                return;
            }

            // 锁定状态同步
            bool shapeLocked = IsElementLocked();
            btnShapeLock.Content = shapeLocked ? "🔒 解锁" : "🔓 锁定";

            // 初始化线宽下拉（只一次）
            if (cbShapeLineWidth.Items.Count == 0)
            {
                int[] widths = { 1, 2, 3, 5, 8 };
                foreach (var lw in widths)
                    cbShapeLineWidth.Items.Add(new ComboBoxItem { Content = lw.ToString(), Tag = lw });
            }

            PopulateShapeEditorFields();

            // 切换侧栏
            rightOLEDSettings.Visibility = Visibility.Collapsed;
            rightOLEDTextEditor.Visibility = Visibility.Collapsed;
            rightOLEDShapeEditor.Visibility = Visibility.Visible;
            sharedProtocolPreview.Visibility = Visibility.Visible;
            tbSidePanelTitle.Text = "图形属性";
        }

        private void ExitShapeEditMode(bool save)
        {
            if (_selectedShape == null) return;

            // text 类型 → 委托给文字编辑退出
            if (_selectedShapeDrawType == "text")
            {
                ExitTextEditMode(save);
                return;
            }

            if (!save)
            {
                // 恢复原始状态
                RestoreOriginalState(_selectedShape, _originalShapeState);
                RemoveControlPoints();
                var bounds = GetShapeBounds(_selectedShape);
                CreateControlPoints(bounds);
            }
            else
            {
                // 保存到 DrawCommand + 同步设备
                if (_selectedIndex >= 0 && _selectedIndex < _displayVM.DrawCommands.Count)
                {
                    var oldCmd = _displayVM.DrawCommands[_selectedIndex];
                    var newArgs = ShapeToProtocolArgs(_selectedShape, oldCmd);
                    if (newArgs != null)
                    {
                        _displayVM.DrawCommands[_selectedIndex] = new DrawCommand
                        {
                            Type = oldCmd.Type,
                            Args = newArgs,
                            Color = _shapeEditColor, // 使用编辑器中的实际颜色
                        };
                        SyncShapeAfterModification(_selectedIndex, newArgs);
                    }
                }
            }

            _originalShapeState = null;

            // 恢复侧栏
            rightOLEDShapeEditor.Visibility = Visibility.Collapsed;
            rightOLEDSettings.Visibility = Visibility.Visible;
            sharedProtocolPreview.Visibility = Visibility.Collapsed;
            tbSidePanelTitle.Text = "OLED 设置";
        }

        /// <summary>动态添加一行字段。subB 为空时只创建单列。</summary>
        private (TextBox a, TextBox b) AddFieldRow(string label, string subA, string subB, string valA, string valB)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(new TextBlock { Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryBrush"), Margin = new Thickness(0, 0, 0, 4) });

            var grid = new Grid();
            bool single = string.IsNullOrEmpty(subB);
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            if (!single) grid.ColumnDefinitions.Add(new ColumnDefinition());

            TextBox mkCol(string sub, string val, int col, Thickness margin)
            {
                var sp = new StackPanel { Margin = margin };
                sp.Children.Add(new TextBlock { Text = sub, FontSize = 10,
                    Foreground = (Brush)FindResource("TextMutedBrush"), Margin = new Thickness(0, 0, 0, 2) });
                var tb = new TextBox { Text = val, Height = 28, FontFamily = new FontFamily("Microsoft YaHei"),
                    FontSize = 12, Foreground = (Brush)FindResource("TextPrimaryBrush"),
                    Background = (Brush)FindResource("CardBgBrush"),
                    BorderBrush = (Brush)FindResource("InputBorderBrush"),
                    BorderThickness = new Thickness(1), Padding = new Thickness(6, 2, 6, 2) };
                tb.TextChanged += ShapeField_Changed;
                sp.Children.Add(tb);
                Grid.SetColumn(sp, col);
                grid.Children.Add(sp);
                return tb;
            }

            var tbA = mkCol(subA, valA, 0, new Thickness(0));
            TextBox tbB = null;
            if (!single)
                tbB = mkCol(subB, valB, 1, new Thickness(3, 0, 0, 0));

            row.Children.Add(grid);
            shapeFieldsContainer.Children.Add(row);
            return (tbA, tbB);
        }

        /// <summary>将选中图形的当前参数填入编辑器字段</summary>
        private void PopulateShapeEditorFields()
        {
            if (_selectedShape == null) return;
            _populatingShapeEditor = true;

            // 清空旧字段
            shapeFieldsContainer.Children.Clear();
            shapeExtrasContainer.Children.Clear();
            _shapeFieldTextBoxes.Clear();

            string type = _selectedShapeDrawType ?? "rect";
            bool isFilled = false;
            string color = "#FFFFFF";
            int lw = 1;

            // 从 Shape 实际画刷取颜色
            if (_selectedShape is Shape sh)
            {
                isFilled = sh.Fill != null && sh.Stroke == null;
                var scb = (isFilled ? sh.Fill : sh.Stroke) as SolidColorBrush;
                if (scb != null) color = $"#{scb.Color.R:X2}{scb.Color.G:X2}{scb.Color.B:X2}";
                if (sh.Stroke != null) lw = (int)sh.StrokeThickness;
            }

            _shapeEditColor = color;

            // 标题
            string[] typeNames = { "point", "rect", "circle", "line", "triangle", "ellipse", "fill", "rrect", "arc" };
            string[] typeLabels = { "点", "矩形", "圆", "直线", "三角形", "椭圆", "实心矩形", "圆角矩形", "弧线" };
            int ti = Array.IndexOf(typeNames, type);
            string typeLabel = ti >= 0 ? typeLabels[ti] : type;
            tbShapeEditTitle.Text = $"图形属性 — {typeLabel}";
            tbShapeEditType.Text = isFilled ? $"类型: {typeLabel}（实心）" : $"类型: {typeLabel}";

            // 默认隐藏（各 case 按需打开）
            cbShapeFill.Visibility = Visibility.Collapsed;

            // 按类型动态生成字段行
            void add(string label, string a, string b, string va, string vb)
            { var (t1, t2) = AddFieldRow(label, a, b, va, vb); _shapeFieldTextBoxes.Add(t1); _shapeFieldTextBoxes.Add(t2); }
            void addSingle(string label, string a, string va)
            { var (t1, _) = AddFieldRow(label, a, "", va, ""); _shapeFieldTextBoxes.Add(t1); }

            switch (type)
            {
                case "point":
                    var ptRect = (System.Windows.Shapes.Rectangle)_selectedShape;
                    add("位置", "X", "Y", ((int)Canvas.GetLeft(ptRect)).ToString(), ((int)Canvas.GetTop(ptRect)).ToString());
                    isFilled = true;
                    break;

                case "rect":
                case "fill":
                    var rect = (System.Windows.Shapes.Rectangle)_selectedShape;
                    add("位置", "X", "Y", ((int)Canvas.GetLeft(rect)).ToString(), ((int)Canvas.GetTop(rect)).ToString());
                    add("尺寸", "宽", "高", ((int)rect.Width).ToString(), ((int)rect.Height).ToString());
                    cbShapeFill.Visibility = Visibility.Visible;
                    break;

                case "circle":
                    var circle = (Ellipse)_selectedShape;
                    int cx = (int)(Canvas.GetLeft(circle) + circle.Width / 2);
                    int cy = (int)(Canvas.GetTop(circle) + circle.Height / 2);
                    int r = (int)(Math.Max(circle.Width, circle.Height) / 2);
                    add("圆心", "CX", "CY", cx.ToString(), cy.ToString());
                    addSingle("半径", "R", r.ToString());
                    cbShapeFill.Visibility = Visibility.Visible;
                    break;

                case "ellipse":
                    var ell = (Ellipse)_selectedShape;
                    int ecx = (int)(Canvas.GetLeft(ell) + ell.Width / 2);
                    int ecy = (int)(Canvas.GetTop(ell) + ell.Height / 2);
                    add("圆心", "CX", "CY", ecx.ToString(), ecy.ToString());
                    add("半轴", "A", "B", ((int)(ell.Width / 2)).ToString(), ((int)(ell.Height / 2)).ToString());
                    cbShapeFill.Visibility = Visibility.Visible;
                    break;

                case "line":
                    var line = (Line)_selectedShape;
                    add("起点", "X1", "Y1", ((int)line.X1).ToString(), ((int)line.Y1).ToString());
                    add("终点", "X2", "Y2", ((int)line.X2).ToString(), ((int)line.Y2).ToString());
                    break;

                case "triangle":
                    var tri = (Polygon)_selectedShape;
                    add("顶点①", "X", "Y", ((int)tri.Points[0].X).ToString(), ((int)tri.Points[0].Y).ToString());
                    add("顶点②", "X", "Y", ((int)tri.Points[1].X).ToString(), ((int)tri.Points[1].Y).ToString());
                    add("顶点③", "X", "Y", ((int)tri.Points[2].X).ToString(), ((int)tri.Points[2].Y).ToString());
                    cbShapeFill.Visibility = Visibility.Visible;
                    break;

                case "rrect":
                    var rr = (System.Windows.Shapes.Rectangle)_selectedShape;
                    add("位置", "X", "Y", ((int)Canvas.GetLeft(rr)).ToString(), ((int)Canvas.GetTop(rr)).ToString());
                    add("尺寸", "宽", "高", ((int)rr.Width).ToString(), ((int)rr.Height).ToString());
                    addSingle("圆角", "R", ((int)rr.RadiusX).ToString());
                    cbShapeFill.Visibility = Visibility.Visible;
                    break;

                case "arc":
                    // 从 DrawCommand 读取几何参数（Path 难反向解析）
                    var arcCmd = _selectedIndex >= 0 && _selectedIndex < _displayVM.DrawCommands.Count
                        ? _displayVM.DrawCommands[_selectedIndex] : null;
                    string arCx = "0", arCy = "0", arR = "10", arSa = "0", arEa = "180";
                    if (arcCmd?.Args != null && arcCmd.Args.Count >= 6)
                    {
                        arCx = arcCmd.Args[1]; arCy = arcCmd.Args[2]; arR = arcCmd.Args[3];
                        arSa = arcCmd.Args[4]; arEa = arcCmd.Args[5];
                    }
                    add("圆心", "CX", "CY", arCx, arCy);
                    addSingle("半径", "R", arR);
                    add("角度 (°)", "起始", "终止", arSa, arEa);
                    cbShapeFill.Visibility = Visibility.Visible;
                    break;
            }

            // 线宽面板 / 圆角半径面板显隐
            bool isArc = type == "arc";
            if (isArc)
            {
                // 弧线：角度行中不显示圆角（复用 extras 区加说明）
                shapeExtrasContainer.Children.Clear();
                var note = new TextBlock
                {
                    Text = "角度：右=0°，顺时针为正（下=90°）",
                    FontSize = 10,
                    Foreground = (Brush)FindResource("TextMutedBrush"),
                    Margin = new Thickness(0, 2, 0, 4)
                };
                shapeExtrasContainer.Children.Add(note);
            }

            // 填充复选框
            cbShapeFill.IsChecked = isFilled;

            // 线宽
            shapeLineWidthPanel.Visibility = isFilled ? Visibility.Collapsed : Visibility.Visible;
            if (!isFilled)
            {
                for (int i = 0; i < cbShapeLineWidth.Items.Count; i++)
                {
                    if (cbShapeLineWidth.Items[i] is ComboBoxItem ci && ci.Tag is int w && w == lw)
                    { cbShapeLineWidth.SelectedIndex = i; break; }
                }
            }

            // 颜色
            var brush = ParseColorBrush(color) ?? Brushes.White;
            shapeEditColorSwatch.Background = brush;

            // 锁定态：禁用所有字段
            if (IsElementLocked())
            {
                foreach (var tb in _shapeFieldTextBoxes)
                    tb.IsEnabled = false;
                cbShapeFill.IsEnabled = false;
                cbShapeLineWidth.IsEnabled = false;
                // 删除按钮无 x:Name，在 btnShapeDelete_Click 里守卫
            }

            UpdateSharedProtocolPreview();
            _populatingShapeEditor = false;
        }

        /// <summary>侧栏字段变更 → 实时更新画布图形</summary>
        private void ShapeField_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLocked || _populatingShapeEditor || _selectedShape == null) return;
            if (IsElementLocked()) return;
            ApplyEditorToShape();
        }

        /// <summary>从编辑器读取所有字段，应用到选中图形</summary>
        private void ApplyEditorToShape()
        {
            if (_selectedShape == null) return;

            string type = _selectedShapeDrawType ?? "rect";
            bool isFilled = cbShapeFill.IsChecked == true;
            string color = _shapeEditColor;
            var brush = ParseColorBrush(color) ?? Brushes.White;
            int lw = GetShapeEditorLineWidth();

            // 从动态字段列表读值
            int F(int i) => i < _shapeFieldTextBoxes.Count && int.TryParse(_shapeFieldTextBoxes[i].Text, out int v) ? v : 0;
            int f1 = F(0), f2 = F(1), f3 = F(2), f4 = F(3), f5 = F(4), f6 = F(5);

            switch (type)
            {
                case "point":
                    var pt = (System.Windows.Shapes.Rectangle)_selectedShape;
                    Canvas.SetLeft(pt, f1);
                    Canvas.SetTop(pt, f2);
                    pt.Fill = brush;
                    break;

                case "rect":
                case "fill":
                    var rect = (System.Windows.Shapes.Rectangle)_selectedShape;
                    rect.Width = Math.Max(1, f3);
                    rect.Height = Math.Max(1, f4);
                    Canvas.SetLeft(rect, f1);
                    Canvas.SetTop(rect, f2);
                    if (isFilled) { rect.Fill = brush; rect.Stroke = null; }
                    else { rect.Stroke = brush; rect.StrokeThickness = lw; rect.Fill = null; }
                    break;

                case "circle":
                    var circle = (Ellipse)_selectedShape;
                    int cr = Math.Max(1, f3);
                    circle.Width = cr * 2; circle.Height = cr * 2;
                    Canvas.SetLeft(circle, f1 - cr);
                    Canvas.SetTop(circle, f2 - cr);
                    if (isFilled) { circle.Fill = brush; circle.Stroke = null; }
                    else { circle.Stroke = brush; circle.StrokeThickness = lw; circle.Fill = null; }
                    break;

                case "ellipse":
                    var ellipse = (Ellipse)_selectedShape;
                    int ea = Math.Max(1, f3), eb = Math.Max(1, f4);
                    ellipse.Width = ea * 2; ellipse.Height = eb * 2;
                    Canvas.SetLeft(ellipse, f1 - ea);
                    Canvas.SetTop(ellipse, f2 - eb);
                    if (isFilled) { ellipse.Fill = brush; ellipse.Stroke = null; }
                    else { ellipse.Stroke = brush; ellipse.StrokeThickness = lw; ellipse.Fill = null; }
                    break;

                case "line":
                    var line = (Line)_selectedShape;
                    line.X1 = f1; line.Y1 = f2; line.X2 = f3; line.Y2 = f4;
                    line.Stroke = brush; line.StrokeThickness = lw;
                    break;

                case "triangle":
                    var tri = (Polygon)_selectedShape;
                    tri.Points = new PointCollection { new Point(f1, f2), new Point(f3, f4), new Point(f5, f6) };
                    if (isFilled) { tri.Fill = brush; tri.Stroke = null; }
                    else { tri.Stroke = brush; tri.StrokeThickness = lw; tri.Fill = null; }
                    break;

                case "rrect":
                    var rrect2 = (System.Windows.Shapes.Rectangle)_selectedShape;
                    rrect2.Width = Math.Max(1, f3);
                    rrect2.Height = Math.Max(1, f4);
                    rrect2.RadiusX = f5; rrect2.RadiusY = f5;
                    Canvas.SetLeft(rrect2, f1);
                    Canvas.SetTop(rrect2, f2);
                    if (isFilled) { rrect2.Fill = brush; rrect2.Stroke = null; }
                    else { rrect2.Stroke = brush; rrect2.StrokeThickness = lw; rrect2.Fill = null; }
                    break;

                case "arc":
                    // 更新弧线 Path：重建几何
                    if (_selectedShape is System.Windows.Shapes.Path arcP)
                    {
                        int arCr = Math.Max(1, f3);
                        var newArcPath = BuildArcPath(f1, f2, arCr, f4, f5, brush, lw, isFilled, preview: false);
                        arcP.Data = newArcPath.Data;
                        if (isFilled) { arcP.Fill = brush; arcP.Stroke = null; }
                        else { arcP.Stroke = brush; arcP.StrokeThickness = lw; arcP.Fill = null; }
                        // 几何含绝对坐标，Canvas 归零
                        Canvas.SetLeft(arcP, 0); Canvas.SetTop(arcP, 0);
                        // 更新 DrawCommand 参数
                        if (_selectedIndex >= 0 && _selectedIndex < _displayVM.DrawCommands.Count)
                        {
                            var cmd = _displayVM.DrawCommands[_selectedIndex];
                            if (cmd.Args != null && cmd.Args.Count >= 6)
                            {
                                cmd.Args[1] = f1.ToString();
                                cmd.Args[2] = f2.ToString();
                                cmd.Args[3] = arCr.ToString();
                                cmd.Args[4] = f4.ToString();
                                cmd.Args[5] = f5.ToString();
                            }
                        }
                    }
                    break;
            }

            // 同步默认画笔颜色/线宽
            _drawColor = color;
            if (!isFilled) _drawLineWidth = lw;

            // 线宽面板显隐
            shapeLineWidthPanel.Visibility = isFilled ? Visibility.Collapsed : Visibility.Visible;

            // 更新选择框和控点
            RemoveControlPoints();
            var bounds = GetShapeBounds(_selectedShape);
            CreateControlPoints(bounds);

            // 更新类型标签
            string[] typeNames = { "point", "rect", "circle", "line", "triangle", "ellipse", "fill", "rrect", "arc" };
            string[] typeLabels = { "点", "矩形", "圆", "直线", "三角形", "椭圆", "实心矩形", "圆角矩形", "弧线" };
            int ti = Array.IndexOf(typeNames, type);
            tbShapeEditType.Text = isFilled ? $"类型: {(ti >= 0 ? typeLabels[ti] : type)}（实心）" : $"类型: {(ti >= 0 ? typeLabels[ti] : type)}";

            UpdateSharedProtocolPreview();
        }

        private void btnCopyProtocol_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbProtocolPreview.Text) || tbProtocolPreview.Text == "—") return;
            SafeSetClipboard(tbProtocolPreview.Text);
            if (sender is Button btn) ShowCopyToastAndShake(btn);
        }

        private int GetShapeEditorLineWidth()
        {
            if (cbShapeLineWidth.SelectedItem is ComboBoxItem item && item.Tag is int w)
                return w;
            return 1;
        }

        // ── 单元素锁定 ──
        private bool IsElementLocked()
        {
            return _selectedIndex >= 0 && _lockedShapeIndices.Contains(_selectedIndex);
        }

        private void SetElementLock(bool locked)
        {
            if (_selectedIndex < 0) return;
            if (locked) _lockedShapeIndices.Add(_selectedIndex);
            else _lockedShapeIndices.Remove(_selectedIndex);

            // 更新 UI
            string lockLabel = locked ? "🔒 解锁" : "🔓 锁定";
            if (btnShapeLock != null) btnShapeLock.Content = lockLabel;
            if (btnTextLock != null) btnTextLock.Content = lockLabel;

            // 锁定态：禁用所有字段 + 重建控点为灰色
            bool isLocked = locked;
            foreach (var tb in _shapeFieldTextBoxes)
                tb.IsEnabled = !isLocked;
            cbShapeFill.IsEnabled = !isLocked;
            cbShapeLineWidth.IsEnabled = !isLocked;
            tbTextEditX.IsEnabled = !isLocked;
            tbTextEditY.IsEnabled = !isLocked;
            tbTextEditContent.IsEnabled = !isLocked;
            cbTextEditFontSize.IsEnabled = !isLocked;
            // btnShapeDelete 无 x:Name，在 Click handler 里判断

            // 重建控点为灰色
            RemoveControlPoints();
            var bounds = GetShapeBounds(_selectedShape);
            CreateControlPoints(bounds);
        }

        private void btnShapeLock_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedIndex < 0) return;
            SetElementLock(!IsElementLocked());
        }

        private void btnTextLock_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedIndex < 0) return;
            SetElementLock(!IsElementLocked());
        }

        private void btnShapeEditCancel_Click(object sender, RoutedEventArgs e)
        {
            // ✕ = 取消编辑：恢复原始状态，取消选中，侧栏回设置
            if (_selectedShape != null)
                ExitShapeEditMode(save: false);
            RemoveControlPoints();
            _selectedShape = null;
            _selectedIndex = -1;
            _selectedShapeDrawType = null;
            _originalShapeState = null;
            _hasMoved = false;
        }

        private void btnShapeDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedShape == null) return;
            if (IsElementLocked()) return; // 锁定元素不可删除
            int idx = _selectedIndex;

            // 从画布移除
            oledCanvas.Children.Remove(_selectedShape);
            _drawElements.Remove(_selectedShape);

            // 从协议列表移除
            if (idx >= 0 && idx < _displayVM.DrawCommands.Count)
                _displayVM.DrawCommands.RemoveAt(idx);

            // 维护锁定索引（删除后后续索引前移）
            _lockedShapeIndices.Remove(idx);
            var shifted = _lockedShapeIndices.Where(i => i > idx).ToList();
            foreach (var i in shifted) { _lockedShapeIndices.Remove(i); _lockedShapeIndices.Add(i - 1); }

            // 如果设备已连，同步删除
            if (_session != null && _session.IsOpen)
                SyncAllShapesToDevice();

            // 清除选中态 + 恢复侧栏
            RemoveControlPoints();
            _selectedShape = null;
            _selectedIndex = -1;
            _selectedShapeDrawType = null;
            _originalShapeState = null;
            _hasMoved = false;

            rightOLEDShapeEditor.Visibility = Visibility.Collapsed;
            rightOLEDSettings.Visibility = Visibility.Visible;
            tbSidePanelTitle.Text = "OLED 设置";
        }

        private void shapeEditColorSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedShape == null) return;
            ShowDrawColorPicker(shapeEditColorSwatch, hex => shapeEditColorApply(hex));
        }

        private void shapeEditColorApply(string hex)
        {
            _shapeEditColor = hex;
            var brush = ParseColorBrush(hex);
            if (brush != null) shapeEditColorSwatch.Background = brush;
            ApplyEditorToShape();
        }

        // ═══════════════════════════════════════
        //  导出 C 数组
        // ═══════════════════════════════════════

        private void btnExportC_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_displayVM == null) { LogSystem("⚠ 导出失败：画布未初始化"); return; }
                int w = _displayVM.CanvasWidth, h = _displayVM.CanvasHeight;

                // 强制刷新布局再光栅化
                oledCanvas.UpdateLayout();
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
                            byte gray = (byte)((pixels[idx+1] + pixels[idx+2] + pixels[idx+3]) / 3);
                            if (gray > 128) b |= (byte)(1 << bit);
                        }
                        bits.Add(b);
                    }
                }

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
            catch (Exception ex)
            {
                LogSystem($"✗ 导出 C 数组失败：{ex.Message}");
            }
        }
    }
}
