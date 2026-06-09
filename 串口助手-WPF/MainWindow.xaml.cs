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
using System.Windows.Media;

namespace 串口助手
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 接收模式：HEX模式 / 文本模式
        /// </summary>
        private string receiveMode = "HEX模式";

        /// <summary>
        /// 接收文本编码：GBK / UTF-8
        /// </summary>
        private string receiveCoding = "GBK";

        /// <summary>
        /// 发送模式：HEX模式 / 文本模式
        /// </summary>
        private string sendMode = "HEX模式";

        /// <summary>
        /// 发送文本编码：GBK / UTF-8
        /// </summary>
        private string sendCoding = "GBK";

        /// <summary>
        /// 接收字节缓存区（跨数据包拼接多字节字符）
        /// </summary>
        private List<byte> byteBuffer = new List<byte>();

        /// <summary>
        /// 串口对象
        /// </summary>
        private SerialPort serialPort = new SerialPort();

        /// <summary>
        /// 串口是否已打开
        /// </summary>
        private bool isSerialOpen = false;

        // ——— 颜色常量（与 XAML 资源保持一致）———

        private static readonly Color PrimaryColor       = Color.FromRgb(0x00, 0x78, 0xD4);
        private static readonly Color PrimaryHoverColor  = Color.FromRgb(0x10, 0x6E, 0xBE);
        private static readonly Color SuccessColor       = Color.FromRgb(0x10, 0xB9, 0x81);
        private static readonly Color SuccessHoverColor  = Color.FromRgb(0x05, 0x96, 0x69);
        private static readonly Color StatusDotIdle      = Color.FromRgb(0xCC, 0xCC, 0xCC);

        public MainWindow()
        {
            InitializeComponent();

            InitComboBoxItems();
            SetDefaultValues();

            serialPort.DataReceived += serialPort_DataReceived;
        }

        // ==================================================================
        //  窗口初始化
        // ==================================================================

        /// <summary>
        /// 填充各下拉框的可选项
        /// </summary>
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
        }

        /// <summary>
        /// 控件状态初始化——设置各下拉框的默认选中项
        /// </summary>
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

            btnSend.IsEnabled = false;
            cbPortName.IsEnabled = true;
            cbBaudRate.IsEnabled = true;
            cbDataBits.IsEnabled = true;
            cbStopBits.IsEnabled = true;
            cbParity.IsEnabled = true;
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
                        CloseSerialPort(); // USB 异常拔出，自动关闭串口
                    }
                }
            }
            return IntPtr.Zero;
        }

        // ==================================================================
        //  串口打开 / 关闭
        // ==================================================================

        /// <summary>
        /// 打开串口
        /// </summary>
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

                // 按钮切换为已连接状态
                btnOpen.Content = "关闭串口";
                btnOpen.Background = new SolidColorBrush(SuccessColor);
                btnOpen.BorderBrush = new SolidColorBrush(SuccessColor);

                btnSend.IsEnabled = true;
                cbPortName.IsEnabled = false;
                cbBaudRate.IsEnabled = false;
                cbDataBits.IsEnabled = false;
                cbStopBits.IsEnabled = false;
                cbParity.IsEnabled = false;

                // 状态栏
                statusDot.Fill = new SolidColorBrush(SuccessColor);
                tbStatusText.Text = "已连接";
                tbPortInfo.Text = $"{cbPortName.Text} @ {cbBaudRate.Text}";
                tbPortInfo.Visibility = Visibility.Visible;
            }
            catch
            {
                MessageBox.Show("串口打开失败", "提示",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 关闭串口
        /// </summary>
        private void CloseSerialPort()
        {
            serialPort.Close();

            isSerialOpen = false;

            // 按钮恢复默认
            btnOpen.Content = "打开串口";
            btnOpen.Background = new SolidColorBrush(PrimaryColor);
            btnOpen.BorderBrush = new SolidColorBrush(PrimaryColor);

            btnSend.IsEnabled = false;
            cbPortName.IsEnabled = true;
            cbBaudRate.IsEnabled = true;
            cbDataBits.IsEnabled = true;
            cbStopBits.IsEnabled = true;
            cbParity.IsEnabled = true;

            // 状态栏恢复
            statusDot.Fill = new SolidColorBrush(StatusDotIdle);
            tbStatusText.Text = "就绪";
            tbPortInfo.Visibility = Visibility.Collapsed;
        }

        // ==================================================================
        //  控件事件处理
        // ==================================================================

        /// <summary>
        /// 打开/关闭串口按钮
        /// </summary>
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

        /// <summary>
        /// 串口号下拉——动态扫描可用串口
        /// </summary>
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

        /// <summary>
        /// 发送按钮
        /// </summary>
        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            SendData();
        }

        /// <summary>
        /// Ctrl+Enter 快捷发送
        /// </summary>
        private void tbSend_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SendData();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 清空接收区
        /// </summary>
        private void btnClearReceive_Click(object sender, RoutedEventArgs e)
        {
            tbReceive.Clear();
        }

        /// <summary>
        /// 清空发送区
        /// </summary>
        private void btnClearSend_Click(object sender, RoutedEventArgs e)
        {
            tbSend.Clear();
        }

        /// <summary>
        /// 接收模式切换
        /// </summary>
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
        }

        /// <summary>
        /// 接收编码切换
        /// </summary>
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
        }

        /// <summary>
        /// 发送模式切换
        /// </summary>
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

        /// <summary>
        /// 发送编码切换
        /// </summary>
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

        // ==================================================================
        //  串口数据收发
        // ==================================================================

        /// <summary>
        /// 执行串口数据发送
        /// </summary>
        private void SendData()
        {
            if (!serialPort.IsOpen) return;

            if (sendMode == "HEX模式")
            {
                byte[] dataSend = HexToBytes(tbSend.Text);
                serialPort.Write(dataSend, 0, dataSend.Length);
            }
            else if (sendMode == "文本模式")
            {
                byte[] dataSend = TextToBytes(tbSend.Text, sendCoding);
                serialPort.Write(dataSend, 0, dataSend.Length);
            }
        }

        /// <summary>
        /// 串口接收数据事件（后台线程回调 → 通过 Dispatcher 分发到 UI 线程）
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
                    tbReceive.AppendText(BytesToHex(dataReceive));
                }
                else if (receiveMode == "文本模式")
                {
                    tbReceive.AppendText(BytesToText(dataReceive, receiveCoding));
                }
            });
        }

        // ==================================================================
        //  数据转换——以下 4 个方法从原始 WinForms 版本直接移植，逻辑不变
        // ==================================================================

        /// <summary>
        /// 字节流 → 文本（GBK / UTF-8 多字节拼接）
        /// </summary>
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
                    if (byteBuffer[0] < 0x80) // 1 字节字符
                    {
                        byteDecode.Add(byteBuffer[0]);
                        byteBuffer.RemoveAt(0);
                    }
                    else // 2 字节字符
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
                    if ((byteBuffer[0] & 0x80) == 0x00) // 1 字节
                    {
                        byteDecode.Add(byteBuffer[0]);
                        byteBuffer.RemoveAt(0);
                    }
                    else if ((byteBuffer[0] & 0xE0) == 0xC0) // 2 字节
                    {
                        if (byteBuffer.Count >= 2)
                        {
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                        }
                    }
                    else if ((byteBuffer[0] & 0xF0) == 0xE0) // 3 字节
                    {
                        if (byteBuffer.Count >= 3)
                        {
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                            byteDecode.Add(byteBuffer[0]); byteBuffer.RemoveAt(0);
                        }
                    }
                    else if ((byteBuffer[0] & 0xF8) == 0xF0) // 4 字节
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

        /// <summary>
        /// 字节流 → HEX 字符串
        /// </summary>
        private string BytesToHex(byte[] bytes)
        {
            string hex = "";
            foreach (byte b in bytes)
            {
                hex += b.ToString("X2") + " ";
            }
            return hex;
        }

        /// <summary>
        /// 文本 → 字节流
        /// </summary>
        private byte[] TextToBytes(string str, string encoding)
        {
            return Encoding.GetEncoding(encoding).GetBytes(str);
        }

        /// <summary>
        /// HEX 字符串 → 字节流
        /// </summary>
        private byte[] HexToBytes(string str)
        {
            // 清除非法字符
            string str1 = Regex.Replace(str, "[^A-F^a-f^0-9]", "");

            // 两两拆分
            double i = str1.Length;
            int len = 2;
            string[] strList = new string[int.Parse(Math.Ceiling(i / len).ToString())];
            for (int j = 0; j < strList.Length; j++)
            {
                len = len <= str1.Length ? len : str1.Length;
                strList[j] = str1.Substring(0, len);
                str1 = str1.Substring(len, str1.Length - len);
            }

            // 依次转为字节
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
