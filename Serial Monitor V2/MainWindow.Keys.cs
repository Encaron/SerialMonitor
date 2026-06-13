using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace 串口助手
{
    public partial class MainWindow
    {
        // ——— 按键面板 ———
        private KeyPanelViewModel _keyVM;
        private List<KeyViewModel> _selectedKeys = new List<KeyViewModel>();
        private Dictionary<Button, KeyViewModel> _keyButtonMap = new Dictionary<Button, KeyViewModel>();
        private Dictionary<int, string> _groupNames = new Dictionary<int, string>();
        private int? _selectedModuleGroupId;

        // 二次点击确认
        private Button _confirmButton;
        private string _confirmOriginalText;
        private DispatcherTimer _confirmTimer;

        // ——— 初始化 ———
        private void InitKeyPanel()
        {
            if (_keyVM != null) return;
            _keyVM = new KeyPanelViewModel();

            // 恢复按键
            if (_prefsData != null && _prefsData.TryGetValue("keys", out var keysObj)
                && keysObj is List<object> rawList && rawList.Count > 0)
            {
                var keysList = new List<Dictionary<string, object>>();
                foreach (var item in rawList)
                    if (item is Dictionary<string, object> d) keysList.Add(d);
                if (keysList.Count > 0) _keyVM.DeserializeKeys(keysList);
            }

            // 恢复模块名
            _groupNames.Clear();
            if (_prefsData != null && _prefsData.TryGetValue("keyGroupNames", out var namesObj)
                && namesObj is Dictionary<string, object> nameDict)
                foreach (var kv in nameDict)
                    if (int.TryParse(kv.Key, out int gid) && kv.Value is string n) _groupNames[gid] = n;

            // 初始化下拉框
            foreach (var cb in new ComboBox[] { cbKeyPressMode, cbKeyReleaseMode, cbKeySendModeMulti, cbKeyReleaseModeMulti, cbModulePressMode, cbModuleReleaseMode })
            {
                cb.Items.Clear(); cb.Items.Add("文本"); cb.Items.Add("HEX"); cb.Items.Add("数据包"); cb.Items.Add("无");
                cb.SelectedIndex = 2;
            }

            InitColorPanels();
            InitModuleColorPanel();
            RefreshKeysUI();
                   }

        // ——— 颜色面板 ———
        private void InitColorPanels()
        {
            string[] colors = { "默认", "红色", "绿色", "蓝色", "黄色", "白色", "灰色" };
            foreach (var colorName in colors)
            {
                keysColorPanel.Children.Add(CreateColorChip(colorName, c => {
                    if (_selectedKeys.Count == 1) { _selectedKeys[0].Color = c; RefreshKeysUI(); RefreshKeysSidePanel(); }
                }));
                keysColorPanelMulti.Children.Add(CreateColorChip(colorName, c => {
                    foreach (var k in _selectedKeys) k.Color = c;
                    RefreshKeysUI(); RefreshKeysSidePanel();
                }));
            }
        }

        private bool _moduleColorPanelInited;
        private void InitModuleColorPanel()
        {
            if (_moduleColorPanelInited) return; _moduleColorPanelInited = true;
            string[] colors = { "默认", "红色", "绿色", "蓝色", "黄色", "白色", "灰色" };
            foreach (var c in colors)
                moduleColorPanel.Children.Add(CreateColorChip(c, cn => {
                    if (_selectedModuleGroupId == null) return;
                    int gid = _selectedModuleGroupId.Value;
                    foreach (var k in _keyVM.Keys.Where(k => k.GroupId == gid)) k.Color = cn;
                    RefreshKeysUI();
                }));
        }

        private Border CreateColorChip(string colorName, Action<string> onClick)
        {
            var isDark = isDarkTheme;
            string hex = KeyPanelViewModel.GetColorHex(colorName, isDark);
            Brush fillBrush = hex == null ? (Brush)FindResource("CardBgBrush")
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            var border = new Border {
                Width = 24, Height = 24, CornerRadius = new CornerRadius(4),
                Background = fillBrush, BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 6, 6),
                Cursor = Cursors.Hand, ToolTip = colorName, Tag = colorName,
            };
            border.MouseLeftButtonDown += (s, e) => onClick((string)((Border)s).Tag);
            return border;
        }

        // ——— 确保侧面板显示按键内容（而不是设置/绘图等） ———
        private void SwitchSidePanelToKeys() {
            if (_currentTab != "Keys") {
                tabKeys.IsChecked = true;  // 联动图标栏：取消设置图标的选中态，触发 TabContent_Checked → RefreshContentVisibility
            }
        }

        // ——— 编辑模式切换 ———
        private void btnKeysEdit_Click(object sender, RoutedEventArgs e) {
            InitKeyPanel(); _keyVM.IsEditMode = true; _selectedKeys.Clear(); _selectedModuleGroupId = null;
            keysToolbarNormal.Visibility = Visibility.Collapsed; keysToolbarEdit.Visibility = Visibility.Visible;
            RefreshKeysUI(); RefreshKeysSidePanel();
        }
        private void btnKeysDone_Click(object sender, RoutedEventArgs e) {
            CancelConfirm();
            _keyVM.IsEditMode = false; _selectedKeys.Clear(); _selectedModuleGroupId = null;
            keysToolbarNormal.Visibility = Visibility.Visible; keysToolbarEdit.Visibility = Visibility.Collapsed;
            RefreshKeysUI(); RefreshKeysSidePanel(); SaveKeysPrefs();        }

        // ——— 添加 / 清空 / 删除 ———
        private void btnKeysAdd_Click(object sender, RoutedEventArgs e) {
            InitKeyPanel();
            SwitchSidePanelToKeys();
            string name = "Key" + (_keyVM.Keys.Count + 1);
            var key = _keyVM.AddKey(name);
            if (!_groupNames.ContainsKey(key.GroupId)) _groupNames[key.GroupId] = "手动按键";
            _selectedModuleGroupId = null;
            RefreshKeysUI(); RefreshKeysSidePanel();        }
        private void btnKeysClearAll_Click(object sender, RoutedEventArgs e) {
            if (_keyVM == null || _keyVM.Keys.Count == 0) return;
            if (_confirmButton == btnKeysClearAll) {
                _keyVM.ClearAll(); _selectedKeys.Clear(); _selectedModuleGroupId = null;
                CancelConfirm(); RefreshKeysUI(); RefreshKeysSidePanel();            } else {
                StartConfirm(btnKeysClearAll, "⚠ 确认清空");
            }
        }
        // ——— 二次点击确认（替代弹窗，无提示音） ———
        private void StartConfirm(Button btn, string confirmText) {
            CancelConfirm();
            _confirmButton = btn;
            _confirmOriginalText = btn.Content?.ToString() ?? "";
            btn.Content = confirmText;
            _confirmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _confirmTimer.Tick += (s2, e2) => CancelConfirm();
            _confirmTimer.Start();
        }
        private void CancelConfirm() {
            if (_confirmButton != null) _confirmButton.Content = _confirmOriginalText;
            _confirmTimer?.Stop(); _confirmTimer = null;
            _confirmButton = null; _confirmOriginalText = null;
        }

        private void btnKeyDelete_Click(object sender, RoutedEventArgs e) {
            if (_selectedKeys.Count == 0) return;
            foreach (var key in _selectedKeys.ToList()) _keyVM.RemoveKey(key);
            _selectedKeys.Clear(); RefreshKeysUI(); RefreshKeysSidePanel();        }

        // ——— 键盘布局下拉 ———
        private void btnKeysLayout_Click(object sender, RoutedEventArgs e) {
            InitKeyPanel();
            var menu = new ContextMenu {
                PlacementTarget = sender as UIElement,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                MinWidth = 240,
            };
            AddLayoutItem(menu, "⌨", "QWERTY 键盘", "标准键位 · ⇧ 大小写切换",
                () => { var ks = _keyVM.CreateKeyboardLayout(); if (ks.Count > 0) _groupNames[ks[0].GroupId] = "QWERTY 键盘"; _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel(); });
            AddLayoutItem(menu, "🖱", "WASD 方向控制", "机器人/小车/云台移动",
                () => { var ks = _keyVM.CreateWASDLayout(); if (ks.Count > 0) _groupNames[ks[0].GroupId] = "WASD 方向"; _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel(); });
            AddLayoutItem(menu, "⬆", "方向键", "↑ ↓ ← → 十字排列",
                () => { var ks = _keyVM.CreateDirectionalLayout(); if (ks.Count > 0) _groupNames[ks[0].GroupId] = "方向键"; _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel(); });
            AddLayoutSeparator(menu);
            AddLayoutItem(menu, "🔢", "数字键盘", "数字 0-9 · 运算符 · Enter",
                () => { var ks = _keyVM.CreateNumpadLayout(); if (ks.Count > 0) _groupNames[ks[0].GroupId] = "数字键盘"; _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel(); });
            AddLayoutItem(menu, "⚙", "F1 — F8 功能键", "模式切换 / 调试控制",
                () => { var ks = _keyVM.CreateFunctionKeysLayout(); if (ks.Count > 0) _groupNames[ks[0].GroupId] = "功能键"; _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel(); });
            AddLayoutItem(menu, "🔘", "逻辑开关对", "ON/OFF · START/STOP · OPEN/CLOSE",
                () => { var ks = _keyVM.CreateSwitchPairsLayout(); if (ks.Count > 0) _groupNames[ks[0].GroupId] = "逻辑开关"; _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel(); });
            menu.IsOpen = true;
        }
        private void AddLayoutItem(ContextMenu menu, string icon, string title, string desc, Action onClick) {
            var header = new DockPanel { Margin = new Thickness(2, 0, 2, 0) };
            var iconTb = new TextBlock { Text = icon, FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            DockPanel.SetDock(iconTb, Dock.Left);
            header.Children.Add(iconTb);
            var textStack = new StackPanel();
            textStack.Children.Add(new TextBlock { Text = title, FontSize = 12, Foreground = (Brush)FindResource("TextPrimaryBrush") });
            textStack.Children.Add(new TextBlock { Text = desc, FontSize = 10, Foreground = (Brush)FindResource("TextMutedBrush"), Margin = new Thickness(0, 1, 0, 0) });
            header.Children.Add(textStack);
            var item = new MenuItem { Header = header, Padding = new Thickness(8, 7, 12, 7) };
            item.Click += (s, a) => onClick();
            menu.Items.Add(item);
        }
        private static void AddLayoutSeparator(ContextMenu menu) {
            menu.Items.Add(new Separator { Margin = new Thickness(8, 3, 8, 3) });
        }

        // ——— Ctrl+A 全选 ———
        private void KeysPanel_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (!_keyVM.IsEditMode) return;
            if (e.Key == Key.A && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))) {
                SwitchSidePanelToKeys();
                _selectedKeys.Clear(); _selectedKeys.AddRange(_keyVM.Keys.Where(k => !k.IsShiftToggle));
                _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel(); e.Handled = true;
            }
        }

        // ═══════════════════════════════════════
        //  按键 UI 刷新
        // ═══════════════════════════════════════

        private void RefreshKeysUI()
        {
            if (_keyVM == null) return;
            keysPanel.Children.Clear(); _keyButtonMap.Clear();
            bool hasKeys = _keyVM.Keys.Count > 0;
            bool isEdit = _keyVM.IsEditMode;
            keysEmptyHintNormal.Visibility = (!hasKeys && !isEdit) ? Visibility.Visible : Visibility.Collapsed;
            keysEmptyHintEdit.Visibility   = (!hasKeys && isEdit)   ? Visibility.Visible : Visibility.Collapsed;
            if (!hasKeys) return;

            var groups = _keyVM.Keys.GroupBy(k => k.GroupId).OrderBy(g => g.Key);
            foreach (var group in groups)
            {
                var groupKeys = group.ToList();
                int gid = group.Key;
                string groupName = _groupNames.TryGetValue(gid, out var n) && !string.IsNullOrEmpty(n) ? n : ("模块 " + gid);

                var container = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // 标题栏
                var titleBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                var nameLabel = new TextBlock {
                    Text = (isEdit ? "✎ " : "") + groupName, FontSize = 10, FontWeight = FontWeights.SemiBold,
                    Foreground = isEdit ? (Brush)FindResource("PrimaryBrush") : (Brush)FindResource("TextMutedBrush"),
                    Cursor = isEdit ? Cursors.Hand : Cursors.Arrow, VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = isEdit ? "点击在侧面板编辑此模块（名字/按下发送/松开发送/颜色）" : groupName,
                };
                int capturedGid = gid;
                nameLabel.MouseLeftButtonDown += (s2, e2) => {
                    if (!isEdit) return;
                    SwitchSidePanelToKeys();
                    _selectedModuleGroupId = capturedGid; _selectedKeys.Clear();
                    RefreshKeysUI(); RefreshKeysSidePanel();
                };

                // 模式标签
                var gModes = groupKeys.GroupBy(k => k.PressSendMode).OrderByDescending(g2 => g2.Count()).Select(g2 => g2.Key).ToList();
                string gMode = gModes.FirstOrDefault() ?? "数据包";
                var modeTag = new TextBlock { Text = " [↓" + gMode + " ↑" + gMode + "]", FontSize = 9,
                    Foreground = (Brush)FindResource("PrimaryBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0) };

                titleBar.Children.Add(nameLabel); titleBar.Children.Add(modeTag);
                Grid.SetRow(titleBar, 0); container.Children.Add(titleBar);

                // 按键内容
                int maxRow = groupKeys.Max(k => k.LayoutY), maxCol = groupKeys.Max(k => k.LayoutX);
                FrameworkElement keysContent;
                if (maxRow == 0 && maxCol < 12) {
                    var wrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 0, 0, 0) };
                    foreach (var kv in groupKeys.OrderBy(k => k.LayoutX)) { var btn = CreateKeyButton(kv, isEdit); wrap.Children.Add(btn); _keyButtonMap[btn] = kv; }
                    keysContent = wrap;
                } else if (maxRow == 1 && maxCol == 2 && groupKeys.All(k => k.LayoutY == 0 || k.LayoutY == 1)) {
                    keysContent = BuildFixedGrid(groupKeys, isEdit, cols: 3, rows: 2, colWidth: 56, rowHeight: 52);
                } else if (maxRow == 2 && maxCol == 2 && groupKeys.Count >= 5 && groupKeys.Count <= 8) {
                    keysContent = BuildFixedGrid(groupKeys, isEdit, cols: 3, rows: 3, colWidth: 64, rowHeight: 48);
                } else if (maxRow >= 3 && maxCol >= 3 && groupKeys.Any(k => k.Name == "Enter")) {
                    keysContent = BuildNumpadGrid(groupKeys, isEdit);
                } else {
                    var stack = new StackPanel { Margin = new Thickness(2, 0, 0, 0) };
                    BuildRowBasedLayoutInto(stack, groupKeys, isEdit);
                    keysContent = stack;
                }

                // 模块边框（编辑模式加左侧强调色条，暗色模式用淡暗色底）
                var border = new Border {
                    BorderBrush = (Brush)FindResource("CardBorderBrush"), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 4, 8, 8),
                    Background = isDarkTheme
                        ? (Brush)new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x32))
                        : (Brush)FindResource("SecondaryHoverBgBrush"),
                    Child = keysContent,
                };
                if (isEdit)
                {
                    // 左侧强调色条 + 加厚左边框
                    border.BorderThickness = new Thickness(3, 1, 1, 1);
                    border.BorderBrush = new SolidColorBrush(isDarkTheme
                        ? Color.FromRgb(0x0E, 0x63, 0x9C) : Color.FromRgb(0x00, 0x78, 0xD4));
                }
                Grid.SetRow(border, 1); container.Children.Add(border);
                keysPanel.Children.Add(container);
            }
        }

        private Button CreateKeyButton(KeyViewModel keyVM, bool isEditMode)
        {
            var btn = new Button {
                Content = keyVM.Name, Width = keyVM.Width, Height = keyVM.Height,
                Margin = new Thickness(0, 0, 6, 6), FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"), Padding = new Thickness(4, 0, 4, 0), Tag = keyVM,
            };
            var isDark = isDarkTheme;
            string hex = KeyPanelViewModel.GetColorHex(keyVM.Color, isDark);
            Brush bgBrush;
            if (hex != null) {
                bgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                btn.Foreground = Brushes.White;
            } else { bgBrush = (Brush)FindResource("CardBgBrush"); btn.Foreground = (Brush)FindResource("TextPrimaryBrush"); }
            btn.Background = bgBrush;
            btn.BorderBrush = (Brush)FindResource("CardBorderBrush");
            btn.BorderThickness = new Thickness(1);
            btn.Cursor = Cursors.Hand;

            // 自锁按下状态 或 STM32 down 反馈 → 蓝色高亮
            bool isActiveDown = (keyVM.IsSelfLock && keyVM.IsPressed) || (!keyVM.IsSelfLock && keyVM.IsDown);
            if (isActiveDown && !keyVM.IsShiftToggle) {
                var accent = isDark ? Color.FromRgb(0x0E, 0x63, 0x9C) : Color.FromRgb(0x00, 0x78, 0xD4);
                btn.BorderBrush = new SolidColorBrush(accent); btn.BorderThickness = new Thickness(2);
                btn.Background = new SolidColorBrush(isDark ? Color.FromRgb(0x1A, 0x3A, 0x5C) : Color.FromRgb(0xDE, 0xEC, 0xFC));
            }

            // 自锁标记：按钮文字加下划线
            if (keyVM.IsSelfLock) btn.FontStyle = FontStyles.Italic;

            // Shift 切换键
            if (keyVM.IsShiftToggle) {
                btn.FontWeight = FontWeights.Bold;
                btn.Background = (Brush)FindResource("SecondaryHoverBgBrush");
                if (_keyVM.ShiftActive) {
                    var accent = isDark ? Color.FromRgb(0x0E, 0x63, 0x9C) : Color.FromRgb(0x00, 0x78, 0xD4);
                    btn.BorderBrush = new SolidColorBrush(accent); btn.BorderThickness = new Thickness(2);
                }
            }

            if (isEditMode) {
                if (_selectedKeys.Contains(keyVM)) {
                    var accent = isDark ? Color.FromRgb(0x0E, 0x63, 0x9C) : Color.FromRgb(0x00, 0x78, 0xD4);
                    btn.BorderBrush = new SolidColorBrush(accent); btn.BorderThickness = new Thickness(2);
                    // 自定义颜色的按键保留原背景，仅用边框表示选中；默认颜色才用选中底色
                    if (keyVM.Color == "默认" || string.IsNullOrEmpty(keyVM.Color)) {
                        btn.Background = new SolidColorBrush(isDark ? Color.FromRgb(0x1A, 0x2E, 0x4A) : Color.FromRgb(0xE6, 0xF0, 0xFA));
                    }
                }
                btn.Click += KeyButtonEdit_Click;
                btn.MouseRightButtonDown += KeyButtonEdit_RightClick;
            } else {
                btn.Click += KeyButtonSend_Click;
            }

            // 按下动画：缩放反馈（物理点击感）
            btn.RenderTransform = new ScaleTransform(1.0, 1.0);
            btn.RenderTransformOrigin = new Point(0.5, 0.5);
            btn.PreviewMouseLeftButtonDown += KeyButton_PressAnimation;
            btn.PreviewMouseLeftButtonUp += KeyButton_ReleaseAnimation;
            return btn;
        }

        private Grid BuildFixedGrid(List<KeyViewModel> keys, bool isEdit, int cols, int rows, double colWidth, double rowHeight) {
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 4) };
            for (int c = 0; c < cols; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(colWidth) });
            for (int r = 0; r < rows; r++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowHeight) });
            foreach (var kv in keys) {
                var btn = CreateKeyButton(kv, isEdit);
                btn.Margin = new Thickness(0); btn.HorizontalAlignment = HorizontalAlignment.Center; btn.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetRow(btn, kv.LayoutY); Grid.SetColumn(btn, kv.LayoutX);
                grid.Children.Add(btn); _keyButtonMap[btn] = kv;
            }
            return grid;
        }
        private Grid BuildNumpadGrid(List<KeyViewModel> keys, bool isEdit) {
            int maxRow = keys.Max(k => k.LayoutY);
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 4) };
            // 固定列宽/行高，消除 Grid.Auto 导致的间距不一致
            for (int c = 0; c < 4; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
            for (int r = 0; r <= maxRow; r++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
            foreach (var kv in keys) {
                var btn = CreateKeyButton(kv, isEdit);
                // 去掉按钮自带的 margin，由 Grid 统一控制间距
                btn.Margin = new Thickness(0, 0, 0, 0);
                btn.HorizontalAlignment = HorizontalAlignment.Center;
                btn.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetRow(btn, kv.LayoutY); Grid.SetColumn(btn, kv.LayoutX);
                grid.Children.Add(btn); _keyButtonMap[btn] = kv;
            }
            return grid;
        }
        private void BuildRowBasedLayoutInto(Panel parent, List<KeyViewModel> keys, bool isEdit) {
            var rows = keys.GroupBy(k => k.LayoutY).OrderBy(g => g.Key);
            double[] stagger = { 0, 10, 16, 24 };
            foreach (var row in rows) {
                var rp = new WrapPanel { Orientation = Orientation.Horizontal,
                    Margin = new Thickness(row.Key < stagger.Length ? stagger[row.Key] : 0, 0, 0, 2) };
                foreach (var kv in row.OrderBy(k => k.LayoutX)) { var btn = CreateKeyButton(kv, isEdit); rp.Children.Add(btn); _keyButtonMap[btn] = kv; }
                parent.Children.Add(rp);
            }
        }

        // ═══════════════════════════════════════
        //  发送逻辑（自锁 + 按下/松开分离）
        // ═══════════════════════════════════════

        private void KeyButtonSend_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button; if (btn == null) return;
            var keyVM = btn.Tag as KeyViewModel; if (keyVM == null || keyVM.IsShiftToggle) return;
            if (keyVM.IsShiftToggle)
            {
                // Shift 切换键：在正常模式也可用
                PulseElement(btn);
                ToggleShift(keyVM.GroupId);
                RefreshKeysUI();
                return;
            }

            if (_session == null || !_session.IsOpen) return;

            // 动画反馈
            PulseElement(btn);
            ShowKeySendFeedback(keyVM);

            if (keyVM.IsSelfLock)
            {
                // 自锁模式：切换按下/松开
                if (keyVM.IsPressed)
                {
                    // 松开：发送松开发送内容
                    string releaseContent = keyVM.GetReleaseContent();
                    if (!string.IsNullOrEmpty(releaseContent)) SendRaw(releaseContent, appendLineEnding: true);
                    keyVM.IsPressed = false;
                }
                else
                {
                    // 按下：发送按下内容
                    string pressContent = keyVM.GetPressContent();
                    if (!string.IsNullOrEmpty(pressContent)) SendRaw(pressContent, appendLineEnding: true);
                    keyVM.IsPressed = true;
                }
            }
            else
            {
                // 非自锁：先发按下，立即发松开
                string pressContent = keyVM.GetPressContent();
                string releaseContent = keyVM.GetReleaseContent();
                if (!string.IsNullOrEmpty(pressContent)) SendRaw(pressContent, appendLineEnding: true);
                if (!string.IsNullOrEmpty(releaseContent)) SendRaw(releaseContent, appendLineEnding: true);
            }
            RefreshKeysUI();
        }

        /// <summary>切换大小写：翻转 ShiftActive，更新同组所有字母键的显示名和文本/HEX模式发送值</summary>
        private void ToggleShift(int groupId)
        {
            _keyVM.ShiftActive = !_keyVM.ShiftActive;
            foreach (var k in _keyVM.Keys.Where(k => k.GroupId == groupId && !k.IsShiftToggle))
            {
                if (k.Name.Length == 1 && char.IsLetter(k.Name[0]))
                {
                    k.Name = _keyVM.ShiftActive
                        ? k.Name.ToUpperInvariant()
                        : k.Name.ToLowerInvariant();
                    // 文本/HEX 模式下同步更新发送值，数据包模式自动从 Name 生成无需管
                    if (k.PressSendMode == "文本" || k.PressSendMode == "HEX")
                        k.PressSendValue = k.Name;
                    if (k.ReleaseSendMode == "文本" || k.ReleaseSendMode == "HEX")
                        k.ReleaseSendValue = k.Name;
                }
            }
        }

        private void ShowKeySendFeedback(KeyViewModel keyVM)
        {
            tbKeyFeedbackName.Text = keyVM.IsSelfLock
                ? string.Format("{0} （自锁·{1}）", keyVM.Name, keyVM.IsPressed ? "已按下" : "已松开")
                : keyVM.Name;
            string press = keyVM.GetPressContent();
            string release = keyVM.GetReleaseContent();
            if (string.IsNullOrEmpty(press) && string.IsNullOrEmpty(release))
                tbKeyFeedbackValue.Text = "（该按键没有配置发送内容）";
            else if (!string.IsNullOrEmpty(press) && !string.IsNullOrEmpty(release))
                tbKeyFeedbackValue.Text = string.Format("↓ {0} ↑ {1}", Truncate(press, 60), Truncate(release, 60));
            else if (!string.IsNullOrEmpty(press))
                tbKeyFeedbackValue.Text = string.Format("↓ {0}", Truncate(press, 80));
            else
                tbKeyFeedbackValue.Text = string.Format("↑ {0}", Truncate(release, 80));
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "…";

        // ═══════════════════════════════════════
        //  编辑模式交互
        // ═══════════════════════════════════════

        private void KeyButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button; if (btn == null) return;
            var keyVM = btn.Tag as KeyViewModel; if (keyVM == null) return;
            SwitchSidePanelToKeys();
            _selectedModuleGroupId = null;
            bool ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            if (keyVM.IsShiftToggle) {
                ToggleShift(keyVM.GroupId);
                RefreshKeysUI(); RefreshKeysSidePanel(); return;
            }

            if (ctrl) { if (_selectedKeys.Contains(keyVM)) _selectedKeys.Remove(keyVM); else _selectedKeys.Add(keyVM); }
            else { _selectedKeys.Clear(); _selectedKeys.Add(keyVM); }
            RefreshKeysUI(); RefreshKeysSidePanel();
        }

        private void KeyButtonEdit_RightClick(object sender, MouseButtonEventArgs e) {
            var btn = sender as Button; if (btn == null) return;
            var keyVM = btn.Tag as KeyViewModel; if (keyVM == null || keyVM.IsShiftToggle) return;
            SwitchSidePanelToKeys();
            _selectedKeys.Clear(); _keyVM.RemoveKey(keyVM); RefreshKeysUI(); RefreshKeysSidePanel();        }

        // ——— 按键按下/松开缩放动画 ———
        private void KeyButton_PressAnimation(object sender, MouseButtonEventArgs e)
        {
            var btn = sender as Button; if (btn == null) return;
            var scale = btn.RenderTransform as ScaleTransform;
            if (scale != null) { scale.ScaleX = 0.90; scale.ScaleY = 0.90; }
        }
        private void KeyButton_ReleaseAnimation(object sender, MouseButtonEventArgs e)
        {
            var btn = sender as Button; if (btn == null) return;
            var scale = btn.RenderTransform as ScaleTransform;
            if (scale != null) { scale.ScaleX = 1.0; scale.ScaleY = 1.0; }
        }

        // ═══════════════════════════════════════
        //  侧面板刷新
        // ═══════════════════════════════════════

        private void RefreshKeysSidePanel()
        {
            if (_keyVM == null) return;
            bool isEdit = _keyVM.IsEditMode;
            int count = _selectedKeys.Count;
            bool hasModule = _selectedModuleGroupId.HasValue;

            rightKeysSentFeedback.Visibility = Visibility.Collapsed;
            rightKeysNoSelection.Visibility  = Visibility.Collapsed;
            rightKeysSingleSelect.Visibility = Visibility.Collapsed;
            rightKeysMultiSelect.Visibility  = Visibility.Collapsed;
            rightKeysModuleSettings.Visibility = Visibility.Collapsed;

            if (!isEdit) { rightKeysSentFeedback.Visibility = Visibility.Visible; _selectedModuleGroupId = null; return; }
            if (hasModule) { ShowModuleSettings(_selectedModuleGroupId.Value); return; }
            if (count == 0) { rightKeysNoSelection.Visibility = Visibility.Visible; return; }
            if (count > 1) {
                rightKeysMultiSelect.Visibility = Visibility.Visible;
                tbKeysSelectedCount.Text = string.Format("已选 {0} 个按键", count);
                var first = _selectedKeys[0];
                bool samePress = _selectedKeys.All(k => k.PressSendMode == first.PressSendMode);
                bool sameRelease = _selectedKeys.All(k => k.ReleaseSendMode == first.ReleaseSendMode);
                cbKeySendModeMulti.SelectedItem = samePress ? first.PressSendMode : null;
                cbKeyReleaseModeMulti.SelectedItem = sameRelease ? first.ReleaseSendMode : null;
                return;
            }

            // 单选
            var key = _selectedKeys[0];
            bool isLayoutKey = key.GroupId != KeyPanelViewModel.ManualGroupId;
            rightKeysSingleSelect.Visibility = Visibility.Visible;
            tbKeyName.Text = key.Name;
            chkKeySelfLock.IsChecked = key.IsSelfLock;
            cbKeyPressMode.SelectedItem = key.PressSendMode;
            tbKeyPressValue.Text = key.PressSendValue;
            cbKeyReleaseMode.SelectedItem = key.ReleaseSendMode;
            tbKeyReleaseValue.Text = key.ReleaseSendValue;
            // 模块布局键不可调大小
            tbKeyWidth.Text = key.Width.ToString();
            tbKeyWidth.IsEnabled = !isLayoutKey;
            tbKeyHeight.Text = key.Height.ToString();
            tbKeyHeight.IsEnabled = !isLayoutKey;
        }

        // ——— 单选属性编辑 ———
        private void tbKeyName_LostFocus(object sender, RoutedEventArgs e) { if (_selectedKeys.Count == 1) { _selectedKeys[0].Name = tbKeyName.Text; RefreshKeysUI(); } }
        private void chkKeySelfLock_Changed(object sender, RoutedEventArgs e) { if (_selectedKeys.Count == 1) { _selectedKeys[0].IsSelfLock = chkKeySelfLock.IsChecked == true; _selectedKeys[0].IsPressed = false; RefreshKeysUI(); } }
        private void cbKeyPressMode_Changed(object sender, SelectionChangedEventArgs e) { if (_selectedKeys.Count == 1 && cbKeyPressMode.SelectedItem != null) _selectedKeys[0].PressSendMode = cbKeyPressMode.SelectedItem.ToString(); }
        private void tbKeyPressValue_LostFocus(object sender, RoutedEventArgs e) { if (_selectedKeys.Count == 1) _selectedKeys[0].PressSendValue = tbKeyPressValue.Text; }
        private void cbKeyReleaseMode_Changed(object sender, SelectionChangedEventArgs e) { if (_selectedKeys.Count == 1 && cbKeyReleaseMode.SelectedItem != null) _selectedKeys[0].ReleaseSendMode = cbKeyReleaseMode.SelectedItem.ToString(); }
        private void tbKeyReleaseValue_LostFocus(object sender, RoutedEventArgs e) { if (_selectedKeys.Count == 1) _selectedKeys[0].ReleaseSendValue = tbKeyReleaseValue.Text; }
        private void tbKeyWidth_LostFocus(object sender, RoutedEventArgs e) { if (_selectedKeys.Count == 1) { if (double.TryParse(tbKeyWidth.Text, out double w) && w >= 20 && w <= 400) { _selectedKeys[0].Width = w; RefreshKeysUI(); } else tbKeyWidth.Text = _selectedKeys[0].Width.ToString(); } }
        private void tbKeyHeight_LostFocus(object sender, RoutedEventArgs e) { if (_selectedKeys.Count == 1) { if (double.TryParse(tbKeyHeight.Text, out double h) && h >= 20 && h <= 400) { _selectedKeys[0].Height = h; RefreshKeysUI(); } else tbKeyHeight.Text = _selectedKeys[0].Height.ToString(); } }

        // ——— 多选批量编辑 ———
        private void cbKeySendModeMulti_Changed(object sender, SelectionChangedEventArgs e) { if (_selectedKeys.Count <= 1 || cbKeySendModeMulti.SelectedItem == null) return; string m = cbKeySendModeMulti.SelectedItem.ToString(); foreach (var k in _selectedKeys) k.PressSendMode = m; }
        private void cbKeyReleaseModeMulti_Changed(object sender, SelectionChangedEventArgs e) { if (_selectedKeys.Count <= 1 || cbKeyReleaseModeMulti.SelectedItem == null) return; string m = cbKeyReleaseModeMulti.SelectedItem.ToString(); foreach (var k in _selectedKeys) k.ReleaseSendMode = m; }

        // ——— 模块设置 ———
        private void ShowModuleSettings(int gid) {
            rightKeysModuleSettings.Visibility = Visibility.Visible;
            tbModuleName.Text = _groupNames.TryGetValue(gid, out var n) ? n : ("模块 " + gid);
            var gk = _keyVM.Keys.Where(k => k.GroupId == gid).ToList();
            var pressMode = gk.GroupBy(k => k.PressSendMode).OrderByDescending(g2 => g2.Count()).Select(g2 => g2.Key).FirstOrDefault() ?? "数据包";
            var releaseMode = gk.GroupBy(k => k.ReleaseSendMode).OrderByDescending(g2 => g2.Count()).Select(g2 => g2.Key).FirstOrDefault() ?? "数据包";
            cbModulePressMode.SelectedItem = pressMode;
            cbModuleReleaseMode.SelectedItem = releaseMode;
        }
        private void tbModuleName_LostFocus(object sender, RoutedEventArgs e) { if (_selectedModuleGroupId == null) return; string nn = tbModuleName.Text?.Trim(); if (!string.IsNullOrEmpty(nn)) { _groupNames[_selectedModuleGroupId.Value] = nn; RefreshKeysUI(); } }
        private void cbModulePressMode_Changed(object sender, SelectionChangedEventArgs e) {
            if (_selectedModuleGroupId == null || cbModulePressMode.SelectedItem == null) return;
            string m = cbModulePressMode.SelectedItem.ToString(); int gid = _selectedModuleGroupId.Value;
            foreach (var k in _keyVM.Keys.Where(k => k.GroupId == gid))
            { k.PressSendMode = m; if (m == "文本" && string.IsNullOrEmpty(k.PressSendValue)) k.PressSendValue = k.Name; }
            RefreshKeysUI();
        }
        private void cbModuleReleaseMode_Changed(object sender, SelectionChangedEventArgs e) {
            if (_selectedModuleGroupId == null || cbModuleReleaseMode.SelectedItem == null) return;
            string m = cbModuleReleaseMode.SelectedItem.ToString(); int gid = _selectedModuleGroupId.Value;
            foreach (var k in _keyVM.Keys.Where(k => k.GroupId == gid))
            { k.ReleaseSendMode = m; if (m == "文本" && string.IsNullOrEmpty(k.ReleaseSendValue)) k.ReleaseSendValue = k.Name; }
            RefreshKeysUI();
        }
        // ── 模块按下/松开值快捷操作 ──
        private void ModulePressValue_Click(object sender, RoutedEventArgs e) {
            if (_selectedModuleGroupId == null) return;
            string tag = (sender as Button)?.Tag?.ToString(); if (tag == null) return;
            int gid = _selectedModuleGroupId.Value;
            foreach (var k in _keyVM.Keys.Where(k => k.GroupId == gid && !k.IsShiftToggle))
                ApplyValueTag(k, tag, isPress: true);
            RefreshKeysUI(); RefreshKeysSidePanel();
        }
        private void ModuleReleaseValue_Click(object sender, RoutedEventArgs e) {
            if (_selectedModuleGroupId == null) return;
            string tag = (sender as Button)?.Tag?.ToString(); if (tag == null) return;
            int gid = _selectedModuleGroupId.Value;
            foreach (var k in _keyVM.Keys.Where(k => k.GroupId == gid && !k.IsShiftToggle))
                ApplyValueTag(k, tag, isPress: false);
            RefreshKeysUI(); RefreshKeysSidePanel();
        }
        private static void ApplyValueTag(KeyViewModel k, string tag, bool isPress)
        {
            switch (tag)
            {
                case "null":
                    if (isPress) k.PressSendMode = "无"; else k.ReleaseSendMode = "无";
                    break;
                case "name":
                    if (isPress) { k.PressSendMode = "文本"; k.PressSendValue = k.Name; }
                    else        { k.ReleaseSendMode = "文本"; k.ReleaseSendValue = k.Name; }
                    break;
                default: // up, down, on, off
                    if (isPress) { k.PressSendMode = "文本"; k.PressSendValue = tag; }
                    else        { k.ReleaseSendMode = "文本"; k.ReleaseSendValue = tag; }
                    break;
            }
        }
        private void btnModuleGenRelease_Click(object sender, RoutedEventArgs e) {
            if (_selectedModuleGroupId == null) return; int gid = _selectedModuleGroupId.Value;
            foreach (var k in _keyVM.Keys.Where(k => k.GroupId == gid && !k.IsShiftToggle))
            {
                k.ReleaseSendMode = "数据包";
                k.ReleaseSendValue = "";  // 默认=up
            }
            RefreshKeysUI(); RefreshKeysSidePanel();
        }
        private void btnModuleDelete_Click(object sender, RoutedEventArgs e) {
            if (_selectedModuleGroupId == null) return;
            if (_confirmButton == btnModuleDelete) {
                int gid = _selectedModuleGroupId.Value;
                var mk = _keyVM.Keys.Where(k => k.GroupId == gid).ToList(); if (mk.Count == 0) return;
                foreach (var k in mk) _keyVM.RemoveKey(k); _groupNames.Remove(gid);
                _selectedModuleGroupId = null; _selectedKeys.Clear();
                CancelConfirm(); RefreshKeysUI(); RefreshKeysSidePanel();            } else {
                StartConfirm(btnModuleDelete, "⚠ 确认删除");
            }
        }

        // ═══════════════════════════════════════
        //  协议处理 & 持久化
        // ═══════════════════════════════════════

        private void HandleKeyMessage(string name, string state) {
            InitKeyPanel(); bool isDown = state == "down"; _keyVM.SetKeyState(name, isDown);
            Dispatcher.InvokeAsync(() => { RefreshKeysUI(); });
        }

        private void SaveKeysPrefs() {
            if (_keyVM == null || _prefsData == null) return;
            var arr = new System.Collections.ArrayList();
            foreach (var d in _keyVM.SerializeKeys()) arr.Add(d);
            _prefsData["keys"] = arr;
            var nameDict = new Dictionary<string, object>();
            foreach (var kv in _groupNames) nameDict[kv.Key.ToString()] = kv.Value;
            _prefsData["keyGroupNames"] = nameDict;
            _prefs.Save(_prefsData);
        }
    }
}
