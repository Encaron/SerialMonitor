using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Search;

namespace 串口助手
{
    public partial class MainWindow : Window
    {
        // ——— 串口参数状态 ———

        // receiveMode / receiveCoding 同时存于 MainWindow（UI 绑定）和 Session（DataReceived 处理）
        private string receiveMode = "hex";
        private string receiveCoding = "GBK";
        private string sendMode = "hex";
        private string sendCoding = "GBK";

        // 串口会话（替代原来的 serialPort / byteBuffer / receiveLineBuffer / flushTimer）
        private SerialPortSession _session;

        // Phase 3: 波形图 ViewModel
        private PlotViewModel _plotVM;
        private OxyPlot.Wpf.PlotView plotView;
        private bool _plotViewCreated;
        // #10 FFT：当前视角（false=时域，true=频域）
        private bool _isFreqDomain;

        // ——— 颜色常量（与 XAML 资源保持一致）———

        private static Color PrimaryColor       = Color.FromRgb(0x00, 0x78, 0xD4);
        private static Color SuccessColor       = Color.FromRgb(0x10, 0xB9, 0x81);
        private static Color LogSystemColor     = Color.FromRgb(0x99, 0x99, 0x99);
        private static Color LogSentColor       = Color.FromRgb(0x00, 0x78, 0xD4);
        private static readonly Color LogReceivedColorLight = Color.FromRgb(0x2D, 0x2D, 0x2D);
        private static readonly Color LogReceivedColorDark  = Color.FromRgb(0xD4, 0xD4, 0xD4);
        private static readonly Color StatusDotIdleLight    = Color.FromRgb(0xCC, 0xCC, 0xCC);
        private static readonly Color StatusDotIdleDark     = Color.FromRgb(0x5A, 0x5A, 0x5A);

        //  运行时根据主题切换 ↓
        private static Color LogReceivedColor = LogReceivedColorLight;
        private static Color StatusDotIdle    = StatusDotIdleLight;

        // ——— 定时发送 ———
        private DispatcherTimer autoSendTimer;

        // ——— 日志/批量操作常量 ———

        private const int MaxLogLines = 2000;
        /// <summary>超出 MaxLogLines 时一次性裁剪的行数（留余量避免频繁触发）</summary>
        private const int CropMargin = 500;
        /// <summary>距底部 ≤ 此像素视为"用户在底部"，自动滚屏</summary>
        private const int SmartScrollLockPixels = 8;

        // ——— 行号 ———
        private bool _showLineNumbers = true;

        // ——— 快捷发送 ———
        /// <summary>
        /// label → content（content 中的 \r\n 以字面量存储，发送时还原）
        /// </summary>
        private Dictionary<string, string> quickSends = new Dictionary<string, string>();

        // ——— 发送历史 ———
        private List<string> sendHistory = new List<string>();
        private const int MaxSendHistory = 20;

        // ——— HEX 输入格式化 ———
        private bool _isHexFormatting = false;

        // ——— 暂停显示 ———
        private bool _isPaused = false;
        private class PausedLine { public string Text; public Color Color; public string Role; }
        private List<PausedLine> _pausedLines = new List<PausedLine>();
        private const int MaxPausedLines = 2000;

        // ——— 自动重连 ———
        private string lastPortName = "";
        private string _lastSuccessfulPort = "";
        private DispatcherTimer reconnectTimer;
        private const int MaxReconnectAttempts = 10;
        private int _reconnectAttempts = 0;

        // ——— 动画画刷（非冻结，支持 ColorAnimation）———
        private SolidColorBrush statusDotBrush;

        // ——— 字体检测 ———
        private bool fontMissing = false;

        // ——— 暗色主题 ———
        private bool isDarkTheme = false;

        // ——— 版本更新检查 ———
        private string _updateUrl;          // 非 null 表示有新版本可用
        private string _remoteVersion;      // "2.5.0"

        // ——— 接收区协议筛选 ———
        private bool _showProtocolMsgs = true;
        private bool _showPlainText = true;
        private Dictionary<string, bool> _protocolTypeFilters = new()
        {
            ["sensor"] = true, ["ctrl"] = true, ["plot"] = true,
            ["slider"] = true, ["key"] = true, ["joystick"] = true,
            ["display"] = true, ["fft"] = true, ["draw"] = true,
        };

        // Phase 2: 标签切换
        private string _currentTab = "Receive";
        private string _previousContentTab = "Receive";
        private string _currentSettingsPage; // null = 未展开子页 / "serial" / "shortcuts" / "about"
        private HashSet<string> _expandedExampleTypes = new(); // 使用示例页已展开的协议类型
        private bool _rebuildingComboBox; // 重建 ComboBox 项目时抑制 SelectionChanged 副作用
        private SearchPanel _searchPanel;

        // 图标栏面板显隐管理（决策 12改：+ 下拉菜单切换）
        // Receive 和 Settings 常驻，不在字典中
        private Dictionary<string, bool> _panelVisible = new Dictionary<string, bool>
        {
            ["Plot"] = true, ["Keys"] = true, ["Sliders"] = true,
            ["OLED"] = true, ["Joystick"] = true, ["Sensors"] = true,
        };

        private static readonly Dictionary<string, (Color Light, Color Dark)> ThemeMap =
            new Dictionary<string, (Color, Color)>
        {
            ["WindowBgBrush"]      = (Color.FromRgb(0xF0,0xF0,0xF0), Color.FromRgb(0x1A,0x1A,0x1C)),
            ["CardBgBrush"]        = (Color.FromRgb(0xFF,0xFF,0xFF), Color.FromRgb(0x25,0x25,0x26)),
            ["CardBorderBrush"]    = (Color.FromRgb(0xE0,0xE0,0xE0), Color.FromRgb(0x3E,0x3E,0x42)),
            ["TextPrimaryBrush"]   = (Color.FromRgb(0x2D,0x2D,0x2D), Color.FromRgb(0xD4,0xD4,0xD4)),
            ["TextSecondaryBrush"] = (Color.FromRgb(0x55,0x55,0x55), Color.FromRgb(0x9D,0x9D,0x9D)),
            ["TextMutedBrush"]     = (Color.FromRgb(0x99,0x99,0x99), Color.FromRgb(0x6A,0x6A,0x6A)),
            ["PrimaryBrush"]       = (Color.FromRgb(0x00,0x78,0xD4), Color.FromRgb(0x0E,0x63,0x9C)),
            ["PrimaryHoverBrush"]  = (Color.FromRgb(0x10,0x6E,0xBE), Color.FromRgb(0x11,0x77,0xBB)),
            ["SecondaryBrush"]     = (Color.FromRgb(0x88,0x88,0x88), Color.FromRgb(0x70,0x70,0x70)),
            ["SecondaryHoverBrush"]= (Color.FromRgb(0x66,0x66,0x66), Color.FromRgb(0x8A,0x8A,0x8A)),
            ["StatusBarBgBrush"]   = (Color.FromRgb(0xE6,0xE6,0xE6), Color.FromRgb(0x33,0x33,0x33)),
            ["SeparatorBrush"]     = (Color.FromRgb(0xEE,0xEE,0xEE), Color.FromRgb(0x33,0x33,0x33)),
            ["InputBorderBrush"]   = (Color.FromRgb(0xD0,0xD0,0xD0), Color.FromRgb(0x3E,0x3E,0x42)),
            ["LogSystemBrush"]     = (Color.FromRgb(0x99,0x99,0x99), Color.FromRgb(0x6A,0x6A,0x6A)),
            ["LogReceivedBrush"]   = (Color.FromRgb(0x2D,0x2D,0x2D), Color.FromRgb(0xD4,0xD4,0xD4)),
            ["TextSubtleBrush"]    = (Color.FromRgb(0xBB,0xBB,0xBB), Color.FromRgb(0x5A,0x5A,0x5A)),
            ["TextFaintBrush"]     = (Color.FromRgb(0xCC,0xCC,0xCC), Color.FromRgb(0x4F,0x4F,0x4F)),
            ["SecondaryHoverBgBrush"]   = (Color.FromRgb(0xF5,0xF5,0xF5), Color.FromRgb(0x2A,0x2A,0x2D)),
            ["ButtonDisabledBgBrush"]   = (Color.FromRgb(0xE8,0xE8,0xE8), Color.FromRgb(0x2D,0x2D,0x2D)),
            ["ReceiveAreaBgBrush"]      = (Color.FromRgb(0xFA,0xFA,0xFA), Color.FromRgb(0x1A,0x1A,0x1C)),
            ["StatusDotIdleBrush"]      = (Color.FromRgb(0xCC,0xCC,0xCC), Color.FromRgb(0x5A,0x5A,0x5A)),
            ["HudBackgroundBrush"]      = (Color.FromArgb(0xF0,0xFF,0xFF,0xFF), Color.FromArgb(0x99,0x00,0x00,0x00)),
            ["HudBorderBrush"]          = (Color.FromArgb(0x33,0x00,0x00,0x00), Color.FromArgb(0x33,0xFF,0xFF,0xFF)),
        };

        // 图标栏面板注册表：名称 → (按钮, 主面板, 侧面板, 初始化, 退出编辑)
        private class TabInfo { public string Name; public UIElement Panel; public UIElement SidePanel; public Action Init; public Action ExitEdit; }
        private readonly Dictionary<string, TabInfo> _tabRegistry = new();

        /// <summary>一处注册面板：自动绑定图标栏二次点击动画，所有显隐逻辑从注册表自动生成</summary>
        private void RegisterTab(string name, RadioButton btn, UIElement panel, UIElement sidePanel, Action init = null, Action exitEdit = null)
        {
            btn.Tag = name;
            btn.AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(IconBarIcon_Click), handledEventsToo: true);
            _tabRegistry[name] = new TabInfo { Name = name, Panel = panel, SidePanel = sidePanel, Init = init, ExitEdit = exitEdit };
        }

        public MainWindow()
        {
            InitializeComponent();

            // 双语：注入所有中文 key 到 Application + Window 两级资源字典
            Locale.Initialize(this.Resources);

            // 注册切语言时自动重建的页面（和 RegisterThemePanel 同机制：一处注册，自动跟）
            Locale.RegisterLocaleRebuild(RefreshLocaleInlines);
            Locale.RegisterLocaleRebuild(RefreshComboBoxLocale);
            Locale.RegisterLocaleRebuild(RefreshFreqSourceList);
            Locale.RegisterLocaleRebuild(RefreshStyleButtonLabels);
            // PopulateShortcutPage / PopulateExamplesPage 已改 LocText → SetResourceReference，切语言时 WPF 自动推，无需重建

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

            // 弹窗 toggle：AttachPopupToggle 拦截 PreviewMouseLeftButtonDown（CreatePopup 默认 StaysOpen=true，无竞态）
            AttachPopupToggle(btnFilterMenu, () => _filterPopup);
            AttachPopupToggle(btnAddPanel, () => _addPanelPopup);

            // ToolTip 含逗号或等号 → 不能用 XAML DynamicResource → C# 侧 SetResourceReference
            cbParity.SetResourceReference(FrameworkElement.ToolTipProperty, "错误检测方式。None=不校验，Odd=奇校验，Even=偶校验");
            cbFlowControl.SetResourceReference(FrameworkElement.ToolTipProperty, "数据流控制。None=无，RTS=硬件流控，XOnXOff=软件流控");
            btnModuleGenRelease.SetResourceReference(FrameworkElement.ToolTipProperty, "为模块内所有按键的松开发送填入 [key,Name,up] 文本值");

            // 默认选中接收区图标（InitializeComponent 之后，避免 XAML 阶段触发事件）
            tabReceive.IsChecked = true;

            // Phase 3: PlotViewModel 必须在 LoadWindowSettings 之前初始化——后者可能触发 ApplyTheme
            _plotVM = new PlotViewModel();

            // 恢复上次窗口位置和大小（可能触发 ApplyTheme → UpdateThemeColors）
            LoadWindowSettings();

            // 恢复上次语言选择（默认 zh，由 Locale.Initialize 已设好）
            if (_prefsData != null && _prefsData.TryGetValue("language", out var langObj)
                && langObj is string savedLang && savedLang != "zh")
            {
                Locale.SwitchTo(savedLang);
            }

            // 加载图标栏面板显隐偏好
            if (_prefsData != null && _prefsData.TryGetValue("panelVisible", out var pvObj)
                && pvObj is Dictionary<string, object> pvDict)
            {
                foreach (var kv in pvDict)
                    if (kv.Value is bool b) _panelVisible[kv.Key] = b;
            }
            RefreshIconBarVisibility();

            // 关于页：版本号 + 运行时 + GitHub + 数据路径 + Issue 反馈
            SetVersionDisplay();
            bdAboutVersion.MouseLeftButtonDown += (s, e) =>
            {
                if (_updateUrl != null)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                        _updateUrl) { UseShellExecute = true });
            };
            tbAboutRuntime.Text = $"{RuntimeInformation.FrameworkDescription} · {RuntimeInformation.ProcessArchitecture}";
            tbAboutGitHub.Text = "https://github.com/Encaron/SerialMonitor";
            tbAboutIssues.Text = "https://github.com/Encaron/SerialMonitor/issues";
            tbAboutDataPath.Text = "%LocalAppData%\\SerialMonitor\\\r\n"
                + "  prefs.json    偏好配置\r\n"
                + "  crash.log     崩溃日志\r\n"
                + "  quick_sends.cfg  快捷发送";

            // 素材自定义页：显示运行时素材路径
            var assetsDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Icons");
            tbAssetsPath.Text = assetsDir;
            tbAssetsPath.Cursor = System.Windows.Input.Cursors.Hand;
            tbAssetsPath.MouseLeftButtonDown += (s, e) => SafeSetClipboard(tbAssetsPath.Text);

            PopulateShortcutPage();
            PopulateExamplesPage();

            // "+" 按钮 hover / active 效果
            btnAddPanel.MouseEnter += (s, e) =>
            {
                if (_addPanelPopup == null || !_addPanelPopup.IsOpen)
                    btnAddPanel.Foreground = (Brush)FindResource("TextPrimaryBrush");
            };
            btnAddPanel.MouseLeave += (s, e) =>
            {
                if (_addPanelPopup == null || !_addPanelPopup.IsOpen)
                    btnAddPanel.Foreground = (Brush)FindResource("TextMutedBrush");
            };

            // 图标栏面板注册：一处注册 → 自动绑定动画 + 面板显隐
            RegisterTab("Receive",  tabReceive,  panelReceive,  rightReceive);
            RegisterTab("Plot",     tabPlot,     panelPlot,     rightPlot,     init: () => { EnsurePlotView(); if (!_isFreqDomain) RefreshTuningDrawer(); });
            RegisterTab("Keys",     tabKeys,     panelKeys,     rightKeys,     init: InitKeyPanel,
                exitEdit: () => { if (_keyVM != null && _keyVM.IsEditMode) { CancelKeysConfirm(); _keyVM.IsEditMode = false; _selectedKeys.Clear(); _selectedModuleGroupId = null; keysToolbarNormal.Visibility = Visibility.Visible; keysToolbarEdit.Visibility = Visibility.Collapsed; RefreshKeysUI(); RefreshKeysSidePanel(); } });
            RegisterTab("Sliders",  tabSliders,  panelSliders,  rightSliders,  init: () => InitSliderPanel(),
                exitEdit: () => { if (_sliderVM != null && _sliderVM.IsEditMode) { CancelSlidersConfirm(); _sliderVM.IsEditMode = false; _selectedSliders.Clear(); slidersToolbarNormal.Visibility = Visibility.Visible; slidersToolbarEdit.Visibility = Visibility.Collapsed; RefreshSlidersUI(); RefreshSlidersSidePanel(); } });
            RegisterTab("OLED",     tabOLED,     panelOLED,     rightOLED,     init: InitOLEDPanel,
                exitEdit: () => { if (_isEditingText) ExitTextEditMode(save: _editingTextOriginalKey != null); if (_selectedShape != null) DeselectShape(); ResetOLEDSidePanel(); });
            RegisterTab("Joystick", tabJoystick, panelJoystick, rightJoystick, init: InitJoystickPanel);
            RegisterTab("Sensors",  tabSensors,  panelSensors,  rightSensors,  init: InitSensorPanel,
                exitEdit: () => {
                    if (_sensorVM == null) return;
                    if (_sensorVM.IsEditMode) {
                        _sensorVM.IsEditMode = false; _selectedCard = null; _detailCard = null;
                        btnSensorEdit.LocText("编辑");
                        if (_sensorRefreshTimer != null && !_sensorRefreshTimer.IsEnabled && _sensorVM.IsActive) _sensorRefreshTimer.Start();
                        RefreshAllRows();
                    }
                    // 正常模式卡片详情也退出，回到概览
                    if (_normalDetailCard != null) {
                        _normalDetailCard = null;
                    }
                    rightSensors.Visibility = Visibility.Collapsed;
                    RefreshSensorSidePanel();
                });
            RegisterTab("Settings", tabSettings, panelSettings, rightSettings);

            // 创建串口会话
            _session = new SerialPortSession(Dispatcher);
            _session.LineReceived += OnLineReceived;
            _session.ConnectionChanged += OnConnectionChanged;

            // Phase 3: 设置 CheckBox 初始选中状态（不能放 XAML IsChecked="True"，会导致 BAML 崩溃）
            chkPlotYAuto.IsChecked = true;
            chkPlotLines.IsChecked = true;
            chkPlotValueHud.IsChecked = true;
            // Y 轴自动模式下禁用 min/max 输入框
            tbPlotYMin.IsEnabled = false;
            tbPlotYMax.IsEnabled = false;
            // 显示模式下拉框
            foreach (var k in LogicValueMaps.PlotModeKeys) cbPlotMode.Items.Add(LogicValueMaps.DisplayPlotMode(k));
            cbPlotMode.SelectedIndex = 0;
            // #10 FFT: 窗函数
            foreach (var k in LogicValueMaps.WindowFunctionKeys) cbFreqWindow.Items.Add(LogicValueMaps.DisplayWindowFunction(k));
            cbFreqWindow.SelectedIndex = 0;
            // #10 FFT: FFT 点数选择
            cbFreqSize.Items.Add("64");
            cbFreqSize.Items.Add("128");
            cbFreqSize.Items.Add("256");
            cbFreqSize.Items.Add("512");
            cbFreqSize.SelectedIndex = 1; // 默认 128
            cbFreqSource.DropDownOpened += (s, e) => RefreshFreqSourceList();
            chkFreqLines.IsChecked = true;

            // 动画画刷：单独创建非冻结实例，后续通过 ColorAnimation 驱动
            statusDotBrush = new SolidColorBrush(StatusDotIdle);
            statusDot.Fill = statusDotBrush;

            // 检测更纱黑体是否安装（未安装 → 状态栏琥珀色提示）
            bool hasSarasa = Fonts.SystemFontFamilies.Any(f =>
                f.Source.IndexOf("Sarasa Mono SC", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!hasSarasa)
            {
                fontMissing = true;
                tbPortInfo.LocText("💡 更纱黑体未安装 → 使用备用等宽字体");
                tbPortInfo.Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0x80, 0x00));
                tbPortInfo.Visibility = Visibility.Visible;
            }

            // 初始化 AvalonEdit（着色器 + 行号样式）
            InitEditor();

            // 自动重连定时器：设备重新插入后延迟检测并重连
            reconnectTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            reconnectTimer.Tick += (s, args) =>
            {
                string[] ports = SerialPort.GetPortNames();
                if (ports.Contains(lastPortName))
                {
                    reconnectTimer.Stop();
                    _reconnectAttempts = 0;
                    cbPortName.Text = lastPortName;
                    OpenSerialPort();
                    LogSystem(string.Format(T("---- 自动重连：已重新连接 {0} ----"), lastPortName));
                }
                else
                {
                    _reconnectAttempts++;
                    if (_reconnectAttempts >= MaxReconnectAttempts)
                    {
                        reconnectTimer.Stop();
                        _reconnectAttempts = 0;
                        LogSystem(string.Format(T("---- 自动重连超时：未检测到 {0} ----"), lastPortName));
                    }
                }
            };

            // 定时发送计时器
            autoSendTimer = new DispatcherTimer();
            autoSendTimer.Tick += (s, args) => { if (_session.IsOpen) SendData(); };

            InitComboBoxItems();
            SetDefaultValues();

            // 扫描可用串口并自动选中（在 SetDefaultValues 之后、LoadPreferences 之前）
            ScanPortsAndAutoSelect();

            // 加载用户偏好（需在 InitComboBoxItems + SetDefaultValues 之后）
            LoadPreferences();

            // 应用系统消息独立显示的初始可见性
            lbSystemLog.Visibility = chkSeparateSystemLog.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;

            // 同步行号面板初始可见性（LoadPreferences 可能改了 chkShowLineNumbers）
            _showLineNumbers = chkShowLineNumbers.IsChecked == true;
            editor.ShowLineNumbers = _showLineNumbers;

            // 加载快捷发送按钮
            LoadQuickSends();
            RefreshQuickSendButtons();

            // 确保主题按钮背景在首次加载时正确（亮色模式下 ApplyTheme 尚未被调用）
            btnThemeSwitch.LocText(isDarkTheme ? "☀ 亮色模式" : "🌙 暗色模式");
            btnThemeSwitch.Background = isDarkTheme
                ? new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42))
                : new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            tbThemeToggleLabel.LocText(isDarkTheme ? "亮" : "暗");
            imgThemeToggle.Source = new BitmapImage(new Uri(
                isDarkTheme ? "Icons/setting/theme/theme-sun.png" : "Icons/setting/theme/theme-moon.png",
                UriKind.Relative));

            // 初始化接收区筛选按钮状态
            UpdateFilterButtonAppearance();

            // 接收区"回到底部"按钮——监听滚动
            SetupScrollToBottomButton();

            // 启动后异步检查 GitHub 更新（不阻塞窗口加载）
            Loaded += async (_, _) => await CheckForUpdateAsync();
        }

        /// <summary>
        /// 从 Assembly 读取版本号，更新所有显示位置（不再手写版本号）。
        /// csproj &lt;Version&gt; 是唯一真相源。
        /// </summary>
        private void SetVersionDisplay()
        {
            var ver = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            string v = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v?.?.?";

            tbStatusVersion.Text = v;
            tbSettingsVersion.Text = v;

            if (_updateUrl != null)
            {
                tbAboutVersion.Text = $"{v}  →  v{_remoteVersion} 可用";
                tbAboutVersion.Cursor = Cursors.Hand;
                tbAboutVersion.ToolTip = T("点击查看更新内容");

                // 按钮样式：PrimaryBrush 描边 + 半透明底色
                var primaryBrush = (SolidColorBrush)FindResource("PrimaryBrush");
                bdAboutVersion.BorderBrush = primaryBrush;
                bdAboutVersion.BorderThickness = new Thickness(1);
                bdAboutVersion.Background = new SolidColorBrush(primaryBrush.Color) { Opacity = 0.12 };
                bdAboutVersion.Padding = new Thickness(12, 6, 12, 6);
                bdAboutVersion.Cursor = Cursors.Hand;
                bdAboutVersion.ToolTip = T("点击查看更新内容");
            }
            else
            {
                tbAboutVersion.Text = v;
                bdAboutVersion.BorderBrush = null;
                bdAboutVersion.BorderThickness = new Thickness(0);
                bdAboutVersion.Background = Brushes.Transparent;
                bdAboutVersion.Padding = new Thickness(0);
                bdAboutVersion.Cursor = Cursors.Arrow;
                bdAboutVersion.ToolTip = null;
            }
        }

        /// <summary>
        /// 启动时检查 GitHub Release 是否有新版本。异步、不阻塞、失败静默。
        /// </summary>
        private async System.Threading.Tasks.Task CheckForUpdateAsync()
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.UserAgent.TryParseAdd("SerialMonitor");
                client.Timeout = TimeSpan.FromSeconds(5);

                var response = await client.GetStringAsync(
                    "https://api.github.com/repos/Encaron/SerialMonitor/releases/latest");

                // 从 JSON 中提取 tag_name（不用 Newtonsoft，手写简易解析）
                var match = System.Text.RegularExpressions.Regex.Match(response, "\"tag_name\"\\s*:\\s*\"v?(\\d+\\.\\d+\\.\\d+)\"");
                if (!match.Success) return;

                var remote = new Version(match.Groups[1].Value);
                var local = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
                if (local == null || remote <= local) return;

                // 静默记录新版本信息，只在设置→关于页版本号旁提示
                _remoteVersion = remote.ToString();
                _updateUrl = "https://github.com/Encaron/SerialMonitor/releases/latest";
                Dispatcher.Invoke(() => SetVersionDisplay());
            }
            catch
            {
                // 网络不通/GitHub 挂了/限速 → 静默跳过
            }
        }

        // ==================================================================
        //  SerialPortSession 事件处理
        // ==================================================================

        /// <summary>
        /// Session 收到完整行文本 → 写入接收区（已由 Session 的 Dispatcher.Invoke 切到 UI 线程）
        /// </summary>
        private void OnLineReceived(string line)
        {
            // 解析一次，过滤和路由共用
            var pr = ProtocolParser.Parse(line);
            if (ShouldShowInReceiveArea(line, pr))
                LogReceived(line);
            UpdateTrafficDisplay();

            // Phase 3 & 4: 协议路由——[type,...] → 对应面板
            try
            {
                if (pr.Messages.Count > 0)
                {
                    foreach (var msg in pr.Messages)
                        RouteMessage(msg);
                }
            }
            catch (Exception ex)
            {
                LogSystem(string.Format(T("协议路由异常：{0}"), ex.Message));
            }
        }

        /// <summary>
        /// 协议消息路由 —— switch 派发到各 handler（活接头，未来可升级为注册表）
        /// </summary>
        private void RouteMessage(ProtocolMessage msg)
        {
            switch (msg.Type)
            {
                case "plot":
                    if (msg.Args.Count >= 2)
                        RoutePlotMessage(msg.Args);
                    break;
                case "key":
                    if (msg.Args.Count >= 2)
                        HandleKeyMessage(msg.Args[0], msg.Args[1]);
                    break;
                case "slider":
                    if (msg.Args.Count >= 2)
                    {
                        HandleSliderMessage(msg.Args[0], msg.Args[1]);
                        if (_currentTab == "Plot" && barTuningToggle.Visibility != Visibility.Visible)
                            RefreshTuningDrawer();
                    }
                    break;
                case "joystick":
                    if (msg.Args.Count >= 5
                        && int.TryParse(msg.Args[0], out int joyId)
                        && double.TryParse(msg.Args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double jx)
                        && double.TryParse(msg.Args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double jy))
                    {
                        HandleJoystickMessage(joyId, jx, jy);
                    }
                    break;
                case "fft":
                    if (msg.Args.Count >= 2)
                        ParseFftMessage(msg.Args);
                    break;
                case "display":
                    if (msg.Args.Count >= 4
                        && int.TryParse(msg.Args[0], out int dx)
                        && int.TryParse(msg.Args[1], out int dy)
                        && int.TryParse(msg.Args[3], out int dsize))
                    {
                        string color = msg.Args.Count >= 5 ? msg.Args[4] : null;
                        HandleDisplayMessage(dx, dy, msg.Args[2], dsize, color);
                    }
                    break;
                case "display-clear":
                    HandleDisplayClear();
                    break;
                case "draw":
                    if (msg.Args.Count >= 1)
                        HandleDrawMessage(msg.Args);
                    break;
                case "sensor":
                    if (msg.Args.Count >= 3 && _sensorVM != null)
                        RouteSensorMessage(msg.Args);
                    break;
                case "ctrl":
                    if (msg.Args.Count >= 3 && _sensorVM != null)
                        RouteCtrlMessage(msg.Args);
                    break;
                default:
                    LogSystem(string.Format(T("未知协议类型: [{0}]"), msg.Type));
                    break;
            }
        }

        /// <summary>
        /// [plot,name,value] 路由 —— inline 逻辑从 OnLineReceived 提取
        /// </summary>
        private void RoutePlotMessage(List<string> args)
        {
            if (double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
            {
                _plotVM.OnPlotMessage(args[0], val, DateTime.Now);
                if (plotEmptyHint.Visibility == Visibility.Visible)
                    plotEmptyHint.Visibility = Visibility.Collapsed;
                if (!_isFreqDomain) UpdatePlotHud();
                // #10 FFT：频域下自动重算频谱
                if (_isFreqDomain)
                    _plotVM.RecomputeFft();
                // 初次收到 plot 数据时检测调参按钮（已在 Plot 页无需切标签）
                if (_currentTab == "Plot" && barTuningToggle.Visibility != Visibility.Visible)
                    RefreshTuningDrawer();
            }
        }

        /// <summary>
        /// [sensor,子类型,卡片名,值,辅助] 路由 —— inline 逻辑从 OnLineReceived 提取
        /// </summary>
        private void RouteSensorMessage(List<string> args)
        {
            string subType = args[0];
            string name = args[1];
            string value = args.Count >= 3 ? args[2] : null;
            string aux = args.Count >= 4 ? args[3] : null;

            // 滑杆卡仅手动建——收到数据只更新不创建
            bool isNewCard = (_sensorVM.FindByName(name) == null);
            if (subType == "slider")
            {
                var existing = _sensorVM.FindByName(name);
                if (existing != null)
                {
                    existing.Update(value, aux);
                    if (_sensorVM.IsActive && !_sensorVM.IsEditMode) UpdateCardUI(existing);
                }
                // slider 不存在 → 忽略（等手动建卡）
                return;
            }

            var card = _sensorVM.OnSensorMessage(subType, name, value, aux);
            if (card != null)
            {
                if (_sensorVM.IsActive && !_sensorVM.IsEditMode)
                {
                    if (isNewCard)
                        RefreshAllRows();       // 首次建卡 → 重建
                    else
                        UpdateCardUI(card);     // 已存在 → 增量更新
                }
                SaveSensorPrefs();
            }
        }

        /// <summary>
        /// [ctrl,子类型,卡片名,动作] 路由 —— inline 逻辑从 OnLineReceived 提取
        /// </summary>
        private void RouteCtrlMessage(List<string> args)
        {
            string subType = args[0];
            string name = args[1];
            string action = args[2];

            // 滑杆卡：双向协议——PC 发 [ctrl,slider,...]，MCU 回 [sensor,slider,...]
            // 收到 [ctrl,slider,...] 是 PC→MCU 的发送路径，不建卡不更新
            if (subType == "slider")
            {
                // TODO Phase B: 滑杆卡拖拽时走这里发串口
                return;
            }

            // 开关卡：led/relay → control 类型
            bool isNewCard = (_sensorVM.FindByName(name) == null);
            var card = _sensorVM.OnCtrlMessage(subType, name, action);
            if (card != null)
            {
                if (_sensorVM.IsActive && !_sensorVM.IsEditMode)
                {
                    if (isNewCard)
                        RefreshAllRows();
                    else
                        UpdateCardUI(card);
                }
                SaveSensorPrefs();
            }
        }

        /// <summary>
        /// 接收区协议筛选——判断一行数据是否应该在接收区显示。
        /// </summary>
        private bool ShouldShowInReceiveArea(string line, ParseResult pr)
        {
            // 非协议行（不以 '[' 开头）→ 只看"普通文本"开关
            if (line.Length == 0 || line[0] != '[')
                return _showPlainText;

            if (pr.Messages.Count == 0)
                return _showPlainText; // 解析失败的 [ 开头行当普通文本

            if (!_showProtocolMsgs)
                return false;

            // 至少有一个协议类型的筛选是开启的
            foreach (var msg in pr.Messages)
            {
                if (_protocolTypeFilters.TryGetValue(msg.Type, out var on) && on)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Session 连接状态变化 → 更新 UI
        /// </summary>
        private void OnConnectionChanged(bool isOpen)
        {
            if (isOpen)
            {
                // 连接成功后的 UI 更新
                _lastSuccessfulPort = _session.PortName;

                btnOpen.LocText("关闭串口");
                btnOpen.Background = new SolidColorBrush(SuccessColor);
                btnOpen.BorderBrush = new SolidColorBrush(SuccessColor);
                PulseElement(btnOpen);

                btnSend.IsEnabled = true;
                cbPortName.IsEnabled = false;
                cbBaudRate.IsEnabled = false;
                cbDataBits.IsEnabled = false;
                cbStopBits.IsEnabled = false;
                cbParity.IsEnabled = false;
                cbFlowControl.IsEnabled = false;

                // 若勾选定时发送，启动定时器
                if (chkAutoRepeat.IsChecked == true &&
                    int.TryParse(tbRepeatInterval.Text, out int repeatMs) && repeatMs > 0)
                {
                    autoSendTimer.Interval = TimeSpan.FromMilliseconds(repeatMs);
                    autoSendTimer.Start();
                }

                // 状态栏
                AnimateBrushColor(statusDotBrush, SuccessColor);
                tbStatusText.LocText("已连接");
                tbPortInfo.Text = $"{_session.PortName} @ {cbBaudRate.Text}";
                tbPortInfo.Foreground = new SolidColorBrush(LogSystemColor);
                tbPortInfo.Visibility = Visibility.Visible;

                // 重置流量计数（除非用户选择持久化）
                if (chkPersistTraffic.IsChecked != true)
                {
                    _session.ResetTraffic();
                }
                UpdateTrafficDisplay();

                // 日志
                LogSystem(string.Format(T("---- 已打开串行端口 {0} ----"), _session.PortName));
                // 清除冻结水印
                plotFrozenOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                // 断开连接后的 UI 更新
                autoSendTimer.Stop();

                UpdateTrafficDisplay();

                btnOpen.LocText("打开串口");
                btnOpen.Background = new SolidColorBrush(PrimaryColor);
                btnOpen.BorderBrush = new SolidColorBrush(PrimaryColor);
                PulseElement(btnOpen);

                btnSend.IsEnabled = false;
                cbPortName.IsEnabled = true;
                cbBaudRate.IsEnabled = true;
                cbDataBits.IsEnabled = true;
                cbStopBits.IsEnabled = true;
                cbParity.IsEnabled = true;
                cbFlowControl.IsEnabled = true;

                AnimateBrushColor(statusDotBrush, StatusDotIdle);
                tbStatusText.LocText("就绪");
                // 波形冻结水印（串口已关，数据自然停止）
                plotFrozenOverlay.Visibility = Visibility.Visible;
                if (fontMissing)
                {
                    tbPortInfo.LocText("💡 更纱黑体未安装 → 使用备用等宽字体");
                    tbPortInfo.Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0x80, 0x00));
                    tbPortInfo.Visibility = Visibility.Visible;
                }
                else
                {
                    tbPortInfo.Visibility = Visibility.Collapsed;
                }
            }
        }

        // ==================================================================
        //  窗口位置记忆
        // ==================================================================

        /// <summary>
        /// 全局快捷键
        /// </summary>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Escape → 取消 OLED 画布选中图形（#7 Phase 3）
            if (e.Key == Key.Escape && _selectedShape != null)
            {
                DeselectShape();
                e.Handled = true;
                return;
            }

            // Ctrl+Enter → 打开/关闭串口
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                btnOpen_Click(sender, e);
                e.Handled = true;
                return;
            }

            // Ctrl+L → 清空接收区
            if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
            {
                btnClearReceive_Click(sender, e);
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+L → 清空发送区
            if (e.Key == Key.L && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                btnClearSend_Click(sender, e);
                e.Handled = true;
                return;
            }

            // Ctrl+P → 暂停/继续显示
            if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
            {
                btnPauseDisplay_Click(sender, e);
                e.Handled = true;
                return;
            }

            // ESC → 关闭调参抽屉（#8）
            if (e.Key == Key.Escape && _tuningDrawerOpen)
            {
                ToggleTuningDrawer();
                e.Handled = true;
                return;
            }
        }

        // ==================================================================
        //  窗口初始化
        // ==================================================================

        private void InitComboBoxItems()
        {
            cbBaudRate.Items.Add("2400");
            cbBaudRate.Items.Add("4800");
            cbBaudRate.Items.Add("9600");
            cbBaudRate.Items.Add("14400");
            cbBaudRate.Items.Add("19200");
            cbBaudRate.Items.Add("38400");
            cbBaudRate.Items.Add("57600");
            cbBaudRate.Items.Add("76800");
            cbBaudRate.Items.Add("115200");
            cbBaudRate.Items.Add("128000");
            cbBaudRate.Items.Add("230400");
            cbBaudRate.Items.Add("256000");
            cbBaudRate.Items.Add("460800");
            cbBaudRate.Items.Add("500000");
            cbBaudRate.Items.Add("921600");
            cbBaudRate.Items.Add("1000000");
            cbBaudRate.Items.Add("2000000");

            cbDataBits.Items.Add("5");
            cbDataBits.Items.Add("6");
            cbDataBits.Items.Add("7");
            cbDataBits.Items.Add("8");

            cbStopBits.Items.Add("1");
            cbStopBits.Items.Add("1.5");
            cbStopBits.Items.Add("2");

            foreach (var k in LogicValueMaps.ParityKeys) cbParity.Items.Add(LogicValueMaps.DisplayParity(k));
            foreach (var k in LogicValueMaps.IoModeKeys) cbReceiveMode.Items.Add(LogicValueMaps.DisplayIoMode(k));

            cbReceiveCoding.Items.Add("GBK");
            cbReceiveCoding.Items.Add("UTF-8");

            foreach (var k in LogicValueMaps.IoModeKeys) cbSendMode.Items.Add(LogicValueMaps.DisplayIoMode(k));

            cbSendCoding.Items.Add("GBK");
            cbSendCoding.Items.Add("UTF-8");

            foreach (var k in LogicValueMaps.TimestampFormatKeys) cbTimestampFormat.Items.Add(LogicValueMaps.DisplayTimestampFormat(k));
            foreach (var k in LogicValueMaps.NewlineKeys) cbLineEnding.Items.Add(LogicValueMaps.DisplayNewline(k));
            foreach (var k in LogicValueMaps.FlowControlKeys) cbFlowControl.Items.Add(LogicValueMaps.DisplayFlowControl(k));
        }

        private void SetDefaultValues()
        {
            cbBaudRate.SelectedIndex = 8;       // 115200
            cbDataBits.SelectedIndex = 3;       // 8
            cbStopBits.SelectedIndex = 0;       // 1
            cbParity.SelectedIndex = 0;         // 无

            cbReceiveMode.SelectedIndex = 1;    // 文本模式
            cbReceiveCoding.SelectedIndex = 1;  // UTF-8
            cbSendMode.SelectedIndex = 1;       // 文本模式
            cbSendCoding.SelectedIndex = 1;     // UTF-8

            cbTimestampFormat.SelectedIndex = 2; // HH:mm:ss:fff
            cbLineEnding.SelectedIndex = 0;      // \r\n
            cbFlowControl.SelectedIndex = 0;     // 无

            btnSend.IsEnabled = false;
            cbPortName.IsEnabled = true;
            cbBaudRate.IsEnabled = true;
            cbDataBits.IsEnabled = true;
            cbStopBits.IsEnabled = true;
            cbParity.IsEnabled = true;
        }

        /// <summary>
        /// 启动时扫描可用串口并自动选中上次使用的端口。
        /// 优先级：上次记录的端口 > 仅有一个串口时自动选 > 多串口时选数字最小的 > 空白
        /// </summary>
        private void ScanPortsAndAutoSelect()
        {
            string[] ports = SerialPort.GetPortNames().Distinct().ToArray();
            if (ports.Length == 0) return;

            // 填充串口号列表
            cbPortName.Items.Clear();
            foreach (string p in ports)
                cbPortName.Items.Add(p);

            string selected = "";

            // 1) 上次成功打开的端口仍存在 → 优先
            if (!string.IsNullOrEmpty(_lastSuccessfulPort) &&
                Array.IndexOf(ports, _lastSuccessfulPort) >= 0)
            {
                selected = _lastSuccessfulPort;
            }
            // 2) 只有一个串口 → 直接选
            else if (ports.Length == 1)
            {
                selected = ports[0];
            }
            // 3) 多个串口 → 选数字最小的
            else
            {
                selected = ports[0];
                foreach (string p in ports)
                {
                    if (string.Compare(p, selected, StringComparison.OrdinalIgnoreCase) < 0)
                        selected = p;
                }
            }

            if (!string.IsNullOrEmpty(selected))
            {
                cbPortName.Text = selected;
                LogSystem(string.Format(T("---- 已自动选中 {0} ----"), selected));
            }
        }

        // ————————————————————————————————————————
        //  流量统计
        // ————————————————————————————————————————

        /// <summary>
        /// 字节数 → 可读格式：B / KB / MB
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            else
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        /// <summary>
        /// 更新状态栏流量显示，串口关闭时显示 --
        /// </summary>
        private void UpdateTrafficDisplay()
        {
            if (_session.IsOpen)
            {
                tbTraffic.Text = $"TX: {FormatBytes(_session.TxBytes)} ↑  RX: {FormatBytes(_session.RxBytes)} ↓";
                tbTraffic.Foreground = FindResource("TextSecondaryBrush") as SolidColorBrush;
            }
            else
            {
                tbTraffic.Text = "TX: -- ↑  RX: -- ↓";
                tbTraffic.Foreground = new SolidColorBrush(LogSystemColor);
            }
            tbTraffic.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 右键点击流量文字 → 重置计数
        /// </summary>
        private void tbTraffic_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _session.ResetTraffic();
            UpdateTrafficDisplay();
            LogSystem(T("---- 流量计数已重置 ----"));
        }

        // ————————————————————————————————————————
        //  发送历史 & 快捷发送（已移至 MainWindow.QuickSend.cs）
        // ————————————————————————————————————————


        // ==================================================================
        //  USB 热插拔检测（WM_DEVICECHANGE）
        // ==================================================================

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(WndProc);
            // 启动时即时设置标题栏颜色（ApplyTheme 时 HWND 可能尚未就绪）
            ApplyTitleBarTheme(isDarkTheme);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0219) // WM_DEVICECHANGE
            {
                if (wParam.ToInt32() == 0x8004) // DBT_DEVICEREMOVECOMPLETE
                {
                    if (_session.IsOpen && !_session.IsPortOpen)
                    {
                        lastPortName = cbPortName.Text;
                        CloseSerialPort();
                    }
                }
                else if (wParam.ToInt32() == 0x8000) // DBT_DEVICEARRIVAL
                {
                    if (chkAutoReconnect.IsChecked == true && !_session.IsOpen && !string.IsNullOrEmpty(lastPortName))
                    {
                        reconnectTimer.Start();
                    }
                }
            }
            return IntPtr.Zero;
        }

        // ==================================================================
        //  串口打开 / 关闭
        // ==================================================================

        private void OpenSerialPort()
        {
            try
            {
                string portName = cbPortName.Text;

                // 校验自定义波特率
                if (!int.TryParse(cbBaudRate.Text, out int baudRate) || baudRate <= 0)
                {
                    MessageBox.Show($"波特率「{cbBaudRate.Text}」无效，请输入正整数（如 9600、115200、921600）。",
                                    "串口打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int dataBits = Convert.ToInt32(cbDataBits.Text);

                StopBits[] sb = { StopBits.One, StopBits.OnePointFive, StopBits.Two };
                StopBits stopBits = sb[cbStopBits.SelectedIndex];

                Parity[] pt = { Parity.None, Parity.Odd, Parity.Even };
                Parity parity = pt[cbParity.SelectedIndex];

                Handshake[] hs = { Handshake.None, Handshake.RequestToSend, Handshake.XOnXOff };
                Handshake handshake = hs[cbFlowControl.SelectedIndex];

                bool dtr = chkDtr.IsChecked == true;
                bool rts = chkRts.IsChecked == true;

                _session.Open(portName, baudRate, dataBits, stopBits, parity, handshake, dtr, rts,
                              receiveMode, receiveCoding);
                // 成功后 ConnectionChanged 事件会触发 OnConnectionChanged 更新 UI
            }
            catch (UnauthorizedAccessException)
            {
                LogSystem(T("串口打开失败：被其他程序占用或没有访问权限"));
                MessageBox.Show("串口被其他程序占用，或没有访问权限。\n请关闭其他串口工具后重试。",
                                "串口打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (System.IO.IOException ex)
            {
                LogSystem(string.Format(T("串口打开失败：硬件通信错误 — {0}"), ex.Message));
                MessageBox.Show($"串口硬件通信错误：\n{ex.Message}",
                                "串口打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (ArgumentException ex)
            {
                LogSystem(string.Format(T("串口打开失败：参数无效 — {0}"), ex.Message));
                MessageBox.Show($"串口参数无效（端口名或波特率）：\n{ex.Message}",
                                "串口打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (InvalidOperationException ex)
            {
                LogSystem(string.Format(T("串口打开失败：已被占用 — {0}"), ex.Message));
                MessageBox.Show($"串口已被占用：\n{ex.Message}",
                                "串口打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"串口打开失败：\n{ex.Message}",
                                "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CloseSerialPort()
        {
            if (_session.IsOpen)
            {
                LogSystem(string.Format(T("---- 关闭串行端口 {0} ----"), _session.PortName));
            }

            _session.Close();
            // ConnectionChanged 事件会触发 OnConnectionChanged 更新 UI
        }

        // ==================================================================
        //  控件事件处理
        // ==================================================================

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            if (_session.IsOpen)
            {
                CloseSerialPort();
            }
            else
            {
                OpenSerialPort();
            }
        }

        private void cbPortName_DropDownOpened(object sender, EventArgs e)
        {
            string currentName = cbPortName.Text;
            string[] names = SerialPort.GetPortNames().Distinct().ToArray();
            cbPortName.Items.Clear();
            foreach (string name in names)
                cbPortName.Items.Add(name);
            cbPortName.Text = currentName;
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            SendData();
        }

        /// <summary>
        /// Enter = 发送    Shift+Enter = 换行
        /// </summary>
        private void tbSend_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    // Shift+Enter → 换行，TextBox 自然处理
                }
                else
                {
                    // Enter → 发送
                    SendData();
                    e.Handled = true; // 阻止 TextBox 插入换行
                }
            }
        }

        /// <summary>
        /// HEX 模式输入自动格式化：过滤非法字符、每两个字符插空格、转大写
        /// </summary>
        private void AutoFormatHexInput()
        {
            if (_isHexFormatting) return;

            string text = tbSend.Text;
            if (string.IsNullOrEmpty(text)) return;

            int oldCursor = tbSend.CaretIndex;

            // 统计原文本中光标之前的有效 hex 字符数
            int hexBefore = 0;
            for (int i = 0; i < oldCursor && i < text.Length; i++)
                if (!char.IsWhiteSpace(text[i])) hexBefore++;

            // 过滤：只保留 [0-9A-Fa-f]
            string clean = Regex.Replace(text, "[^A-Fa-f0-9]", "");
            if (clean.Length == 0) return;

            // 大写 + 每两个字符插空格
            var sb = new StringBuilder();
            for (int i = 0; i < clean.Length; i++)
            {
                if (i > 0 && i % 2 == 0)
                    sb.Append(' ');
                sb.Append(char.ToUpperInvariant(clean[i]));
            }
            string formatted = sb.ToString();

            if (text == formatted) return; // 无需修改

            _isHexFormatting = true;
            tbSend.Text = formatted;

            // 映射光标：在格式化字符串中找到第 hexBefore 个 hex 字符的位置
            int hexCount = 0;
            int newCursor = formatted.Length;
            for (int i = 0; i < formatted.Length; i++)
            {
                if (hexCount == hexBefore) { newCursor = i; break; }
                if (!char.IsWhiteSpace(formatted[i])) hexCount++;
            }
            tbSend.CaretIndex = newCursor;
            _isHexFormatting = false;
        }

        /// <summary>
        /// 暂停/继续接收区显示。串口后台照常接收，界面冻结便于分析。
        /// 恢复时补回暂停期间积压的全部数据。
        /// </summary>
        private void btnPauseDisplay_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = !_isPaused;

            if (_isPaused)
            {
                btnPauseDisplay.LocText("▶ 继续显示");
                btnPauseDisplay.Style = FindResource("PrimaryButtonStyle") as Style;
                menuPauseDisplay.LocText("▶ 继续显示");
                LogSystem(T("---- 暂停显示：界面已冻结，后台照常接收 ----"));
            }
            else
            {
                int bufferedCount = _pausedLines.Count;

                // 一次性补回积压数据
                foreach (var line in _pausedLines)
                    AppendColoredLine(line.Text, line.Color, line.Role, skipPause: true);
                _pausedLines.Clear();

                if (bufferedCount > 0)
                    LogSystem(string.Format(T("---- 继续显示：补回暂停期间的 {0} 条数据 ----"), bufferedCount));
                else
                    LogSystem(T("---- 继续显示 ----"));

                btnPauseDisplay.LocText("暂停显示");
                btnPauseDisplay.Style = FindResource("SecondaryButtonStyle") as Style;
                menuPauseDisplay.LocText("⏸ 暂停显示");
            }
        }


        private void btnClearReceive_Click(object sender, RoutedEventArgs e)
        {
            ClearEditorContent();
        }

        // ——— 接收区协议筛选 ———

        private Popup _filterPopup;

        private void BtnFilterMenu_Click(object sender, RoutedEventArgs e)
        {
            // 已打开 → 关闭
            if (_filterPopup != null && _filterPopup.IsOpen)
            {
                _filterPopup.IsOpen = false;
                return;
            }

            var btn = sender as Button;
            if (btn == null) return;

            var popup = CreatePopup(btn);
            var border = new Border
            {
                Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 8, 12, 8),
                MinWidth = 190,
            };

            var stack = new StackPanel();

            // —— 协议主开关 ——
            var cbProtocol = new CheckBox
            {
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                Margin = new Thickness(0, 0, 0, 4),
            };
            cbProtocol.LocText("📡 协议消息");
            cbProtocol.IsChecked = _showProtocolMsgs;
            cbProtocol.Checked += (s2, args2) =>
            {
                _showProtocolMsgs = true;
                UpdateFilterButtonAppearance();
                RebuildProtocolSubItems(stack);
            };
            cbProtocol.Unchecked += (s2, args2) =>
            {
                _showProtocolMsgs = false;
                UpdateFilterButtonAppearance();
                RebuildProtocolSubItems(stack);
            };
            stack.Children.Add(cbProtocol);

            // —— 子类型占位（初始填充） ——
            var subPanel = new StackPanel { Margin = new Thickness(18, 0, 0, 4) };
            stack.Children.Add(subPanel);

            // —— 分隔线 ——
            stack.Children.Add(new Border
            {
                Height = 1,
                Background = (Brush)FindResource("SeparatorBrush"),
                Margin = new Thickness(0, 4, 0, 6),
            });

            // —— 普通文本开关 ——
            var cbPlain = new CheckBox
            {
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                Margin = new Thickness(0, 0, 0, 2),
            };
            cbPlain.LocText("普通文本");
            cbPlain.IsChecked = _showPlainText;
            cbPlain.Checked += (s2, args2) => { _showPlainText = true; UpdateFilterButtonAppearance(); };
            cbPlain.Unchecked += (s2, args2) => { _showPlainText = false; UpdateFilterButtonAppearance(); };
            stack.Children.Add(cbPlain);

            border.Child = stack;
            popup.Child = border;

            // 初始填充子类型
            RebuildProtocolSubItems(stack);

            // 点击 Popup 外部 → 关闭
            // 窗口移动+失焦 → CreatePopup 已内置 LocationChanged
            MouseButtonEventHandler closeHandler = null;
            closeHandler = (s2, args2) =>
            {
                if (!popup.IsOpen) return;
                // 检查点击是否在 Popup 内部
                var clicked = args2.OriginalSource as DependencyObject;
                if (clicked != null && popup.Child is Border b && b.IsAncestorOf(clicked))
                    return;
                // 点击了打开 Popup 的按钮自身 → 由 BtnFilterMenu_Click 处理 toggle
                if (args2.OriginalSource == btn || (btn.IsAncestorOf(args2.OriginalSource as DependencyObject)))
                    return;

                popup.IsOpen = false;
            };
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                PreviewMouseLeftButtonDown += closeHandler;
            }));

            popup.Closed += (s2, args2) =>
            {
                PreviewMouseLeftButtonDown -= closeHandler;
                _filterPopup = null;
                UpdateFilterButtonAppearance();
            };

            popup.IsOpen = true;
            _filterPopup = popup;
        }

        /// <summary>
        /// 重建协议子类型复选框——协议主开关切换时刷新灰态/可用。
        /// </summary>
        private void RebuildProtocolSubItems(StackPanel parentStack)
        {
            // 找到子类型占位 StackPanel（index 1）
            if (parentStack.Children.Count < 2) return;
            if (parentStack.Children[1] is not StackPanel subPanel) return;

            subPanel.Children.Clear();

            var labels = new Dictionary<string, string>
            {
                ["sensor"] = "传感", ["ctrl"] = "控制", ["plot"] = "波形",
                ["slider"] = "滑杆", ["key"] = "按键", ["joystick"] = "摇杆",
                ["display"] = "OLED", ["fft"] = "频谱", ["draw"] = "绘图",
            };

            foreach (var kv in labels)
            {
                var cb = new CheckBox
                {
                    Content = $"[{kv.Key}]  {T(kv.Value)}",
                    IsChecked = _protocolTypeFilters[kv.Key],
                    IsEnabled = _showProtocolMsgs,
                    Opacity = _showProtocolMsgs ? 1.0 : 0.45,
                    Foreground = (Brush)FindResource("TextSecondaryBrush"),
                    FontSize = 12,
                    Margin = new Thickness(0, 1, 0, 1),
                    Tag = kv.Key,
                };
                cb.Checked += (s, args) =>
                {
                    if (cb.Tag is string t) _protocolTypeFilters[t] = true;
                };
                cb.Unchecked += (s, args) =>
                {
                    if (cb.Tag is string t) _protocolTypeFilters[t] = false;
                };
                subPanel.Children.Add(cb);
            }
        }

        /// <summary>
        /// 筛选按钮外观：主开关关了才灰（子类型不影响）。
        /// </summary>
        private void UpdateFilterButtonAppearance()
        {
            bool anyFiltered = !_showProtocolMsgs || !_showPlainText;
            btnFilterMenu.Opacity = anyFiltered ? 0.55 : 1.0;
        }

        private void btnClearSend_Click(object sender, RoutedEventArgs e)
        {
            tbSend.Clear();
            tbSendPlaceholder.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 发送区 placeholder：有内容时隐藏水印文字
        /// HEX 模式下自动格式化（每两个字符插空格）
        /// </summary>
        private void tbSend_TextChanged(object sender, TextChangedEventArgs e)
        {
            tbSendPlaceholder.Visibility = string.IsNullOrEmpty(tbSend.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (sendMode == "hex")
            {
                AutoFormatHexInput();
                UpdateHexWarning();
            }
            else
            {
                tbHexWarning.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// HEX 模式实时校验：发送按钮旁黄色警告非法字符
        /// </summary>
        private void UpdateHexWarning()
        {
            string invalid = DataConverter.ValidateHexString(tbSend.Text);
            if (!string.IsNullOrEmpty(invalid))
            {
                tbHexWarning.Text = $"⚠ 无效 HEX 字符: {invalid}";
                tbHexWarning.Visibility = Visibility.Visible;
            }
            else
            {
                tbHexWarning.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 导出接收区日志为 .txt 文件
        /// </summary>
        private void btnExportLog_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt|日志文件 (*.log)|*.log|所有文件 (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"串口日志_{DateTime.Now:yyyyMMdd_HHmmss}"
            };
            if (dialog.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(dialog.FileName, editor.Document.Text, System.Text.Encoding.UTF8);
                LogSystem(string.Format(T("---- 日志已导出至 {0} ----"), dialog.FileName));
            }
        }

        private void cbReceiveMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_rebuildingComboBox || cbReceiveMode.SelectedItem == null || cbReceiveMode.SelectedIndex < 0) return;
            string mode = LogicValueMaps.IoModeKeys[cbReceiveMode.SelectedIndex];
            cbReceiveCoding.IsEnabled = mode == "text";
            receiveMode = mode;
            _session.UpdateReceiveSettings(receiveMode, receiveCoding);
        }

        private void cbReceiveCoding_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbReceiveCoding.SelectedItem == null) return;

            string coding = cbReceiveCoding.SelectedItem.ToString();
            if (coding == "GBK")
            {
                receiveCoding = "GBK";
            }
            else if (coding == "UTF-8")
            {
                receiveCoding = "UTF-8";
            }

            _session.UpdateReceiveSettings(receiveMode, receiveCoding);
        }

        private void cbSendMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_rebuildingComboBox || cbSendMode.SelectedItem == null || cbSendMode.SelectedIndex < 0) return;
            string mode = LogicValueMaps.IoModeKeys[cbSendMode.SelectedIndex];
            sendMode = mode;
            if (mode == "hex")
            {
                cbSendCoding.IsEnabled = false;
                if (IsLoaded) { AutoFormatHexInput(); UpdateHexWarning(); }
            }
            else
            {
                cbSendCoding.IsEnabled = true;
                tbHexWarning.Visibility = Visibility.Collapsed;
            }
        }

        private void cbSendCoding_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbSendCoding.SelectedItem == null) return;

            string coding = cbSendCoding.SelectedItem.ToString();
            if (coding == "GBK")
            {
                sendCoding = "GBK";
            }
            else if (coding == "UTF-8")
            {
                sendCoding = "UTF-8";
            }
        }

        /// <summary>
        /// 时间戳格式 — 切换时写一条标记
        /// </summary>
        private void cbTimestampFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _rebuildingComboBox) return;
            if (cbTimestampFormat.SelectedItem == null || cbTimestampFormat.SelectedIndex < 0) return;
            string key = LogicValueMaps.TimestampFormatKeys[cbTimestampFormat.SelectedIndex];
            if (key == "none")
                LogSystem(T("---- 时间戳：关 ----"));
            else
                LogSystem(string.Format(T("---- 时间戳：{0} ----"), cbTimestampFormat.SelectedItem.ToString()));
        }

        /// <summary>
        /// 定时发送 — 切换时启动/停止定时器
        /// </summary>
        private void chkAutoRepeat_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            if (chkAutoRepeat.IsChecked == true)
            {
                if (int.TryParse(tbRepeatInterval.Text, out int ms) && ms > 0)
                {
                    autoSendTimer.Interval = TimeSpan.FromMilliseconds(ms);
                    autoSendTimer.Start();
                    LogSystem(string.Format(T("---- 定时发送：开（每 {0} ms）----"), ms));
                }
                else
                {
                    chkAutoRepeat.IsChecked = false;
                    MessageBox.Show("请输入有效的间隔时间（正整数，单位毫秒）", "提示",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                autoSendTimer.Stop();
                LogSystem(T("---- 定时发送：关 ----"));
            }
        }

        /// <summary>
        /// 定时发送间隔更新
        /// </summary>
        private void tbRepeatInterval_LostFocus(object sender, RoutedEventArgs e)
        {
            if (chkAutoRepeat.IsChecked == true)
            {
                if (int.TryParse(tbRepeatInterval.Text, out int ms) && ms > 0)
                {
                    autoSendTimer.Stop();
                    autoSendTimer.Interval = TimeSpan.FromMilliseconds(ms);
                    autoSendTimer.Start();
                }
                else
                {
                    tbRepeatInterval.Text = "1000";
                }
            }
        }

        /// <summary>
        /// 消息回显 — 切换时写一条标记
        /// </summary>
        private void chkShowEcho_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            if (chkShowEcho.IsChecked == true)
            {
                LogSystem(T("---- 消息回显：开 ----"));
            }
            else
            {
                LogSystem(T("---- 消息回显：关 ----"));
            }
        }

        /// <summary>
        /// 行号显示 — 切换时写一条标记
        /// </summary>
        private void chkShowLineNumbers_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            _showLineNumbers = chkShowLineNumbers.IsChecked == true;
            editor.ShowLineNumbers = _showLineNumbers;

            if (_showLineNumbers)
            {
                LogSystem(T("---- 行号显示：开 ----"));
            }
            else
            {
                LogSystem(T("---- 行号显示：关 ----"));
            }
        }

        /// <summary>
        /// 系统消息独立显示 — 淡入淡出 + 写标记
        /// </summary>
        private void chkSeparateSystemLog_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            bool separate = chkSeparateSystemLog.IsChecked == true;

            if (separate)
            {
                // 显示：透明→不透明
                lbSystemLog.Visibility = Visibility.Visible;
                lbSystemLog.Opacity = 0;
                var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(
                    1.0, TimeSpan.FromMilliseconds(200))
                {
                    FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
                };
                lbSystemLog.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                LogSystem(T("---- 系统消息独立显示：开 ----"));
            }
            else
            {
                // 隐藏：不透明→透明，完成后收起
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(
                    0.0, TimeSpan.FromMilliseconds(200))
                {
                    FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
                };
                fadeOut.Completed += (s2, e2) =>
                {
                    lbSystemLog.Visibility = Visibility.Collapsed;
                    lbSystemLog.BeginAnimation(UIElement.OpacityProperty, null);
                };
                lbSystemLog.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                LogSystem(T("---- 系统消息独立显示：关 ----"));
            }
        }

        /// <summary>
        /// 自动重连 — 切换时写一条标记
        /// </summary>
        private void chkAutoReconnect_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            if (chkAutoReconnect.IsChecked == true)
                LogSystem(T("---- 自动重连：开 ----"));
            else
                LogSystem(T("---- 自动重连：关 ----"));
        }

        /// <summary>
        /// DTR — 数据终端就绪信号；连接中可即时切换
        /// </summary>
        private void chkDtr_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            bool enable = chkDtr.IsChecked == true;
            _session.SetDtr(enable);

            LogSystem(string.Format(T("---- DTR：{0} ----"), T(enable ? "开" : "关")));
        }

        /// <summary>
        /// RTS — 请求发送信号；连接中可即时切换
        /// </summary>
        private void chkRts_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            bool enable = chkRts.IsChecked == true;
            _session.SetRts(enable);

            LogSystem(string.Format(T("---- RTS：{0} ----"), T(enable ? "开" : "关")));
        }

        /// <summary>
        /// 流量持久化 — 切换时写一条标记
        /// </summary>
        private void chkPersistTraffic_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AnimateToggleSwitch(sender as CheckBox);

            if (chkPersistTraffic.IsChecked == true)
                LogSystem(T("---- 流量计数持久化：开 ----"));
            else
                LogSystem(T("---- 流量计数持久化：关 ----"));
        }

        // ==================================================================
        //  串口数据收发
        // ==================================================================

        private void SendData()
        {
            if (!_session.IsOpen) return;
            if (string.IsNullOrEmpty(tbSend.Text)) return;

            string content = tbSend.Text;
            SendRaw(content, appendLineEnding: true);

            // 发送后自动清空
            if (chkAutoClear.IsChecked == true)
                tbSend.Clear();
        }

        /// <summary>
        /// 发送原始内容（不依赖 TextBox，供快捷发送按钮复用）
        /// appendLineEnding：是否追加拿前选中的换行符（仅主发送区使用）
        /// </summary>
        private void SendRaw(string content, bool appendLineEnding = false)
        {
            if (!_session.IsOpen) return;
            if (string.IsNullOrEmpty(content)) return;

            // 追加拿前选中的换行符（主发送区专用，快捷发送按钮已有内置换行）
            if (appendLineEnding)
            {
                string leText = GetLineEndingText();
                string leHex  = GetLineEndingHex();
                if (!string.IsNullOrEmpty(leText))
                {
                    if (sendMode == "text")
                        content += leText;
                    else if (sendMode == "hex" && !string.IsNullOrEmpty(leHex))
                        content += " " + leHex;
                }
            }

            RecordSendHistory(content);

            try
            {
                byte[] dataSend;
                if (sendMode == "hex")
                {
                    string invalidChars = DataConverter.ValidateHexString(content);
                    if (!string.IsNullOrEmpty(invalidChars))
                    {
                        LogSystem(string.Format(T("HEX 输入含无效字符，已忽略：{0}"), invalidChars));
                    }
                    dataSend = DataConverter.HexToBytes(content);
                }
                else
                {
                    dataSend = DataConverter.TextToBytes(content, sendCoding);
                }

                _session.SendBytes(dataSend);
                UpdateTrafficDisplay();
                LogSent(content);
            }
            catch (Exception ex)
            {
                LogSystem(string.Format(T("发送失败：{0}"), ex.Message));
            }
        }

        // ==================================================================
        //  换行符映射
        // ==================================================================

        /// <summary>
        /// 当前选中的换行符 → 文本形式（文本模式发送时追加到内容末尾）
        /// </summary>
        private string GetLineEndingText()
        {
            if (cbLineEnding.SelectedIndex < 0) return "";
            return LogicValueMaps.NewlineKeys[cbLineEnding.SelectedIndex] switch
            {
                "crlf" => "\r\n", "lf" => "\n", "cr" => "\r",
                _ => ""
            };
        }

        /// <summary>
        /// 当前选中的换行符 → HEX 形式（HEX 模式发送时追加拿字节）
        /// </summary>
        private string GetLineEndingHex()
        {
            if (cbLineEnding.SelectedIndex < 0) return "";
            return LogicValueMaps.NewlineKeys[cbLineEnding.SelectedIndex] switch
            {
                "crlf" => "0D 0A", "lf" => "0A", "cr" => "0D",
                _ => ""
            };
        }

        // ==================================================================
        //  Phase 2: 标签切换 & 侧面板
        // ==================================================================
        private void TabContent_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not string newTab) return;
            if (!_tabRegistry.TryGetValue(newTab, out var info)) return;

            // 退出当前面板的编辑模式
            if (_currentTab != null && _tabRegistry.TryGetValue(_currentTab, out var prev))
                prev.ExitEdit?.Invoke();

            _currentSettingsPage = null;
            _currentTab = newTab;
            _previousContentTab = newTab;
            info.Init?.Invoke();

            // Plot IsActive 管理
            if (_plotVM != null)
            {
                bool wasActive = _plotVM.IsActive;
                _plotVM.IsActive = (newTab == "Plot");
                if (!wasActive && _plotVM.IsActive) _plotVM.Flush();
            }
            // 传感面板 IsActive 管理
            if (_sensorVM != null)
            {
                _sensorVM.IsActive = (newTab == "Sensors");
                if (_sensorVM.IsActive)
                {
                    if (_sensorRefreshTimer != null && !_sensorRefreshTimer.IsEnabled && !_sensorVM.IsEditMode)
                        _sensorRefreshTimer.Start();
                    RefreshAllRows();
                }
                else
                {
                    if (_sensorRefreshTimer != null && _sensorRefreshTimer.IsEnabled)
                        _sensorRefreshTimer.Stop();
                    DeselectCard();
                }
            }
            RefreshContentVisibility();
        }
        private void TabSettings_Checked(object sender, RoutedEventArgs e)
        {
            // 切换到设置页时自动退出「正在离开」的面板的编辑模式（不保存，只有点"完成"才保存）
            if (_currentTab == "Sensors" && _sensorVM != null && _sensorVM.IsEditMode)
            {
                _sensorVM.IsEditMode = false;
                _selectedCard = null;
                _detailCard = null;
                _normalDetailCard = null;
                btnSensorEdit.LocText("编辑");
                rightSensors.Visibility = Visibility.Collapsed;
                if (_sensorRefreshTimer != null && !_sensorRefreshTimer.IsEnabled && _sensorVM.IsActive)
                    _sensorRefreshTimer.Start();
                RefreshAllRows();
                RefreshSensorSidePanel();
            }
            else if (_currentTab == "Keys" && _keyVM != null && _keyVM.IsEditMode)
            {
                CancelKeysConfirm();
                _keyVM.IsEditMode = false; _selectedKeys.Clear(); _selectedModuleGroupId = null;
                keysToolbarNormal.Visibility = Visibility.Visible; keysToolbarEdit.Visibility = Visibility.Collapsed;
                RefreshKeysUI(); RefreshKeysSidePanel();
            }
            else if (_currentTab == "Sliders" && _sliderVM != null && _sliderVM.IsEditMode)
            {
                CancelSlidersConfirm();
                _sliderVM.IsEditMode = false; _selectedSliders.Clear();
                slidersToolbarNormal.Visibility = Visibility.Visible; slidersToolbarEdit.Visibility = Visibility.Collapsed;
                RefreshSlidersUI(); RefreshSlidersSidePanel();
            }

            _currentTab = "Settings";
            _currentSettingsPage = null;
            RefreshContentVisibility();
        }

        // ═══ 设置子页面导航 ═══

        private void BtnSettingsSerial_Click(object sender, RoutedEventArgs e)
            => SwitchSettingsPage("serial");

        private void BtnSettingsShortcuts_Click(object sender, RoutedEventArgs e)
            => SwitchSettingsPage("shortcuts");

        private void BtnSettingsExamples_Click(object sender, RoutedEventArgs e)
            => SwitchSettingsPage("examples");

        private void BtnSettingsAbout_Click(object sender, RoutedEventArgs e)
            => SwitchSettingsPage("about");

        private void BtnSettingsAssets_Click(object sender, RoutedEventArgs e)
            => SwitchSettingsPage("assets");

        private void BtnSettingsBack_Click(object sender, RoutedEventArgs e)
            => SwitchSettingsPage(null);

        // ==================================================================
        //  接收区右键菜单
        // ==================================================================

        private void EditorContext_Copy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(editor.SelectedText))
                SafeSetClipboard(editor.SelectedText);
        }

        private void EditorContext_SelectAll_Click(object sender, RoutedEventArgs e)
        {
            editor.SelectAll();
        }

        private void EditorContext_Clear_Click(object sender, RoutedEventArgs e)
        {
            editor.Clear();
            // 同步 txCount（btnClearReceive_Click 的逻辑）
            _session.ResetTraffic();
            UpdateTrafficDisplay();
        }

        private void EditorContext_Pause_Click(object sender, RoutedEventArgs e)
        {
            btnPauseDisplay_Click(sender, e);
        }

        /// <summary>
        /// 快捷键提示页 — 与 Window_PreviewKeyDown / tbSend_PreviewKeyDown 对应
        /// </summary>
        private void PopulateShortcutPage()
        {
            shortcutListPanel.Children.Clear();

            var shortcuts = new (string Group, string[] Keys, string Desc)[]
            {
                ("全局", new[] {"Ctrl", "Enter"},      "打开 / 关闭串口"),
                ("全局", new[] {"Ctrl", "P"},           "暂停 / 继续显示"),
                ("全局", new[] {"Ctrl", "L"},           "清空接收区"),
                ("全局", new[] {"Ctrl", "Shift", "L"},  "清空发送区"),
            ("全局", new[] {"Ctrl", "F"},           "呼出接收区搜索栏"),
                ("发送区", new[] {"Enter"},             "发送"),
                ("发送区", new[] {"Shift", "Enter"},    "换行"),
            };

            // 键帽颜色随主题
            Color capBgColor, capFgColor;
            if (isDarkTheme)
            {
                capBgColor = Color.FromRgb(0x3C, 0x3C, 0x40);
                capFgColor = Color.FromRgb(0xD4, 0xD4, 0xD4);
            }
            else
            {
                capBgColor = Color.FromRgb(0xE8, 0xE8, 0xEC);
                capFgColor = Color.FromRgb(0x1E, 0x1E, 0x1E);
            }
            var keycapBg = new SolidColorBrush(capBgColor);
            var keycapFg = new SolidColorBrush(capFgColor);
            var plusBrush = (Brush)FindResource("TextMutedBrush");

            string lastGroup = null;

            foreach (var sc in shortcuts)
            {
                // 分组标题
                if (sc.Group != lastGroup)
                {
                    var groupHeader = new TextBlock
                    {
                        FontSize = 11,
                        FontWeight = System.Windows.FontWeights.SemiBold,
                        Foreground = (Brush)FindResource("TextMutedBrush"),
                        Margin = new Thickness(0, lastGroup == null ? 0 : 12, 0, 4),
                    };
                    groupHeader.LocText(sc.Group);
                    shortcutListPanel.Children.Add(groupHeader);
                    lastGroup = sc.Group;
                }

                // 行容器
                var row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 5),
                };

                // 键帽
                for (int i = 0; i < sc.Keys.Length; i++)
                {
                    if (i > 0)
                    {
                        row.Children.Add(new TextBlock
                        {
                            Text = " + ",
                            FontSize = 11,
                            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                            Foreground = plusBrush,
                            VerticalAlignment = VerticalAlignment.Center,
                        });
                    }

                    var cap = new Border
                    {
                        Background = keycapBg,
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 3),
                        Margin = new Thickness(0),
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    cap.Child = new TextBlock
                    {
                        Text = sc.Keys[i],
                        FontSize = 11,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        Foreground = keycapFg,
                    };
                    row.Children.Add(cap);
                }

                // 描述
                var desc = new TextBlock
                {
                    FontSize = 13,
                    Foreground = (Brush)FindResource("TextPrimaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(16, 0, 0, 0),
                };
                desc.LocText(sc.Desc);
                row.Children.Add(desc);

                shortcutListPanel.Children.Add(row);
            }
        }

        /// <summary>
        /// 使用示例页 — 8 组协议示例（plot / key / slider / joystick / sensor / ctrl / fft / draw），按类型折叠分组
        /// </summary>
        private void PopulateExamplesPage()
        {
            examplesListPanel.Children.Clear();

            var examples = new (string Title, string Type, string Desc, string Format, string Example, (string Name, string Desc)[] Params, string Code)[]
            {
                ("波形数据", "plot",
                 "发送数值到 PC，在 Plot 面板实时绘制波形曲线。建议发送频率 10~100 Hz。",
                 "[plot,通道名,数值]",
                 "[plot,ch1,25.3]",
                 new[] { ("通道名", "曲线的标识名称，如 \"ch1\"、\"温度\""),
                         ("数值", "浮点数，如 25.3、1024、-0.5") },
                 "// 每 50ms 发送一次\r\nprintf(\"[plot,ch1,%.1f]\\r\\n\", adc_val);"),

                ("按键事件", "key",
                 "设备端按键按下/松开时通知 PC，KeyPanel 对应按键高亮并可选回传命令。",
                 "[key,名称,状态]",
                 "[key,btn_a,down]",
                 new[] { ("名称", "按键标识，需匹配 KeyPanel 中已定义的按键名"),
                         ("状态", "\"down\"（按下）或 \"up\"（松开）") },
                 "// 按键按下\r\nprintf(\"[key,btn_a,down]\\r\\n\");\r\n// 按键松开\r\nprintf(\"[key,btn_a,up]\\r\\n\");"),

                ("滑杆数值", "slider",
                 "发送数值到 PC，Slider 面板对应滑块同步位置。常用于传感器反馈。",
                 "[slider,名称,数值]",
                 "[slider,speed,512]",
                 new[] { ("名称", "滑块标识，匹配 SliderPanel 中已定义的滑块名"),
                         ("数值", "浮点数，范围由面板设置决定，如 0~1023") },
                 "// 发送传感器读数\r\nprintf(\"[slider,speed,%.1f]\\r\\n\", sensor_val);"),

                ("摇杆位置", "joystick",
                 "发送摇杆坐标到 PC，Joystick 面板实时显示位置。常用于遥控器或姿态反馈。",
                 "[joystick,id,x,y]",
                 "[joystick,0,128,128]",
                 new[] { ("id", "摇杆编号（整数），如 0"),
                         ("x", "X 轴坐标，范围 0~255"),
                         ("y", "Y 轴坐标，范围 0~255") },
                 "// 发送摇杆位置\r\nprintf(\"[joystick,0,%d,%d]\\r\\n\", x_adc, y_adc);"),

                ("传感数据", "sensor",
                 "MCU 上报传感器数据，PC 端传感面板自动建卡。子类型决定卡片样式（竖条色/进度条/波形/开关）。",
                 "[sensor,子类型,卡片名,数值,辅助参数]",
                 "[sensor,temp,Chip Temp,42.5,45.0]",
                 new[] { ("子类型",
                          "8 种（与添加卡片面板一致）：\n" +
                          "  temp      温度卡 — 黄色竖条 + 迷你波形\n" +
                          "  humidity  湿度卡 — 蓝色竖条 + 进度条 + 迷你波形\n" +
                          "  pressure  气压卡 — 青色竖条 + 进度条 + 迷你波形\n" +
                          "  status    状态卡 — 绿/红色竖条，无波形，支持 alarm/error/offline\n" +
                          "  control   开关卡 — 橙色竖条 + 胶囊开关，点击回控 MCU\n" +
                          "  motor     电机卡 — 紫色竖条 + 迷你波形 + 转速单位\n" +
                          "  slider    滑杆卡 — 靛蓝竖条 + Slider + ±微调 + 发送间隔\n" +
                          "  generic   通用卡 — 灰色竖条，自定义名称/单位/颜色"),
                         ("卡片名", "显示名称，支持中文。跨所有组唯一。"),
                         ("数值", "浮点数 / on·off / online·alarm·error·offline"),
                         ("辅助参数", "可选。温度=最大值，湿度=露点，气压=趋势，状态=告警原因…") },
                 "// 温度（带最大值）\r\nprintf(\"[sensor,temp,芯片温度,%.1f,%.1f]\\r\\n\", val, max_val);\r\n// 湿度（自动进度条）\r\nprintf(\"[sensor,humidity,环境湿度,%.1f]\\r\\n\", humidity);\r\n// 状态（在线/告警/离线）\r\nprintf(\"[sensor,status,主板,online]\\r\\n\");\r\n// 开关（点击回控 MCU）\r\nprintf(\"[sensor,control,主板LED,off]\\r\\n\");\r\n// 通用（自定义单位/颜色）\r\nprintf(\"[sensor,generic,电池电压,%.2f]\\r\\n\", battery_v);"),

                ("控制指令", "ctrl",
                 "PC 端向 MCU 发送控制指令。开关卡点击自动发送，滑杆卡拖拽节流发送（间隔在卡片详情面板设置）。",
                 "[ctrl,子类型,卡片名,动作/数值]",
                 "[ctrl,led,Mainboard LED,on]",
                 new[] { ("子类型", "led / relay / slider（PC→MCU 方向。固件端自行解析，可扩展）"),
                         ("卡片名", "匹配传感面板中已存在的卡片名"),
                         ("动作", "开关类：on / off；滑杆类：浮点数值") },
                 "// MCU 端解析 ctrl 消息控制硬件\r\nif (strcmp(type, \"ctrl\") == 0) {\r\n    if (strcmp(subType, \"led\") == 0)\r\n        HAL_GPIO_WritePin(LED_PORT, LED_PIN,\r\n            strcmp(action, \"on\") == 0\r\n                ? GPIO_PIN_RESET : GPIO_PIN_SET);\r\n}"),

                ("频谱数据", "fft",
                 "MCU 发送 FFT 频谱数据，PC 端频域页显示柱状图。PC 端亦可从 [plot,...] 原始波形自动滑窗 FFT（MCU 零代码）。",
                 "[fft,通道名,点数,bin0,bin1,...]",
                 "[fft,ch1,512,0.12,0.35,0.67,0.89,0.45,...]",
                 new[] { ("通道名", "FFT 数据标识名，出现在频域页数据源下拉框（📶 前缀）。兼容旧格式省略通道名"),
                         ("点数", "FFT bin 数量（整数），如 64/128/256/512"),
                         ("bin值", "各频率 bin 归一化幅度（0~1），从低到高排列。bin 数需与点数一致") },
                 "// CMSIS-DSP FFT（MCU 端运算）\r\nprintf(\"[fft,ch1,%d\", N);\r\nfor (int i = 0; i < N; i++)\r\n    printf(\",%.2f\", mag[i]);\r\nprintf(\"]\\r\\n\");\r\n// 或交给 PC 端自动 FFT：发 [plot,...] 后选 📈 数据源即可"),

                ("OLED 绘图协议", "draw",
                 "通过串口发送绘图指令，PC 虚拟 OLED 面板实时渲染。支持 11 种图形 + 清屏 + F5 增量同步（[draw,set,id,...]/[draw,del,id]）。旧 [display,...] 由 [draw,text,...] 取代。\n\n协议统一在 [draw,<type>,<params>...] 命名空间下。末尾可追加 fill（填充，忽略线宽）或 a<角度>（旋转度数，仅 rect/ellipse）。",
                 "[draw,<type>,<params>...]",
                 "[draw,rect,10,10,50,30,#FF0000,2]",
                 new (string, string)[0],
                 "// ═══ MCU printf wrapper（复制到工程任意 .c 文件） ═══\r\n#include <stdio.h>\r\n\r\n// ── 文字 ──\r\nvoid Draw_Text(int x, int y, const char *t, int fs, const char *c)\r\n{ printf(\"[draw,text,%d,%d,%s,%d,%s]\\r\\n\", x, y, t, fs, c); }\r\n\r\n// ── 画点 ──\r\nvoid Draw_Point(int x, int y, const char *c)\r\n{ printf(\"[draw,point,%d,%d,%s]\\r\\n\", x, y, c); }\r\n\r\n// ── 画线 ──\r\nvoid Draw_Line(int x1, int y1, int x2, int y2, const char *c)\r\n{ printf(\"[draw,line,%d,%d,%d,%d,%s]\\r\\n\", x1, y1, x2, y2, c); }\r\nvoid Draw_LineW(int x1, int y1, int x2, int y2, const char *c, int w)\r\n{ printf(\"[draw,line,%d,%d,%d,%d,%s,%d]\\r\\n\", x1, y1, x2, y2, c, w); }\r\n\r\n// ── 矩形（空心/实心） ──\r\nvoid Draw_Rect(int x, int y, int w, int h, const char *c)\r\n{ printf(\"[draw,rect,%d,%d,%d,%d,%s]\\r\\n\", x, y, w, h, c); }\r\nvoid Draw_Fill(int x, int y, int w, int h, const char *c)\r\n{ printf(\"[draw,rect,%d,%d,%d,%d,%s,1,fill]\\r\\n\", x, y, w, h, c); }\r\n\r\n// ── 圆（空心/实心） ──\r\nvoid Draw_Circle(int cx, int cy, int r, const char *c)\r\n{ printf(\"[draw,circle,%d,%d,%d,%s]\\r\\n\", cx, cy, r, c); }\r\nvoid Draw_CircleFilled(int cx, int cy, int r, const char *c)\r\n{ printf(\"[draw,circle,%d,%d,%d,%s,1,fill]\\r\\n\", cx, cy, r, c); }\r\n\r\n// ── 椭圆 ──\r\nvoid Draw_Ellipse(int cx, int cy, int a, int b, const char *c)\r\n{ printf(\"[draw,ellipse,%d,%d,%d,%d,%s]\\r\\n\", cx, cy, a, b, c); }\r\n\r\n// ── 三角形（空心/实心） ──\r\nvoid Draw_Triangle(int x0, int y0, int x1, int y1, int x2, int y2, const char *c)\r\n{ printf(\"[draw,triangle,%d,%d,%d,%d,%d,%d,%s]\\r\\n\", x0, y0, x1, y1, x2, y2, c); }\r\nvoid Draw_TriangleFilled(int x0, int y0, int x1, int y1, int x2, int y2, const char *c)\r\n{ printf(\"[draw,triangle,%d,%d,%d,%d,%d,%d,%s,1,fill]\\r\\n\", x0, y0, x1, y1, x2, y2, c); }\r\n\r\n// ── 清屏 ──\r\nvoid Draw_Clear(void)\r\n{ printf(\"[draw,clear]\\r\\n\"); }\r\nvoid Draw_ClearColor(const char *c)\r\n{ printf(\"[draw,clear,%s]\\r\\n\", c); }"),
            };

            // 颜色随主题
            bool dark = isDarkTheme;
            var cardBg = (Brush)FindResource("SecondaryHoverBgBrush");
            var cardBorderBrush = (Brush)FindResource("CardBorderBrush");
            var textPrimary = (Brush)FindResource("TextPrimaryBrush");
            var textSecondary = (Brush)FindResource("TextSecondaryBrush");
            var textMuted = (Brush)FindResource("TextMutedBrush");
            var primary = (Brush)FindResource("PrimaryBrush");
            var codeBg = dark ? (Brush)new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E))
                              : (Brush)new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
            var codeFg = dark ? (Brush)new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4))
                              : (Brush)new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));

            var codeFont = new System.Windows.Media.FontFamily("Consolas");
            var yaheiFont = new System.Windows.Media.FontFamily("Microsoft YaHei");

            foreach (var ex in examples)
            {
                // ——— 折叠组容器 ———
                var groupBorder = new Border
                {
                    Background = cardBg,
                    BorderBrush = cardBorderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 0, 8),
                };
                var groupStack = new StackPanel();

                // ——— Header 行（始终可见，点按折叠/展开） ———
                var headerBorder = new Border
                {
                    Background = Brushes.Transparent,
                    Padding = new Thickness(10, 8, 12, 8),
                    Cursor = System.Windows.Input.Cursors.Hand,
                };
                // hover 效果
                headerBorder.MouseEnter += (s, e) =>
                {
                    if (s is Border bd) bd.Background = (Brush)FindResource("SecondaryHoverBgBrush");
                };
                headerBorder.MouseLeave += (s, e) =>
                {
                    if (s is Border bd) bd.Background = Brushes.Transparent;
                };

                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // 折叠箭头
                var arrowText = new TextBlock
                {
                    Text = "▶",
                    FontSize = 11, FontFamily = codeFont,
                    Foreground = textMuted, VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                Grid.SetColumn(arrowText, 0);
                headerGrid.Children.Add(arrowText);

                // 类型小标签
                var typeTag = new Border
                {
                    Background = new SolidColorBrush(dark ? Color.FromRgb(0x1E, 0x50, 0x7C) : Color.FromRgb(0xDE, 0xEC, 0xF9)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                typeTag.Child = new TextBlock
                {
                    Text = ex.Type,
                    FontSize = 11, FontFamily = codeFont,
                    Foreground = textPrimary,
                };
                Grid.SetColumn(typeTag, 1);
                headerGrid.Children.Add(typeTag);

                // 标题
                var titleTb = new TextBlock
                {
                    FontSize = 13, FontWeight = System.Windows.FontWeights.SemiBold,
                    Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                };
                titleTb.LocText(ex.Title);
                Grid.SetColumn(titleTb, 2);
                headerGrid.Children.Add(titleTb);

                // 复制代码按钮
                var btnCopyCode = new Button
                {
                    Style = (Style)FindResource("SecondaryButtonStyle"),
                    Height = 24, MinWidth = 0, Padding = new Thickness(8, 0, 8, 0),
                    FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
                };
                btnCopyCode.LocText("📋 复制代码");
                btnCopyCode.Tag = ex.Code;
                btnCopyCode.Click += (s, e) =>
                {
                    if (s is Button b && b.Tag is string code)
                    {
                        CopyWithFeedback(code, b);
                    }
                };
                Grid.SetColumn(btnCopyCode, 3);
                headerGrid.Children.Add(btnCopyCode);

                headerBorder.Child = headerGrid;
                groupStack.Children.Add(headerBorder);

                // ——— 内容区（根据记忆恢复展开/折叠） ———
                bool isExpanded = _expandedExampleTypes.Contains(ex.Type);
                var contentBorder = new Border
                {
                    Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed,
                    Padding = new Thickness(14, 0, 14, 14),
                };
                var card = new StackPanel();

                // 折叠箭头初始状态
                arrowText.Text = isExpanded ? "▼" : "▶";

                // 点击 header 切换展开/折叠
                headerBorder.MouseLeftButtonDown += (s, e) =>
                {
                    isExpanded = !isExpanded;
                    arrowText.Text = isExpanded ? "▼" : "▶";
                    contentBorder.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
                    if (isExpanded)
                        _expandedExampleTypes.Add(ex.Type);
                    else
                        _expandedExampleTypes.Remove(ex.Type);
                    e.Handled = true;
                };

                // ——— 描述 ———
                var descTb = new TextBlock
                {
                    FontSize = 12, FontFamily = yaheiFont,
                    Foreground = textSecondary, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 10),
                };
                descTb.LocText(ex.Desc);
                card.Children.Add(descTb);

                if (ex.Type == "draw")
                {
                    // ═══════════════════════════════════════════
                    // draw 协议专属渲染：子类型速查表 + 常用示例 + 可折叠代码
                    // ═══════════════════════════════════════════

                    // ── 子类型速查表 ──
                    var tableHeader = new TextBlock
                    {
                        FontSize = 11, FontWeight = System.Windows.FontWeights.SemiBold,
                        Foreground = textMuted, Margin = new Thickness(0, 0, 0, 6),
                    };
                    tableHeader.LocText("子类型速查");
                    card.Children.Add(tableHeader);

                    var basicShapes = new (string Type, string Params, string Note)[]
                    {
                        ("point",    "x, y, #color",                                         "画点"),
                        ("line",     "x1, y1, x2, y2, #color [,w]",                         "画线，w 默认 1"),
                        ("rect",     "x, y, w, h, #color [,w] [,fill] [,a°]",               "空心/实心矩形，支持旋转"),
                        ("circle",   "cx, cy, r, #color [,w] [,fill]",                      "空心/实心圆"),
                        ("triangle", "x0, y0, x1, y1, x2, y2, #color [,w] [,fill]",         "空心/实心三角形"),
                        ("text",     "x, y, content, fontSize, #color",                     "文字"),
                        ("clear",    "[#BG色]",                                              "清屏，默认底色 #111111"),
                    };

                    var advancedShapes = new (string Type, string Params, string Note)[]
                    {
                        ("ellipse",  "cx, cy, a, b, #color [,w] [,fill] [,a°]",             "空心/实心椭圆，支持旋转"),
                        ("arc",      "cx, cy, r, start, end, #color [,w] [,fill]",          "弧线/扇形，角度制·顺时针"),
                        ("rrect",    "x, y, w, h, #color, w, radius [,fill]",               "圆角矩形"),
                        ("set",      "id, <type>, <params>…",                               "增量同步：创建或更新"),
                        ("del",      "id",                                                   "增量同步：删除图形"),
                    };

                    void RenderTable(string title, (string Type, string Params, string Note)[] items)
                    {
                        var titleTb = new TextBlock
                        {
                            FontSize = 10, FontFamily = yaheiFont,
                            Foreground = textMuted, Margin = new Thickness(0, 6, 0, 4),
                        };
                        titleTb.LocText(title);
                        card.Children.Add(titleTb);

                        var tblBorder = new Border
                        {
                            Background = codeBg,
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(10, 8, 10, 8),
                            Margin = new Thickness(0, 0, 0, 8),
                        };
                        var tblStack = new StackPanel();

                        foreach (var (type, prm, note) in items)
                        {
                            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
                            var tag = new Border
                            {
                                Background = new SolidColorBrush(dark ? Color.FromRgb(0x1E, 0x50, 0x7C) : Color.FromRgb(0xDE, 0xEC, 0xF9)),
                                CornerRadius = new CornerRadius(3),
                                Padding = new Thickness(5, 1, 5, 1),
                                Width = 68,
                            };
                            tag.Child = new TextBlock
                            {
                                Text = type, FontSize = 11, FontFamily = codeFont,
                                Foreground = textPrimary, HorizontalAlignment = HorizontalAlignment.Center,
                            };
                            row.Children.Add(tag);
                            row.Children.Add(new TextBlock
                            {
                                Text = prm, FontSize = 11, FontFamily = codeFont,
                                Foreground = textSecondary, VerticalAlignment = VerticalAlignment.Center,
                                Margin = new Thickness(8, 0, 6, 0),
                            });
                            if (!string.IsNullOrEmpty(note))
                            {
                                var noteTb = new TextBlock
                                {
                                    FontSize = 10, FontFamily = yaheiFont,
                                    Foreground = textMuted, VerticalAlignment = VerticalAlignment.Center,
                                };
                                noteTb.LocText(note);
                                row.Children.Add(noteTb);
                            }
                            tblStack.Children.Add(row);
                        }
                        tblBorder.Child = tblStack;
                        card.Children.Add(tblBorder);
                    }

                    RenderTable("基础图形", basicShapes);
                    RenderTable("进阶 & 增量同步", advancedShapes);

                    // ── 规则说明 ──
                    var rulesTb = new TextBlock
                    {
                        FontSize = 10, FontFamily = yaheiFont,
                        Foreground = textMuted, TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 12),
                    };
                    rulesTb.LocText("fill → 填充模式（忽略线宽），a<度数> → 旋转（仅 rect / ellipse）");
                    card.Children.Add(rulesTb);

                    // ── 常用示例 ──
                    var exHeader = new TextBlock
                    {
                        FontSize = 11, FontWeight = System.Windows.FontWeights.SemiBold,
                        Foreground = textMuted, Margin = new Thickness(0, 4, 0, 4),
                    };
                    exHeader.LocText("常用示例");
                    card.Children.Add(exHeader);

                    var drawExamples = new (string Example, string Label)[]
                    {
                        ("[draw,rect,10,10,50,30,#FF0000]",                 "空心矩形·省略线宽（默认 1）"),
                        ("[draw,rect,10,10,50,30,#FF0000,2]",               "空心矩形·指定线宽 2"),
                        ("[draw,rect,10,10,50,30,#FF0000,1,fill]",          "实心矩形（fill 忽略线宽）"),
                        ("[draw,rect,10,10,50,30,#FF0000,2,a45]",           "空心矩形·旋转 45°"),
                        ("[draw,circle,64,32,20,#00FF00]",                  "空心圆·省略线宽"),
                        ("[draw,circle,64,32,20,#00FF00,1,fill]",           "实心圆"),
                        ("[draw,text,0,0,hello,24,#FFFFFF]",                 "文字"),
                        ("[draw,clear]",                                     "清屏（默认底色）"),
                        ("[draw,clear,#DEF]",                                "清屏·指定底色"),
                    };

                    foreach (var (exp, label) in drawExamples)
                    {
                        var exRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };

                        exRow.Children.Add(new TextBox
                        {
                            Text = exp,
                            IsReadOnly = true, BorderThickness = new Thickness(0),
                            FontSize = 11, FontFamily = codeFont,
                            Foreground = primary, Background = Brushes.Transparent,
                            Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Center,
                            Cursor = System.Windows.Input.Cursors.Arrow,
                        });

                        var labelTb = new TextBlock
                        {
                            FontSize = 10, FontFamily = yaheiFont,
                            Foreground = textMuted, VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(6, 0, 6, 0),
                        };
                        labelTb.LocText(label);
                        exRow.Children.Add(labelTb);

                        var btnCopyEx = new Button
                        {
                            Content = "📋",
                            Style = (Style)FindResource("SecondaryButtonStyle"),
                            Height = 24, Width = 32, MinWidth = 0, Padding = new Thickness(0),
                            FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
                            Tag = exp,
                        };
                        btnCopyEx.Click += (s2, e2) =>
                        {
                            if (s2 is Button b2 && b2.Tag is string e) { CopyWithFeedback(e, b2); }
                        };
                        exRow.Children.Add(btnCopyEx);
                        card.Children.Add(exRow);
                    }

                    // ── 设备端代码（默认折叠） ──
                    var codeToggleRow = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 12, 0, 6),
                        Cursor = System.Windows.Input.Cursors.Hand,
                    };
                    var codeArrow = new TextBlock
                    {
                        Text = "▶",
                        FontSize = 11, FontFamily = codeFont,
                        Foreground = textMuted, VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 0),
                    };
                    codeToggleRow.Children.Add(codeArrow);
                    var codeToggleLabel = new TextBlock
                    {
                        FontSize = 11, FontWeight = System.Windows.FontWeights.SemiBold,
                        Foreground = textMuted, VerticalAlignment = VerticalAlignment.Center,
                    };
                    codeToggleLabel.LocText("设备端代码（MCU printf wrapper）");
                    codeToggleRow.Children.Add(codeToggleLabel);
                    card.Children.Add(codeToggleRow);

                    var codeContent = new Border
                    {
                        Background = codeBg,
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(12, 10, 12, 10),
                        Visibility = Visibility.Collapsed,
                    };
                    codeContent.Child = new TextBlock
                    {
                        Text = ex.Code,
                        FontSize = 11, FontFamily = codeFont,
                        Foreground = codeFg,
                        TextWrapping = TextWrapping.Wrap,
                        LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                        LineHeight = 18,
                    };
                    card.Children.Add(codeContent);

                    codeToggleRow.MouseLeftButtonDown += (s, e) =>
                    {
                        bool show = codeContent.Visibility != Visibility.Visible;
                        codeContent.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                        codeArrow.Text = show ? "▼" : "▶";
                        e.Handled = true;
                    };
                }
                else
                {
                    // ——— 协议格式 ———
                    var formatRow = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 0, 0, 4),
                    };
                    var formatLabel = new TextBlock
                    {
                        FontSize = 11, FontWeight = System.Windows.FontWeights.SemiBold,
                        Foreground = textMuted, VerticalAlignment = VerticalAlignment.Center,
                    };
                    formatLabel.LocText("协议格式");
                    formatRow.Children.Add(formatLabel);
                    var formatValueTb = new TextBlock
                    {
                        FontSize = 12, FontFamily = codeFont,
                        Foreground = textSecondary,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    formatValueTb.LocText(ex.Format);
                    formatRow.Children.Add(formatValueTb);
                    card.Children.Add(formatRow);

                    // ——— 协议示例 ———
                    var exampleHeader = new TextBlock
                    {
                        FontSize = 11, FontWeight = System.Windows.FontWeights.SemiBold,
                        Foreground = textMuted, Margin = new Thickness(0, 0, 0, 4),
                    };
                    exampleHeader.LocText("协议示例");
                    card.Children.Add(exampleHeader);

                    void AddExampleRow(string exampleText)
                    {
                        var exampleRow = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = new Thickness(0, 0, 0, 4),
                        };
                        var tbExample = new TextBox
                        {
                            Text = exampleText,
                            IsReadOnly = true, BorderThickness = new Thickness(0),
                            FontSize = 12, FontFamily = codeFont,
                            Foreground = primary,
                            Background = Brushes.Transparent,
                            Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Center,
                            Cursor = System.Windows.Input.Cursors.Arrow,
                        };
                        exampleRow.Children.Add(tbExample);

                        var btnCopy = new Button
                        {
                            Content = "📋",
                            Style = (Style)FindResource("SecondaryButtonStyle"),
                            Height = 24, Width = 32, MinWidth = 0, Padding = new Thickness(0),
                            FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(6, 0, 0, 0),
                        };
                        btnCopy.Tag = exampleText;
                        btnCopy.Click += (s2, e2) =>
                        {
                            if (s2 is Button b2 && b2.Tag is string exp)
                            {
                                CopyWithFeedback(exp, b2);
                            }
                        };
                        exampleRow.Children.Add(btnCopy);
                        card.Children.Add(exampleRow);
                    }

                    AddExampleRow(ex.Example);

                    // ——— 参数 ———
                    if (ex.Params.Length > 0)
                    {
                        var paramsHeader = new TextBlock
                        {
                            FontSize = 11, FontWeight = System.Windows.FontWeights.SemiBold,
                            Foreground = textMuted, Margin = new Thickness(0, 8, 0, 4),
                        };
                        paramsHeader.LocText("参数");
                        card.Children.Add(paramsHeader);

                        foreach (var (name, desc) in ex.Params)
                        {
                            var paramStack = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Margin = new Thickness(0, 0, 0, 3),
                            };
                            var nameTb = new TextBlock
                            {
                                FontSize = 11, FontFamily = codeFont,
                                Foreground = textSecondary,
                                VerticalAlignment = VerticalAlignment.Top,
                            };
                            nameTb.LocText(name);
                            paramStack.Children.Add(nameTb);
                            var paramDescTb = new TextBlock
                            {
                                FontSize = 11, FontFamily = yaheiFont,
                                Foreground = textMuted, TextWrapping = TextWrapping.Wrap,
                            };
                            paramDescTb.LocText(desc);
                            paramStack.Children.Add(paramDescTb);
                            card.Children.Add(paramStack);
                        }
                    }

                    // ——— 设备端代码 ———
                    var codeHeader = new TextBlock
                    {
                        FontSize = 11, FontWeight = System.Windows.FontWeights.SemiBold,
                        Foreground = textMuted, Margin = new Thickness(0, 10, 0, 6),
                    };
                    codeHeader.LocText("设备端代码");
                    card.Children.Add(codeHeader);

                    var codeBlock = new Border
                    {
                        Background = codeBg,
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(12, 10, 12, 10),
                    };
                    var codeText = new TextBlock
                    {
                        Text = ex.Code,
                        FontSize = 11, FontFamily = codeFont,
                        Foreground = codeFg,
                        TextWrapping = TextWrapping.Wrap,
                        LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                        LineHeight = 18,
                    };
                    codeBlock.Child = codeText;
                    card.Children.Add(codeBlock);
                }

                contentBorder.Child = card;
                groupStack.Children.Add(contentBorder);

                groupBorder.Child = groupStack;
                examplesListPanel.Children.Add(groupBorder);
            }
        }

        private void BtnCopyGitHub_Click(object sender, RoutedEventArgs e)
        {
            CopyWithFeedback(tbAboutGitHub.Text, sender as Button);
        }

        private void BtnCopyDataPath_Click(object sender, RoutedEventArgs e)
        {
            CopyWithFeedback(tbAboutDataPath.Text, sender as Button);
        }

        private void BtnCopyIssues_Click(object sender, RoutedEventArgs e)
        {
            CopyWithFeedback(tbAboutIssues.Text, sender as Button);
        }

        private void BtnOpenIssues_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(tbAboutIssues.Text) { UseShellExecute = true });
        }

        /// <summary>Clipboard.SetText 安全封装：后台 STA 线程执行，不阻塞 UI，不抛异常</summary>
        private static void SafeSetClipboard(string text)
        {
            var t = new Thread(() =>
            {
                try { Clipboard.SetText(text); }
                catch { /* 剪贴板被占用，静默放弃 */ }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }

        /// <summary>
        /// 复制按钮反馈：弹出"已复制"提示 + 按钮抖动
        /// </summary>
        private void ShowCopyToastAndShake(Button btn)
        {
            // ——— 快速抖动（150ms，干脆利落）———
            btn.RenderTransformOrigin = new Point(0.5, 0.5);
            var st = new ScaleTransform(1, 1);
            btn.RenderTransform = st;

            var anim = new DoubleAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.FromMilliseconds(150)) };
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0,  KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(33))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0.90, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(75))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.03, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0,  KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));

            Dispatcher.BeginInvoke(new Action(() => {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            }), System.Windows.Threading.DispatcherPriority.Render);

            // ——— 浮窗提示 ———
            var toastBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4D)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 5, 10, 5),
                SnapsToDevicePixels = true,
            };
            toastBorder.Child = new TextBlock
            {
                Text = T("✓ 已复制"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = btn.FontFamily,
            };

            var toast = new Popup
            {
                Placement = PlacementMode.Mouse,
                HorizontalOffset = -10,
                VerticalOffset = 10,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = toastBorder,
            };

            toast.IsOpen = true;

            // 0.8 秒后自动关闭
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(800),
            };
            timer.Tick += (s2, e2) => { toast.IsOpen = false; timer.Stop(); };
            timer.Start();
        }

        /// <summary>
        /// 复制到剪贴板 + 按钮反馈（抖动 + 已复制提示）。
        /// 统一包装 SafeSetClipboard + ShowCopyToastAndShake，消除"只复制不弹"的遗漏。
        /// </summary>
        private void CopyWithFeedback(string text, Button btn)
        {
            SafeSetClipboard(text);
            if (btn != null)
                ShowCopyToastAndShake(btn);
        }

        /// <summary>
        /// 统一创建 Popup：自动设 PlacementTarget + AllowsTransparency，
        /// 消除"忘设 → 暗色模式找不到主题色"的遗漏。窗口移动时自动关闭。
        /// </summary>
        private Popup CreatePopup(UIElement placementTarget, PlacementMode placement = PlacementMode.Bottom)
        {
            var popup = new Popup
            {
                PlacementTarget = placementTarget,
                Placement = placement,
                StaysOpen = true,  // 避免与 AttachPopupToggle 竞态；关闭由 toggle + 外部点击管
                AllowsTransparency = true,
            };
            // 窗口移动时自动关闭
            EventHandler onMoved = null;
            onMoved = (_, _) => { popup.IsOpen = false; LocationChanged -= onMoved; };
            LocationChanged += onMoved;
            popup.Closed += (_, _) => LocationChanged -= onMoved;
            return popup;
        }

        /// <summary>按钮 toggle 弹窗：拦截 PreviewMouseLeftButtonDown，若弹窗已开则关闭并阻止 Click。Click 逻辑由 XAML 或调用方自行绑定。</summary>
        private static void AttachPopupToggle(Button btn, Func<Popup> getPopup)
        {
            btn.PreviewMouseLeftButtonDown += (_, e) =>
            {
                var p = getPopup();
                if (p != null && p.IsOpen)
                { p.IsOpen = false; e.Handled = true; }
            };
        }

        private void SwitchSettingsPage(string page)
        {
            _currentSettingsPage = page;
            settingsSerialPage.Visibility     = page == "serial"    ? Visibility.Visible : Visibility.Collapsed;
            settingsShortcutsPage.Visibility  = page == "shortcuts"  ? Visibility.Visible : Visibility.Collapsed;
            settingsExamplesPage.Visibility   = page == "examples"   ? Visibility.Visible : Visibility.Collapsed;
            settingsAssetsPage.Visibility     = page == "assets"     ? Visibility.Visible : Visibility.Collapsed;
            settingsAboutPage.Visibility      = page == "about"      ? Visibility.Visible : Visibility.Collapsed;
            // 高亮当前子页导航按钮
            UpdateSettingsNavHighlight(page);
            // 有子页 → 主区显示设置面板（_previousContentTab 不动，保持之前内容标签）
            RefreshContentVisibility();
        }

        /// <summary>设置子页导航高亮：当前页按钮 PrimaryBrush + SemiBold，其余还原</summary>
        private void UpdateSettingsNavHighlight(string active)
        {
            var navButtons = new[] {
                (btnSettingsSerial, "serial"),
                (btnSettingsShortcuts, "shortcuts"),
                (btnSettingsExamples, "examples"),
                (btnSettingsAssets, "assets"),
                (btnSettingsAbout, "about"),
            };
            var primary = (Brush)FindResource("PrimaryBrush");
            var secondary = (Brush)FindResource("TextSecondaryBrush");
            foreach (var (btn, key) in navButtons)
            {
                bool isActive = key == active;
                btn.Foreground = isActive ? primary : secondary;
                btn.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Regular;
            }
        }

        /// <summary>
        /// 图标栏 Click：已选中图标二次点击 → 水平脉冲（缩放 X 1→1.25→0.85→1）
        /// </summary>
        private void IconBarIcon_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not string tabName) return;
            if (tabName != _currentTab) return; // 只在已激活图标上二次点击才抖动

            fe.RenderTransformOrigin = new Point(0.5, 0.5);
            var st = new ScaleTransform(1, 1);
            fe.RenderTransform = st;

            var animX = new DoubleAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.FromMilliseconds(280)) };
            animX.KeyFrames.Add(new LinearDoubleKeyFrame(1.0,  KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
            animX.KeyFrames.Add(new LinearDoubleKeyFrame(1.25, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
            animX.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
            animX.KeyFrames.Add(new LinearDoubleKeyFrame(1.08, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
            animX.KeyFrames.Add(new LinearDoubleKeyFrame(1.0,  KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260))));

            Dispatcher.BeginInvoke(new Action(() => {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, animX);
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void RefreshContentVisibility()
        {
            // 设置面板有子页展开时 → 主区显示设置页；否则 → 保持 _previousContentTab 面板不动
            bool showSettingsPanel = _currentTab == "Settings" && _currentSettingsPage != null;
            string mainTab = showSettingsPanel ? null : _previousContentTab;
            foreach (var kv in _tabRegistry)
            {
                kv.Value.Panel.Visibility     = kv.Key == mainTab      ? Visibility.Visible : Visibility.Collapsed;
                kv.Value.SidePanel.Visibility = kv.Key == _currentTab  ? Visibility.Visible : Visibility.Collapsed;
            }
            panelSettings.Visibility = showSettingsPanel ? Visibility.Visible : Visibility.Collapsed;
            // Plot 侧面板特殊处理（频域/时域切换）
            if (_currentTab == "Plot")
            {
                if (_isFreqDomain)
                {
                    rightPlot.Visibility           = Visibility.Collapsed;
                    rightPlotDetail.Visibility     = Visibility.Collapsed;
                    rightPlotFreq.Visibility       = _plotShowDetail ? Visibility.Collapsed : Visibility.Visible;
                    rightPlotDetailFreq.Visibility = _plotShowDetail ? Visibility.Visible      : Visibility.Collapsed;
                }
                else
                {
                    rightPlot.Visibility           = _plotShowDetail ? Visibility.Collapsed : Visibility.Visible;
                    rightPlotDetail.Visibility     = _plotShowDetail ? Visibility.Visible      : Visibility.Collapsed;
                    rightPlotFreq.Visibility       = Visibility.Collapsed;
                    rightPlotDetailFreq.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                rightPlot.Visibility           = Visibility.Collapsed;
                rightPlotDetail.Visibility     = Visibility.Collapsed;
                rightPlotFreq.Visibility       = Visibility.Collapsed;
                rightPlotDetailFreq.Visibility = Visibility.Collapsed;
            }
            switch (_currentTab)
            {
                case "Receive":  tbSidePanelTitle.LocText("收发设置"); break;
                case "Plot":     tbSidePanelTitle.LocText("绘图设置"); break;
                case "Keys":     tbSidePanelTitle.LocText("按键属性"); break;
                case "Sliders":  tbSidePanelTitle.LocText("滑杆属性"); break;
                case "Joystick": tbSidePanelTitle.LocText("摇杆设置"); break;
                case "OLED":     tbSidePanelTitle.LocText("OLED 设置"); break;
                case "Sensors":  tbSidePanelTitle.LocText((_sensorVM?.IsEditMode == true) ? "卡片管理" : "传感面板"); break;
                case "Settings": tbSidePanelTitle.LocText("设置"); break;
            }
            // 切换到按键/滑杆/传感面板时刷新侧面板
            if (_currentTab == "Keys") RefreshKeysSidePanel();
            if (_currentTab == "Sliders") RefreshSlidersSidePanel();
            if (_currentTab == "Sensors" && _sensorVM != null)
                RefreshSensorSidePanel();
            if (_currentTab == "OLED") ResetOLEDSidePanel();
        }

        /// <summary>
        /// 图标栏显隐刷新：根据 _panelVisible 控制图标可见性（决策 12 改）
        /// Receive/Settings 常驻，Plot/Keys/Sliders/OLED/Joystick 由 + 菜单切换
        /// </summary>
        private void RefreshIconBarVisibility()
        {
            tabPlot.Visibility     = _panelVisible["Plot"]     ? Visibility.Visible : Visibility.Collapsed;
            tabKeys.Visibility     = _panelVisible["Keys"]     ? Visibility.Visible : Visibility.Collapsed;
            tabSliders.Visibility  = _panelVisible["Sliders"]  ? Visibility.Visible : Visibility.Collapsed;
            tabOLED.Visibility     = _panelVisible["OLED"]     ? Visibility.Visible : Visibility.Collapsed;
            tabJoystick.Visibility = _panelVisible["Joystick"] ? Visibility.Visible : Visibility.Collapsed;
            tabSensors.Visibility  = _panelVisible["Sensors"]  ? Visibility.Visible : Visibility.Collapsed;
            // 隐藏的图标如果正处于选中状态，回退到接收区
            string tab = _currentTab;
            if ((tab == "Plot" && !_panelVisible["Plot"])
                || (tab == "Keys" && !_panelVisible["Keys"])
                || (tab == "Sliders" && !_panelVisible["Sliders"])
                || (tab == "OLED" && !_panelVisible["OLED"])
                || (tab == "Joystick" && !_panelVisible["Joystick"])
                || (tab == "Sensors" && !_panelVisible["Sensors"]))
                tabReceive.IsChecked = true;
        }

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

        /// <summary>切语言时重建 ComboBox Items（Items 是静态字符串，不走 DynamicResource）</summary>
        private void RefreshComboBoxLocale()
        {
            if (_rebuildingComboBox) return;
            _rebuildingComboBox = true;
            void Rebuild(ComboBox cb, string[] keys, Func<string, string> display)
            {
                int idx = cb.SelectedIndex;
                cb.Items.Clear();
                foreach (var k in keys) cb.Items.Add(display(k));
                if (idx >= 0 && idx < cb.Items.Count) cb.SelectedIndex = idx;
            }
            Rebuild(cbReceiveMode, LogicValueMaps.IoModeKeys, LogicValueMaps.DisplayIoMode);
            Rebuild(cbSendMode, LogicValueMaps.IoModeKeys, LogicValueMaps.DisplayIoMode);
            Rebuild(cbParity, LogicValueMaps.ParityKeys, LogicValueMaps.DisplayParity);
            Rebuild(cbFlowControl, LogicValueMaps.FlowControlKeys, LogicValueMaps.DisplayFlowControl);
            Rebuild(cbTimestampFormat, LogicValueMaps.TimestampFormatKeys, LogicValueMaps.DisplayTimestampFormat);
            Rebuild(cbLineEnding, LogicValueMaps.NewlineKeys, LogicValueMaps.DisplayNewline);
            Rebuild(cbPlotMode, LogicValueMaps.PlotModeKeys, LogicValueMaps.DisplayPlotMode);
            Rebuild(cbFreqWindow, LogicValueMaps.WindowFunctionKeys, LogicValueMaps.DisplayWindowFunction);
            // Keys 面板 — 按键发送模式
            foreach (var cb in new ComboBox[] { cbKeyPressMode, cbKeyReleaseMode, cbKeySendModeMulti, cbKeyReleaseModeMulti, cbModulePressMode, cbModuleReleaseMode })
                Rebuild(cb, LogicValueMaps.SendModeKeys, LogicValueMaps.DisplaySendMode);
            _rebuildingComboBox = false;
        }

        /// <summary>切语言时刷新风格按钮文字（和 ComboBox 同款问题——按钮 Content 不走 DynamicResource）</summary>
        private void RefreshStyleButtonLabels()
        {
            if (btnJoystickPadStyle != null)
                btnJoystickPadStyle.Content = JoyPadLabel() + " ▾";
            if (btnJoystickThumbStyle != null)
                btnJoystickThumbStyle.Content = JoyThumbLabel() + " ▾";
        }

        /// <summary>重建含逗号/等号的说明文字 Inlines（XAML DynamicResource 语法限制）</summary>
        private void RefreshLocaleInlines()
        {
            // FFT 操作提示
            tbFftHints.Inlines.Clear();
            tbFftHints.Inlines.Add(new Run(T("🔬 数据源选 [plot,...] 通道 → PC 自动算 FFT")) { FontWeight = FontWeights.SemiBold });
            tbFftHints.Inlines.Add(new LineBreak());
            tbFftHints.Inlines.Add(new Run(T("收到 [fft,...] 协议则覆盖自动频谱")));
            tbFftHints.Inlines.Add(new LineBreak());
            tbFftHints.Inlines.Add(new Run(T("🖱 滚轮缩放 · 右键拖拽平移")));
            tbFftHints.Inlines.Add(new LineBreak());
            tbFftHints.Inlines.Add(new Run(T("📊 暂停后点「详细」按钮查看频谱指标")));

            // OLED C 代码说明
            tbOledCodeHints.Inlines.Clear();
            tbOledCodeHints.Inlines.Add(new Run(T("坐标轴：起/中/终点标有刻度数字")));
            tbOledCodeHints.Inlines.Add(new LineBreak());
            tbOledCodeHints.Inlines.Add(new LineBreak());
            tbOledCodeHints.Inlines.Add(new Run(T("C 代码写法：")) { FontWeight = FontWeights.SemiBold });
            tbOledCodeHints.Inlines.Add(new LineBreak());
            tbOledCodeHints.Inlines.Add(new Run("Serial_Printf(&huart1,"));
            tbOledCodeHints.Inlines.Add(new LineBreak());
            tbOledCodeHints.Inlines.Add(new Run("  \"[display,0,0,\\\"hello\\\",24]\\r\\n\");"));
            tbOledCodeHints.Inlines.Add(new LineBreak());
            tbOledCodeHints.Inlines.Add(new Run("  \"[display,10,20,\\\"ok\\\",16,#FF0000]\\r\\n\");"));
            tbOledCodeHints.Inlines.Add(new LineBreak());
            tbOledCodeHints.Inlines.Add(new LineBreak());
            tbOledCodeHints.Inlines.Add(new Run(T("⚠ 内层引号前加 \\ 转义")) { Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x48, 0x56)) });
            tbOledCodeHints.Inlines.Add(new LineBreak());
            tbOledCodeHints.Inlines.Add(new Run(T("末尾 #RRGGBB 可选，不写=白色")));
            tbOledCodeHints.Inlines.Add(new LineBreak());
            tbOledCodeHints.Inlines.Add(new Run(T("[display-clear] 清屏")));

            // 使用示例
            tbUsageExample.Inlines.Clear();
            tbUsageExample.Inlines.Add(new Run(T("设备通过串口发送 ")));
            tbUsageExample.Inlines.Add(new Run("[type,arg1,arg2,...]") { FontFamily = new System.Windows.Media.FontFamily("Consolas"), Foreground = (Brush)FindResource("PrimaryBrush") });
            tbUsageExample.Inlines.Add(new Run(T(" 格式的消息，")));
            tbUsageExample.Inlines.Add(new Run(T("工具自动解析并路由到对应面板。方括号 ")));
            tbUsageExample.Inlines.Add(new Run("[ ]") { FontFamily = new System.Windows.Media.FontFamily("Consolas"), Foreground = (Brush)FindResource("PrimaryBrush") });
            tbUsageExample.Inlines.Add(new Run(T(" 包裹每条消息，参数用逗号分隔。含逗号的参数用双引号包裹。")));
        }

        private Popup _addPanelPopup;

        /// <summary>
        /// "+" 按钮下拉面板：列出所有面板（接收区常驻不可切换，其余可切换显隐）
        /// 使用 Popup 替代 ContextMenu：支持点击空白自动关闭 + 完全控制样式
        /// </summary>
        private void BtnAddPanel_Click(object sender, RoutedEventArgs e)
        {
            // 已打开 → 关闭
            if (_addPanelPopup != null && _addPanelPopup.IsOpen)
            {
                CloseAddPanelPopup();
                return;
            }

            var panel = new StackPanel { MinWidth = 180 };

            // 标题
            var popupTitle = new TextBlock {
                FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(14, 10, 14, 6),
            };
            popupTitle.LocText("管理标签显示");
            panel.Children.Add(popupTitle);

            // 接收区：常驻
            AddPopupItem(panel, T("📡 接收区"), isVisible: true, canToggle: false);
            panel.Children.Add(new Separator { Margin = new Thickness(10, 4, 10, 4),
                Background = (Brush)FindResource("CardBorderBrush") });

            // 可切换面板
            AddPopupItem(panel, T("📈 波形图"), _panelVisible["Plot"], tab: "Plot");
            AddPopupItem(panel, T("🎮 按键面板"), _panelVisible["Keys"], tab: "Keys");
            AddPopupItem(panel, T("🎚 滑杆面板"), _panelVisible["Sliders"], tab: "Sliders");
            AddPopupItem(panel, T("📱 OLED"), _panelVisible["OLED"], tab: "OLED");
            AddPopupItem(panel, T("🕹 摇杆面板"), _panelVisible["Joystick"], tab: "Joystick");
            AddPopupItem(panel, T("📡 传感面板"), _panelVisible["Sensors"], tab: "Sensors");

            var border = new Border {
                Background = (Brush)FindResource("StatusBarBgBrush"),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                Child = panel,
            };

            _addPanelPopup = CreatePopup(btnAddPanel, System.Windows.Controls.Primitives.PlacementMode.Left);
            _addPanelPopup.Child = border;
            _addPanelPopup.PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade;
            _addPanelPopup.HorizontalOffset = -4;

            _addPanelPopup.Closed += (s2, e2) =>
            {
                btnAddPanel.Foreground = (Brush)FindResource("TextMutedBrush");
            };
            _addPanelPopup.IsOpen = true;

            // "+" 按钮 active 态
            btnAddPanel.Foreground = (Brush)FindResource("PrimaryBrush");

            // 点 Popup 外部关闭（延迟订阅，避开当前 Click 事件）
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                MouseButtonEventHandler outsideHandler = null;
                outsideHandler = (s2, e2) =>
                {
                    if (_addPanelPopup == null || !_addPanelPopup.IsOpen) return;
                    var clicked = e2.OriginalSource as DependencyObject;
                    // 检查是否点在 Popup 内或 "+" 按钮上（后者由 BtnAddPanel_Click 处理 toggle）
                    bool inside = false;
                    var current = clicked;
                    while (current != null)
                    {
                        if (current == _addPanelPopup.Child || current == btnAddPanel) { inside = true; break; }
                        current = VisualTreeHelper.GetParent(current);
                    }
                    if (!inside)
                    {
                        CloseAddPanelPopup();
                        RemoveHandler(PreviewMouseLeftButtonDownEvent, outsideHandler);
                    }
                };
                AddHandler(PreviewMouseLeftButtonDownEvent, outsideHandler, handledEventsToo: true);
            }));
        }

        private void CloseAddPanelPopup()
        {
            if (_addPanelPopup != null)
            {
                _addPanelPopup.IsOpen = false;
                _addPanelPopup = null;
            }
            btnAddPanel.Foreground = (Brush)FindResource("TextMutedBrush");
        }

        private void AddPopupItem(StackPanel panel, string label, bool isVisible, string tab = null, bool canToggle = true)
        {
            var item = new Border {
                Padding = new Thickness(14, 6, 14, 6),
                Background = Brushes.Transparent,
                Cursor = canToggle ? Cursors.Hand : Cursors.Arrow,
                Tag = tab,
            };

            var tb = new TextBlock { FontSize = 13 };
            tb.Text = (isVisible ? "✓  " : "    ") + label;
            tb.Foreground = isVisible
                ? (Brush)FindResource("PrimaryBrush")
                : (Brush)FindResource("TextMutedBrush");
            tb.FontWeight = isVisible ? FontWeights.SemiBold : FontWeights.Regular;
            item.Child = tb;

            if (canToggle)
            {
                item.MouseEnter += (s, e) => {
                    item.Background = (Brush)FindResource("SecondaryHoverBgBrush");
                };
                item.MouseLeave += (s, e) => {
                    item.Background = Brushes.Transparent;
                };
                item.MouseLeftButtonDown += (s2, e2) => {
                    _panelVisible[tab] = !_panelVisible[tab];
                    bool nowVisible = _panelVisible[tab];
                    tb.Text = (nowVisible ? "✓  " : "    ") + label;
                    tb.Foreground = nowVisible
                        ? (Brush)FindResource("PrimaryBrush")
                        : (Brush)FindResource("TextMutedBrush");
                    tb.FontWeight = nowVisible ? FontWeights.SemiBold : FontWeights.Regular;
                    RefreshIconBarVisibility();
                    SavePanelVisibility();
                };
            }
            else
            {
                tb.Opacity = 0.5;
            }

            panel.Children.Add(item);
        }

        private void SavePanelVisibility()
        {
            if (_prefsData == null) return;
            var dict = new Dictionary<string, object>();
            foreach (var kv in _panelVisible) dict[kv.Key] = kv.Value;
            _prefsData["panelVisible"] = dict;
            _prefs.Save(_prefsData);
        }

        private void BtnPanelCollapse_Click(object sender, RoutedEventArgs e)
        {
            if (colSidePanel.Width.Value > 30)
            {
                colSidePanel.MinWidth = 0;
                colSidePanel.Width = new GridLength(28);
                btnPanelCollapse.Content = "▶";
                btnPanelCollapse.ToolTip = T("展开侧面板");
            }
            else
            {
                colSidePanel.MinWidth = 120;
                colSidePanel.Width = new GridLength(220);
                btnPanelCollapse.Content = "◀";
                btnPanelCollapse.ToolTip = T("折叠侧面板");
            }
        }
        // ═══ Phase 3: 波形图 ═══

        private void EnsurePlotView()
        {
            if (_plotViewCreated) return;
            _plotViewCreated = true;

            plotView = new OxyPlot.Wpf.PlotView
            {
                Model = _plotVM.Model,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = System.Windows.Media.Brushes.Transparent, // 让父级背景透过来
            };
            // 运行时禁用 OxyPlot 交互，暂停后启用默认控制器
            plotView.Controller = null;
            // 鼠标左键：运行中→暂停 / 暂停中→HitTest 曲线切详情 / 未命中→OxyPlot 默认 tracking
            plotView.MouseLeftButtonDown += PlotView_MouseLeftButtonDown;
            plotArea.Children.Insert(0, plotView);
            // 暗色模式追踪框颜色修复（ClassHandler 方式，TrackerControl 创建时自动设色）
            FixPlotTrackerColors(isDarkTheme);
        }

        /// <summary>
        /// #10 FFT 视角切换：时域 ↔ 频域
        /// </summary>
        private void btnPerspectiveToggle_Click(object sender, RoutedEventArgs e)
        {
            _isFreqDomain = !_isFreqDomain;
            if (plotView != null)
            {
                var oldModel = plotView.Model;
                var newModel = _isFreqDomain ? _plotVM.FreqModel : _plotVM.TimeModel;
                if (oldModel != newModel)
                {
                    plotView.Model = newModel;
                    // 强制刷新避免 Model 切换后 X 轴残留
                    newModel.InvalidatePlot(true);
                }
            }
            // 更新按钮文案
            btnPerspectiveToggle.LocText(_isFreqDomain ? "📶 频域  →  时域 ⏱" : "⏱ 时域  →  频域 📶");
            // 刷新侧面板（时域/频域内容切换）+ 数据源下拉框
            RefreshContentVisibility();
            if (_isFreqDomain)
            {
                RefreshFreqSourceList();
                _plotVM.RecomputeFft();
                UpdateFreqSideInfo();
            }
            // 频域下隐藏调参抽屉（调参是时域功能）
            if (_isFreqDomain) ResetTuningDrawer();
            // HUD 切换：时域→数值浮层，频域→数据源名
            if (_isFreqDomain)
            {
                plotHud.Visibility = Visibility.Collapsed;
                UpdateFreqHud();
            }
            else
            {
                freqHud.Visibility = Visibility.Collapsed;
            }
            // 更新空提示
            if (plotEmptyHint.Visibility == Visibility.Visible)
                plotEmptyHint.LocText(_isFreqDomain ? "等待 FFT 数据…" : "等待串口数据…");
        }

        /// <summary>收起调参抽屉</summary>
        private void ResetTuningDrawer()
        {
            if (panelTuningDrawer.Visibility == Visibility.Visible)
            {
                panelTuningDrawer.Visibility = Visibility.Collapsed;
                splitterTuning.Visibility = Visibility.Collapsed;
                barTuningToggle.Height = 28;
                btnTuningToggle.LocText("▲ 调参工作台");
            }
        }

        private void btnPlotPause_Click(object sender, RoutedEventArgs e)
        {
            TogglePlotPauseWithDetail();
        }

        private void btnPlotClear_Click(object sender, RoutedEventArgs e)
        {
            if (_isFreqDomain)
            {
                _plotVM.ClearFreq();
                plotEmptyHint.LocText("等待 FFT 数据…");
            }
            else
            {
                _plotVM.Clear();
                plotEmptyHint.LocText("等待串口数据…");
            }
            plotEmptyHint.Visibility = Visibility.Visible;
            plotHud.Visibility = Visibility.Collapsed;
            freqHud.Visibility = Visibility.Collapsed;
        }

        // ── #10 FFT 频域控件事件 ──

        private void cbFreqSource_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_plotVM == null || cbFreqSource.SelectedIndex < 0) return;
            string display = cbFreqSource.SelectedItem as string;
            if (cbFreqSource.SelectedIndex <= 0)
            {
                _plotVM.SetFftSource(null);
                UpdateFreqSideInfo();
                return;
            }
            // 解析前缀：📈 = plot通道, 📶 = 命名fft
            string key;
            if (display.StartsWith("📶 "))
                key = "fft:" + display.Substring(3);
            else if (display.StartsWith("📈 "))
                key = "plot:" + display.Substring(3);
            else
                key = "plot:" + display; // 兼容旧格式无前缀
            _plotVM.SetFftSource(key);
            if (key.StartsWith("plot:"))
                _plotVM.RecomputeFft();
            UpdateFreqSideInfo();
            if (_isFreqDomain) UpdateFreqHud();
        }

        private void tbFreqSampleRate_Changed(object sender, TextChangedEventArgs e)
        {
            if (_plotVM == null) return;
            if (double.TryParse(tbFreqSampleRate.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double sr) && sr > 0)
                _plotVM.SetFftSampleRate(sr);
            else
                _plotVM.SetFftSampleRate(0);
            _plotVM.RecomputeFft();
            UpdateFreqSideInfo();
            if (_isFreqDomain) UpdateFreqHud();
        }

        private void cbFreqSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_plotVM == null || cbFreqSize.SelectedItem == null) return;
            if (int.TryParse(cbFreqSize.SelectedItem.ToString(), out int size))
            {
                _plotVM.SetFftWindowSize(size);
                _plotVM.SetFftSampleRate(0); // 改点数清采样率（Hz/bin 变了）
                tbFreqSampleRate.Text = "";
                _plotVM.RecomputeFft();
                UpdateFreqSideInfo();
            }
        }

        private void btnFreqRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshFreqSourceList();
            if (_plotVM != null && _plotVM.GetFftChannel() != null)
                _plotVM.RecomputeFft();
            UpdateFreqSideInfo();
        }

        private void cbFreqWindow_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_rebuildingComboBox || _plotVM == null) return;
            _plotVM.SetFftWindowType(cbFreqWindow.SelectedIndex);
            _plotVM.RecomputeFft();
            UpdateFreqSideInfo();
        }

        private void chkFreqMarkers_Changed(object sender, RoutedEventArgs e)
        {
            if (_plotVM == null) return;
            bool show = chkFreqMarkers.IsChecked == true;
            _plotVM.SetFreqMarkers(show);
        }

        private void chkFreqLines_Changed(object sender, RoutedEventArgs e)
        {
            if (_plotVM == null) return;
            bool show = chkFreqLines.IsChecked == true;
            _plotVM.SetFreqLines(show);
        }

        private void chkFreqValueHud_Changed(object sender, RoutedEventArgs e)
        {
            // 频域数值显示：目前暂无 HUD 浮层，预留
        }

        /// <summary>解析 [fft,...] 消息：兼容旧格式 [fft,点数,bin0,...] 和新格式 [fft,name,点数,bin0,...]</summary>
        private void ParseFftMessage(List<string> args)
        {
            string fftName = null;
            int countOffset; // args 中 bin 数据起始位置
            int fftPoints;

            // args[0] 不是数字 → 新格式 [fft,name,count,bin0,...]
            if (!int.TryParse(args[0], out _) && args.Count >= 3)
            {
                fftName = args[0];
                if (!int.TryParse(args[1], out fftPoints) || fftPoints <= 0) return;
                countOffset = 2;
            }
            else
            {
                if (!int.TryParse(args[0], out fftPoints) || fftPoints <= 0) return;
                countOffset = 1;
            }

            int count = Math.Min(fftPoints, args.Count - countOffset);
            var bins = new double[count];
            int valid = 0;
            for (int i = 0; i < count; i++)
            {
                if (double.TryParse(args[countOffset + i], NumberStyles.Float, CultureInfo.InvariantCulture, out double m))
                    bins[valid++] = m;
            }
            if (valid == count)
            {
                _plotVM.OnFftMessage(fftName, fftPoints, bins);
                if (_isFreqDomain && plotEmptyHint.Visibility == Visibility.Visible)
                    plotEmptyHint.Visibility = Visibility.Collapsed;
                if (fftName != null)
                {
                    // 新增命名 FFT 源（不重建整表，防闪烁）
                    string key = $"📶 {fftName}";
                    if (!cbFreqSource.Items.Contains(key))
                        cbFreqSource.Items.Add(key);
                }
                UpdateFreqSideInfo();
                if (_isFreqDomain) UpdateFreqHud();
            }
        }

        /// <summary>刷新频域数据源下拉框：列出 [plot,...] 通道 + [fft,...] 命名源</summary>
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

        /// <summary>更新频域 HUD：显示当前数据源名</summary>
        private void UpdateFreqHud()
        {
            var sel = cbFreqSource.SelectedItem as string;
            if (cbFreqSource.SelectedIndex > 0)
            {
                freqHudLabel.Text = sel;
                freqHud.Visibility = Visibility.Visible;
            }
            else
            {
                freqHud.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>收到 [fft,...] 或 FFT 数据变化后更新侧面板只读字段 + 详情面板指标</summary>
        private void UpdateFreqSideInfo()
        {
            if (_plotVM == null) return;
            var bins = _plotVM.GetFreqBins();
            int points = bins.Length;
            // 频域指标
            double dc = _plotVM.GetFreqDcBias();
            var (fundBin, fundMag) = _plotVM.GetFreqFundamental();
            double thd = _plotVM.GetFreqTHD();
            double snr = _plotVM.GetFreqSNR();
            double sr = _plotVM.GetFftSampleRate();
            double resHz = _plotVM.GetFreqResolution();
            double fundHz = _plotVM.GetFreqFundamentalHz();
            // 填充详情面板
            tbFreqDetailFund.Text    = fundHz > 0 ? $"{fundHz:F1} Hz" : (fundBin > 0 ? $"Bin {fundBin}" : "—");
            tbFreqDetailFundMag.Text = fundBin > 0 ? $"{fundMag:F3}" : "—";
            tbFreqDetailTHD.Text     = fundBin > 0 ? $"{thd * 100:F2}%" : "—";
            tbFreqDetailDC.Text      = points > 0 ? $"{dc:F4}" : "—";
            tbFreqDetailSNR.Text     = fundBin > 0 ? $"{snr:F1} dB" : "—";
            tbFreqDetailPoints.Text  = points > 0 ? $"{points}" : "—";
            tbFreqDetailRes.Text     = resHz > 0 ? $"{resHz:F2} Hz/bin" : "—";
            tbFreqDetailBw.Text      = sr > 0 ? $"0 ~ {sr / 2:F0} Hz" : "—";
            // 侧面板频率范围
            tbFreqRange.Text = sr > 0 ? $"0 ~ {sr / 2:F0} Hz" : (points > 0 ? $"Bin 0 ~ {points - 1}" : "—");
        }

        private void btnFreqDetailCopyData_Click(object sender, RoutedEventArgs e)
        {
            string csv = _plotVM.ExportFreqCsv();
            if (!string.IsNullOrEmpty(csv))
                Clipboard.SetText(csv);
        }

        private void btnFreqDetailCopyStats_Click(object sender, RoutedEventArgs e)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"基频: {tbFreqDetailFund.Text}");
            sb.AppendLine($"基频幅度: {tbFreqDetailFundMag.Text}");
            sb.AppendLine($"THD: {tbFreqDetailTHD.Text}");
            sb.AppendLine($"DC偏置: {tbFreqDetailDC.Text}");
            sb.AppendLine($"信噪比SNR: {tbFreqDetailSNR.Text}");
            Clipboard.SetText(sb.ToString());
        }

        private void btnFreqDetailCopyAll_Click(object sender, RoutedEventArgs e)
        {
            var sb = new System.Text.StringBuilder();
            string csv = _plotVM.ExportFreqCsv();
            if (!string.IsNullOrEmpty(csv))
            {
                sb.AppendLine("[频谱数据]");
                sb.AppendLine(csv);
                sb.AppendLine();
            }
            sb.AppendLine("[频域指标]");
            sb.AppendLine($"基频: {tbFreqDetailFund.Text}");
            sb.AppendLine($"基频幅度: {tbFreqDetailFundMag.Text}");
            sb.AppendLine($"THD: {tbFreqDetailTHD.Text}");
            sb.AppendLine($"DC偏置: {tbFreqDetailDC.Text}");
            sb.AppendLine($"信噪比SNR: {tbFreqDetailSNR.Text}");
            Clipboard.SetText(sb.ToString());
        }

        private void btnPlotCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string csv;
                string prefix;
                if (_isFreqDomain)
                {
                    csv = _plotVM.ExportFreqCsv();
                    prefix = "fft";
                }
                else
                {
                    csv = _plotVM.ExportCsv();
                    prefix = "plot";
                }
                if (string.IsNullOrEmpty(csv) || csv.IndexOf(',') < 0)
                {
                    LogSystem(string.Format(T("---- 无{0}数据可导出 ----"), T(_isFreqDomain ? "频谱" : "波形")));
                    return;
                }
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                };
                if (dlg.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(dlg.FileName, csv, Encoding.UTF8);
                    LogSystem(string.Format(T("---- 波形数据已导出到: {0} ----"), dlg.FileName));
                }
            }
            catch (Exception ex) { LogSystem(string.Format(T("---- 导出 CSV 失败: {0} ----"), ex.Message)); }
        }

        private void btnPlotFit_Click(object sender, RoutedEventArgs e)
        {
            if (_isFreqDomain)
                _plotVM.ResetFreqView();
            else
                _plotVM.ResetView();
        }

        // ═══ #8 调参工作台（Plot 底部抽屉）═══

        private bool _tuningDrawerOpen = false;
        private readonly Dictionary<string, TextBlock> _tuningValueLabels = new Dictionary<string, TextBlock>();

        private void btnTuningToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleTuningDrawer();
        }

        private void ToggleTuningDrawer()
        {
            _tuningDrawerOpen = !_tuningDrawerOpen;
            if (_tuningDrawerOpen)
            {
                RefreshTuningDrawer();
                // 先强制完成当前布局，再改 Visibility，避免 OxyPlot resize 时拿到非法尺寸
                panelPlot.UpdateLayout();
                panelTuningDrawer.Visibility = Visibility.Visible;
                splitterTuning.Visibility = Visibility.Visible;
                panelTuningDrawer.Height = 160;
                btnTuningToggle.LocText("▼ 收起");
                if (_plotVM != null) _plotVM.UseBackgroundRender = true;
            }
            else
            {
                panelTuningDrawer.Visibility = Visibility.Collapsed;
                splitterTuning.Visibility = Visibility.Collapsed;
                btnTuningToggle.LocText("▲ 调参");
                if (_plotVM != null) _plotVM.UseBackgroundRender = false;
            }
        }

        /// <summary>
        /// 轻量初始化 _sliderVM（不创建 UI），供调参抽屉使用。
        /// InitSliderPanel() 需要 slidersPanel 可见，这里只恢复 VM 数据。
        /// </summary>
        private void EnsureSliderVM()
        {
            if (_sliderVM != null) return;
            _sliderVM = new SliderPanelViewModel();
            if (_prefsData != null && _prefsData.TryGetValue("sliders", out var slidersObj)
                && slidersObj is List<object> rawList && rawList.Count > 0)
            {
                var list = new List<Dictionary<string, object>>();
                foreach (var item in rawList)
                    if (item is Dictionary<string, object> d) list.Add(d);
                if (list.Count > 0) _sliderVM.DeserializeSliders(list);
            }
        }

        /// <summary>
        /// 填充调参抽屉：仅显示与 Plot Series 同名的滑杆（名字交集），紧凑排列。
        /// </summary>
        private void RefreshTuningDrawer()
        {
            tuningSlidersPanel.Children.Clear();
            _tuningValueLabels.Clear();
            if (_plotVM == null) return;
            EnsureSliderVM();
            if (_sliderVM == null || _sliderVM.Sliders.Count == 0) return;

            var plotNames = new HashSet<string>(_plotVM.GetChannelNames());
            var matching = _sliderVM.Sliders.Where(s => plotNames.Contains(s.Name)).ToList();

            barTuningToggle.Visibility = matching.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (matching.Count == 0) return;

            foreach (var svm in matching)
            {
                var row = new Grid { Height = 32, Margin = new Thickness(0, 0, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });       // 名称
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 滑杆
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });       // 数值
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });       // -
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });       // +

                // 名称
                var nameTb = new TextBlock
                {
                    Text = svm.Name,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("TextPrimaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                Grid.SetColumn(nameTb, 0); row.Children.Add(nameTb);

                // 滑杆
                var slider = new Slider
                {
                    Minimum = svm.MinValue,
                    Maximum = svm.MaxValue,
                    Value = svm.Value,
                    SmallChange = svm.Step,
                    LargeChange = svm.Step * 10,
                    VerticalAlignment = VerticalAlignment.Center,
                    Style = (Style)FindResource("ColoredSliderStyle"),
                    Tag = svm,
                };
                slider.ValueChanged += (s, e2) =>
                {
                    var sl = s as Slider; var vm = sl?.Tag as SliderViewModel; if (vm == null) return;
                    double rounded = Math.Round(sl.Value / vm.Step) * vm.Step;
                    rounded = Math.Max(vm.MinValue, Math.Min(vm.MaxValue, rounded));
                    vm.Value = rounded;
                    if (_tuningValueLabels.TryGetValue(vm.Name, out var tb))
                        tb.Text = vm.DisplayValue;
                    // 节流发送（复用已有发送逻辑）
                    var now = DateTime.Now;
                    if (!_sliderLastSent.TryGetValue(vm.Name, out var last)
                        || (now - last).TotalMilliseconds >= vm.SendIntervalMs)
                    {
                        _sliderLastSent[vm.Name] = now;
                        SendSliderValue(vm);
                    }
                };
                // Q 弹（复用 Sliders.cs 的 SpringPress/SpringRelease）
                slider.PreviewMouseLeftButtonDown += (s, e2) =>
                {
                    var sl = s as Slider; sl?.ApplyTemplate();
                    var thumb = sl?.Template.FindName("Thumb", sl) as FrameworkElement;
                    if (thumb != null) SpringPress(thumb);
                };
                slider.PreviewMouseLeftButtonUp += (s, e2) =>
                {
                    var sl = s as Slider; sl?.ApplyTemplate();
                    var thumb = sl?.Template.FindName("Thumb", sl) as FrameworkElement;
                    if (thumb != null) SpringRelease(thumb);
                    var vm = (sl?.Tag as SliderViewModel); if (vm == null) return;
                    double rounded = Math.Round(sl.Value / vm.Step) * vm.Step;
                    rounded = Math.Max(vm.MinValue, Math.Min(vm.MaxValue, rounded));
                    sl.Value = rounded; vm.Value = rounded;
                    if (_tuningValueLabels.TryGetValue(vm.Name, out var tb2))
                        tb2.Text = vm.DisplayValue;
                };
                Grid.SetColumn(slider, 1); row.Children.Add(slider);

                // 数值
                var valTb = new TextBlock
                {
                    Text = svm.DisplayValue,
                    FontSize = 11,
                    FontFamily = new System.Windows.Media.FontFamily("Sarasa Mono SC, Consolas, Courier New"),
                    Foreground = (Brush)FindResource("TextSecondaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                _tuningValueLabels[svm.Name] = valTb;
                Grid.SetColumn(valTb, 2); row.Children.Add(valTb);

                // - 按钮
                var btnMinus = new Button
                {
                    Content = "−",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Width = 20, Height = 20,
                    Padding = new Thickness(0),
                    Style = (Style)FindResource("SecondaryButtonStyle"),
                    Tag = svm,
                };
                btnMinus.Click += (s, e2) =>
                {
                    var vm = (s as Button)?.Tag as SliderViewModel; if (vm == null) return;
                    double step = vm.Step > 0 ? vm.Step : 1;
                    vm.Value = Math.Max(vm.MinValue, vm.Value - step);
                    // 同步更新滑杆控件
                    UpdateTuningSliderPosition(vm);
                };
                Grid.SetColumn(btnMinus, 3); row.Children.Add(btnMinus);

                // + 按钮
                var btnPlus = new Button
                {
                    Content = "+",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Width = 20, Height = 20,
                    Padding = new Thickness(0),
                    Style = (Style)FindResource("SecondaryButtonStyle"),
                    Tag = svm,
                };
                btnPlus.Click += (s, e2) =>
                {
                    var vm = (s as Button)?.Tag as SliderViewModel; if (vm == null) return;
                    double step = vm.Step > 0 ? vm.Step : 1;
                    vm.Value = Math.Min(vm.MaxValue, vm.Value + step);
                    UpdateTuningSliderPosition(vm);
                };
                Grid.SetColumn(btnPlus, 4); row.Children.Add(btnPlus);

                tuningSlidersPanel.Children.Add(row);
            }
        }

        /// <summary>
        /// +/- 微调后同步滑杆控件位置、数值显示、发送。
        /// </summary>
        private void UpdateTuningSliderPosition(SliderViewModel vm)
        {
            // 更新数值显示
            if (_tuningValueLabels.TryGetValue(vm.Name, out var tb))
                tb.Text = vm.DisplayValue;
            // 找到对应的 Slider 控件并更新位置
            foreach (var child in tuningSlidersPanel.Children)
            {
                if (child is Grid row && row.Children.Count >= 2)
                {
                    var slider = row.Children[1] as Slider;
                    if (slider != null && (slider.Tag as SliderViewModel) == vm)
                    {
                        slider.Value = vm.Value;
                        break;
                    }
                }
            }
            // 立即发送
            SendSliderValue(vm);
            _sliderLastSent[vm.Name] = DateTime.Now;
        }

        private void UpdatePlotHud()
        {
            if (!_plotVM.ShowValueHud || _plotVM.LatestValues.Count == 0) return;
            plotHud.Visibility = Visibility.Visible;
            plotHudPanel.Children.Clear();
            foreach (var kv in _plotVM.LatestValues)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
                row.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 8, Height = 8,
                    Fill = (Brush)FindResource("PrimaryBrush"),
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                });
                row.Children.Add(new TextBlock
                {
                    Text = kv.Key + ": ",
                    Foreground = (Brush)FindResource("TextMutedBrush"),
                    FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                });
                row.Children.Add(new TextBlock
                {
                    Text = kv.Value.ToString("F4"),
                    Foreground = (Brush)FindResource("TextPrimaryBrush"),
                    FontSize = 12, FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                plotHudPanel.Children.Add(row);
            }
        }

        // Phase 3 rightPlot handlers

        private void chkPlotYAuto_Changed(object sender, RoutedEventArgs e)
        {
            bool isAuto = chkPlotYAuto.IsChecked == true;
            _plotVM.YAxisAutoRange = isAuto;
            tbPlotYMin.IsEnabled = !isAuto;
            tbPlotYMax.IsEnabled = !isAuto;
            if (isAuto) _plotVM.RecalcYAxis();
        }

        private void chkPlotMarkers_Changed(object sender, RoutedEventArgs e)
        {
            _plotVM.SetMarkers(chkPlotMarkers.IsChecked == true);
        }

        private void chkPlotLines_Changed(object sender, RoutedEventArgs e)
        {
            _plotVM.SetLines(chkPlotLines.IsChecked == true);
        }

        private void chkPlotValueHud_Changed(object sender, RoutedEventArgs e)
        {
            _plotVM.ShowValueHud = chkPlotValueHud.IsChecked == true;
            plotHud.Visibility = (_plotVM.ShowValueHud && _plotVM.LatestValues.Count > 0)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void cbPlotMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_rebuildingComboBox) return;
            switch (cbPlotMode.SelectedIndex)
            {
                case 0: _plotVM.DisplayMode = PlotDisplayMode.Scroll; break;
                case 1: _plotVM.DisplayMode = PlotDisplayMode.Sweep; break;
            }
        }

        private void tbPlotXRange_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(tbPlotXRange.Text, out int val) && val > 0 && val != _plotVM.MaxDataPoints)
            {
                _plotVM.MaxDataPoints = val;
                _plotVM.ApplyXAxisWindow();  // 调整 X 轴显示窗口，不删数据
            }
        }

        private void tbPlotYRange_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(tbPlotYMin.Text, out double min) &&
                double.TryParse(tbPlotYMax.Text, out double max) && max > min)
                _plotVM.SetYRange(min, max);
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            _searchPanel?.Open();
        }
    }
}
