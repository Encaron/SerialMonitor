using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Linq;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Search;

namespace 串口助手
{
    public partial class MainWindow : Window
    {
        // ——— 串口参数状态 ———

        // receiveMode / receiveCoding 同时存于 MainWindow（UI 绑定）和 Session（DataReceived 处理）
        private string receiveMode = "HEX模式";
        private string receiveCoding = "GBK";
        private string sendMode = "HEX模式";
        private string sendCoding = "GBK";

        // 串口会话（替代原来的 serialPort / byteBuffer / receiveLineBuffer / flushTimer）
        private SerialPortSession _session;

        // Phase 3: 波形图 ViewModel
        private PlotViewModel _plotVM;
        private OxyPlot.Wpf.PlotView plotView;
        private bool _plotViewCreated;

        // ——— 颜色常量（与 XAML 资源保持一致）———

        private static Color PrimaryColor       = Color.FromRgb(0x00, 0x78, 0xD4);
        private static Color SuccessColor       = Color.FromRgb(0x10, 0xB9, 0x81);
        private static Color LogSystemColor     = Color.FromRgb(0x99, 0x99, 0x99);
        private static Color LogSentColor       = Color.FromRgb(0x00, 0x78, 0xD4);
        private static readonly Color LogReceivedColorLight = Color.FromRgb(0x2D, 0x2D, 0x2D);
        private static readonly Color LogReceivedColorDark  = Color.FromRgb(0xD4, 0xD4, 0xD4);
        private static readonly Color StatusDotIdleLight    = Color.FromRgb(0xCC, 0xCC, 0xCC);
        private static readonly Color StatusDotIdleDark     = Color.FromRgb(0x5A, 0x5A, 0x5A);

        //  运行时根据主题切换 ↓
        private static Color LogReceivedColor = LogReceivedColorLight;
        private static Color StatusDotIdle    = StatusDotIdleLight;

        // ——— 定时发送 ———
        private DispatcherTimer autoSendTimer;

        // ——— 日志/批量操作常量 ———

        private const int MaxLogLines = 2000;
        /// <summary>超出 MaxLogLines 时一次性裁剪的行数（留余量避免频繁触发）</summary>
        private const int CropMargin = 500;
        /// <summary>距底部 ≤ 此像素视为"用户在底部"，自动滚屏</summary>
        private const int SmartScrollLockPixels = 8;

        // ——— 行号 ———
        private bool _showLineNumbers = true;

        // ——— 快捷发送 ———
        /// <summary>
        /// label → content（content 中的 \r\n 以字面量存储，发送时还原）
        /// </summary>
        private Dictionary<string, string> quickSends = new Dictionary<string, string>();

        // ——— 发送历史 ———
        private List<string> sendHistory = new List<string>();
        private const int MaxSendHistory = 20;

        // ——— HEX 输入格式化 ———
        private bool _isHexFormatting = false;

        // ——— 暂停显示 ———
        private bool _isPaused = false;
        private class PausedLine { public string Text; public Color Color; public string Role; }
        private List<PausedLine> _pausedLines = new List<PausedLine>();
        private const int MaxPausedLines = 2000;

        // ——— 自动重连 ———
        private string lastPortName = "";
        private string _lastSuccessfulPort = "";
        private DispatcherTimer reconnectTimer;
        private const int MaxReconnectAttempts = 10;
        private int _reconnectAttempts = 0;

        // ——— 动画画刷（非冻结，支持 ColorAnimation）———
        private SolidColorBrush statusDotBrush;

        // ——— 字体检测 ———
        private bool fontMissing = false;

        // ——— 暗色主题 ———
        private bool isDarkTheme = false;

        // Phase 2: 标签切换
        private string _currentTab = "Receive";
        private string _previousContentTab = "Receive";
        private SearchPanel _searchPanel;

        private static readonly Dictionary<string, (Color Light, Color Dark)> ThemeMap =
            new Dictionary<string, (Color, Color)>
        {
            ["WindowBgBrush"]      = (Color.FromRgb(0xF0,0xF0,0xF0), Color.FromRgb(0x1A,0x1A,0x1C)),
            ["CardBgBrush"]        = (Color.FromRgb(0xFF,0xFF,0xFF), Color.FromRgb(0x25,0x25,0x26)),
            ["CardBorderBrush"]    = (Color.FromRgb(0xE0,0xE0,0xE0), Color.FromRgb(0x3E,0x3E,0x42)),
            ["TextPrimaryBrush"]   = (Color.FromRgb(0x2D,0x2D,0x2D), Color.FromRgb(0xD4,0xD4,0xD4)),
            ["TextSecondaryBrush"] = (Color.FromRgb(0x55,0x55,0x55), Color.FromRgb(0x9D,0x9D,0x9D)),
            ["TextMutedBrush"]     = (Color.FromRgb(0x99,0x99,0x99), Color.FromRgb(0x6A,0x6A,0x6A)),
            ["PrimaryBrush"]       = (Color.FromRgb(0x00,0x78,0xD4), Color.FromRgb(0x0E,0x63,0x9C)),
            ["PrimaryHoverBrush"]  = (Color.FromRgb(0x10,0x6E,0xBE), Color.FromRgb(0x11,0x77,0xBB)),
            ["SecondaryBrush"]     = (Color.FromRgb(0x88,0x88,0x88), Color.FromRgb(0x70,0x70,0x70)),
            ["SecondaryHoverBrush"]= (Color.FromRgb(0x66,0x66,0x66), Color.FromRgb(0x8A,0x8A,0x8A)),
            ["StatusBarBgBrush"]   = (Color.FromRgb(0xE6,0xE6,0xE6), Color.FromRgb(0x33,0x33,0x33)),
            ["SeparatorBrush"]     = (Color.FromRgb(0xEE,0xEE,0xEE), Color.FromRgb(0x33,0x33,0x33)),
            ["InputBorderBrush"]   = (Color.FromRgb(0xD0,0xD0,0xD0), Color.FromRgb(0x3E,0x3E,0x42)),
            ["LogSystemBrush"]     = (Color.FromRgb(0x99,0x99,0x99), Color.FromRgb(0x6A,0x6A,0x6A)),
            ["LogReceivedBrush"]   = (Color.FromRgb(0x2D,0x2D,0x2D), Color.FromRgb(0xD4,0xD4,0xD4)),
            ["TextSubtleBrush"]    = (Color.FromRgb(0xBB,0xBB,0xBB), Color.FromRgb(0x5A,0x5A,0x5A)),
            ["TextFaintBrush"]     = (Color.FromRgb(0xCC,0xCC,0xCC), Color.FromRgb(0x4F,0x4F,0x4F)),
            ["SecondaryHoverBgBrush"]   = (Color.FromRgb(0xF5,0xF5,0xF5), Color.FromRgb(0x2A,0x2A,0x2D)),
            ["ButtonDisabledBgBrush"]   = (Color.FromRgb(0xE8,0xE8,0xE8), Color.FromRgb(0x2D,0x2D,0x2D)),
            ["ReceiveAreaBgBrush"]      = (Color.FromRgb(0xFA,0xFA,0xFA), Color.FromRgb(0x1A,0x1A,0x1C)),
            ["StatusDotIdleBrush"]      = (Color.FromRgb(0xCC,0xCC,0xCC), Color.FromRgb(0x5A,0x5A,0x5A)),
        };

        public MainWindow()
        {
            InitializeComponent();

            // 默认选中接收区图标（InitializeComponent 之后，避免 XAML 阶段触发事件）
            tabReceive.IsChecked = true;

            // Phase 3: PlotViewModel 必须在 LoadWindowSettings 之前初始化——后者可能触发 ApplyTheme
            _plotVM = new PlotViewModel();

            // 恢复上次窗口位置和大小（可能触发 ApplyTheme → UpdateThemeColors）
            LoadWindowSettings();

            // 创建串口会话
            _session = new SerialPortSession(Dispatcher);
            _session.LineReceived += OnLineReceived;
            _session.ConnectionChanged += OnConnectionChanged;

            // Phase 3: 设置 CheckBox 初始选中状态（不能放 XAML IsChecked="True"，会导致 BAML 崩溃）
            chkPlotYAuto.IsChecked = true;
            chkPlotLines.IsChecked = true;
            chkPlotValueHud.IsChecked = true;
            // Y 轴自动模式下禁用 min/max 输入框
            tbPlotYMin.IsEnabled = false;
            tbPlotYMax.IsEnabled = false;
            // 显示模式下拉框
            cbPlotMode.Items.Add("滚动 (Roll)");
            cbPlotMode.Items.Add("扫描 (Sweep)");
            cbPlotMode.SelectedIndex = 0;

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

            // 初始化 AvalonEdit（着色器 + 行号样式）
            InitEditor();

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
            autoSendTimer.Tick += (s, args) => { if (_session.IsOpen) SendData(); };

            InitComboBoxItems();
            SetDefaultValues();

            // 扫描可用串口并自动选中（在 SetDefaultValues 之后、LoadPreferences 之前）
            ScanPortsAndAutoSelect();

            // 加载用户偏好（需在 InitComboBoxItems + SetDefaultValues 之后）
            LoadPreferences();

            // 应用系统消息独立显示的初始可见性
            lbSystemLog.Visibility = chkSeparateSystemLog.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;

            // 同步行号面板初始可见性（LoadPreferences 可能改了 chkShowLineNumbers）
            _showLineNumbers = chkShowLineNumbers.IsChecked == true;
            editor.ShowLineNumbers = _showLineNumbers;

            // 加载快捷发送按钮
            LoadQuickSends();
            RefreshQuickSendButtons();
        }

        // ==================================================================
        //  SerialPortSession 事件处理
        // ==================================================================

        /// <summary>
        /// Session 收到完整行文本 → 写入接收区（已由 Session 的 Dispatcher.Invoke 切到 UI 线程）
        /// </summary>
        private void OnLineReceived(string line)
        {
            LogReceived(line);
            UpdateTrafficDisplay();

            // Phase 3: 协议路由——[plot,...] → PlotViewModel
            var pr = ProtocolParser.Parse(line);
            if (pr.Messages.Count > 0)
            {
                foreach (var msg in pr.Messages)
                {
                    if (msg.Type == "plot" && msg.Args.Count >= 2)
                    {
                        if (double.TryParse(msg.Args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                        {
                            _plotVM.OnPlotMessage(msg.Args[0], val, DateTime.Now);
                            if (plotEmptyHint.Visibility == Visibility.Visible)
                                plotEmptyHint.Visibility = Visibility.Collapsed;
                            UpdatePlotHud();
                        }
                    }
                    // Phase 4: [key,name,state]
                    else if (msg.Type == "key" && msg.Args.Count >= 2)
                    {
                        string keyName = msg.Args[0];
                        string state = msg.Args[1]; // "down" or "up"
                        HandleKeyMessage(keyName, state);
                    }
                }
            }
        }

        /// <summary>
        /// Session 连接状态变化 → 更新 UI
        /// </summary>
        private void OnConnectionChanged(bool isOpen)
        {
            if (isOpen)
            {
                // 连接成功后的 UI 更新
                _lastSuccessfulPort = _session.PortName;

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
                cbFlowControl.IsEnabled = false;

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
                tbPortInfo.Text = $"{_session.PortName} @ {cbBaudRate.Text}";
                tbPortInfo.Foreground = new SolidColorBrush(LogSystemColor);
                tbPortInfo.Visibility = Visibility.Visible;

                // 重置流量计数（除非用户选择持久化）
                if (chkPersistTraffic.IsChecked != true)
                {
                    _session.ResetTraffic();
                }
                UpdateTrafficDisplay();

                // 日志
                LogSystem($"---- 已打开串行端口 {_session.PortName} ----");
            }
            else
            {
                // 断开连接后的 UI 更新
                autoSendTimer.Stop();

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
                cbFlowControl.IsEnabled = true;

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
        }

        // ==================================================================
        //  窗口位置记忆
        // ==================================================================

        /// <summary>
        /// 全局快捷键
        /// </summary>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+Enter → 打开/关闭串口
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                btnOpen_Click(sender, e);
                e.Handled = true;
                return;
            }

            // Ctrl+L → 清空接收区
            if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
            {
                btnClearReceive_Click(sender, e);
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+L → 清空发送区
            if (e.Key == Key.L && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                btnClearSend_Click(sender, e);
                e.Handled = true;
                return;
            }

            // Ctrl+P → 暂停/继续显示
            if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
            {
                btnPauseDisplay_Click(sender, e);
                e.Handled = true;
                return;
            }
        }

        // ==================================================================
        //  窗口初始化
        // ==================================================================

        private void InitComboBoxItems()
        {
            cbBaudRate.Items.Add("2400");
            cbBaudRate.Items.Add("4800");
            cbBaudRate.Items.Add("9600");
            cbBaudRate.Items.Add("14400");
            cbBaudRate.Items.Add("19200");
            cbBaudRate.Items.Add("38400");
            cbBaudRate.Items.Add("57600");
            cbBaudRate.Items.Add("76800");
            cbBaudRate.Items.Add("115200");
            cbBaudRate.Items.Add("128000");
            cbBaudRate.Items.Add("230400");
            cbBaudRate.Items.Add("256000");
            cbBaudRate.Items.Add("460800");
            cbBaudRate.Items.Add("500000");
            cbBaudRate.Items.Add("921600");
            cbBaudRate.Items.Add("1000000");
            cbBaudRate.Items.Add("2000000");

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

            cbFlowControl.Items.Add("无");
            cbFlowControl.Items.Add("RTS/CTS");
            cbFlowControl.Items.Add("XON/XOFF");
        }

        private void SetDefaultValues()
        {
            cbBaudRate.SelectedIndex = 8;       // 115200
            cbDataBits.SelectedIndex = 3;       // 8
            cbStopBits.SelectedIndex = 0;       // 1
            cbParity.SelectedIndex = 0;         // 无

            cbReceiveMode.SelectedIndex = 1;    // 文本模式
            cbReceiveCoding.SelectedIndex = 1;  // UTF-8
            cbSendMode.SelectedIndex = 1;       // 文本模式
            cbSendCoding.SelectedIndex = 1;     // UTF-8

            cbTimestampFormat.SelectedIndex = 2; // HH:mm:ss:fff
            cbLineEnding.SelectedIndex = 0;      // \r\n
            cbFlowControl.SelectedIndex = 0;     // 无

            btnSend.IsEnabled = false;
            cbPortName.IsEnabled = true;
            cbBaudRate.IsEnabled = true;
            cbDataBits.IsEnabled = true;
            cbStopBits.IsEnabled = true;
            cbParity.IsEnabled = true;
        }

        /// <summary>
        /// 启动时扫描可用串口并自动选中上次使用的端口。
        /// 优先级：上次记录的端口 > 仅有一个串口时自动选 > 多串口时选数字最小的 > 空白
        /// </summary>
        private void ScanPortsAndAutoSelect()
        {
            string[] ports = SerialPort.GetPortNames().Distinct().ToArray();
            if (ports.Length == 0) return;

            // 填充串口号列表
            cbPortName.Items.Clear();
            foreach (string p in ports)
                cbPortName.Items.Add(p);

            string selected = "";

            // 1) 上次成功打开的端口仍存在 → 优先
            if (!string.IsNullOrEmpty(_lastSuccessfulPort) &&
                Array.IndexOf(ports, _lastSuccessfulPort) >= 0)
            {
                selected = _lastSuccessfulPort;
            }
            // 2) 只有一个串口 → 直接选
            else if (ports.Length == 1)
            {
                selected = ports[0];
            }
            // 3) 多个串口 → 选数字最小的
            else
            {
                selected = ports[0];
                foreach (string p in ports)
                {
                    if (string.Compare(p, selected, StringComparison.OrdinalIgnoreCase) < 0)
                        selected = p;
                }
            }

            if (!string.IsNullOrEmpty(selected))
            {
                cbPortName.Text = selected;
                LogSystem($"---- 已自动选中 {selected} ----");
            }
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
            if (_session.IsOpen)
            {
                tbTraffic.Text = $"TX: {FormatBytes(_session.TxBytes)} ↑  RX: {FormatBytes(_session.RxBytes)} ↓";
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
            _session.ResetTraffic();
            UpdateTrafficDisplay();
            LogSystem("---- 流量计数已重置 ----");
        }

        // ————————————————————————————————————————
        //  发送历史 & 快捷发送（已移至 MainWindow.QuickSend.cs）
        // ————————————————————————————————————————


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
                    if (_session.IsOpen && !_session.IsPortOpen)
                    {
                        lastPortName = cbPortName.Text;
                        CloseSerialPort();
                    }
                }
                else if (wParam.ToInt32() == 0x8000) // DBT_DEVICEARRIVAL
                {
                    if (chkAutoReconnect.IsChecked == true && !_session.IsOpen && !string.IsNullOrEmpty(lastPortName))
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
                string portName = cbPortName.Text;

                // 校验自定义波特率
                if (!int.TryParse(cbBaudRate.Text, out int baudRate) || baudRate <= 0)
                {
                    MessageBox.Show($"波特率「{cbBaudRate.Text}」无效，请输入正整数（如 9600、115200、921600）。",
                                    "串口打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int dataBits = Convert.ToInt32(cbDataBits.Text);

                StopBits[] sb = { StopBits.One, StopBits.OnePointFive, StopBits.Two };
                StopBits stopBits = sb[cbStopBits.SelectedIndex];

                Parity[] pt = { Parity.None, Parity.Odd, Parity.Even };
                Parity parity = pt[cbParity.SelectedIndex];

                Handshake[] hs = { Handshake.None, Handshake.RequestToSend, Handshake.XOnXOff };
                Handshake handshake = hs[cbFlowControl.SelectedIndex];

                bool dtr = chkDtr.IsChecked == true;
                bool rts = chkRts.IsChecked == true;

                _session.Open(portName, baudRate, dataBits, stopBits, parity, handshake, dtr, rts,
                              receiveMode, receiveCoding);
                // 成功后 ConnectionChanged 事件会触发 OnConnectionChanged 更新 UI
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("串口被其他程序占用，或没有访问权限。\n请关闭其他串口工具后重试。",
                                "串口打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (System.IO.IOException ex)
            {
                MessageBox.Show($"串口硬件通信错误：\n{ex.Message}",
                                "串口打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show($"串口参数无效（端口名或波特率）：\n{ex.Message}",
                                "串口打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show($"串口已被占用：\n{ex.Message}",
                                "串口打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"串口打开失败：\n{ex.Message}",
                                "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CloseSerialPort()
        {
            if (_session.IsOpen)
            {
                LogSystem($"---- 关闭串行端口 {_session.PortName} ----");
            }

            _session.Close();
            // ConnectionChanged 事件会触发 OnConnectionChanged 更新 UI
        }

        // ==================================================================
        //  控件事件处理
        // ==================================================================

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            if (_session.IsOpen)
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
            string[] names = SerialPort.GetPortNames().Distinct().ToArray();
            cbPortName.Items.Clear();
            foreach (string name in names)
                cbPortName.Items.Add(name);
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

        /// <summary>
        /// 暂停/继续接收区显示。串口后台照常接收，界面冻结便于分析。
        /// 恢复时补回暂停期间积压的全部数据。
        /// </summary>
        private void btnPauseDisplay_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = !_isPaused;

            if (_isPaused)
            {
                btnPauseDisplay.Content = "▶ 继续显示";
                btnPauseDisplay.Style = FindResource("PrimaryButtonStyle") as Style;
                LogSystem("---- 暂停显示：界面已冻结，后台照常接收 ----");
            }
            else
            {
                int bufferedCount = _pausedLines.Count;

                // 一次性补回积压数据
                foreach (var line in _pausedLines)
                    AppendColoredLine(line.Text, line.Color, line.Role, skipPause: true);
                _pausedLines.Clear();

                if (bufferedCount > 0)
                    LogSystem($"---- 继续显示：补回暂停期间的 {bufferedCount} 条数据 ----");
                else
                    LogSystem("---- 继续显示 ----");

                btnPauseDisplay.Content = "暂停显示";
                btnPauseDisplay.Style = FindResource("SecondaryButtonStyle") as Style;
            }
        }


        private void btnClearReceive_Click(object sender, RoutedEventArgs e)
        {
            ClearEditorContent();
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
                System.IO.File.WriteAllText(dialog.FileName, editor.Document.Text, System.Text.Encoding.UTF8);
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

            _session.UpdateReceiveSettings(receiveMode, receiveCoding);
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

            _session.UpdateReceiveSettings(receiveMode, receiveCoding);
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
            editor.ShowLineNumbers = _showLineNumbers;

            if (_showLineNumbers)
            {
                LogSystem("---- 行号显示：开 ----");
            }
            else
            {
                LogSystem("---- 行号显示：关 ----");
            }
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
        /// DTR — 数据终端就绪信号；连接中可即时切换
        /// </summary>
        private void chkDtr_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            bool enable = chkDtr.IsChecked == true;
            _session.SetDtr(enable);

            LogSystem($"---- DTR：{(enable ? "开" : "关")} ----");
        }

        /// <summary>
        /// RTS — 请求发送信号；连接中可即时切换
        /// </summary>
        private void chkRts_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            bool enable = chkRts.IsChecked == true;
            _session.SetRts(enable);

            LogSystem($"---- RTS：{(enable ? "开" : "关")} ----");
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
            if (!_session.IsOpen) return;
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
            if (!_session.IsOpen) return;
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

            byte[] dataSend;
            if (sendMode == "HEX模式")
            {
                dataSend = DataConverter.HexToBytes(content);
            }
            else
            {
                dataSend = DataConverter.TextToBytes(content, sendCoding);
            }

            _session.SendBytes(dataSend);
            UpdateTrafficDisplay();
            LogSent(content);
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
        //  Phase 2: 标签切换 & 侧面板
        // ==================================================================
        private void TabContent_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == tabReceive)      { _currentTab = "Receive"; _previousContentTab = "Receive"; }
            else if (sender == tabPlot)    { _currentTab = "Plot";    _previousContentTab = "Plot";  EnsurePlotView(); }
            else if (sender == tabKeys)    { _currentTab = "Keys";    _previousContentTab = "Keys"; InitKeyPanel(); }
            else if (sender == tabSliders) { _currentTab = "Sliders"; _previousContentTab = "Sliders"; }
            else if (sender == tabOLED)    { _currentTab = "OLED";    _previousContentTab = "OLED"; }
            RefreshContentVisibility();
        }
        private void TabSettings_Checked(object sender, RoutedEventArgs e)
        {
            _currentTab = "Settings";
            RefreshContentVisibility();
        }
        private void RefreshContentVisibility()
        {
            panelReceive.Visibility = _previousContentTab == "Receive" ? Visibility.Visible : Visibility.Collapsed;
            panelPlot.Visibility    = _previousContentTab == "Plot"    ? Visibility.Visible : Visibility.Collapsed;
            panelKeys.Visibility    = _previousContentTab == "Keys"    ? Visibility.Visible : Visibility.Collapsed;
            panelSliders.Visibility = _previousContentTab == "Sliders" ? Visibility.Visible : Visibility.Collapsed;
            panelOLED.Visibility    = _previousContentTab == "OLED"    ? Visibility.Visible : Visibility.Collapsed;
            rightReceive.Visibility  = _currentTab == "Receive"  ? Visibility.Visible : Visibility.Collapsed;
            rightPlot.Visibility     = _currentTab == "Plot"     ? Visibility.Visible : Visibility.Collapsed;
            rightKeys.Visibility     = _currentTab == "Keys"     ? Visibility.Visible : Visibility.Collapsed;
            rightSettings.Visibility = _currentTab == "Settings" ? Visibility.Visible : Visibility.Collapsed;
            switch (_currentTab)
            {
                case "Receive": tbSidePanelTitle.Text = "收发设置"; break;
                case "Plot":    tbSidePanelTitle.Text = "绘图设置"; break;
                case "Keys":    tbSidePanelTitle.Text = "按键属性"; break;
                case "Settings": tbSidePanelTitle.Text = "串口配置"; break;
            }
            // 切换到按键面板时刷新侧面板
            if (_currentTab == "Keys") RefreshKeysSidePanel();
        }
        private void BtnPanelCollapse_Click(object sender, RoutedEventArgs e)
        {
            if (colSidePanel.Width.Value > 30)
            {
                colSidePanel.MinWidth = 0;
                colSidePanel.Width = new GridLength(28);
                btnPanelCollapse.Content = "▶";
                btnPanelCollapse.ToolTip = "展开侧面板";
            }
            else
            {
                colSidePanel.MinWidth = 120;
                colSidePanel.Width = new GridLength(220);
                btnPanelCollapse.Content = "◀";
                btnPanelCollapse.ToolTip = "折叠侧面板";
            }
        }
        // ═══ Phase 3: 波形图 ═══

        private void EnsurePlotView()
        {
            if (_plotViewCreated) return;
            _plotViewCreated = true;

            plotView = new OxyPlot.Wpf.PlotView
            {
                Model = _plotVM.Model,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = System.Windows.Media.Brushes.Transparent, // 让父级背景透过来
            };
            // 点击曲线 → 钉住十字光标标记
            plotView.MouseDown += (s, e) =>
            {
                if (!_plotVM.IsPaused) _plotVM.TogglePause();
                btnPlotPause.Content = _plotVM.IsPaused ? "▶ 继续" : "⏸ 暂停";
            };
            plotArea.Children.Insert(0, plotView);
        }

        private void btnPlotPause_Click(object sender, RoutedEventArgs e)
        {
            _plotVM.TogglePause();
            btnPlotPause.Content = _plotVM.IsPaused ? "▶ 继续" : "⏸ 暂停";
        }

        private void btnPlotClear_Click(object sender, RoutedEventArgs e)
        {
            _plotVM.Clear();
            plotEmptyHint.Visibility = Visibility.Visible;
            plotHud.Visibility = Visibility.Collapsed;
        }

        private void btnPlotCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string csv = _plotVM.ExportCsv();
                if (string.IsNullOrEmpty(csv) || csv.IndexOf(',') < 0)
                {
                    LogSystem("---- 无波形数据可导出 ----");
                    return;
                }
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"plot_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                };
                if (dlg.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(dlg.FileName, csv, Encoding.UTF8);
                    LogSystem($"---- 波形数据已导出到: {dlg.FileName} ----");
                }
            }
            catch (Exception ex) { LogSystem($"---- 导出 CSV 失败: {ex.Message} ----"); }
        }

        private void btnPlotFit_Click(object sender, RoutedEventArgs e)
        {
            _plotVM.ResetView();
        }

        private void UpdatePlotHud()
        {
            if (!_plotVM.ShowValueHud || _plotVM.LatestValues.Count == 0) return;
            plotHud.Visibility = Visibility.Visible;
            plotHudPanel.Children.Clear();
            foreach (var kv in _plotVM.LatestValues)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
                row.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 8, Height = 8,
                    Fill = new SolidColorBrush(Color.FromRgb(0x0E, 0x63, 0x9C)),
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                });
                row.Children.Add(new TextBlock
                {
                    Text = kv.Key + ": ",
                    Foreground = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB)),
                    FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                });
                row.Children.Add(new TextBlock
                {
                    Text = kv.Value.ToString("F4"),
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 12, FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                plotHudPanel.Children.Add(row);
            }
        }

        // Phase 3 rightPlot handlers

        private void chkPlotYAuto_Changed(object sender, RoutedEventArgs e)
        {
            bool isAuto = chkPlotYAuto.IsChecked == true;
            _plotVM.YAxisAutoRange = isAuto;
            tbPlotYMin.IsEnabled = !isAuto;
            tbPlotYMax.IsEnabled = !isAuto;
            if (isAuto) _plotVM.RecalcYAxis();
        }

        private void chkPlotMarkers_Changed(object sender, RoutedEventArgs e)
        {
            _plotVM.SetMarkers(chkPlotMarkers.IsChecked == true);
        }

        private void chkPlotLines_Changed(object sender, RoutedEventArgs e)
        {
            _plotVM.SetLines(chkPlotLines.IsChecked == true);
        }

        private void chkPlotValueHud_Changed(object sender, RoutedEventArgs e)
        {
            _plotVM.ShowValueHud = chkPlotValueHud.IsChecked == true;
            plotHud.Visibility = (_plotVM.ShowValueHud && _plotVM.LatestValues.Count > 0)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void cbPlotMode_Changed(object sender, RoutedEventArgs e)
        {
            switch (cbPlotMode.SelectedIndex)
            {
                case 0: _plotVM.DisplayMode = PlotDisplayMode.Scroll; break;
                case 1: _plotVM.DisplayMode = PlotDisplayMode.Sweep; break;
            }
        }

        private void tbPlotXRange_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(tbPlotXRange.Text, out int val) && val > 0 && val != _plotVM.MaxDataPoints)
            {
                _plotVM.MaxDataPoints = val;
                _plotVM.ApplyXAxisWindow();  // 调整 X 轴显示窗口，不删数据
            }
        }

        private void tbPlotYRange_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(tbPlotYMin.Text, out double min) &&
                double.TryParse(tbPlotYMax.Text, out double max) && max > min)
                _plotVM.SetYRange(min, max);
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            _searchPanel?.Open();
        }
    }
}
