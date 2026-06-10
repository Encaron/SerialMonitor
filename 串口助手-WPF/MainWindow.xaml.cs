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

            // 日志批量刷新定时器：高频接收时合并 UI 更新，避免 RichTextBox 逐行布局卡顿
            _batchFlushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            _batchFlushTimer.Tick += (s, args) => FlushLogBatch();

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

            // 扫描可用串口并自动选中（在 SetDefaultValues 之后、LoadPreferences 之前）
            ScanPortsAndAutoSelect();

            // 加载用户偏好（需在 InitComboBoxItems + SetDefaultValues 之后）
            LoadPreferences();

            // 应用系统消息独立显示的初始可见性
            lbSystemLog.Visibility = chkSeparateSystemLog.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;

            // 同步行号面板初始可见性（LoadPreferences 可能改了 chkShowLineNumbers）
            _showLineNumbers = chkShowLineNumbers.IsChecked == true;
            svLineNumbers.Visibility = _showLineNumbers ? Visibility.Visible : Visibility.Collapsed;

            // 加载快捷发送按钮
            LoadQuickSends();
            RefreshQuickSendButtons();

            serialPort.DataReceived += serialPort_DataReceived;
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
            string[] ports = SerialPort.GetPortNames();
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
                // 校验自定义波特率
                if (!int.TryParse(cbBaudRate.Text, out int baudRate) || baudRate <= 0)
                {
                    MessageBox.Show($"波特率「{cbBaudRate.Text}」无效，请输入正整数（如 9600、115200、921600）。",
                                    "串口打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                serialPort.BaudRate = baudRate;
                serialPort.DataBits = Convert.ToInt32(cbDataBits.Text);

                StopBits[] sb = { StopBits.One, StopBits.OnePointFive, StopBits.Two };
                serialPort.StopBits = sb[cbStopBits.SelectedIndex];

                Parity[] pt = { Parity.None, Parity.Odd, Parity.Even };
                serialPort.Parity = pt[cbParity.SelectedIndex];

                // 硬件流控
                Handshake[] hs = { Handshake.None, Handshake.RequestToSend, Handshake.XOnXOff };
                serialPort.Handshake = hs[cbFlowControl.SelectedIndex];

                // DTR / RTS 控制信号
                serialPort.DtrEnable = chkDtr.IsChecked == true;
                serialPort.RtsEnable = chkRts.IsChecked == true;

                serialPort.Open();

                isSerialOpen = true;
                _lastSuccessfulPort = cbPortName.Text; // 记住本次成功打开的端口

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
            if (isSerialOpen)
            {
                // 先刷新待处理的批量日志
                FlushLogBatch();

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
                    AppendColoredLineUnbuffered(line.Text, line.Color, line.Role);
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
            _batchFlushTimer.Stop();
            _logBatch.Clear();
            _lineCount = 0;
            rtReceive.Document.Blocks.Clear();
            icLineNumbers.Items.Clear();
            // 清空接收区时也清空暂停缓冲
            _pausedLines.Clear();
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
            svLineNumbers.Visibility = _showLineNumbers ? Visibility.Visible : Visibility.Collapsed;

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
        /// DTR — 数据终端就绪信号；连接中可即时切换
        /// </summary>
        private void chkDtr_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            bool enable = chkDtr.IsChecked == true;
            if (isSerialOpen && serialPort.IsOpen)
                serialPort.DtrEnable = enable;

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
            if (isSerialOpen && serialPort.IsOpen)
                serialPort.RtsEnable = enable;

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
