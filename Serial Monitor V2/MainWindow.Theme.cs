using System;
using System.Windows;
using System.Windows.Controls;
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
                statusDotBrush.Color = _session.IsOpen ? SuccessColor : StatusDotIdle;

            // 切换按钮文字
            btnThemeSwitch.Content = dark ? "☀ 亮色模式" : "🌙 暗色模式";

            // 触发 LogColorizer 重新着色所有可见行
            editor.TextArea.TextView.Redraw();

            LogSystem($"---- 主题切换：{(dark ? "暗色" : "亮色")} ----");

            // Phase 3: 同步 OxyPlot 颜色
            _plotVM?.UpdateThemeColors(dark);

            // Phase 4: 重建按键/滑杆面板（颜色随主题更新）
            if (_keyVM != null && _keyVM.Keys.Count > 0)
                RefreshKeysUI();
            if (_sliderVM != null && _sliderVM.Sliders.Count > 0)
                RefreshSlidersUI();
        }

        private void btnThemeSwitch_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(!isDarkTheme);
        }
    }
}
