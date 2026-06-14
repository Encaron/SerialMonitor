using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Search;

namespace 串口助手
{
    public partial class MainWindow : Window
    {
        // ==================================================================
        //  彩色日志 — AvalonEdit 虚拟化渲染
        // ==================================================================

        /// <summary>
        /// 每行的角色标签，与 editor.Document 的行一一对应。
        /// 用于主题切换时按角色重新着色："system" / "sent" / "received"
        /// </summary>
        private List<string> _lineRoles = new List<string>();

        /// <summary>
        /// AvalonEdit 行着色器：根据 _lineRoles 给每行文字上色。
        /// 主题切换后调用 editor.TextArea.TextView.Redraw() 触发重绘。
        /// </summary>
        private class LogColorizer : DocumentColorizingTransformer
        {
            private MainWindow _owner;
            public LogColorizer(MainWindow owner) { _owner = owner; }

            protected override void ColorizeLine(DocumentLine line)
            {
                int idx = line.LineNumber - 1;
                if (idx < 0 || idx >= _owner._lineRoles.Count) return;

                string role = _owner._lineRoles[idx];
                // LogSystemColor / LogSentColor / LogReceivedColor 是 static 字段
                Color c;
                switch (role)
                {
                    case "system":   c = LogSystemColor;   break;
                    case "sent":     c = LogSentColor;     break;
                    default:         c = LogReceivedColor; break;
                }
                ChangeLinePart(line.Offset, line.EndOffset,
                    element => element.TextRunProperties.SetForegroundBrush(new SolidColorBrush(c)));
            }
        }

        /// <summary>
        /// 初始化 AvalonEdit 样式：注册着色器
        /// </summary>
        private void InitEditor()
        {
            editor.TextArea.TextView.LineTransformers.Add(new LogColorizer(this));
            _searchPanel = SearchPanel.Install(editor.TextArea);
        }

        /// <summary>
        /// 系统消息（灰色）：打开/关闭串口等
        /// 独立显示模式 → lbSystemLog；否则回退到 editor
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
                // 转义真实换行符为字面量，避免换行符在编辑器中裂成多行导致着色失效
                string safeText = text.Replace("\r\n", "\\r\\n").Replace("\n", "\\n").Replace("\r", "\\r");
                msg = $"{timestamp} ---- 已发送 {sendCoding.ToLower()} 编码消息: \"{safeText}\" ----";
            }
            else
            {
                msg = $"{timestamp} ---- 已发送 HEX 消息 ({text.Length / 3 + 1} 字节) ----";
            }

            if (sendMode == "HEX模式")
            {
                AppendColoredLine(msg, LogSentColor, "sent");
                string hexPreview = text.Length > 80 ? text.Substring(0, 80) + "..." : text;
                AppendColoredLine("    " + hexPreview, LogSentColor, "sent");
            }
            else
            {
                AppendColoredLine(msg, LogSentColor, "sent");
            }
        }

        /// <summary>
        /// 接收数据：串口实际收到的数据
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
        /// 追加一行带颜色的日志（直接写入 AvalonEdit Document，虚拟化渲染无需批量队列）。
        /// role 用于主题切换时重新着色："system" / "sent" / "received"
        /// skipPause：暂停恢复回放时跳过暂停检查
        /// </summary>
        private void AppendColoredLine(string text, Color color, string role = null, bool skipPause = false)
        {
            // 暂停期间：写入暂停缓冲区，不更新界面
            if (_isPaused && !skipPause)
            {
                bool bufferWasFull = _pausedLines.Count >= MaxPausedLines;
                while (_pausedLines.Count >= MaxPausedLines)
                    _pausedLines.RemoveAt(0);
                if (bufferWasFull)
                {
                    LogSystem("⚠ 暂停缓冲已满（2000 条），最早的数据已被丢弃");
                    lbSystemLog.Items.Add("---- ⚠ 暂停缓冲已满，最早的数据已被丢弃 ----");
                    while (lbSystemLog.Items.Count > 50)
                        lbSystemLog.Items.RemoveAt(0);
                    if (lbSystemLog.Items.Count > 0)
                        lbSystemLog.ScrollIntoView(lbSystemLog.Items[lbSystemLog.Items.Count - 1]);
                }
                _pausedLines.Add(new PausedLine { Text = text, Color = color, Role = role });
                return;
            }

            // 追加前检测用户是否在底部
            bool atBottom;
            try
            {
                var tv = editor.TextArea.TextView;
                atBottom = tv.VerticalOffset >= tv.DocumentHeight - tv.ActualHeight - SmartScrollLockPixels;
            }
            catch
            {
                atBottom = true;
            }

            // 末尾追加一行
            if (editor.Document.TextLength > 0)
                editor.Document.Insert(editor.Document.TextLength, Environment.NewLine);
            editor.Document.Insert(editor.Document.TextLength, text);

            // 记录角色
            _lineRoles.Add(role ?? "received");

            // 超出上限时裁剪最早的行（一次清 CropMargin 行，留余量避免频繁触发）
            if (editor.Document.LineCount > MaxLogLines)
            {
                var firstLine = editor.Document.GetLineByNumber(1);
                int removeCount = Math.Min(CropMargin, editor.Document.LineCount);
                int offset = firstLine.Offset;
                int length = 0;
                for (int i = 0; i < removeCount && offset < editor.Document.TextLength; i++)
                {
                    var line = editor.Document.GetLineByOffset(offset);
                    length += line.TotalLength;
                    offset += line.TotalLength;
                }
                editor.Document.Remove(firstLine.Offset, length);
                for (int i = 0; i < removeCount && _lineRoles.Count > 0; i++)
                    _lineRoles.RemoveAt(0);
            }

            // 自动滚底
            if (atBottom)
                editor.ScrollToEnd();
        }

        /// <summary>
        /// 清除接收区所有内容（日志 + 行号 + 角色记录 + 暂停缓冲）
        /// </summary>
        private void ClearEditorContent()
        {
            editor.Document.Text = "";
            _lineRoles.Clear();
            _pausedLines.Clear();
        }
    }
}
