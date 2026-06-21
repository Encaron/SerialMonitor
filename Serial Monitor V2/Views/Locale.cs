using System.Windows;
using System.Windows.Controls;

namespace 串口助手
{
    // ==================================================================
    //  中英双语切换 —— 和 Theme.cs 同一套资源机制
    //  LocText("中文") → SetResourceReference(prop, "中文")
    //  SwitchTo("en") → foreach Resources[key] = enValue → WPF 自动推
    //  中文即 key。漏英文映射 → 保留中文，不崩。
    // ==================================================================

    /// <summary>控件的 LocText 扩展方法：中文即 key，内部走 SetResourceReference。</summary>
    internal static class LocaleExtensions
    {
        public static void LocText(this Button btn, string zh)
            => btn.SetResourceReference(Button.ContentProperty, zh);

        public static void LocText(this TextBlock tb, string zh)
            => tb.SetResourceReference(TextBlock.TextProperty, zh);

        public static void LocText(this MenuItem mi, string zh)
            => mi.SetResourceReference(HeaderedItemsControl.HeaderProperty, zh);

        public static void LocText(this Label lb, string zh)
            => lb.SetResourceReference(Label.ContentProperty, zh);

        public static void LocText(this CheckBox cb, string zh)
            => cb.SetResourceReference(CheckBox.ContentProperty, zh);

        /// <summary>通用：任意 FrameworkElement 的任意属性</summary>
        public static void LocText(this FrameworkElement fe, DependencyProperty prop, string zh)
            => fe.SetResourceReference(prop, zh);
    }

    /// <summary>语言切换入口。启动时调 Initialize()，切语言时调 SwitchTo(lang)。</summary>
    internal static class Locale
    {
        public static string Current { get; private set; } = "zh";

        /// <summary>切语言后触发，参数 isZh。MainWindow 设此回调更新按钮字重字号。</summary>
        public static System.Action<bool> OnLangChanged = _ => { };

        /// <summary>切语言后重绘回调（使用示例页/快捷键页等运行时重建的 UI）。注册即自动跟，和 RegisterThemePanel 同机制。</summary>
        private static readonly System.Collections.Generic.List<System.Action> _localeRebuildCallbacks = new();

        /// <summary>注册切语言时自动调用的重建回调。新面板加一行 RegisterLocaleRebuild(() => ...) 即可。</summary>
        public static void RegisterLocaleRebuild(System.Action rebuild)
            => _localeRebuildCallbacks.Add(rebuild);

        /// <summary>本次语言切换中缺失 EnMap 的 key。切英文后系统日志可打印，定位遗漏。</summary>
        public static readonly System.Collections.Generic.HashSet<string> MissingKeys = new();

        /// <summary>Window 级别 Resources（DynamicResource 主要解析层），Initialize 时注入。</summary>
        private static ResourceDictionary _windowResources;

        /// <summary>启动时调用一次：注入所有中文 key 到 Application + Window 两级资源字典。</summary>
        public static void Initialize(ResourceDictionary windowResources)
        {
            _windowResources = windowResources;
            SwitchTo("zh");
        }

        /// <summary>切换语言。Application + Window 两级写入，和 Theme.cs 同机制。</summary>
        public static void SwitchTo(string lang)
        {
            Current = lang;
            bool isZh = (lang == "zh");

            foreach (var zhKey in LocaleData.EnMap.Keys)
            {
                var value = isZh ? zhKey : LocaleData.EnMap[zhKey];
                Application.Current.Resources[zhKey] = value;
                if (_windowResources != null)
                    _windowResources[zhKey] = value;
            }

            OnLangChanged(isZh);

            // 切语言后自动触发所有注册的重建回调（使用示例页、快捷键页等）
            foreach (var cb in _localeRebuildCallbacks)
                cb();
        }
    }
}
