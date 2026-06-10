using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace 串口助手
{
    public partial class MainWindow : Window
    {
        // ==================================================================
        //  RichTextBox 彩色日志 — 三种消息类型
        // ==================================================================

        /// <summary>
        /// 批量日志条目
        /// </summary>
        private struct LogEntry
        {
            public string Text;
            public Color Color;
            public string Role;
        }

        /// <summary>
        /// 批量日志队列：高频接收时先入队，由定时器合并写入 RichTextBox，
        /// 避免每条数据都触发一次 FlowDocument 全量布局导致界面卡死。
        /// </summary>
        private List<LogEntry> _logBatch = new List<LogEntry>();

        /// <summary>
        /// 批量刷新定时器：100ms 无新数据后一次性写入 RichTextBox。
        /// 若队列积压超过 200 条则强制立即刷新（防止极端高速下的内存堆积）。
        /// </summary>
        private DispatcherTimer _batchFlushTimer;

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
                AppendColoredLine(text, LogSystemColor, "system");
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
                AppendColoredLine(msg, LogSentColor, "sent");
                // HEX 原文单独一行
                string hexPreview = text.Length > 80 ? text.Substring(0, 80) + "..." : text;
                AppendColoredLine("    " + hexPreview, LogSentColor, "sent");
            }
            else
            {
                AppendColoredLine(msg, LogSentColor, "sent");
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
                AppendColoredLine($"{timestamp} -> {text}", LogReceivedColor, "received");
            }
            else
            {
                AppendColoredLine(text, LogReceivedColor, "received");
            }
        }

        /// <summary>
        /// 通用：追加一行带颜色的日志。
        /// 数据先进入批量队列，由 _batchFlushTimer 合并写入 RichTextBox，
        /// 避免高频接收时每条数据触发一次 FlowDocument 全量布局导致界面卡死。
        /// role 用于主题切换时重新着色："system" / "sent" / "received"
        /// </summary>
        private void AppendColoredLine(string text, Color color, string role = null)
        {
            // 暂停期间：写入暂停缓冲区，不更新界面
            if (_isPaused)
            {
                while (_pausedLines.Count >= MaxPausedLines)
                    _pausedLines.RemoveAt(0);
                _pausedLines.Add(new PausedLine { Text = text, Color = color, Role = role });
                return;
            }

            // 入队而非立即写 RichTextBox：高频率下合并 UI 更新可减少 ~90% 布局重算
            _logBatch.Add(new LogEntry { Text = text, Color = color, Role = role ?? "received" });

            // 首次入队时启动固定间隔定时器，之后不重置——保证持续高速数据也能定期刷新
            if (!_batchFlushTimer.IsEnabled)
                _batchFlushTimer.Start();

            // 极端高速下队列积压过多 → 强制立即刷新，防止长时间不更新
            if (_logBatch.Count >= 50)
                FlushLogBatch();
        }

        /// <summary>
        /// 行号列宽随位数自动扩容：4位48px → 5位52px → 6位58px → 7位64px（封顶）
        /// </summary>
        private void UpdateLineNumberColumnWidth()
        {
            if (_lineCount >= 1000000)
                svLineNumbers.MinWidth = 64;
            else if (_lineCount >= 100000)
                svLineNumbers.MinWidth = 58;
            else if (_lineCount >= 10000)
                svLineNumbers.MinWidth = 52;
        }

        /// <summary>
        /// RichTextBox 加载后找到内部 ScrollViewer，hook 滚动事件同步行号面板
        /// </summary>
        private ScrollViewer _rtScrollViewer;
        private void rtReceive_Loaded(object sender, RoutedEventArgs e)
        {
            _rtScrollViewer = FindVisualChild<ScrollViewer>(rtReceive);
            if (_rtScrollViewer != null)
            {
                _rtScrollViewer.ScrollChanged += RtScrollViewer_ScrollChanged;
            }
        }

        private void RtScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (svLineNumbers != null)
                svLineNumbers.ScrollToVerticalOffset(e.VerticalOffset);
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;
                var found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 绕过暂停检查的直接追加（暂停恢复回放时使用）
        /// </summary>
        private void AppendColoredLineUnbuffered(string text, Color color, string role)
        {
            _lineCount++;

            if (_lineCount == 10000 || _lineCount == 100000 || _lineCount == 1000000)
                UpdateLineNumberColumnWidth();

            Paragraph para = new Paragraph();
            para.Margin = new Thickness(0);
            para.LineHeight = 2;
            if (role != null)
                para.Tag = role;
            else
                para.Tag = "received";
            para.Inlines.Add(new Run(text) { Foreground = new SolidColorBrush(color) });
            rtReceive.Document.Blocks.Add(para);

            if (_showLineNumbers)
                icLineNumbers.Items.Add(_lineCount.ToString());

            while (rtReceive.Document.Blocks.Count > MaxLogLines)
            {
                rtReceive.Document.Blocks.Remove(rtReceive.Document.Blocks.FirstBlock);
                if (_showLineNumbers && icLineNumbers.Items.Count > 0)
                    icLineNumbers.Items.RemoveAt(0);
            }

            bool isAtBottom = rtReceive.VerticalOffset >= rtReceive.ExtentHeight - rtReceive.ViewportHeight - 8;
            if (isAtBottom)
                rtReceive.ScrollToEnd();
        }

        /// <summary>
        /// 批量刷新：将队列中积累的日志一次性写入 RichTextBox。
        /// 由 _batchFlushTimer 固定间隔触发（80ms），或队列积压 ≥50 条时强制调用。
        /// 只做一次滚动检测 + 一次裁剪 + 一次滚底，替代逐行操作。
        /// 刷新后若队列已空则停止定时器，否则等待下一个周期。
        /// </summary>
        private void FlushLogBatch()
        {
            if (_logBatch.Count == 0) return;

            // 取出队列并立即替换为新队列（防止重入，后续入队的新数据写入新队列）
            var batch = _logBatch;
            _logBatch = new List<LogEntry>();

            // 批量写入前检测用户是否在底部（只测一次）
            bool atBottom = rtReceive.VerticalOffset >= rtReceive.ExtentHeight - rtReceive.ViewportHeight - 8;

            foreach (var entry in batch)
            {
                _lineCount++;

                if (_lineCount == 10000 || _lineCount == 100000 || _lineCount == 1000000)
                    UpdateLineNumberColumnWidth();

                Paragraph para = new Paragraph();
                para.Margin = new Thickness(0);
                para.LineHeight = 2;
                para.Tag = entry.Role;

                para.Inlines.Add(new Run(entry.Text) { Foreground = new SolidColorBrush(entry.Color) });

                rtReceive.Document.Blocks.Add(para);

                if (_showLineNumbers)
                    icLineNumbers.Items.Add(_lineCount.ToString());
            }

            // 统一裁剪（只做一次）
            while (rtReceive.Document.Blocks.Count > MaxLogLines)
            {
                rtReceive.Document.Blocks.Remove(rtReceive.Document.Blocks.FirstBlock);
                if (_showLineNumbers && icLineNumbers.Items.Count > 0)
                    icLineNumbers.Items.RemoveAt(0);
            }

            // 仅在用户此前在底部时才自动滚底
            if (atBottom)
                rtReceive.ScrollToEnd();

            // 队列空了则停止定时器，否则保持运行等待下一轮
            if (_logBatch.Count == 0)
                _batchFlushTimer.Stop();
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
    }
}
