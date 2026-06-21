using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace 串口助手
{
    public partial class MainWindow : Window
    {
        // DWM 标题栏暗色模式（Win10 20H1+ / Win11）
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        // ==================================================================
        //  暗色主题
        // ==================================================================

        /// <summary>面板级主题回调注册表（条件色：编辑态蓝条、拖拽中差色等）。纯色用 SetResourceReference 自动跟，不经过这里。</summary>
        private readonly Dictionary<string, Action> _themePanelCallbacks = new();

        /// <summary>注册面板主题回调。同名覆盖写，防重复。</summary>
        internal void RegisterThemePanel(string name, Action callback)
        {
            _themePanelCallbacks[name] = callback;
        }

        /// <summary>透明按钮去 chrome：约束 #27——普通 Button 默认模板悬停时渲染系统高亮色（暗色浅蓝底框）。所有 Background=Transparent 的按钮必须调此方法。</summary>
        internal static void ApplyIconButtonTemplate(Button btn)
        {
            var tpl = new ControlTemplate(typeof(Button));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            tpl.VisualTree = cp;
            btn.Template = tpl;
        }

        private void ApplyTheme(bool dark)
        {
            isDarkTheme = dark;

            ApplyTitleBarTheme(dark);

            foreach (var kv in ThemeMap)
            {
                var brush = FindResource(kv.Key) as SolidColorBrush;
                if (brush == null) continue;

                Color target = dark ? kv.Value.Dark : kv.Value.Light;
                // 一律新建替换：SetResourceReference 绑定的控件订阅资源 key，替换对象 → 触发通知 → 自动更新
                Resources[kv.Key] = new SolidColorBrush(target);
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
            btnThemeSwitch.LocText(dark ? "☀ 亮色模式" : "🌙 暗色模式");
            btnThemeSwitch.Background = dark
                ? new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42))
                : new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            // 顶栏紧凑切换按钮
            tbThemeToggleLabel.LocText(dark ? "亮" : "暗");
            imgThemeToggle.Source = new BitmapImage(new Uri(
                dark ? "Icons/setting/theme/theme-sun.png" : "Icons/setting/theme/theme-moon.png",
                UriKind.Relative));

            // 触发 LogColorizer 重新着色所有可见行
            editor.TextArea.TextView.Redraw();

            LogSystem($"---- 主题切换：{(dark ? "暗色" : "亮色")} ----");

            // Phase 3: 同步 OxyPlot 颜色
            _plotVM?.UpdateThemeColors(dark);
            // 追踪框（TrackerControl internal 类）背景/文字适配主题
            FixPlotTrackerColors(dark);

            // Phase 4: 触发已注册的面板回调（条件色：编辑态蓝条、拖拽中差色等）
            // 纯色通过 SetResourceReference 自动跟，不经过这里
            foreach (var cb in _themePanelCallbacks.Values)
                cb();

            // 快捷键页键帽颜色随主题
            PopulateShortcutPage();
            PopulateExamplesPage();
            // 设置子页导航高亮颜色随主题
            if (_currentSettingsPage != null)
                UpdateSettingsNavHighlight(_currentSettingsPage);
        }

        private void btnThemeSwitch_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(!isDarkTheme);
        }

        private void BtnThemeToggleTop_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(!isDarkTheme);
        }

        private void BtnLangToggleTop_Click(object sender, RoutedEventArgs e)
            => Locale.SwitchTo(Locale.Current == "zh" ? "en" : "zh");

        private void BtnLangToggleSettings_Click(object sender, RoutedEventArgs e)
            => Locale.SwitchTo(Locale.Current == "zh" ? "en" : "zh");

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            var manualPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "用户手册", "index.html");
            if (System.IO.File.Exists(manualPath))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = manualPath,
                    UseShellExecute = true
                });
        }

        /// <summary>标题栏 DWM 暗色模式（Win10 1809+）</summary>
        internal void ApplyTitleBarTheme(bool dark)
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                int useDark = dark ? 1 : 0;
                _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            }
            catch { /* 非 Windows / 早期 OS 静默跳过 */ }
        }
    }
}
