using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace 串口助手
{
    public partial class MainWindow : Window
    {
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

            // 同步 C# 颜色常量（暗色下改用 VS Code Dark+ 色值）
            LogReceivedColor = dark ? LogReceivedColorDark : LogReceivedColorLight;
            StatusDotIdle    = dark ? StatusDotIdleDark    : StatusDotIdleLight;
            PrimaryColor     = dark ? Color.FromRgb(0x0E, 0x63, 0x9C) : Color.FromRgb(0x00, 0x78, 0xD4);
            LogSystemColor   = dark ? Color.FromRgb(0x6A, 0x6A, 0x6A) : Color.FromRgb(0x99, 0x99, 0x99);
            LogSentColor     = dark ? Color.FromRgb(0x0E, 0x63, 0x9C) : Color.FromRgb(0x00, 0x78, 0xD4);

            // 同步代码创建的状态圆点画刷
            if (statusDotBrush != null && !statusDotBrush.IsFrozen)
                statusDotBrush.Color = isSerialOpen ? SuccessColor : StatusDotIdle;

            // 切换按钮文字
            btnThemeSwitch.Content = dark ? "☀ 亮色模式" : "🌙 暗色模式";

            // 刷新已有日志行的颜色
            RefreshRichTextBoxColors();

            LogSystem($"---- 主题切换：{(dark ? "暗色" : "亮色")} ----");
        }

        private void btnThemeSwitch_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(!isDarkTheme);
        }

        /// <summary>
        /// 主题切换后，遍历 RichTextBox 中所有 Paragraph，
        /// 根据角色标签更新文本颜色
        /// </summary>
        private void RefreshRichTextBoxColors()
        {
            foreach (Paragraph para in rtReceive.Document.Blocks)
            {
                string role = para.Tag as string ?? "received";
                Color newColor;
                switch (role)
                {
                    case "system":   newColor = LogSystemColor;   break;
                    case "sent":     newColor = LogSentColor;     break;
                    default:         newColor = LogReceivedColor; break;
                }

                var runs = para.Inlines.OfType<Run>().ToList();
                if (runs.Count == 0) continue;

                foreach (var run in runs)
                    run.Foreground = new SolidColorBrush(newColor);
            }
        }
    }
}
