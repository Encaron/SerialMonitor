using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace 串口助手
{
    public partial class MainWindow : Window
    {
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
        /// 通用：向 RichTextBox 追加一行带颜色的文本，可选行号
        /// role 用于主题切换时重新着色："system" / "sent" / "received"
        /// </summary>
        private void AppendColoredLine(string text, Color color, string role = null)
        {
            // 暂停期间：写入缓冲区，不更新界面
            if (_isPaused)
            {
                while (_pausedLines.Count >= MaxPausedLines)
                    _pausedLines.RemoveAt(0);
                _pausedLines.Add(new PausedLine { Text = text, Color = color, Role = role });
                return;
            }

            _lineCount++;

            // 行号扩容：10000→52px  100000→58px  1000000→64px（封顶）
            if (_lineCount == 10000 || _lineCount == 100000 || _lineCount == 1000000)
                UpdateLineNumberColumnWidth();

            Paragraph para = new Paragraph();
            para.Margin = new Thickness(0);
            para.LineHeight = 2;

            // 存储角色标签，供 RefreshRichTextBoxColors 使用
            if (role != null)
                para.Tag = role;
            else
                para.Tag = "received"; // 默认当作接收数据

            para.Inlines.Add(new Run(text) { Foreground = new SolidColorBrush(color) });

            rtReceive.Document.Blocks.Add(para);

            // 行号面板同步追加
            if (_showLineNumbers)
                icLineNumbers.Items.Add(_lineCount.ToString());

            // 超行裁剪
            while (rtReceive.Document.Blocks.Count > MaxLogLines)
            {
                rtReceive.Document.Blocks.Remove(rtReceive.Document.Blocks.FirstBlock);
                if (_showLineNumbers && icLineNumbers.Items.Count > 0)
                    icLineNumbers.Items.RemoveAt(0);
            }

            // 仅在用户已滚到底部时自动滚屏（避免打断手动翻阅历史）
            bool isAtBottom = rtReceive.VerticalOffset >= rtReceive.ExtentHeight - rtReceive.ViewportHeight - 8;
            if (isAtBottom)
                rtReceive.ScrollToEnd();
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
