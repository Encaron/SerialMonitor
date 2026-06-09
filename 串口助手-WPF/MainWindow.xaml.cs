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
        private static readonly Color LogReceivedColor   = Color.FromRgb(0x2D, 0x2D, 0x2D);
        private static readonly Color StatusDotIdle      = Color.FromRgb(0xCC, 0xCC, 0xCC);

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

        // ——— 发送历史 ———
        private List<string> sendHistory = new List<string>();
        private const int MaxSendHistory = 20;

        // ——— 动画画刷（非冻结，支持 ColorAnimation）———
        private SolidColorBrush statusDotBrush;

        // ——— 字体检测 ———
        private bool fontMissing = false;

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

            // 定时发送计时器
            autoSendTimer = new DispatcherTimer();
            autoSendTimer.Tick += (s, args) => { if (serialPort.IsOpen) SendData(); };

            InitComboBoxItems();
            SetDefaultValues();

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
                    $"State={this.WindowState}"
                };
                System.IO.File.WriteAllLines(SettingsFilePath, lines);
            }
            catch { /* 静默失败，不影响关闭 */ }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowSettings();
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
        /// </summary>
        private void LogSystem(string text)
        {
            AppendColoredLine(text, LogSystemColor);
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
        /// 通用：向 RichTextBox 追加一行带颜色的文本
        /// </summary>
        private void AppendColoredLine(string text, Color color)
        {
            Paragraph para = new Paragraph();
            para.Inlines.Add(new Run(text) { Foreground = new SolidColorBrush(color) });
            para.Margin = new Thickness(0);
            para.LineHeight = 2;

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
                        CloseSerialPort();
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

        private void btnClearReceive_Click(object sender, RoutedEventArgs e)
        {
            rtReceive.Document.Blocks.Clear();
        }

        private void btnClearSend_Click(object sender, RoutedEventArgs e)
        {
            tbSend.Clear();
            tbSendPlaceholder.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 发送区 placeholder：有内容时隐藏水印文字
        /// </summary>
        private void tbSend_TextChanged(object sender, TextChangedEventArgs e)
        {
            tbSendPlaceholder.Visibility = string.IsNullOrEmpty(tbSend.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
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

        /// 消息回显 — 切换时写一条标记
        /// </summary>
        private void chkShowEcho_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            if (chkShowEcho.IsChecked == true)
            {
                LogSystem("---- 消息回显：开 ----");
            }
            else
            {
                LogSystem("---- 消息回显：关 ----");
            }
        }

        // ==================================================================
        //  串口数据收发
        // ==================================================================

        private void SendData()
        {
            if (!serialPort.IsOpen) return;

            string rawText = tbSend.Text;
            if (string.IsNullOrEmpty(rawText)) return;

            RecordSendHistory(rawText);

            if (sendMode == "HEX模式")
            {
                byte[] dataSend = HexToBytes(rawText);
                serialPort.Write(dataSend, 0, dataSend.Length);
                LogSent(rawText);
            }
            else if (sendMode == "文本模式")
            {
                byte[] dataSend = TextToBytes(rawText, sendCoding);
                serialPort.Write(dataSend, 0, dataSend.Length);
                LogSent(rawText);
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
