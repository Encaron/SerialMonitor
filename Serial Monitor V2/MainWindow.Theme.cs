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

            // 覆盖系统颜色，修复 ComboBox/TextBox 等原生控件在暗色下的白色背景
            if (dark)
            {
                Resources[System.Windows.SystemColors.WindowBrushKey]         = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
                Resources[System.Windows.SystemColors.WindowTextBrushKey]     = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
                Resources[System.Windows.SystemColors.ControlBrushKey]        = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
                Resources[System.Windows.SystemColors.ControlTextBrushKey]    = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
                Resources[System.Windows.SystemColors.HighlightBrushKey]      = new SolidColorBrush(Color.FromRgb(0x0E, 0x63, 0x9C));
                Resources[System.Windows.SystemColors.HighlightTextBrushKey]  = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            }
            else
            {
                Resources[System.Windows.SystemColors.WindowBrushKey]         = new SolidColorBrush(Colors.White);
                Resources[System.Windows.SystemColors.WindowTextBrushKey]     = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
                Resources[System.Windows.SystemColors.ControlBrushKey]        = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
                Resources[System.Windows.SystemColors.ControlTextBrushKey]    = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
                Resources[System.Windows.SystemColors.HighlightBrushKey]      = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
                Resources[System.Windows.SystemColors.HighlightTextBrushKey]  = new SolidColorBrush(Colors.White);
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

            // 切换按钮：文字 + 外观随主题变化（暗色下亮一点，亮色下暗一点）
            btnThemeSwitch.Content = dark ? "☀ 亮色模式" : "🌙 暗色模式";
            btnThemeSwitch.Background = dark
                ? new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42))
                : new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));

            // 触发 LogColorizer 重新着色所有可见行
            editor.TextArea.TextView.Redraw();

            LogSystem($"---- 主题切换：{(dark ? "暗色" : "亮色")} ----");

            // Phase 3: 同步 OxyPlot 颜色
            _plotVM?.UpdateThemeColors(dark);
            // 追踪框（TrackerControl internal 类）背景/文字适配主题
            FixPlotTrackerColors(dark);

            // Phase 4: 重建控制面板（颜色随主题更新——代码创建的控件不自动跟随 DynamicResource）
            if (_keyVM != null && _keyVM.Keys.Count > 0)
                RefreshKeysUI();
            if (_sliderVM != null && _sliderVM.Sliders.Count > 0)
                RefreshSlidersUI();
            if (_joyVM != null)
                RefreshJoystickUI();
            if (_displayVM != null)
                RefreshOLEDUI();

            // 快捷键页键帽颜色随主题
            PopulateShortcutPage();
            PopulateExamplesPage();
        }

        private void btnThemeSwitch_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(!isDarkTheme);
        }
    }
}
