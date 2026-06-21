using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace 串口助手
{
    /// <summary>
    /// 双语基础设施：初始化、T() 查找、切语言回调、ComboBox/StyleButton/Inlines 刷新。
    /// 从 MainWindow 构造函数抽出，减少主线文件体积。
    /// </summary>
    public partial class MainWindow : Window
    {
        // ——— 双语初始化（构造函数中调用一次）———

        private void InitLocale()
        {
            // 注入所有中文 key 到 Application + Window 两级资源字典
            Locale.Initialize(this.Resources);

            // 注册切语言时自动重建的页面（和 RegisterThemePanel 同机制：一处注册，自动跟）
            Locale.RegisterLocaleRebuild(RefreshLocaleInlines);
            Locale.RegisterLocaleRebuild(RefreshComboBoxLocale);
            Locale.RegisterLocaleRebuild(RefreshFreqSourceList);
            Locale.RegisterLocaleRebuild(RefreshStyleButtonLabels);

            // Initialize 里的 SwitchTo("zh") 执行时回调还没注册——补一次首次 Inlines 创建
            RefreshLocaleInlines();

            // 双语按钮：切语言时更新 "中/EN" 字重字号
            Locale.OnLangChanged = isZh =>
            {
                if (isZh)
                {
                    tbLangZh.FontWeight = FontWeights.Bold;   tbLangZh.FontSize = 14;
                    tbLangEn.FontWeight = FontWeights.Regular; tbLangEn.FontSize = 11;
                    tbLangZhSettings.FontWeight = FontWeights.Bold;   tbLangZhSettings.FontSize = 14;
                    tbLangEnSettings.FontWeight = FontWeights.Regular; tbLangEnSettings.FontSize = 11;
                }
                else
                {
                    tbLangZh.FontWeight = FontWeights.Regular; tbLangZh.FontSize = 11;
                    tbLangEn.FontWeight = FontWeights.Bold;    tbLangEn.FontSize = 14;
                    tbLangZhSettings.FontWeight = FontWeights.Regular; tbLangZhSettings.FontSize = 11;
                    tbLangEnSettings.FontWeight = FontWeights.Bold;    tbLangEnSettings.FontSize = 14;
                }
                // 切语言后重绑侧栏标题等（避免 WPF 资源通知延迟导致不跟）
                RefreshContentVisibility();

                // 切英文后报告缺失的 EnMap key（帮助定位遗漏）
                if (!isZh && Locale.MissingKeys.Count > 0)
                {
                    var list = string.Join(", ", Locale.MissingKeys.OrderBy(k => k));
                    LogSystem($"[i18n] Missing EnMap keys ({Locale.MissingKeys.Count}): {list}");
                    Locale.MissingKeys.Clear();
                }
                LogSystem(isZh ? "---- 语言：中文 ----" : "---- Language: English ----");
            };

            // ToolTip 含逗号或等号 → 不能用 XAML DynamicResource → C# 侧 SetResourceReference
            cbParity.SetResourceReference(FrameworkElement.ToolTipProperty, "错误检测方式。None=不校验，Odd=奇校验，Even=偶校验");
            cbFlowControl.SetResourceReference(FrameworkElement.ToolTipProperty, "数据流控制。None=无，RTS=硬件流控，XOnXOff=软件流控");
            btnModuleGenRelease.SetResourceReference(FrameworkElement.ToolTipProperty, "为模块内所有按键的松开发送填入 [key,Name,up] 文本值");
        }

        // ——— T() 双语查找 ———

        /// <summary>安全取双语资源：Application.Resources 有 → 翻译；没有 → 回退原文。</summary>
        private string T(string zh)
        {
            var appRes = Application.Current.Resources;
            if (appRes.Contains(zh) && appRes[zh] is string val)
                return val;
            // 只记录含中文的缺 key（纯英文/协议字符串不需要翻译）
            if (ContainsCjk(zh))
                Locale.MissingKeys.Add(zh);
            return zh;
        }

        private static bool ContainsCjk(string s)
        {
            foreach (char c in s)
                if (c >= 0x4E00 && c <= 0x9FFF) return true;
            return false;
        }

        // ——— ComboBox 切语言刷新 ———

        /// <summary>
        /// 切语言时刷新所有 ComboBox 显示文字。LocaleComboItem 自带 Refresh()，
        /// 只需刷新 Display 然后 ItemsSource 空→还原一次，不走 Clear + N×Add。
        /// </summary>
        private void RefreshComboBoxLocale()
        {
            if (_rebuildingComboBox) return;
            _rebuildingComboBox = true;

            void Rebuild(ComboBox cb)
            {
                var src = cb.ItemsSource as LocaleComboItem[];
                if (src == null) return;
                int idx = cb.SelectedIndex;
                foreach (var item in src) item.Refresh();
                cb.ItemsSource = null;
                cb.ItemsSource = src;
                if (idx >= 0 && idx < src.Length) cb.SelectedIndex = idx;
            }

            Rebuild(cbReceiveMode);
            Rebuild(cbSendMode);
            Rebuild(cbParity);
            Rebuild(cbFlowControl);
            Rebuild(cbTimestampFormat);
            Rebuild(cbLineEnding);
            Rebuild(cbPlotMode);
            Rebuild(cbFreqWindow);

            // Keys 面板 — 按键发送模式
            foreach (var cb in new ComboBox[] { cbKeyPressMode, cbKeyReleaseMode, cbKeySendModeMulti, cbKeyReleaseModeMulti, cbModulePressMode, cbModuleReleaseMode })
                Rebuild(cb);

            _rebuildingComboBox = false;
        }

        // ——— 风格按钮切语言刷新 ———

        /// <summary>切语言时刷新风格按钮文字（按钮 Content 不走 DynamicResource）。</summary>
        private void RefreshStyleButtonLabels()
        {
            if (btnJoystickPadStyle != null)
                btnJoystickPadStyle.Content = JoyPadLabel() + " ▾";
            if (btnJoystickThumbStyle != null)
                btnJoystickThumbStyle.Content = JoyThumbLabel() + " ▾";
        }

        // ——— Inlines 切语言刷新 ———

        /// <summary>
        /// 切语言时刷新 Inlines 内可翻译的 Run.Text。首次调用创建 Run 并缓存，
        /// 后续调用只改 Text，不触发 Clear + 重建（避免 ~22 次布局无效）。
        /// </summary>
        private void RefreshLocaleInlines()
        {
            if (!_localeInlinesCached)
            {
                _localeInlinesCached = true;

                // ═══ FFT 操作提示 ═══
                tbFftHints.Inlines.Clear();
                var fftRuns = new List<Run>();
                AddRun(tbFftHints, fftRuns, "🔬 数据源选 [plot,...] 通道 → PC 自动算 FFT", FontWeights.SemiBold, null, null);
                tbFftHints.Inlines.Add(new LineBreak());
                AddRun(tbFftHints, fftRuns, "收到 [fft,...] 协议则覆盖自动频谱", null, null, null);
                tbFftHints.Inlines.Add(new LineBreak());
                AddRun(tbFftHints, fftRuns, "🖱 滚轮缩放 · 右键拖拽平移", null, null, null);
                tbFftHints.Inlines.Add(new LineBreak());
                AddRun(tbFftHints, fftRuns, "📊 暂停后点「详细」按钮查看频谱指标", null, null, null);
                _fftHintRuns = fftRuns.ToArray();

                // ═══ OLED C 代码说明 ═══
                tbOledCodeHints.Inlines.Clear();
                var oledRuns = new List<Run>();
                AddRun(tbOledCodeHints, oledRuns, "坐标轴：起/中/终点标有刻度数字", null, null, null);
                tbOledCodeHints.Inlines.Add(new LineBreak());
                tbOledCodeHints.Inlines.Add(new LineBreak());
                AddRun(tbOledCodeHints, oledRuns, "C 代码写法：", FontWeights.SemiBold, null, null);
                tbOledCodeHints.Inlines.Add(new LineBreak());
                // 以下为固定 C 代码示例，不翻译
                tbOledCodeHints.Inlines.Add(new Run("Serial_Printf(&huart1,"));
                tbOledCodeHints.Inlines.Add(new LineBreak());
                tbOledCodeHints.Inlines.Add(new Run("  \"[display,0,0,\\\"hello\\\",24]\\r\\n\";"));
                tbOledCodeHints.Inlines.Add(new LineBreak());
                tbOledCodeHints.Inlines.Add(new Run("  \"[display,10,20,\\\"ok\\\",16,#FF0000]\\r\\n\";"));
                tbOledCodeHints.Inlines.Add(new LineBreak());
                tbOledCodeHints.Inlines.Add(new LineBreak());
                var warnBrush = new SolidColorBrush(Color.FromRgb(0xE7, 0x48, 0x56));
                AddRun(tbOledCodeHints, oledRuns, "⚠ 内层引号前加 \\ 转义", null, warnBrush, null);
                tbOledCodeHints.Inlines.Add(new LineBreak());
                AddRun(tbOledCodeHints, oledRuns, "末尾 #RRGGBB 可选，不写=白色", null, null, null);
                tbOledCodeHints.Inlines.Add(new LineBreak());
                AddRun(tbOledCodeHints, oledRuns, "[display-clear] 清屏", null, null, null);
                _oledCodeRuns = oledRuns.ToArray();

                // ═══ 使用示例 ═══
                tbUsageExample.Inlines.Clear();
                var usageRuns = new List<Run>();
                AddRun(tbUsageExample, usageRuns, "设备通过串口发送 ", null, null, null);
                var consolasFont = new System.Windows.Media.FontFamily("Consolas");
                var primaryBrush = (Brush)FindResource("PrimaryBrush");
                tbUsageExample.Inlines.Add(new Run("[type,arg1,arg2,...]") { FontFamily = consolasFont, Foreground = primaryBrush });
                AddRun(tbUsageExample, usageRuns, " 格式的消息，", null, null, null);
                AddRun(tbUsageExample, usageRuns, "工具自动解析并路由到对应面板。方括号 ", null, null, null);
                tbUsageExample.Inlines.Add(new Run("[ ]") { FontFamily = consolasFont, Foreground = primaryBrush });
                AddRun(tbUsageExample, usageRuns, " 包裹每条消息，参数用逗号分隔。含逗号的参数用双引号包裹。", null, null, null);
                _usageExampleRuns = usageRuns.ToArray();
            }
            else
            {
                // 切语言：只更新缓存的 Run.Text，不重建 Inlines
                RefreshCachedRuns(_fftHintRuns);
                RefreshCachedRuns(_oledCodeRuns);
                RefreshCachedRuns(_usageExampleRuns);
            }
        }

        /// <summary>创建 Run 并加入 TextBlock 和缓存列表。</summary>
        private void AddRun(TextBlock tb, List<Run> cache, string zhKey,
            FontWeight? weight, Brush foreground, FontFamily fontFamily)
        {
            var run = new Run(T(zhKey));
            run.Tag = zhKey;
            if (weight.HasValue) run.FontWeight = weight.Value;
            if (foreground != null) run.Foreground = foreground;
            if (fontFamily != null) run.FontFamily = fontFamily;
            cache.Add(run);
            tb.Inlines.Add(run);
        }

        /// <summary>重新计算并更新缓存 Run 的 Text（切语言时调用）。从 Tag 取中文 key 重译。</summary>
        private void RefreshCachedRuns(Run[] runs)
        {
            foreach (var run in runs)
            {
                var zhKey = run.Tag as string;
                if (!string.IsNullOrEmpty(zhKey))
                    run.Text = T(zhKey);
            }
        }

        // ——— FFT 数据源列表切语言刷新 ———

        /// <summary>刷新频域数据源下拉框：列出 [plot,...] 通道 + [fft,...] 命名源。</summary>
        private void RefreshFreqSourceList()
        {
            if (_plotVM == null) return;
            var current = cbFreqSource.SelectedItem as string;
            cbFreqSource.Items.Clear();
            cbFreqSource.Items.Add(T("（不选）"));
            // [plot,...] 通道 → PC 自动算 FFT
            foreach (var n in _plotVM.GetChannelNames())
                cbFreqSource.Items.Add($"📈 {n}");
            // [fft,name,...] 命名源 → STM32 直连
            foreach (var n in _plotVM.GetNamedFftKeys())
                cbFreqSource.Items.Add($"📶 {n}");
            // 恢复选中
            if (current != null && cbFreqSource.Items.Contains(current))
                cbFreqSource.SelectedItem = current;
            else if (cbFreqSource.SelectedIndex < 0)
                cbFreqSource.SelectedIndex = 0;
        }
    }
}
