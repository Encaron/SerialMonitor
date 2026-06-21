using System;

namespace 串口助手
{
    /// <summary>
    /// ComboBox 语言切换的零重建方案：ItemsSource 存 LocaleComboItem，
    /// DisplayMemberPath="Display"。切语言时只写 Resource 字典，
    /// 然后 ItemsSource 空→还原一次强制重读（远轻于 Clear + N×Add）。
    /// </summary>
    internal sealed class LocaleComboItem
    {
        /// <summary>英文 key，逻辑值，存 prefs.json 用</summary>
        public string Key { get; }

        /// <summary>当前语言下的显示文字。Tr(DisplayXxx(Key)) 的结果。</summary>
        public string Display { get; private set; }

        readonly Func<string, string> _display;

        public LocaleComboItem(string key, Func<string, string> display)
        {
            Key = key;
            _display = display;
            Display = _display(key);
        }

        /// <summary>切语言后调用：重新计算 Display</summary>
        public void Refresh() => Display = _display(Key);

        /// <summary>ComboBox 默认用 ToString() 展示，省 DisplayMemberPath</summary>
        public override string ToString() => Display;
    }
}
