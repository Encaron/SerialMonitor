using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Linq;
using System.Windows.Media;
using System.Windows.Threading;

namespace 串口助手
{
    public partial class MainWindow : Window
    {
        // ——— 串口参数状态 ———

        private string receiveMode = "HEX模式";
        private string receiveCoding = "GBK";
        private string sendMode = "HEX模式";
        private string sendCoding = "GBK";

        private List<byte> byteBuffer = new List<byte>();
        private SerialPort serialPort = new SerialPort();
        private bool isSerialOpen = false;

        // ——— 颜色常量（与 XAML 资源保持一致）———

        private static readonly Color PrimaryColor       = Color.FromRgb(0x00, 0x78, 0xD4);
        private static readonly Color SuccessColor       = Color.FromRgb(0x10, 0xB9, 0x81);
        private static readonly Color LogSystemColor     = Color.FromRgb(0x99, 0x99, 0x99);
        private static readonly Color LogSentColor       = Color.FromRgb(0x00, 0x78, 0xD4);
        private static readonly Color LogReceivedColorLight = Color.FromRgb(0x2D, 0x2D, 0x2D);
        private static readonly Color LogReceivedColorDark  = Color.FromRgb(0xE0, 0xE0, 0xE0);
        private static readonly Color StatusDotIdleLight    = Color.FromRgb(0xCC, 0xCC, 0xCC);
        private static readonly Color StatusDotIdleDark     = Color.FromRgb(0x66, 0x66, 0x66);

        //  运行时根据主题切换 ↓
        private static Color LogReceivedColor = LogReceivedColorLight;
        private static Color StatusDotIdle    = StatusDotIdleLight;

        // ——— 接收缓冲：跨 DataReceived 碎片拼成完整行 ———

        /// <summary>
        /// 文本模式接收缓冲区：累积碎片直到遇到换行符再输出
        /// </summary>
        private string receiveLineBuffer = "";

        /// <summary>
        /// 空闲定时器：100ms 无新数据到达时，强制输出缓冲区剩余文本
        /// </summary>
        private DispatcherTimer flushTimer;

        // ——— 定时发送 ———
        private DispatcherTimer autoSendTimer;

        // ——— 日志行数上限 ———

        private const int MaxLogLines = 2000;

        // ——— 行号 ———
        private int _lineCount = 0;
        private bool _showLineNumbers = true;

        // ——— 流量统计 ———
        private long txByteCount = 0;
        private long rxByteCount = 0;

        // ——— 快捷发送 ———
        /// <summary>
        /// label → content（content 中的 \r\n 以字面量存储，发送时还原）
        /// </summary>
        private Dictionary<string, string> quickSends = new Dictionary<string, string>();

        private string QuickSendsFilePath => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "串口助手WPF", "quick_sends.cfg");

        // ——— 发送历史 ———
        private List<string> sendHistory = new List<string>();
        private const int MaxSendHistory = 20;

        // ——— HEX 输入格式化 ———
        private bool _isHexFormatting = false;

        // ——— 自动重连 ———
        private string lastPortName = "";
        private DispatcherTimer reconnectTimer;
        private const int MaxReconnectAttempts = 10;
        private int _reconnectAttempts = 0;

        // ——— 动画画刷（非冻结，支持 ColorAnimation）———
        private SolidColorBrush statusDotBrush;

        // ——— 字体检测 ———
        private bool fontMissing = false;

        // ——— 暗色主题 ———
        private bool isDarkTheme = false;

        private static readonly Dictionary<string, (Color Light, Color Dark)> ThemeMap =
            new Dictionary<string, (Color, Color)>
        {
            ["WindowBgBrush"]      = (Color.FromRgb(0xF0,0xF0,0xF0), Color.FromRgb(0x1E,0x1E,0x1E)),
            ["CardBgBrush"]        = (Color.FromRgb(0xFF,0xFF,0xFF), Color.FromRgb(0x2D,0x2D,0x2D)),
            ["CardBorderBrush"]    = (Color.FromRgb(0xE0,0xE0,0xE0), Color.FromRgb(0x40,0x40,0x40)),
            ["TextPrimaryBrush"]   = (Color.FromRgb(0x2D,0x2D,0x2D), Color.FromRgb(0xE0,0xE0,0xE0)),
            ["TextSecondaryBrush"] = (Color.FromRgb(0x55,0x55,0x55), Color.FromRgb(0xBB,0xBB,0xBB)),
            ["TextMutedBrush"]     = (Color.FromRgb(0x99,0x99,0x99), Color.FromRgb(0x88,0x88,0x88)),
            ["PrimaryHoverBrush"]  = (Color.FromRgb(0x10,0x6E,0xBE), Color.FromRgb(0x1A,0x8C,0xE8)),
            ["SecondaryBrush"]     = (Color.FromRgb(0x88,0x88,0x88), Color.FromRgb(0xAA,0xAA,0xAA)),
            ["SecondaryHoverBrush"]= (Color.FromRgb(0x66,0x66,0x66), Color.FromRgb(0xBB,0xBB,0xBB)),
            ["StatusBarBgBrush"]   = (Color.FromRgb(0xE6,0xE6,0xE6), Color.FromRgb(0x25,0x25,0x25)),
            ["SeparatorBrush"]     = (Color.FromRgb(0xEE,0xEE,0xEE), Color.FromRgb(0x38,0x38,0x38)),
            ["InputBorderBrush"]   = (Color.FromRgb(0xD0,0xD0,0xD0), Color.FromRgb(0x50,0x50,0x50)),
            ["LogSystemBrush"]     = (Color.FromRgb(0x99,0x99,0x99), Color.FromRgb(0x88,0x88,0x88)),
            ["LogReceivedBrush"]   = (Color.FromRgb(0x2D,0x2D,0x2D), Color.FromRgb(0xE0,0xE0,0xE0)),
            ["TextSubtleBrush"]    = (Color.FromRgb(0xBB,0xBB,0xBB), Color.FromRgb(0x66,0x66,0x66)),
            ["TextFaintBrush"]     = (Color.FromRgb(0xCC,0xCC,0xCC), Color.FromRgb(0x55,0x55,0x55)),
            ["SecondaryHoverBgBrush"]   = (Color.FromRgb(0xF5,0xF5,0xF5), Color.FromRgb(0x38,0x38,0x38)),
            ["ButtonDisabledBgBrush"]   = (Color.FromRgb(0xE8,0xE8,0xE8), Color.FromRgb(0x33,0x33,0x33)),
            ["ReceiveAreaBgBrush"]      = (Color.FromRgb(0xFA,0xFA,0xFA), Color.FromRgb(0x22,0x22,0x22)),
            ["StatusDotIdleBrush"]      = (Color.FromRgb(0xCC,0xCC,0xCC), Color.FromRgb(0x66,0x66,0x66)),
        };

        public MainWindow()
        {
            InitializeComponent();

            // 恢复上次窗口位置和大小
            LoadWindowSettings();

            // 动画画刷：单独创建非冻结实例，后续通过 ColorAnimation 驱动
            statusDotBrush = new SolidColorBrush(StatusDotIdle);
            statusDot.Fill = statusDotBrush;

            // 检测更纱黑体是否安装（未安装 → 状态栏琥珀色提示）
            bool hasSarasa = Fonts.SystemFontFamilies.Any(f =>
                f.Source.IndexOf("Sarasa Mono SC", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!hasSarasa)
            {
                fontMissing = true;
                tbPortInfo.Text = "💡 更纱黑体未安装 → 使用备用等宽字体";
                tbPortInfo.Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0x80, 0x00));
                tbPortInfo.Visibility = Visibility.Visible;
            }

            // 空闲刷新定时器：数据停止到达 100ms 后输出缓冲区剩余文本
            flushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            flushTimer.Tick += FlushReceiveBuffer;

            // 自动重连定时器：设备重新插入后延迟检测并重连
            reconnectTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            reconnectTimer.Tick += (s, args) =>
            {
                string[] ports = SerialPort.GetPortNames();
                if (ports.Contains(lastPortName))
                {
                    reconnectTimer.Stop();
                    _reconnectAttempts = 0;
                    cbPortName.Text = lastPortName;
                    OpenSerialPort();
                    LogSystem($"---- 自动重连：已重新连接 {lastPortName} ----");
                }
                else
                {
                    _reconnectAttempts++;
                    if (_reconnectAttempts >= MaxReconnectAttempts)
                    {
                        reconnectTimer.Stop();
                        _reconnectAttempts = 0;
                        LogSystem($"---- 自动重连超时：未检测到 {lastPortName} ----");
                    }
                }
            };

            // 定时发送计时器
            autoSendTimer = new DispatcherTimer();
            autoSendTimer.Tick += (s, args) => { if (serialPort.IsOpen) SendData(); };

            InitComboBoxItems();
            SetDefaultValues();

            // 加载用户偏好（需在 InitComboBoxItems + SetDefaultValues 之后）
            LoadPreferences();

            // 应用系统消息独立显示的初始可见性
            lbSystemLog.Visibility = chkSeparateSystemLog.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;

            // 加载快捷发送按钮
            LoadQuickSends();
            RefreshQuickSendButtons();

            serialPort.DataReceived += serialPort_DataReceived;
        }

        // ==================================================================
        //  窗口位置记忆
        // ==================================================================

        private string SettingsFilePath => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "串口助手WPF", "window.cfg");

        private void LoadWindowSettings()
        {
            try
            {
                if (!System.IO.File.Exists(SettingsFilePath)) return;

                var dict = new Dictionary<string, string>();
                foreach (var line in System.IO.File.ReadAllLines(SettingsFilePath))
                {
                    int idx = line.IndexOf('=');
                    if (idx > 0) dict[line.Substring(0, idx)] = line.Substring(idx + 1);
                }

                if (dict.TryGetValue("Left",   out string l) && double.TryParse(l, out double left))
                    this.Left = Math.Max(0, Math.Min(left, SystemParameters.PrimaryScreenWidth - 100));
                if (dict.TryGetValue("Top",    out string t) && double.TryParse(t, out double top))
                    this.Top  = Math.Max(0, Math.Min(top,  SystemParameters.PrimaryScreenHeight - 100));
                if (dict.TryGetValue("Width",  out string w) && double.TryParse(w, out double width))
                    this.Width  = Math.Max(MinWidth,  Math.Min(width,  SystemParameters.PrimaryScreenWidth));
                if (dict.TryGetValue("Height", out string h) && double.TryParse(h, out double height))
                    this.Height = Math.Max(MinHeight, Math.Min(height, SystemParameters.PrimaryScreenHeight));
                if (dict.TryGetValue("State",  out string s) && s == "Maximized")
                    this.WindowState = WindowState.Maximized;

                // 加载主题偏好
                if (dict.TryGetValue("Theme", out string theme) && theme == "Dark")
                {
                    ApplyTheme(true);
                }
            }
            catch { /* 静默失败，使用默认值 */ }
        }

        private void SaveWindowSettings()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(SettingsFilePath);
                System.IO.Directory.CreateDirectory(dir);

                var lines = new[]
                {
                    $"Left={this.Left}",
                    $"Top={this.Top}",
                    $"Width={this.Width}",
                    $"Height={this.Height}",
                    $"State={this.WindowState}",
                    $"Theme={(isDarkTheme ? "Dark" : "Light")}",
                    $"LineEnding={cbLineEnding.SelectedItem}",
                    $"AutoClear={(chkAutoClear.IsChecked == true ? "1" : "0")}",
                    $"AutoReconnect={(chkAutoReconnect.IsChecked == true ? "1" : "0")}",
                    $"ShowEcho={(chkShowEcho.IsChecked == true ? "1" : "0")}",
                    $"ShowLineNumbers={(chkShowLineNumbers.IsChecked == true ? "1" : "0")}",
                    $"PersistTraffic={(chkPersistTraffic.IsChecked == true ? "1" : "0")}",
                    $"SeparateSystemLog={(chkSeparateSystemLog.IsChecked == true ? "1" : "0")}",
                };
                System.IO.File.WriteAllLines(SettingsFilePath, lines);
            }
            catch { /* 静默失败，不影响关闭 */ }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowSettings();
        }

        /// <summary>
        /// 从配置文件加载 UI 偏好（ComboBox 选择 + CheckBox 状态）
        /// 需在 InitComboBoxItems + SetDefaultValues 之后调用
        /// </summary>
        private void LoadPreferences()
        {
            try
            {
                if (!System.IO.File.Exists(SettingsFilePath)) return;

                var dict = new Dictionary<string, string>();
                foreach (var line in System.IO.File.ReadAllLines(SettingsFilePath))
                {
                    int idx = line.IndexOf('=');
                    if (idx > 0) dict[line.Substring(0, idx)] = line.Substring(idx + 1);
                }

                if (dict.TryGetValue("LineEnding", out string le))
                {
                    int idx = cbLineEnding.Items.IndexOf(le);
                    if (idx >= 0) cbLineEnding.SelectedIndex = idx;
                }
                if (dict.TryGetValue("AutoClear", out string ac))
                    chkAutoClear.IsChecked = ac == "1";
                if (dict.TryGetValue("AutoReconnect", out string ar))
                    chkAutoReconnect.IsChecked = ar == "1";
                if (dict.TryGetValue("PersistTraffic", out string pt))
                    chkPersistTraffic.IsChecked = pt == "1";
                if (dict.TryGetValue("ShowEcho", out string se))
                    chkShowEcho.IsChecked = se == "1";
                if (dict.TryGetValue("ShowLineNumbers", out string sln))
                    chkShowLineNumbers.IsChecked = sln == "1";
                if (dict.TryGetValue("SeparateSystemLog", out string ssl))
                    chkSeparateSystemLog.IsChecked = ssl == "1";
            }
            catch { /* 静默失败 */ }
        }

        // ==================================================================
        //  暗色主题
        // ==================================================================

        private void ApplyTheme(bool dark)
        {
            isDarkTheme = dark;

            foreach (var kv in ThemeMap)
            {
                var brush = FindResource(kv.Key) as SolidColorBrush;
                if (brush == null) continue;

                Color target = dark ? kv.Value.Dark : kv.Value.Light;
                if (brush.IsFrozen)
                    Resources[kv.Key] = new SolidColorBrush(target);
                else
                    brush.Color = target;
            }

            // 同步 C# 常量
            LogReceivedColor = dark ? LogReceivedColorDark : LogReceivedColorLight;
            StatusDotIdle    = dark ? StatusDotIdleDark    : StatusDotIdleLight;

            // 同步代码创建的状态圆点画刷
            if (statusDotBrush != null && !statusDotBrush.IsFrozen)
                statusDotBrush.Color = isSerialOpen ? SuccessColor : StatusDotIdle;

            // 切换按钮图标
            btnThemeToggle.Content = dark ? "☀" : "🌙";

            LogSystem($"---- 主题切换：{(dark ? "暗色" : "亮色")} ----");
        }

        private void btnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(!isDarkTheme);
        }

        // ==================================================================
        //  窗口初始化
        // ==================================================================

        private void InitComboBoxItems()
        {
            cbBaudRate.Items.Add("4800");
            cbBaudRate.Items.Add("9600");
            cbBaudRate.Items.Add("38400");
            cbBaudRate.Items.Add("115200");

            cbDataBits.Items.Add("5");
            cbDataBits.Items.Add("6");
            cbDataBits.Items.Add("7");
            cbDataBits.Items.Add("8");

            cbStopBits.Items.Add("1");
            cbStopBits.Items.Add("1.5");
            cbStopBits.Items.Add("2");

            cbParity.Items.Add("无");
            cbParity.Items.Add("奇校验");
            cbParity.Items.Add("偶校验");

            cbReceiveMode.Items.Add("HEX模式");
            cbReceiveMode.Items.Add("文本模式");

            cbReceiveCoding.Items.Add("GBK");
            cbReceiveCoding.Items.Add("UTF-8");

            cbSendMode.Items.Add("HEX模式");
            cbSendMode.Items.Add("文本模式");

            cbSendCoding.Items.Add("GBK");
            cbSendCoding.Items.Add("UTF-8");

            cbTimestampFormat.Items.Add("不显示");
            cbTimestampFormat.Items.Add("HH:mm:ss");
            cbTimestampFormat.Items.Add("HH:mm:ss:fff");

            cbLineEnding.Items.Add("\\r\\n");
            cbLineEnding.Items.Add("\\n");
            cbLineEnding.Items.Add("\\r");
            cbLineEnding.Items.Add("无");
        }

        private void SetDefaultValues()
        {
            cbBaudRate.SelectedIndex = 3;       // 115200
            cbDataBits.SelectedIndex = 3;       // 8
            cbStopBits.SelectedIndex = 0;       // 1
            cbParity.SelectedIndex = 0;         // 无

            cbReceiveMode.SelectedIndex = 1;    // 文本模式
            cbReceiveCoding.SelectedIndex = 1;  // UTF-8
            cbSendMode.SelectedIndex = 1;       // 文本模式
            cbSendCoding.SelectedIndex = 1;     // UTF-8

            cbTimestampFormat.SelectedIndex = 2; // HH:mm:ss:fff
            cbLineEnding.SelectedIndex = 0;      // \r\n

            btnSend.IsEnabled = false;
            cbPortName.IsEnabled = true;
            cbBaudRate.IsEnabled = true;
            cbDataBits.IsEnabled = true;
            cbStopBits.IsEnabled = true;
            cbParity.IsEnabled = true;
        }

        // ==================================================================
        //  RichTextBox 彩色日志 — 三种消息类型
        // ==================================================================

        /// <summary>
        /// 系统消息（灰色）：打开/关闭串口等
        /// 独立显示模式 → lbSystemLog；否则回退到 RichTextBox
        /// </summary>
        private void LogSystem(string text)
        {
            if (chkSeparateSystemLog.IsChecked == true)
            {
                lbSystemLog.Items.Add(text);
                while (lbSystemLog.Items.Count > 50)
                    lbSystemLog.Items.RemoveAt(0);
                lbSystemLog.ScrollIntoView(lbSystemLog.Items[lbSystemLog.Items.Count - 1]);
            }
            else
            {
                AppendColoredLine(text, LogSystemColor);
            }
        }

        /// <summary>
        /// 发送回显（蓝色）：显示已发送内容
        /// </summary>
        private void LogSent(string text)
        {
            if (chkShowEcho.IsChecked != true) return;

            string timestamp = DateTime.Now.ToString("HH:mm:ss:fff");
            string msg;

            if (sendMode == "文本模式")
            {
                msg = $"{timestamp} ---- 已发送 {sendCoding.ToLower()} 编码消息: \"{text}\" ----";
            }
            else
            {
                // HEX 模式：显示字节数
                msg = $"{timestamp} ---- 已发送 HEX 消息 ({text.Length / 3 + 1} 字节) ----";
            }

            // 额外追加 HEX 内容（如果是 HEX 模式）
            if (sendMode == "HEX模式")
            {
                AppendColoredLine(msg, LogSentColor);
                // HEX 原文单独一行
                string hexPreview = text.Length > 80 ? text.Substring(0, 80) + "..." : text;
                AppendColoredLine("    " + hexPreview, LogSentColor);
            }
            else
            {
                AppendColoredLine(msg, LogSentColor);
            }
        }

        /// <summary>
        /// 接收数据（深色）：串口实际收到的数据
        /// </summary>
        private void LogReceived(string text)
        {
            string format = cbTimestampFormat.SelectedItem?.ToString() ?? "不显示";
            if (format != "不显示")
            {
                string timestamp = DateTime.Now.ToString(format);
                AppendColoredLine($"{timestamp} -> {text}", LogReceivedColor);
            }
            else
            {
                AppendColoredLine(text, LogReceivedColor);
            }
        }

        /// <summary>
        /// 通用：向 RichTextBox 追加一行带颜色的文本，可选行号
        /// </summary>
        private void AppendColoredLine(string text, Color color)
        {
            _lineCount++;

            Paragraph para = new Paragraph();
            para.Margin = new Thickness(0);
            para.LineHeight = 2;

            if (_showLineNumbers)
            {
                // 行号：右对齐 5 位，灰色
                para.Inlines.Add(new Run($"{_lineCount,5}  ")
                {
                    Foreground = FindResource("TextMutedBrush") as Brush,
                    FontWeight = FontWeights.Normal,
                });
            }

            para.Inlines.Add(new Run(text) { Foreground = new SolidColorBrush(color) });

            rtReceive.Document.Blocks.Add(para);

            // 超行裁剪
            while (rtReceive.Document.Blocks.Count > MaxLogLines)
            {
                rtReceive.Document.Blocks.Remove(rtReceive.Document.Blocks.FirstBlock);
            }

            // 仅在用户已滚到底部时自动滚屏（避免打断手动翻阅历史）
            bool isAtBottom = rtReceive.VerticalOffset >= rtReceive.ExtentHeight - rtReceive.ViewportHeight - 8;
            if (isAtBottom)
                rtReceive.ScrollToEnd();
        }

        // ————————————————————————————————————————
        //  动画辅助
        // ————————————————————————————————————————

        private void AnimateBrushColor(SolidColorBrush brush, Color to)
        {
            var anim = new System.Windows.Media.Animation.ColorAnimation(
                to, TimeSpan.FromMilliseconds(280))
            {
                FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
            };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        }

        private void PulseElement(FrameworkElement element)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(
                1.0, 0.55, TimeSpan.FromMilliseconds(130))
            {
                AutoReverse = true,
                FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop
            };
            element.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        /// <summary>
        /// 滑动开关动画：圆钮左→右 / 右→左（200ms）
        /// </summary>
        private void AnimateToggleSwitch(CheckBox cb)
        {
            if (cb == null) return;
            cb.ApplyTemplate();
            var knob = cb.Template.FindName("Knob", cb) as System.Windows.Shapes.Ellipse;
            if (knob == null) return;

            double toLeft = cb.IsChecked == true ? 20 : 2;
            var anim = new System.Windows.Media.Animation.DoubleAnimation(
                toLeft, TimeSpan.FromMilliseconds(200))
            {
                FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
            };
            knob.BeginAnimation(Canvas.LeftProperty, anim);
        }

        // ————————————————————————————————————————
        //  流量统计
        // ————————————————————————————————————————

        /// <summary>
        /// 字节数 → 可读格式：B / KB / MB
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            else
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        /// <summary>
        /// 更新状态栏流量显示，串口关闭时显示 --
        /// </summary>
        private void UpdateTrafficDisplay()
        {
            if (isSerialOpen)
            {
                tbTraffic.Text = $"TX: {FormatBytes(txByteCount)} ↑  RX: {FormatBytes(rxByteCount)} ↓";
                tbTraffic.Foreground = FindResource("TextSecondaryBrush") as SolidColorBrush;
            }
            else
            {
                tbTraffic.Text = "TX: -- ↑  RX: -- ↓";
                tbTraffic.Foreground = new SolidColorBrush(LogSystemColor);
            }
            tbTraffic.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 右键点击流量文字 → 重置计数
        /// </summary>
        private void tbTraffic_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            txByteCount = 0;
            rxByteCount = 0;
            UpdateTrafficDisplay();
            LogSystem("---- 流量计数已重置 ----");
        }

        // ————————————————————————————————————————
        //  发送历史
        // ————————————————————————————————————————

        private void RecordSendHistory(string text)
        {
            sendHistory.RemoveAll(s => s == text);
            sendHistory.Insert(0, text);
            if (sendHistory.Count > MaxSendHistory)
                sendHistory.RemoveAt(sendHistory.Count - 1);
        }

        private void btnSendHistory_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            if (sendHistory.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "（暂无历史记录）", IsEnabled = false });
            }
            else
            {
                foreach (var item in sendHistory)
                {
                    string display = item.Length > 60 ? item.Substring(0, 60) + "…" : item;
                    var menuItem = new MenuItem { Header = display.Replace("_", "__"), Tag = item };
                    menuItem.Click += (s, args) =>
                    {
                        tbSend.Text = (s as MenuItem)?.Tag?.ToString() ?? "";
                        tbSend.Focus();
                        tbSend.CaretIndex = tbSend.Text.Length;
                    };
                    menu.Items.Add(menuItem);
                }
            }
            menu.IsOpen = true;
        }

        // ==================================================================
        //  快捷发送按钮
        // ==================================================================

        private void LoadQuickSends()
        {
            try
            {
                if (!System.IO.File.Exists(QuickSendsFilePath))
                {
                    // 首次启动：加载预置 AT 指令模板
                    quickSends = new Dictionary<string, string>
                    {
                        ["AT"]          = "AT\\r\\n",
                        ["AT+CWLAP"]    = "AT+CWLAP\\r\\n",
                        ["AT+CWMODE=1"] = "AT+CWMODE=1\\r\\n",
                        ["AT+CIFSR"]    = "AT+CIFSR\\r\\n",
                        ["AT+RST"]      = "AT+RST\\r\\n",
                    };
                    SaveQuickSends();
                    return;
                }

                quickSends.Clear();
                foreach (var line in System.IO.File.ReadAllLines(QuickSendsFilePath))
                {
                    int idx = line.IndexOf('\t');
                    if (idx > 0)
                    {
                        string label = line.Substring(0, idx);
                        string content = line.Substring(idx + 1);
                        quickSends[label] = content;
                    }
                }
            }
            catch { /* 静默失败 */ }
        }

        private void SaveQuickSends()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(QuickSendsFilePath);
                System.IO.Directory.CreateDirectory(dir);

                var lines = new List<string>();
                foreach (var kv in quickSends)
                    lines.Add($"{kv.Key}\t{kv.Value}");
                System.IO.File.WriteAllLines(QuickSendsFilePath, lines);
            }
            catch { /* 静默失败 */ }
        }

        /// <summary>
        /// 重建快捷发送按钮面板
        /// </summary>
        private void RefreshQuickSendButtons()
        {
            quickSendPanel.Children.Clear();

            foreach (var kv in quickSends)
            {
                var btn = new Button
                {
                    Content = kv.Key,
                    Tag = kv.Value,
                    Style = FindResource("QuickSendChipStyle") as Style,
                };

                // 左键 → 立即发送
                btn.Click += (s, e) =>
                {
                    string content = (s as Button)?.Tag?.ToString();
                    if (string.IsNullOrEmpty(content)) return;
                    // 还原 \r\n 字面量为真实换行
                    content = content.Replace("\\r\\n", "\r\n").Replace("\\n", "\r\n");
                    SendRaw(content);
                };

                // 右键 → 编辑 / 删除
                btn.MouseRightButtonDown += (s, e) =>
                {
                    var menu = new ContextMenu();

                    var editItem = new MenuItem { Header = "编辑标签" };
                    string oldLabel = kv.Key;
                    editItem.Click += (s2, e2) =>
                    {
                        string newLabel = ShowInputDialog("编辑标签", "按钮名称：", oldLabel);
                        if (!string.IsNullOrEmpty(newLabel) && newLabel != oldLabel)
                        {
                            string val = quickSends[oldLabel];
                            quickSends.Remove(oldLabel);
                            quickSends[newLabel] = val;
                            SaveQuickSends();
                            RefreshQuickSendButtons();
                        }
                    };
                    menu.Items.Add(editItem);

                    var deleteItem = new MenuItem { Header = "删除" };
                    deleteItem.Click += (s2, e2) =>
                    {
                        quickSends.Remove(oldLabel);
                        SaveQuickSends();
                        RefreshQuickSendButtons();
                        LogSystem($"---- 快捷发送「{oldLabel}」已删除 ----");
                    };
                    menu.Items.Add(deleteItem);

                    menu.IsOpen = true;
                    e.Handled = true;
                };

                quickSendPanel.Children.Add(btn);
            }

            // "+" 添加按钮
            var addBtn = new Button
            {
                Content = "＋",
                Width = 24, Height = 24,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 4),
                Background = Brushes.Transparent,
                BorderBrush = FindResource("CardBorderBrush") as Brush,
                BorderThickness = new Thickness(1),
                Foreground = FindResource("TextMutedBrush") as Brush,
                Cursor = Cursors.Hand,
                ToolTip = "将当前输入区内容添加为快捷发送按钮",
                SnapsToDevicePixels = true,
            };
            // "+" 按钮模板（虚线边框风格表示"添加"语义）
            var addTemplate = new ControlTemplate(typeof(Button));
            var addBorder = new FrameworkElementFactory(typeof(Border));
            addBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            addBorder.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            addBorder.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            addBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            addBorder.SetValue(Border.SnapsToDevicePixelsProperty, true);
            var addCP = new FrameworkElementFactory(typeof(ContentPresenter));
            addCP.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            addCP.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            addBorder.AppendChild(addCP);
            addTemplate.VisualTree = addBorder;

            var addHoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            addHoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, FindResource("SecondaryHoverBgBrush") as Brush));
            addHoverTrigger.Setters.Add(new Setter(Button.BorderBrushProperty, FindResource("PrimaryBrush") as Brush));
            addHoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, FindResource("PrimaryBrush") as Brush));
            addTemplate.Triggers.Add(addHoverTrigger);
            addBtn.Template = addTemplate;

            addBtn.Click += (s, e) =>
            {
                string content = tbSend.Text;
                if (string.IsNullOrEmpty(content))
                {
                    LogSystem("---- 快捷发送：请先在发送区输入内容再添加 ----");
                    return;
                }

                string defaultLabel = content.Length > 15 ? content.Substring(0, 15) + "…" : content;
                string label = ShowInputDialog("添加快捷发送", "按钮名称：", defaultLabel);
                if (!string.IsNullOrEmpty(label))
                {
                    // 以字面量存储换行
                    content = content.Replace("\r\n", "\\r\\n").Replace("\n", "\\r\\n");
                    quickSends[label] = content;
                    SaveQuickSends();
                    RefreshQuickSendButtons();
                    LogSystem($"---- 快捷发送「{label}」已添加 ----");
                }
            };

            quickSendPanel.Children.Add(addBtn);

            // 有按钮时显示面板
            quickSendPanel.Visibility = quickSends.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 简易输入对话框（ToolWindow，居中于主窗口）
        /// </summary>
        private string ShowInputDialog(string title, string prompt, string defaultText)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 320,
                Height = 170,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                FontFamily = new FontFamily("Microsoft YaHei"),
                FontSize = 12,
                Background = FindResource("CardBgBrush") as Brush,
                Foreground = FindResource("TextPrimaryBrush") as Brush,
            };

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var promptText = new TextBlock
            {
                Text = prompt,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = FindResource("TextSecondaryBrush") as Brush,
            };
            Grid.SetRow(promptText, 0);
            grid.Children.Add(promptText);

            var input = new TextBox
            {
                Text = defaultText,
                Height = 28,
                Margin = new Thickness(0, 0, 0, 12),
                Background = FindResource("CardBgBrush") as Brush,
                Foreground = FindResource("TextPrimaryBrush") as Brush,
                BorderBrush = FindResource("InputBorderBrush") as Brush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 2, 6, 2),
                FontFamily = new FontFamily("Microsoft YaHei"),
                FontSize = 12,
            };
            input.SelectAll();
            input.Loaded += (s, e2) => input.Focus();
            Grid.SetRow(input, 1);
            grid.Children.Add(input);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            var cancelBtn = new Button
            {
                Content = "取消",
                Width = 64,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0),
                Style = FindResource("SecondaryButtonStyle") as Style,
            };
            cancelBtn.Click += (s, e2) => { dialog.DialogResult = false; dialog.Close(); };
            buttonPanel.Children.Add(cancelBtn);

            var okBtn = new Button
            {
                Content = "确定",
                Width = 64,
                Height = 28,
                Style = FindResource("PrimaryButtonStyle") as Style,
            };
            okBtn.Click += (s, e2) => { dialog.DialogResult = true; dialog.Close(); };
            buttonPanel.Children.Add(okBtn);

            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            input.KeyDown += (s, e2) =>
            {
                if (e2.Key == Key.Enter) { dialog.DialogResult = true; dialog.Close(); }
            };

            if (dialog.ShowDialog() == true)
                return input.Text.Trim();
            else
                return null;
        }

        // ==================================================================
        //  USB 热插拔检测（WM_DEVICECHANGE）
        // ==================================================================

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0219) // WM_DEVICECHANGE
            {
                if (wParam.ToInt32() == 0x8004) // DBT_DEVICEREMOVECOMPLETE
                {
                    if (isSerialOpen && !serialPort.IsOpen)
                    {
                        lastPortName = cbPortName.Text;
                        CloseSerialPort();
                    }
                }
                else if (wParam.ToInt32() == 0x8000) // DBT_DEVICEARRIVAL
                {
                    if (chkAutoReconnect.IsChecked == true && !isSerialOpen && !string.IsNullOrEmpty(lastPortName))
                    {
                        reconnectTimer.Start();
                    }
                }
            }
            return IntPtr.Zero;
        }

        // ==================================================================
        //  串口打开 / 关闭
        // ==================================================================

        private void OpenSerialPort()
        {
            try
            {
                serialPort.PortName = cbPortName.Text;
                serialPort.BaudRate = Convert.ToInt32(cbBaudRate.Text);
                serialPort.DataBits = Convert.ToInt32(cbDataBits.Text);

                StopBits[] sb = { StopBits.One, StopBits.OnePointFive, StopBits.Two };
                serialPort.StopBits = sb[cbStopBits.SelectedIndex];

                Parity[] pt = { Parity.None, Parity.Odd, Parity.Even };
                serialPort.Parity = pt[cbParity.SelectedIndex];

                serialPort.Open();

                isSerialOpen = true;

                btnOpen.Content = "关闭串口";
                btnOpen.Background = new SolidColorBrush(SuccessColor);
                btnOpen.BorderBrush = new SolidColorBrush(SuccessColor);
                PulseElement(btnOpen);

                btnSend.IsEnabled = true;
                cbPortName.IsEnabled = false;
                cbBaudRate.IsEnabled = false;
                cbDataBits.IsEnabled = false;
                cbStopBits.IsEnabled = false;
                cbParity.IsEnabled = false;

                // 若勾选定时发送，启动定时器
                if (chkAutoRepeat.IsChecked == true &&
                    int.TryParse(tbRepeatInterval.Text, out int repeatMs) && repeatMs > 0)
                {
                    autoSendTimer.Interval = TimeSpan.FromMilliseconds(repeatMs);
                    autoSendTimer.Start();
                }

                // 状态栏
                AnimateBrushColor(statusDotBrush, SuccessColor);
                tbStatusText.Text = "已连接";
                tbPortInfo.Text = $"{cbPortName.Text} @ {cbBaudRate.Text}";
                tbPortInfo.Foreground = new SolidColorBrush(LogSystemColor);
                tbPortInfo.Visibility = Visibility.Visible;

                // 重置流量计数（除非用户选择持久化）
                if (chkPersistTraffic.IsChecked != true)
                {
                    txByteCount = 0;
                    rxByteCount = 0;
                }
                UpdateTrafficDisplay();

                // 日志
                LogSystem($"---- 已打开串行端口 {cbPortName.Text} ----");
            }
            catch
            {
                MessageBox.Show("串口打开失败", "提示",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CloseSerialPort()
        {
            if (isSerialOpen)
            {
                // 强制输出缓冲区残留文本再关
                flushTimer.Stop();
                if (!string.IsNullOrEmpty(receiveLineBuffer))
                {
                    LogReceived(receiveLineBuffer);
                    receiveLineBuffer = "";
                }

                LogSystem($"---- 关闭串行端口 {cbPortName.Text} ----");
            }

            serialPort.Close();

            autoSendTimer.Stop();
            isSerialOpen = false;

            UpdateTrafficDisplay();

            btnOpen.Content = "打开串口";
            btnOpen.Background = new SolidColorBrush(PrimaryColor);
            btnOpen.BorderBrush = new SolidColorBrush(PrimaryColor);
            PulseElement(btnOpen);

            btnSend.IsEnabled = false;
            cbPortName.IsEnabled = true;
            cbBaudRate.IsEnabled = true;
            cbDataBits.IsEnabled = true;
            cbStopBits.IsEnabled = true;
            cbParity.IsEnabled = true;

            AnimateBrushColor(statusDotBrush, StatusDotIdle);
            tbStatusText.Text = "就绪";
            if (fontMissing)
            {
                tbPortInfo.Text = "💡 更纱黑体未安装 → 使用备用等宽字体";
                tbPortInfo.Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0x80, 0x00));
                tbPortInfo.Visibility = Visibility.Visible;
            }
            else
            {
                tbPortInfo.Visibility = Visibility.Collapsed;
            }
        }

        // ==================================================================
        //  控件事件处理
        // ==================================================================

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            if (isSerialOpen)
            {
                CloseSerialPort();
            }
            else
            {
                OpenSerialPort();
            }
        }

        private void cbPortName_DropDownOpened(object sender, EventArgs e)
        {
            string currentName = cbPortName.Text;
            string[] names = SerialPort.GetPortNames();
            cbPortName.Items.Clear();
            foreach (string name in names)
            {
                cbPortName.Items.Add(name);
            }
            cbPortName.Text = currentName;
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            SendData();
        }

        /// <summary>
        /// Enter = 发送    Shift+Enter = 换行
        /// </summary>
        private void tbSend_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    // Shift+Enter → 换行，TextBox 自然处理
                }
                else
                {
                    // Enter → 发送
                    SendData();
                    e.Handled = true; // 阻止 TextBox 插入换行
                }
            }
        }

        /// <summary>
        /// HEX 模式输入自动格式化：过滤非法字符、每两个字符插空格、转大写
        /// </summary>
        private void AutoFormatHexInput()
        {
            if (_isHexFormatting) return;

            string text = tbSend.Text;
            if (string.IsNullOrEmpty(text)) return;

            int oldCursor = tbSend.CaretIndex;

            // 统计原文本中光标之前的有效 hex 字符数
            int hexBefore = 0;
            for (int i = 0; i < oldCursor && i < text.Length; i++)
                if (!char.IsWhiteSpace(text[i])) hexBefore++;

            // 过滤：只保留 [0-9A-Fa-f]
            string clean = Regex.Replace(text, "[^A-Fa-f0-9]", "");
            if (clean.Length == 0) return;

            // 大写 + 每两个字符插空格
            var sb = new StringBuilder();
            for (int i = 0; i < clean.Length; i++)
            {
                if (i > 0 && i % 2 == 0)
                    sb.Append(' ');
                sb.Append(char.ToUpperInvariant(clean[i]));
            }
            string formatted = sb.ToString();

            if (text == formatted) return; // 无需修改

            _isHexFormatting = true;
            tbSend.Text = formatted;

            // 映射光标：在格式化字符串中找到第 hexBefore 个 hex 字符的位置
            int hexCount = 0;
            int newCursor = formatted.Length;
            for (int i = 0; i < formatted.Length; i++)
            {
                if (hexCount == hexBefore) { newCursor = i; break; }
                if (!char.IsWhiteSpace(formatted[i])) hexCount++;
            }
            tbSend.CaretIndex = newCursor;
            _isHexFormatting = false;
        }

        private void btnClearReceive_Click(object sender, RoutedEventArgs e)
        {
            _lineCount = 0;
            rtReceive.Document.Blocks.Clear();
        }

        private void btnClearSend_Click(object sender, RoutedEventArgs e)
        {
            tbSend.Clear();
            tbSendPlaceholder.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 发送区 placeholder：有内容时隐藏水印文字
        /// HEX 模式下自动格式化（每两个字符插空格）
        /// </summary>
        private void tbSend_TextChanged(object sender, TextChangedEventArgs e)
        {
            tbSendPlaceholder.Visibility = string.IsNullOrEmpty(tbSend.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (sendMode == "HEX模式")
                AutoFormatHexInput();
        }

        /// <summary>
        /// 导出接收区日志为 .txt 文件
        /// </summary>
        private void btnExportLog_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt|日志文件 (*.log)|*.log|所有文件 (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"串口日志_{DateTime.Now:yyyyMMdd_HHmmss}"
            };
            if (dialog.ShowDialog() == true)
            {
                var range = new TextRange(rtReceive.Document.ContentStart, rtReceive.Document.ContentEnd);
                System.IO.File.WriteAllText(dialog.FileName, range.Text, System.Text.Encoding.UTF8);
                LogSystem($"---- 日志已导出至 {dialog.FileName} ----");
            }
        }

        private void cbReceiveMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbReceiveMode.SelectedItem == null) return;

            string mode = cbReceiveMode.SelectedItem.ToString();
            if (mode == "HEX模式")
            {
                cbReceiveCoding.IsEnabled = false;
                receiveMode = "HEX模式";
            }
            else if (mode == "文本模式")
            {
                cbReceiveCoding.IsEnabled = true;
                receiveMode = "文本模式";
            }
            byteBuffer.Clear();

            // 模式切换时强制输出缓冲区残留
            flushTimer.Stop();
            if (!string.IsNullOrEmpty(receiveLineBuffer))
            {
                LogReceived(receiveLineBuffer);
                receiveLineBuffer = "";
            }
        }

        private void cbReceiveCoding_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbReceiveCoding.SelectedItem == null) return;

            string coding = cbReceiveCoding.SelectedItem.ToString();
            if (coding == "GBK")
            {
                receiveCoding = "GBK";
            }
            else if (coding == "UTF-8")
            {
                receiveCoding = "UTF-8";
            }
            byteBuffer.Clear();

            flushTimer.Stop();
            if (!string.IsNullOrEmpty(receiveLineBuffer))
            {
                LogReceived(receiveLineBuffer);
                receiveLineBuffer = "";
            }
        }

        private void cbSendMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbSendMode.SelectedItem == null) return;

            string mode = cbSendMode.SelectedItem.ToString();
            if (mode == "HEX模式")
            {
                cbSendCoding.IsEnabled = false;
                sendMode = "HEX模式";
                // 切换到 HEX 时自动格式化现有内容
                if (IsLoaded)
                    AutoFormatHexInput();
            }
            else if (mode == "文本模式")
            {
                cbSendCoding.IsEnabled = true;
                sendMode = "文本模式";
            }
        }

        private void cbSendCoding_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbSendCoding.SelectedItem == null) return;

            string coding = cbSendCoding.SelectedItem.ToString();
            if (coding == "GBK")
            {
                sendCoding = "GBK";
            }
            else if (coding == "UTF-8")
            {
                sendCoding = "UTF-8";
            }
        }

        /// <summary>
        /// 时间戳格式 — 切换时写一条标记
        /// </summary>
        private void cbTimestampFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (cbTimestampFormat.SelectedItem == null) return;

            string format = cbTimestampFormat.SelectedItem.ToString();
            if (format == "不显示")
                LogSystem("---- 时间戳：关 ----");
            else
                LogSystem($"---- 时间戳：{format} ----");
        }

        /// <summary>
        /// 定时发送 — 切换时启动/停止定时器
        /// </summary>
        private void chkAutoRepeat_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            if (chkAutoRepeat.IsChecked == true)
            {
                if (int.TryParse(tbRepeatInterval.Text, out int ms) && ms > 0)
                {
                    autoSendTimer.Interval = TimeSpan.FromMilliseconds(ms);
                    autoSendTimer.Start();
                    LogSystem($"---- 定时发送：开（每 {ms} ms）----");
                }
                else
                {
                    chkAutoRepeat.IsChecked = false;
                    MessageBox.Show("请输入有效的间隔时间（正整数，单位毫秒）", "提示",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                autoSendTimer.Stop();
                LogSystem("---- 定时发送：关 ----");
            }
        }

        /// <summary>
        /// 定时发送间隔更新
        /// </summary>
        private void tbRepeatInterval_LostFocus(object sender, RoutedEventArgs e)
        {
            if (chkAutoRepeat.IsChecked == true)
            {
                if (int.TryParse(tbRepeatInterval.Text, out int ms) && ms > 0)
                {
                    autoSendTimer.Stop();
                    autoSendTimer.Interval = TimeSpan.FromMilliseconds(ms);
                    autoSendTimer.Start();
                }
                else
                {
                    tbRepeatInterval.Text = "1000";
                }
            }
        }

        /// <summary>
        /// 消息回显 — 切换时写一条标记
        /// </summary>
        private void chkShowEcho_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            if (chkShowEcho.IsChecked == true)
            {
                LogSystem("---- 消息回显：开 ----");
            }
            else
            {
                LogSystem("---- 消息回显：关 ----");
            }
        }

        /// <summary>
        /// 行号显示 — 切换时写一条标记
        /// </summary>
        private void chkShowLineNumbers_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            _showLineNumbers = chkShowLineNumbers.IsChecked == true;

            if (_showLineNumbers)
                LogSystem("---- 行号显示：开 ----");
            else
                LogSystem("---- 行号显示：关 ----");
        }

        /// <summary>
        /// 系统消息独立显示 — 淡入淡出 + 写标记
        /// </summary>
        private void chkSeparateSystemLog_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            bool separate = chkSeparateSystemLog.IsChecked == true;

            if (separate)
            {
                // 显示：透明→不透明
                lbSystemLog.Visibility = Visibility.Visible;
                lbSystemLog.Opacity = 0;
                var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(
                    1.0, TimeSpan.FromMilliseconds(200))
                {
                    FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
                };
                lbSystemLog.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                LogSystem("---- 系统消息独立显示：开 ----");
            }
            else
            {
                // 隐藏：不透明→透明，完成后收起
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(
                    0.0, TimeSpan.FromMilliseconds(200))
                {
                    FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
                };
                fadeOut.Completed += (s2, e2) =>
                {
                    lbSystemLog.Visibility = Visibility.Collapsed;
                    lbSystemLog.BeginAnimation(UIElement.OpacityProperty, null);
                };
                lbSystemLog.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                LogSystem("---- 系统消息独立显示：关 ----");
            }
        }

        /// <summary>
        /// 自动重连 — 切换时写一条标记
        /// </summary>
        private void chkAutoReconnect_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            if (chkAutoReconnect.IsChecked == true)
                LogSystem("---- 自动重连：开 ----");
            else
                LogSystem("---- 自动重连：关 ----");
        }

        /// <summary>
        /// 流量持久化 — 切换时写一条标记
        /// </summary>
        private void chkPersistTraffic_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            if (chkPersistTraffic.IsChecked == true)
                LogSystem("---- 流量计数持久化：开 ----");
            else
                LogSystem("---- 流量计数持久化：关 ----");
        }

        // ==================================================================
        //  串口数据收发
        // ==================================================================

        private void SendData()
        {
            if (!serialPort.IsOpen) return;
            if (string.IsNullOrEmpty(tbSend.Text)) return;

            string content = tbSend.Text;
            SendRaw(content, appendLineEnding: true);

            // 发送后自动清空
            if (chkAutoClear.IsChecked == true)
                tbSend.Clear();
        }

        /// <summary>
        /// 发送原始内容（不依赖 TextBox，供快捷发送按钮复用）
        /// appendLineEnding：是否追加拿前选中的换行符（仅主发送区使用）
        /// </summary>
        private void SendRaw(string content, bool appendLineEnding = false)
        {
            if (!serialPort.IsOpen) return;
            if (string.IsNullOrEmpty(content)) return;

            // 追加拿前选中的换行符（主发送区专用，快捷发送按钮已有内置换行）
            if (appendLineEnding)
            {
                string leText = GetLineEndingText();
                string leHex  = GetLineEndingHex();
                if (!string.IsNullOrEmpty(leText))
                {
                    if (sendMode == "文本模式")
                        content += leText;
                    else if (sendMode == "HEX模式" && !string.IsNullOrEmpty(leHex))
                        content += " " + leHex;
                }
            }

            RecordSendHistory(content);

            if (sendMode == "HEX模式")
            {
                byte[] dataSend = HexToBytes(content);
                serialPort.Write(dataSend, 0, dataSend.Length);
                txByteCount += dataSend.Length;
                UpdateTrafficDisplay();
                LogSent(content);
            }
            else if (sendMode == "文本模式")
            {
                byte[] dataSend = TextToBytes(content, sendCoding);
                serialPort.Write(dataSend, 0, dataSend.Length);
                txByteCount += dataSend.Length;
                UpdateTrafficDisplay();
                LogSent(content);
            }
        }

        /// <summary>
        /// 串口接收数据事件（后台线程 → Dispatcher 分发到 UI 线程）
        /// 文本模式：碎片拼成完整行后再输出，避免 "Rece\nived: LED\nON" 这种断裂
        /// HEX 模式：直接追加输出
        /// </summary>
        private void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!serialPort.IsOpen) return;

            int count = serialPort.BytesToRead;
            byte[] dataReceive = new byte[count];
            serialPort.Read(dataReceive, 0, count);

            rxByteCount += count;

            Dispatcher.Invoke(() =>
            {
                if (receiveMode == "HEX模式")
                {
                    // HEX 模式照原样直接输出
                    LogReceived(BytesToHex(dataReceive));
                }
                else if (receiveMode == "文本模式")
                {
                    string text = BytesToText(dataReceive, receiveCoding);
                    if (string.IsNullOrEmpty(text)) return;

                    receiveLineBuffer += text;

                    // 按换行符拆分，输出完整行
                    int idx;
                    bool hasNewline = false;
                    while ((idx = receiveLineBuffer.IndexOf('\n')) >= 0)
                    {
                        string line = receiveLineBuffer.Substring(0, idx).TrimEnd('\r');
                        receiveLineBuffer = receiveLineBuffer.Substring(idx + 1);
                        hasNewline = true;

                        if (!string.IsNullOrEmpty(line))
                        {
                            LogReceived(line);
                        }
                    }

                    if (hasNewline || receiveLineBuffer.Length > 0)
                    {
                        // 重置空闲定时器：100ms 内无新数据就强制输出剩余碎片
                        flushTimer.Stop();
                        flushTimer.Start();
                    }
                }

                UpdateTrafficDisplay();
            });
        }

        /// <summary>
        /// 空闲刷新：100ms 无新数据到达时，强制输出缓冲区残留文本
        /// </summary>
        private void FlushReceiveBuffer(object sender, EventArgs e)
        {
            flushTimer.Stop();

            if (!string.IsNullOrEmpty(receiveLineBuffer))
            {
                LogReceived(receiveLineBuffer);
                receiveLineBuffer = "";
            }
        }

        // ==================================================================
        //  换行符映射
        // ==================================================================

        /// <summary>
        /// 当前选中的换行符 → 文本形式（文本模式发送时追加到内容末尾）
        /// </summary>
        private string GetLineEndingText()
        {
            string selected = cbLineEnding.SelectedItem?.ToString();
            switch (selected)
            {
                case "\\r\\n": return "\r\n";
                case "\\n":    return "\n";
                case "\\r":    return "\r";
                default:       return "";
            }
        }

        /// <summary>
        /// 当前选中的换行符 → HEX 形式（HEX 模式发送时追加拿字节）
        /// </summary>
        private string GetLineEndingHex()
        {
            string selected = cbLineEnding.SelectedItem?.ToString();
            switch (selected)
            {
                case "\\r\\n": return "0D 0A";
                case "\\n":    return "0A";
                case "\\r":    return "0D";
                default:       return "";
            }
        }

        // ==================================================================
        //  数据转换 — 从原始 WinForms 版本直接移植，逻辑不变
        // ==================================================================

        private string BytesToText(byte[] bytes, string encoding)
        {
            List<byte> byteDecode = new List<byte>();
            byteBuffer.AddRange(bytes);

            int count = byteBuffer.Count;
            for (int i = 0; i < count; i++)
            {
                if (byteBuffer.Count == 0) break;

                if (encoding == "GBK")
                {
                    if (byteBuffer[0] < 0x80)
                    {
                        byteDecode.Add(byteBuffer[0]);
                        byteBuffer.RemoveAt(0);
                    }
                    else
                    {
                        if (byteBuffer.Count >= 2)
                        {
                            byteDecode.Add(byteBuffer[0]);
                            byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]);
                            byteBuffer.RemoveAt(0);
                        }
                    }
                }
                else if (encoding == "UTF-8")
                {
                    if ((byteBuffer[0] & 0x80) == 0x00)
                    {
                        byteDecode.Add(byteBuffer[0]);
                        byteBuffer.RemoveAt(0);
                    }
                    else if ((byteBuffer[0] & 0xE0) == 0xC0)
                    {
                        if (byteBuffer.Count >= 2)
                        {
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                        }
                    }
                    else if ((byteBuffer[0] & 0xF0) == 0xE0)
                    {
                        if (byteBuffer.Count >= 3)
                        {
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                        }
                    }
                    else if ((byteBuffer[0] & 0xF8) == 0xF0)
                    {
                        if (byteBuffer.Count >= 4)
                        {
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                        }
                    }
                    else
                    {
                        byteDecode.Add(byteBuffer[0]);
                        byteBuffer.RemoveAt(0);
                    }
                }
            }

            return Encoding.GetEncoding(encoding).GetString(byteDecode.ToArray());
        }

        private string BytesToHex(byte[] bytes)
        {
            string hex = "";
            foreach (byte b in bytes)
            {
                hex += b.ToString("X2") + " ";
            }
            return hex;
        }

        private byte[] TextToBytes(string str, string encoding)
        {
            return Encoding.GetEncoding(encoding).GetBytes(str);
        }

        private byte[] HexToBytes(string str)
        {
            string str1 = Regex.Replace(str, "[^A-F^a-f^0-9]", "");

            double i = str1.Length;
            int len = 2;
            string[] strList = new string[int.Parse(Math.Ceiling(i / len).ToString())];
            for (int j = 0; j < strList.Length; j++)
            {
                len = len <= str1.Length ? len : str1.Length;
                strList[j] = str1.Substring(0, len);
                str1 = str1.Substring(len, str1.Length - len);
            }

            int count = strList.Length;
            byte[] bytes = new byte[count];
            for (int j = 0; j < count; j++)
            {
                bytes[j] = byte.Parse(strList[j], NumberStyles.HexNumber);
            }

            return bytes;
        }
    }
}
