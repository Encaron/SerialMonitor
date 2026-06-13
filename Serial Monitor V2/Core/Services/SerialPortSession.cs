using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Windows.Threading;

namespace 串口助手
{
    /// <summary>
    /// 单个串口的完整生命周期管理。
    /// 从 MainWindow 提取，行为与 v1.0.0 完全一致。
    ///
    /// 职责：
    ///   - 串口打开/关闭（参数配置、异常抛出）
    ///   - DataReceived 字节读取 → 行拼装（HEX/文本模式）
    ///   - 空闲定时器（100ms 无数据强制输出残留行）
    ///   - 发送字节写入 + TX/RX 计数
    ///   - DTR / RTS 控制
    ///
    /// 不负责：
    ///   - UI 更新（通过事件通知 MainWindow）
    ///   - 数据转换（MainWindow 调用 DataConverter 转换后传入字节）
    ///   - 发送历史、换行符拼接、日志显示（MainWindow 职责）
    /// </summary>
    public class SerialPortSession : IDisposable
    {
        // ==================================================================
        //  字段
        // ==================================================================

        private SerialPort _serialPort;
        private Dispatcher _dispatcher;

        // 接收缓冲：跨 DataReceived 碎片拼成完整行
        private List<byte> _byteBuffer;
        private string _receiveLineBuffer;

        // 空闲定时器：100ms 无新数据到达时强制输出残留
        private DispatcherTimer _flushTimer;

        // 当前接收参数（DataReceived 处理时需要）
        private string _receiveMode;
        private string _receiveCoding;

        // 流量统计
        private long _txByteCount;
        private long _rxByteCount;

        // 关闭中标记（防止 DataReceived 回调在 Close 期间访问已关闭的串口）
        private volatile bool _isClosing;

        // ==================================================================
        //  公开属性 & 事件
        // ==================================================================

        /// <summary>完整行文本已就绪（HEX：每次读取为一行；文本：遇到换行符或空闲超时）</summary>
        public event Action<string> LineReceived;

        /// <summary>连接状态变化（true=已连接，false=已断开）</summary>
        public event Action<bool> ConnectionChanged;

        public bool IsOpen { get; private set; }
        public long TxBytes => _txByteCount;
        public long RxBytes => _rxByteCount;
        public string PortName { get; private set; }

        // ==================================================================
        //  构造 & 释放
        // ==================================================================

        public SerialPortSession(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _serialPort = new SerialPort();
            _byteBuffer = new List<byte>();
            _receiveLineBuffer = "";

            // 空闲刷新定时器（与 v1 行为一致：100ms 无数据 → 输出残留）
            _flushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _flushTimer.Tick += OnFlushTimerTick;
        }

        public void Dispose()
        {
            if (IsOpen)
                Close();
            _serialPort?.Dispose();
            _serialPort = null;
        }

        /// <summary>底层 SerialPort 的实际打开状态（用于 USB 热插拔检测）</summary>
        public bool IsPortOpen => _serialPort?.IsOpen ?? false;

        /// <summary>重置流量计数</summary>
        public void ResetTraffic()
        {
            _txByteCount = 0;
            _rxByteCount = 0;
        }

        // ==================================================================
        //  串口打开 / 关闭
        // ==================================================================

        /// <summary>
        /// 打开串口。参数无效或端口被占用时抛出异常（MainWindow 负责 catch 并弹窗）。
        /// </summary>
        public void Open(
            string portName,
            int baudRate,
            int dataBits,
            StopBits stopBits,
            Parity parity,
            Handshake handshake,
            bool dtrEnable,
            bool rtsEnable,
            string receiveMode,
            string receiveCoding)
        {
            _serialPort.PortName = portName;
            _serialPort.BaudRate = baudRate;
            _serialPort.DataBits = dataBits;
            _serialPort.StopBits = stopBits;
            _serialPort.Parity = parity;
            _serialPort.Handshake = handshake;
            _serialPort.DtrEnable = dtrEnable;
            _serialPort.RtsEnable = rtsEnable;

            _serialPort.Open();
            _serialPort.DiscardInBuffer(); // 丢弃打开前硬件缓冲区堆积的旧数据

            // 重新订阅 DataReceived（Close 时会退订以防死锁）
            _serialPort.DataReceived -= OnDataReceived;
            _serialPort.DataReceived += OnDataReceived;

            _receiveMode = receiveMode;
            _receiveCoding = receiveCoding;
            _isClosing = false;
            IsOpen = true;
            PortName = portName;

            ConnectionChanged?.Invoke(true);
        }

        /// <summary>
        /// 关闭串口。强制输出缓冲区残留文本后断开。
        ///
        /// 防死锁设计：
        ///   1. _isClosing 标记先置位 → 已在队列中的 BeginInvoke 回调检查后直接跳过
        ///   2. DataReceived 退订在 Close() 之前，阻止新事件进入
        ///   3. OnDataReceived 用 BeginInvoke（异步），不会阻塞等待 UI 线程
        /// </summary>
        public void Close()
        {
            // ① 标记关闭中 —— 队列中的 BeginInvoke 回调检查此标记后跳过处理
            _isClosing = true;

            // ② 停止空闲定时器
            _flushTimer.Stop();

            // ③ 退订 DataReceived —— 阻止新事件
            _serialPort.DataReceived -= OnDataReceived;

            // ④ 冲刷缓冲区残留文本
            if (!string.IsNullOrEmpty(_receiveLineBuffer))
            {
                string residual = _receiveLineBuffer;
                _receiveLineBuffer = "";
                LineReceived?.Invoke(residual);
            }

            // ⑤ 关闭串口（此时不会再有 DataReceived 回调竞争）
            _serialPort.Close();

            IsOpen = false;

            ConnectionChanged?.Invoke(false);
        }

        // ==================================================================
        //  数据发送
        // ==================================================================

        /// <summary>
        /// 写入字节到串口（MainWindow 负责用 DataConverter 做好转换后再调用）。
        /// </summary>
        public void SendBytes(byte[] data)
        {
            if (!_serialPort.IsOpen) return;
            if (data == null || data.Length == 0) return;

            try
            {
                _serialPort.Write(data, 0, data.Length);
                _txByteCount += data.Length;
            }
            catch (Exception ex)
            {
                // 写入失败 → 通知 UI 并关闭连接
                string errMsg = $"串口写入异常：{ex.Message}";
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    LineReceived?.Invoke($"[系统] {errMsg}");
                    ConnectionChanged?.Invoke(false);
                }));
                _isClosing = true;
                try { _serialPort.Close(); } catch { /* 尽力关闭 */ }
                IsOpen = false;
            }
        }

        // ==================================================================
        //  DTR / RTS 控制
        // ==================================================================

        public void SetDtr(bool enable)
        {
            if (_serialPort.IsOpen)
                _serialPort.DtrEnable = enable;
        }

        public void SetRts(bool enable)
        {
            if (_serialPort.IsOpen)
                _serialPort.RtsEnable = enable;
        }

        // ==================================================================
        //  接收模式更新（用户切换下拉框时调用）
        // ==================================================================

        /// <summary>
        /// 更新接收模式和编码。会清空字节缓冲并强制输出残留行。
        /// </summary>
        public void UpdateReceiveSettings(string mode, string coding)
        {
            _receiveMode = mode;
            _receiveCoding = coding;
            _byteBuffer.Clear();

            _flushTimer.Stop();
            if (!string.IsNullOrEmpty(_receiveLineBuffer))
            {
                string residual = _receiveLineBuffer;
                _receiveLineBuffer = "";
                LineReceived?.Invoke(residual);
            }
        }

        // ==================================================================
        //  DataReceived 处理（后台线程 → UI 线程）
        // ==================================================================

        /// <summary>
        /// 串口数据到达事件（后台线程触发）。
        /// 使用 BeginInvoke（异步）而非 Invoke（同步），避免与 Close() 形成死锁。
        /// 字节读取 + 行拼装逻辑与 v1 完全一致。
        /// </summary>
        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_isClosing || !_serialPort.IsOpen) return;

            int count;
            byte[] dataReceive;
            try
            {
                count = _serialPort.BytesToRead;
                dataReceive = new byte[count];
                _serialPort.Read(dataReceive, 0, count);
            }
            catch (Exception ex)
            {
                // 后台线程异常：记录日志并安全关闭
                string errMsg = $"串口读取异常：{ex.Message}";
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!_isClosing) LineReceived?.Invoke($"[系统] {errMsg}");
                    ConnectionChanged?.Invoke(false);
                }));
                _isClosing = true;
                try { _serialPort.Close(); } catch { /* 尽力关闭 */ }
                IsOpen = false;
                return;
            }

            _rxByteCount += count;

            // BeginInvoke：异步投递到 UI 线程后立即返回，DataReceived 不会被阻塞
            // 即使 Close() 正在 UI 线程执行，也不会形成相互等待的死锁
            _dispatcher.BeginInvoke(new Action(() =>
            {
                // 关闭中的回调直接跳过（Close() 已冲刷缓冲区）
                if (_isClosing) return;

                if (_receiveMode == "HEX模式")
                {
                    // HEX 模式照原样直接输出
                    LineReceived?.Invoke(DataConverter.BytesToHex(dataReceive));
                }
                else if (_receiveMode == "文本模式")
                {
                    string text = DataConverter.BytesToText(dataReceive, _receiveCoding, _byteBuffer);
                    if (string.IsNullOrEmpty(text)) return;

                    _receiveLineBuffer += text;

                    // 按换行符拆分，输出完整行
                    int idx;
                    bool hasNewline = false;
                    while ((idx = _receiveLineBuffer.IndexOf('\n')) >= 0)
                    {
                        string line = _receiveLineBuffer.Substring(0, idx).TrimEnd('\r');
                        _receiveLineBuffer = _receiveLineBuffer.Substring(idx + 1);
                        hasNewline = true;

                        if (!string.IsNullOrEmpty(line))
                        {
                            LineReceived?.Invoke(line);
                        }
                    }

                    if (hasNewline || _receiveLineBuffer.Length > 0)
                    {
                        // 重置空闲定时器：100ms 内无新数据就强制输出剩余碎片
                        _flushTimer.Stop();
                        _flushTimer.Start();
                    }
                }
            }));
        }

        /// <summary>
        /// 空闲刷新：100ms 无新数据到达时，强制输出缓冲区残留文本
        /// </summary>
        private void OnFlushTimerTick(object sender, EventArgs e)
        {
            _flushTimer.Stop();

            if (!string.IsNullOrEmpty(_receiveLineBuffer))
            {
                string residual = _receiveLineBuffer;
                _receiveLineBuffer = "";
                LineReceived?.Invoke(residual);
            }
        }
    }
}
