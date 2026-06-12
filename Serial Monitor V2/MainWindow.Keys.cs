using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

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

        // ——— 初始化 ———
        private void InitKeyPanel()
        {
            if (_keyVM != null) return;
            _keyVM = new KeyPanelViewModel();

            // 恢复按键
            if (_prefsData != null && _prefsData.TryGetValue("keys", out var keysObj)
                && keysObj is System.Collections.ArrayList arr && arr.Count > 0)
            {
                var keysList = new List<Dictionary<string, object>>();
                foreach (var item in arr)
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

        // ——— 编辑模式切换 ———
        private void btnKeysEdit_Click(object sender, RoutedEventArgs e) {
            InitKeyPanel(); _keyVM.IsEditMode = true; _selectedKeys.Clear(); _selectedModuleGroupId = null;
            keysToolbarNormal.Visibility = Visibility.Collapsed; keysToolbarEdit.Visibility = Visibility.Visible;
            RefreshKeysUI(); RefreshKeysSidePanel();
        }
        private void btnKeysDone_Click(object sender, RoutedEventArgs e) {
            _keyVM.IsEditMode = false; _selectedKeys.Clear(); _selectedModuleGroupId = null;
            keysToolbarNormal.Visibility = Visibility.Visible; keysToolbarEdit.Visibility = Visibility.Collapsed;
            RefreshKeysUI(); RefreshKeysSidePanel(); SaveKeysPrefs();
        }

        // ——— 添加 / 清空 / 删除 ———
        private void btnKeysAdd_Click(object sender, RoutedEventArgs e) {
            InitKeyPanel();
            string name = "Key" + (_keyVM.Keys.Count + 1);
            var key = _keyVM.AddKey(name);
            if (!_groupNames.ContainsKey(key.GroupId)) _groupNames[key.GroupId] = "手动按键";
            _selectedModuleGroupId = null;
            RefreshKeysUI(); RefreshKeysSidePanel();
        }
        private void btnKeysClearAll_Click(object sender, RoutedEventArgs e) {
            if (_keyVM == null || _keyVM.Keys.Count == 0) return;
            if (MessageBox.Show(string.Format("确定要删除全部 {0} 个按键吗？", _keyVM.Keys.Count),
                "清空全部按键", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            { _keyVM.ClearAll(); _selectedKeys.Clear(); _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel(); }
        }
        private void btnKeyDelete_Click(object sender, RoutedEventArgs e) {
            if (_selectedKeys.Count == 0) return;
            foreach (var key in _selectedKeys.ToList()) _keyVM.RemoveKey(key);
            _selectedKeys.Clear(); RefreshKeysUI(); RefreshKeysSidePanel();
        }

        // ——— 键盘布局 ———
        private void btnKeysLayout_Click(object sender, RoutedEventArgs e) {
            InitKeyPanel();
            var menu = new ContextMenu { PlacementTarget = sender as UIElement, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
            AddLayoutItem(menu, "键盘布局（QWERTY 标准）", () => { var ks = _keyVM.CreateKeyboardLayout(); if (ks.Count > 0) _groupNames[ks[0].GroupId] = "QWERTY 键盘"; _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel(); });
            AddLayoutItem(menu, "方向键布局（↑ ↓ ← →）", () => { var ks = _keyVM.CreateDirectionalLayout(); if (ks.Count > 0) _groupNames[ks[0].GroupId] = "方向键"; _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel(); });
            AddLayoutItem(menu, "数字键盘布局（3×4 小键盘）", () => { var ks = _keyVM.CreateNumpadLayout(); if (ks.Count > 0) _groupNames[ks[0].GroupId] = "数字键盘"; _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel(); });
            menu.IsOpen = true;
        }
        private void AddLayoutItem(ContextMenu menu, string header, Action onClick) {
            var item = new MenuItem { Header = header }; item.Click += (s, a) => onClick(); menu.Items.Add(item);
        }

        // ——— Ctrl+A 全选 ———
        private void KeysPanel_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (!_keyVM.IsEditMode) return;
            if (e.Key == Key.A && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))) {
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
                    keysContent = BuildDirectionalGrid(groupKeys, isEdit);
                } else if (maxRow >= 3 && maxCol >= 3 && groupKeys.Any(k => k.Name == "Enter")) {
                    keysContent = BuildNumpadGrid(groupKeys, isEdit);
                } else {
                    var stack = new StackPanel { Margin = new Thickness(2, 0, 0, 0) };
                    BuildRowBasedLayoutInto(stack, groupKeys, isEdit);
                    keysContent = stack;
                }

                // 模块边框（编辑模式加左侧强调色条）
                var border = new Border {
                    BorderBrush = (Brush)FindResource("CardBorderBrush"), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 4, 8, 8),
                    Background = (Brush)FindResource("SecondaryHoverBgBrush"), Child = keysContent,
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
                    btn.Background = new SolidColorBrush(isDark ? Color.FromRgb(0x1A, 0x2E, 0x4A) : Color.FromRgb(0xE6, 0xF0, 0xFA));
                }
                btn.Click += KeyButtonEdit_Click;
                btn.MouseRightButtonDown += KeyButtonEdit_RightClick;
            } else {
                btn.Click += KeyButtonSend_Click;
            }
            return btn;
        }

        private Grid BuildDirectionalGrid(List<KeyViewModel> keys, bool isEdit) {
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 4) };
            for (int c = 0; c < 3; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            foreach (var kv in keys) {
                var btn = CreateKeyButton(kv, isEdit);
                Grid.SetRow(btn, kv.LayoutY); Grid.SetColumn(btn, kv.LayoutX);
                btn.HorizontalAlignment = HorizontalAlignment.Center; btn.VerticalAlignment = VerticalAlignment.Center;
                grid.Children.Add(btn); _keyButtonMap[btn] = kv;
            }
            return grid;
        }
        private Grid BuildNumpadGrid(List<KeyViewModel> keys, bool isEdit) {
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 4) };
            for (int c = 0; c < 4; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int r = 0; r < 4; r++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            foreach (var kv in keys) {
                var btn = CreateKeyButton(kv, isEdit);
                Grid.SetRow(btn, kv.LayoutY); Grid.SetColumn(btn, kv.LayoutX);
                if (kv.Name == "Enter") { Grid.SetRowSpan(btn, 2); btn.VerticalAlignment = VerticalAlignment.Stretch; }
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
            _selectedKeys.Clear(); _keyVM.RemoveKey(keyVM); RefreshKeysUI(); RefreshKeysSidePanel();
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
                k.ReleaseSendMode = "文本";
                k.ReleaseSendValue = string.Format("[key,{0},up]", k.Name);
            }
            RefreshKeysUI(); RefreshKeysSidePanel();
        }
        private void btnModuleDelete_Click(object sender, RoutedEventArgs e) {
            if (_selectedModuleGroupId == null) return; int gid = _selectedModuleGroupId.Value;
            var mk = _keyVM.Keys.Where(k => k.GroupId == gid).ToList(); if (mk.Count == 0) return;
            if (MessageBox.Show(string.Format("确定要删除模块「{0}」（{1} 个按键）吗？",
                _groupNames.TryGetValue(gid, out var nm) ? nm : "未命名", mk.Count),
                "删除模块", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) {
                foreach (var k in mk) _keyVM.RemoveKey(k); _groupNames.Remove(gid);
                _selectedModuleGroupId = null; _selectedKeys.Clear(); RefreshKeysUI(); RefreshKeysSidePanel();
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
