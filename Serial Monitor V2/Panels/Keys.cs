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

        // ——— 初始化 ———
        private void InitKeyPanel()
        {
            if (_keyVM != null) return;
            _keyVM = new KeyPanelViewModel();

            // 恢复按键
            if (_prefsData != null && _prefsData.TryGetValue("keys", out var keysObj)
                && keysObj is System.Collections.IList arr && arr.Count > 0)
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

            // 初始化下拉框（Display=中文 Value=英文 key）
            foreach (var cb in new ComboBox[] { cbKeyPressMode, cbKeyReleaseMode, cbKeySendModeMulti, cbKeyReleaseModeMulti, cbModulePressMode, cbModuleReleaseMode })
            {
                cb.Items.Clear();
                foreach (var k in LogicValueMaps.SendModeKeys)
                    cb.Items.Add(new { Display = LogicValueMaps.DisplaySendMode(k), Value = k });
                cb.DisplayMemberPath = "Display";
                cb.SelectedValuePath = "Value";
                cb.SelectedValue = "packet";
            }

            InitColorPanels();
            InitModuleColorPanel();
            RefreshKeysUI();
        }

        // ——— 颜色面板 ———
        private void InitColorPanels()
        {
            keysColorPanel.Children.Clear();
            keysColorPanelMulti.Children.Clear();
            string[] colors = LogicValueMaps.ColorKeys;
            foreach (var colorName in colors)
            {
                keysColorPanel.Children.Add(CreateColorChip(colorName, c => {
                    if (_selectedKeys.Count == 1) { _selectedKeys[0].Color = c; RefreshKeysUI(); RefreshKeysSidePanel(); }
                    UpdateColorChipSelection(keysColorPanel, c);
                }));
                keysColorPanelMulti.Children.Add(CreateColorChip(colorName, c => {
                    foreach (var k in _selectedKeys) k.Color = c;
                    RefreshKeysUI(); RefreshKeysSidePanel();
                    UpdateColorChipSelection(keysColorPanelMulti, c);
                }));
            }
            // 初始选中态
            if (_selectedKeys.Count == 1) UpdateColorChipSelection(keysColorPanel, _selectedKeys[0].Color);
            else if (_selectedKeys.Count > 1) UpdateColorChipSelection(keysColorPanelMulti, _selectedKeys[0].Color);
        }

        private bool _moduleColorPanelInited;
        private void InitModuleColorPanel()
        {
            if (_moduleColorPanelInited) return; _moduleColorPanelInited = true;
            string[] colors = LogicValueMaps.ColorKeys;
            foreach (var c in colors)
                moduleColorPanel.Children.Add(CreateColorChip(c, cn => {
                    if (_selectedModuleGroupId == null) return;
                    int gid = _selectedModuleGroupId.Value;
                    foreach (var k in _keyVM.Keys.Where(k => k.GroupId == gid)) k.Color = cn;
                    RefreshKeysUI();
                    UpdateColorChipSelection(moduleColorPanel, cn);
                }));
        }

        private Border CreateColorChip(string colorName, Action<string> onClick)
        {
            var isDark = isDarkTheme;
            string hex = KeyPanelViewModel.GetColorHex(colorName, isDark);
            Brush fillBrush;
            if (hex != null)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                // 自定义 hex 颜色 → 小色块变两行：上为颜色、下为 hex 缩写
                fillBrush = new SolidColorBrush(color);
            }
            else
            {
                fillBrush = (Brush)FindResource("CardBgBrush");
            }
            var border = new Border {
                Width = 24, Height = 24, CornerRadius = new CornerRadius(4),
                Background = fillBrush, BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 6, 6),
                Cursor = Cursors.Hand, ToolTip = LogicValueMaps.DisplayColor(colorName), Tag = colorName,
            };
            border.PreviewMouseLeftButtonDown += (s, e) => { onClick((string)((Border)s).Tag); e.Handled = true; };
            return border;
        }

        /// <summary>更新色块选中态：当前颜色对应的色块加蓝色边框</summary>
        private void UpdateColorChipSelection(WrapPanel panel, string currentColor)
        {
            foreach (Border chip in panel.Children)
            {
                bool isSelected = chip.Tag != null && (string)chip.Tag == currentColor;
                chip.BorderBrush = isSelected ? (Brush)FindResource("PrimaryBrush")
                                              : (Brush)FindResource("CardBorderBrush");
                chip.BorderThickness = new Thickness(isSelected ? 2 : 1);
            }
        }

        /// <summary>弹出 40 色拾色器 Popup，确认后回调 onColorPicked(hex)</summary>
        private void ShowColorPickerPopup(FrameworkElement placementTarget, Action<string> onColorPicked)
        {
            string[] palette = {
                "#F44336","#E91E63","#9C27B0","#673AB7","#3F51B5","#2196F3","#03A9F4","#00BCD4",
                "#009688","#4CAF50","#8BC34A","#CDDC39","#FFEB3B","#FFC107","#FF9800","#FF5722",
                "#795548","#9E9E9E","#607D8B","#555555","#FFFFFF","#FF4081","#7C4DFF","#536DFE",
                "#448AFF","#40C4FF","#18FFFF","#64FFDA","#69F0AE","#B2FF59","#EEFF41","#FFD740",
                "#FFAB40","#FF6E40","#FF8A80","#EA80FC","#B388FF","#8C9EFF","#80D8FF","#A7FFEB",
            };

            string currentHex = "#2196F3"; // 默认蓝
            var border = new Border {
                Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12), MaxWidth = 244,
            };
            var stack = new StackPanel();
            var title = new TextBlock {
                Text = "自定义颜色", FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryBrush"), Margin = new Thickness(0, 0, 0, 8),
            };
            stack.Children.Add(title);

            // 40 色块 8×5
            var colorGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 8, Margin = new Thickness(0, 0, 0, 8) };
            TextBox hexPreview = null;
            Border previewSwatch = null;
            Border selectedPopupSwatch = null;
            var popupSwatches = new System.Collections.Generic.List<Border>();
            foreach (var hex in palette)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var swatch = new Border {
                    Width = 24, Height = 24, CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(color),
                    BorderBrush = (Brush)FindResource("CardBorderBrush"),
                    BorderThickness = new Thickness(1), Margin = new Thickness(1),
                    Cursor = Cursors.Hand, Tag = hex,
                };
                swatch.PreviewMouseLeftButtonDown += (s, e) => {
                    currentHex = hex;
                    if (hexPreview != null) hexPreview.Text = hex;
                    if (previewSwatch != null) previewSwatch.Background = new SolidColorBrush(color);
                    // 更新选中态
                    if (selectedPopupSwatch != null) {
                        selectedPopupSwatch.BorderBrush = (Brush)FindResource("CardBorderBrush");
                        selectedPopupSwatch.BorderThickness = new Thickness(1);
                    }
                    swatch.BorderBrush = (Brush)FindResource("PrimaryBrush");
                    swatch.BorderThickness = new Thickness(2);
                    selectedPopupSwatch = swatch;
                    e.Handled = true;
                };
                popupSwatches.Add(swatch);
                colorGrid.Children.Add(swatch);
            }
            // 初始选中态：匹配 currentHex ("#2196F3")
            foreach (var sw in popupSwatches) {
                if ((string)sw.Tag == currentHex) {
                    sw.BorderBrush = (Brush)FindResource("PrimaryBrush");
                    sw.BorderThickness = new Thickness(2);
                    selectedPopupSwatch = sw;
                    break;
                }
            }
            stack.Children.Add(colorGrid);

            // Hex 输入行
            var hexRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            previewSwatch = new Border {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(currentHex)),
                BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left,
            };
            Grid.SetColumn(previewSwatch, 0);
            hexRow.Children.Add(previewSwatch);
            hexPreview = new TextBox {
                Text = currentHex, FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12, Height = 24, Padding = new Thickness(6, 2, 6, 2),
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(hexPreview, 1);
            hexRow.Children.Add(hexPreview);
            hexPreview.TextChanged += (_, __) => {
                currentHex = hexPreview.Text;
                try {
                    var c = (Color)ColorConverter.ConvertFromString(currentHex);
                    previewSwatch.Background = new SolidColorBrush(c);
                } catch { }
                // 同步色块选中态
                if (selectedPopupSwatch != null) {
                    selectedPopupSwatch.BorderBrush = (Brush)FindResource("CardBorderBrush");
                    selectedPopupSwatch.BorderThickness = new Thickness(1);
                    selectedPopupSwatch = null;
                }
                foreach (var sw in popupSwatches) {
                    if ((string)sw.Tag == currentHex) {
                        sw.BorderBrush = (Brush)FindResource("PrimaryBrush");
                        sw.BorderThickness = new Thickness(2);
                        selectedPopupSwatch = sw;
                        break;
                    }
                }
            };
            stack.Children.Add(hexRow);

            // 确认/取消按钮
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancelBtn = new Button {
                Content = "取消", Style = (Style)FindResource("SecondaryButtonStyle"),
                Height = 26, MinWidth = 52, FontSize = 12, Padding = new Thickness(8, 0, 8, 0),
            };
            var confirmBtn = new Button {
                Content = "确认", Style = (Style)FindResource("PrimaryButtonStyle"),
                Height = 26, MinWidth = 52, FontSize = 12, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(8, 0, 0, 0),
            };
            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(confirmBtn);
            stack.Children.Add(btnRow);

            border.Child = stack;
            var popup = new System.Windows.Controls.Primitives.Popup {
                Child = border, AllowsTransparency = true,
                PlacementTarget = placementTarget, Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                StaysOpen = false, Width = 260,
            };

            cancelBtn.Click += (_, __) => popup.IsOpen = false;
            confirmBtn.Click += (_, __) => { popup.IsOpen = false; onColorPicked(currentHex); };
            popup.IsOpen = true;
        }

        // ——— "自定义颜色" 按钮 ———
        private void btnKeysCustomColor_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            // 根据按钮确定作用范围
            Action<string> apply = null;
            WrapPanel chipPanel = null;
            if (btn == btnKeysCustomColor && _selectedKeys.Count == 1)
            {
                var key = _selectedKeys[0];
                chipPanel = keysColorPanel;
                apply = hex => { key.Color = hex; RefreshKeysUI(); RefreshKeysSidePanel(); UpdateColorChipSelection(chipPanel, hex); };
            }
            else if (btn == btnKeysMultiCustomColor && _selectedKeys.Count > 1)
            {
                var keys = _selectedKeys.ToList();
                chipPanel = keysColorPanelMulti;
                apply = hex => { foreach (var k in keys) k.Color = hex; RefreshKeysUI(); RefreshKeysSidePanel(); UpdateColorChipSelection(chipPanel, hex); };
            }
            else if (btn == btnModuleCustomColor && _selectedModuleGroupId.HasValue)
            {
                int gid = _selectedModuleGroupId.Value;
                var keys = _keyVM.Keys.Where(k => k.GroupId == gid).ToList();
                chipPanel = moduleColorPanel;
                apply = hex => { foreach (var k in keys) k.Color = hex; RefreshKeysUI(); UpdateColorChipSelection(chipPanel, hex); };
            }
            else return;

            if (apply != null)
                ShowColorPickerPopup(btn, apply);
        }

        // ——— 侧面板切换 ———
        private void SwitchSidePanelToKeys() {
            if (_currentTab != "Keys") { tabKeys.IsChecked = true; }
        }

        // ——— 编辑模式切换 ———
        private void btnKeysEdit_Click(object sender, RoutedEventArgs e) {
            InitKeyPanel(); _keyVM.IsEditMode = true; _selectedKeys.Clear(); _selectedModuleGroupId = null;
            keysToolbarNormal.Visibility = Visibility.Collapsed; keysToolbarEdit.Visibility = Visibility.Visible;
            RefreshKeysUI(); RefreshKeysSidePanel();
        }
        private void btnKeysDone_Click(object sender, RoutedEventArgs e) {
            CancelKeysConfirm();
            _keyVM.IsEditMode = false; _selectedKeys.Clear(); _selectedModuleGroupId = null;
            keysToolbarNormal.Visibility = Visibility.Visible; keysToolbarEdit.Visibility = Visibility.Collapsed;
            RefreshKeysUI(); RefreshKeysSidePanel(); SaveKeysPrefs();
        }

        // ——— 添加 / 清空 / 删除 ———
        private void btnKeysAdd_Click(object sender, RoutedEventArgs e) {
            InitKeyPanel();
            SwitchSidePanelToKeys();
            string name = "Key" + (_keyVM.Keys.Count + 1);
            var key = _keyVM.AddKey(name);
            if (!_groupNames.ContainsKey(key.GroupId)) _groupNames[key.GroupId] = "手动按键";
            _selectedModuleGroupId = null; _selectedKeys.Clear(); _selectedKeys.Add(key);
            RefreshKeysUI(); RefreshKeysSidePanel();
        }
        private void btnKeysClearAll_Click(object sender, RoutedEventArgs e) {
            if (_keyVM == null || _keyVM.Keys.Count == 0) return;
            if (_keysConfirmButton == btnKeysClearAll) {
                _keyVM.ClearAll(); _selectedKeys.Clear(); _selectedModuleGroupId = null;
                CancelKeysConfirm(); RefreshKeysUI(); RefreshKeysSidePanel();
            } else { StartKeysConfirm(btnKeysClearAll, "⚠ 确认清空"); }
        }
        private void btnKeyDelete_Click(object sender, RoutedEventArgs e) {
            if (_selectedKeys.Count == 0) return;
            foreach (var key in _selectedKeys.ToList()) _keyVM.RemoveKey(key);
            _selectedKeys.Clear(); RefreshKeysUI(); RefreshKeysSidePanel();
        }

        // ——— 键盘布局 ———
        private void btnKeysLayout_Click(object sender, RoutedEventArgs e) {
            InitKeyPanel();
            SwitchSidePanelToKeys();
            var menu = new ContextMenu { PlacementTarget = sender as UIElement, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
            var style = (Style)FindResource("ContextMenuMenuItemStyle");
            var sepStyle = (Style)FindResource("ContextMenuSeparatorStyle");

            AddLayoutItem(menu, "⌨  键盘布局（QWERTY 标准）", "数字行 + QWERTY 全键盘 + ⇧ 切换", style, () => {
                var ks = _keyVM.CreateKeyboardLayout(); if (ks.Count > 0) _groupNames[ks[0].GroupId] = "QWERTY 键盘";
                _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel();
            });
            AddLayoutItem(menu, "🎯  方向键（↑ ↓ ← →）", "十字方向键布局", style, () => {
                var ks = _keyVM.CreateDirectionalLayout(); if (ks.Count > 0) _groupNames[ks[0].GroupId] = "方向键";
                _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel();
            });
            AddLayoutItem(menu, "🔢  数字键盘（3×4 + 运算符）", "数字 0-9 + 运算符 + Enter", style, () => {
                var ks = _keyVM.CreateNumpadLayout(); if (ks.Count > 0) _groupNames[ks[0].GroupId] = "数字键盘";
                _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel();
            });

            AddSeparator(menu, sepStyle);

            AddLayoutItem(menu, "🎮  游戏键位（W A S D + Q E）", "WASD 移动 + QE 侧移键", style, () => {
                var ks = _keyVM.CreateWASDLayout(); if (ks.Count > 0) _groupNames[ks[0].GroupId] = "WASD 游戏键位";
                _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel();
            });
            AddLayoutItem(menu, "⚙  功能键（F1 - F12）", "F1 到 F12 功能键，2 行 × 6 列", style, () => {
                var ks = _keyVM.CreateFunctionKeyLayout(); if (ks.Count > 0) _groupNames[ks[0].GroupId] = "F1-F12 功能键";
                _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel();
            });
            AddLayoutItem(menu, "🔗  逻辑按键对", "up/down · on/off · open/close · start/stop · left/right · lock/unlock", style, () => {
                var ks = _keyVM.CreateLogicPairLayout(); if (ks.Count > 0) _groupNames[ks[0].GroupId] = "逻辑按键对";
                _selectedModuleGroupId = null; RefreshKeysUI(); RefreshKeysSidePanel();
            });

            menu.IsOpen = true;
        }
        private void AddLayoutItem(ContextMenu menu, string header, string tooltip, Style style, Action onClick) {
            var item = new MenuItem { Header = header, ToolTip = tooltip, Style = style };
            item.Click += (s, a) => onClick(); menu.Items.Add(item);
        }
        private static void AddSeparator(ContextMenu menu, Style style) {
            menu.Items.Add(new Separator { Style = style });
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
                    SwitchSidePanelToKeys();
                    _selectedModuleGroupId = capturedGid; _selectedKeys.Clear();
                    RefreshKeysUI(); RefreshKeysSidePanel();
                };

                // 模式标签
                var gModes = groupKeys.GroupBy(k => k.PressSendMode).OrderByDescending(g2 => g2.Count()).Select(g2 => g2.Key).ToList();
                string gMode = LogicValueMaps.DisplaySendMode(gModes.FirstOrDefault() ?? "packet");
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
                } else if (maxRow >= 1 && groupKeys.GroupBy(k => k.LayoutY).Select(g => g.Count()).Distinct().Count() == 1) {
                    // 统一列宽的网格布局（逻辑按键对、WASD、功能键等），不用 stagger
                    keysContent = BuildUniformGrid(groupKeys, maxRow, maxCol, isEdit);
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
                Template = (ControlTemplate)FindResource("KeyButtonTemplate"),
            };
            var isDark = isDarkTheme;
            string hex = KeyPanelViewModel.GetColorHex(keyVM.Color, isDark);
            Brush bgBrush, fgBrush;
            if (hex != null) {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                bgBrush = new SolidColorBrush(color);
                // 亮度计算：浅底用深色字，深底用白色字
                double lum = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
                fgBrush = lum > 140 ? new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)) : Brushes.White;
            } else { bgBrush = (Brush)FindResource("CardBgBrush"); fgBrush = (Brush)FindResource("TextPrimaryBrush"); }
            btn.Background = bgBrush;
            btn.Foreground = fgBrush;
            btn.BorderBrush = (Brush)FindResource("CardBorderBrush");
            btn.BorderThickness = new Thickness(1);
            btn.Cursor = Cursors.Hand;

            // 自锁按下状态 或 STM32 down 反馈
            bool isActiveDown = (keyVM.IsSelfLock && keyVM.IsPressed) || (!keyVM.IsSelfLock && keyVM.IsDown);
            if (isActiveDown && !keyVM.IsShiftToggle) {
                Color accent = hex != null ? ((SolidColorBrush)bgBrush).Color
                    : isDark ? Color.FromRgb(0x0E, 0x63, 0x9C) : Color.FromRgb(0x00, 0x78, 0xD4);
                btn.BorderBrush = new SolidColorBrush(accent); btn.BorderThickness = new Thickness(2);
                if (hex == null)
                    btn.Background = new SolidColorBrush(isDark ? Color.FromRgb(0x1A, 0x3A, 0x5C) : Color.FromRgb(0xDE, 0xEC, 0xFC));
            }

            // 自锁标记：按钮文字加下划线
            if (keyVM.IsSelfLock) btn.FontStyle = FontStyles.Italic;

            // Shift 切换键
            if (keyVM.IsShiftToggle) {
                btn.FontWeight = FontWeights.Bold;
                btn.Background = (Brush)FindResource("SecondaryHoverBgBrush");
                if (_keyVM.ShiftActive) {
                    Color accent = hex != null ? ((SolidColorBrush)bgBrush).Color
                        : isDark ? Color.FromRgb(0x0E, 0x63, 0x9C) : Color.FromRgb(0x00, 0x78, 0xD4);
                    btn.BorderBrush = new SolidColorBrush(accent); btn.BorderThickness = new Thickness(2);
                }
            }

            if (isEditMode) {
                if (_selectedKeys.Contains(keyVM)) {
                    Color accent = hex != null ? ((SolidColorBrush)bgBrush).Color
                        : isDark ? Color.FromRgb(0x0E, 0x63, 0x9C) : Color.FromRgb(0x00, 0x78, 0xD4);
                    btn.BorderBrush = new SolidColorBrush(accent); btn.BorderThickness = new Thickness(2);
                    // 自定义颜色键保留原背景（边框已足够标记选中态）
                    if (hex == null)
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
            for (int c = 0; c < 3; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
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
        private Grid BuildUniformGrid(List<KeyViewModel> keys, int maxRow, int maxCol, bool isEdit) {
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(2, 0, 0, 0) };
            for (int c = 0; c <= maxCol; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int r = 0; r <= maxRow; r++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            foreach (var kv in keys) {
                var btn = CreateKeyButton(kv, isEdit);
                btn.Margin = new Thickness(3);
                Grid.SetRow(btn, kv.LayoutY); Grid.SetColumn(btn, kv.LayoutX);
                grid.Children.Add(btn); _keyButtonMap[btn] = kv;
            }
            return grid;
        }

        // ═══════════════════════════════════════
        //  发送逻辑（自锁 + 按下/松开分离）
        // ═══════════════════════════════════════

        private void KeyButtonSend_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button; if (btn == null) return;
            var keyVM = btn.Tag as KeyViewModel; if (keyVM == null) return;

            // Shift 切换键：在正常模式也可用
            if (keyVM.IsShiftToggle)
            {
                PulseElement(btn);
                ToggleShift(keyVM.GroupId);
                RefreshKeysUI();
                return;
            }

            SwitchSidePanelToKeys();
            if (_session == null || !_session.IsOpen) return;

            ShowKeySendFeedback(keyVM);

            if (keyVM.IsSelfLock)
            {
                if (keyVM.IsPressed)
                {
                    string releaseContent = keyVM.GetReleaseContent();
                    if (!string.IsNullOrEmpty(releaseContent)) SendRaw(releaseContent, appendLineEnding: true);
                    keyVM.IsPressed = false;
                }
                else
                {
                    string pressContent = keyVM.GetPressContent();
                    if (!string.IsNullOrEmpty(pressContent)) SendRaw(pressContent, appendLineEnding: true);
                    keyVM.IsPressed = true;
                }
                // 自锁：状态变了必须重建 UI，之后在新按钮上做动画
                RefreshKeysUI();
                var kvp = _keyButtonMap.FirstOrDefault(x => x.Value == keyVM);
                if (kvp.Key != null) PulseElement(kvp.Key);
            }
            else
            {
                string pressContent = keyVM.GetPressContent();
                string releaseContent = keyVM.GetReleaseContent();
                if (!string.IsNullOrEmpty(pressContent)) SendRaw(pressContent, appendLineEnding: true);
                if (!string.IsNullOrEmpty(releaseContent)) SendRaw(releaseContent, appendLineEnding: true);
                // 非自锁：状态没变，不重建 UI，直接在当前按钮上做动画
                PulseElement(btn);
            }
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
                    if (k.PressSendMode == "text" || k.PressSendMode == "hex")
                        k.PressSendValue = k.Name;
                    if (k.ReleaseSendMode == "text" || k.ReleaseSendMode == "hex")
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

            UpdateKeyProtocolPreview(keyVM);
        }

        private void UpdateKeyProtocolPreview(KeyViewModel k)
        {
            if (tbKeyProtoDown == null || tbKeyProtoUp == null) return;
            if (k == null)
            {
                tbKeyProtoDown.Text = "—";
                tbKeyProtoUp.Text = "—";
                return;
            }
            tbKeyProtoDown.Text = string.Format("[key,{0},{1}]", k.Name,
                string.IsNullOrEmpty(k.PressSendValue) ? "down" : k.PressSendValue);
            tbKeyProtoUp.Text = string.Format("[key,{0},{1}]", k.Name,
                string.IsNullOrEmpty(k.ReleaseSendValue) ? "up" : k.ReleaseSendValue);
        }

        private void btnKeyCopyDown_Click(object sender, RoutedEventArgs e)
        {
            if (tbKeyProtoDown == null || string.IsNullOrEmpty(tbKeyProtoDown.Text)
                || tbKeyProtoDown.Text == "—") return;
            SafeSetClipboard(tbKeyProtoDown.Text);
            if (sender is Button btn) ShowCopyToastAndShake(btn);
        }

        private void btnKeyCopyUp_Click(object sender, RoutedEventArgs e)
        {
            if (tbKeyProtoUp == null || string.IsNullOrEmpty(tbKeyProtoUp.Text)
                || tbKeyProtoUp.Text == "—") return;
            SafeSetClipboard(tbKeyProtoUp.Text);
            if (sender is Button btn) ShowCopyToastAndShake(btn);
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
                cbKeySendModeMulti.SelectedValue = samePress ? first.PressSendMode : null;
                cbKeyReleaseModeMulti.SelectedValue = sameRelease ? first.ReleaseSendMode : null;
                return;
            }

            // 单选
            var key = _selectedKeys[0];
            bool isLayoutKey = key.GroupId != KeyPanelViewModel.ManualGroupId;
            rightKeysSingleSelect.Visibility = Visibility.Visible;
            tbKeyName.Text = key.Name;
            chkKeySelfLock.IsChecked = key.IsSelfLock;
            cbKeyPressMode.SelectedValue = key.PressSendMode;
            tbKeyPressValue.Text = key.PressSendValue;
            cbKeyReleaseMode.SelectedValue = key.ReleaseSendMode;
            tbKeyReleaseValue.Text = key.ReleaseSendValue;
            // 模块布局键不可调大小
            tbKeyWidth.Text = key.Width.ToString();
            tbKeyWidth.IsEnabled = !isLayoutKey;
            tbKeyHeight.Text = key.Height.ToString();
            tbKeyHeight.IsEnabled = !isLayoutKey;
            UpdateColorChipSelection(keysColorPanel, key.Color);
        }

        // ——— 单选属性编辑 ———
        private void tbKeyName_LostFocus(object sender, RoutedEventArgs e) { if (_selectedKeys.Count == 1) { _selectedKeys[0].Name = tbKeyName.Text; RefreshKeysUI(); } }
        private void chkKeySelfLock_Changed(object sender, RoutedEventArgs e) { if (_selectedKeys.Count == 1) { _selectedKeys[0].IsSelfLock = chkKeySelfLock.IsChecked == true; _selectedKeys[0].IsPressed = false; RefreshKeysUI(); } }
        private void cbKeyPressMode_Changed(object sender, SelectionChangedEventArgs e) { if (_selectedKeys.Count == 1 && cbKeyPressMode.SelectedValue != null) _selectedKeys[0].PressSendMode = cbKeyPressMode.SelectedValue.ToString(); }
        private void tbKeyPressValue_LostFocus(object sender, RoutedEventArgs e) { if (_selectedKeys.Count == 1) _selectedKeys[0].PressSendValue = tbKeyPressValue.Text; }
        private void cbKeyReleaseMode_Changed(object sender, SelectionChangedEventArgs e) { if (_selectedKeys.Count == 1 && cbKeyReleaseMode.SelectedValue != null) _selectedKeys[0].ReleaseSendMode = cbKeyReleaseMode.SelectedValue.ToString(); }
        private void tbKeyReleaseValue_LostFocus(object sender, RoutedEventArgs e) { if (_selectedKeys.Count == 1) _selectedKeys[0].ReleaseSendValue = tbKeyReleaseValue.Text; }
        private void tbKeyWidth_LostFocus(object sender, RoutedEventArgs e) { if (_selectedKeys.Count == 1) { if (double.TryParse(tbKeyWidth.Text, out double w) && w >= 20 && w <= 400) { _selectedKeys[0].Width = w; RefreshKeysUI(); } else tbKeyWidth.Text = _selectedKeys[0].Width.ToString(); } }
        private void tbKeyHeight_LostFocus(object sender, RoutedEventArgs e) { if (_selectedKeys.Count == 1) { if (double.TryParse(tbKeyHeight.Text, out double h) && h >= 20 && h <= 400) { _selectedKeys[0].Height = h; RefreshKeysUI(); } else tbKeyHeight.Text = _selectedKeys[0].Height.ToString(); } }

        // ——— 多选批量编辑 ———
        private void cbKeySendModeMulti_Changed(object sender, SelectionChangedEventArgs e) { if (_selectedKeys.Count <= 1 || cbKeySendModeMulti.SelectedValue == null) return; string m = cbKeySendModeMulti.SelectedValue.ToString(); foreach (var k in _selectedKeys) k.PressSendMode = m; }
        private void cbKeyReleaseModeMulti_Changed(object sender, SelectionChangedEventArgs e) { if (_selectedKeys.Count <= 1 || cbKeyReleaseModeMulti.SelectedValue == null) return; string m = cbKeyReleaseModeMulti.SelectedValue.ToString(); foreach (var k in _selectedKeys) k.ReleaseSendMode = m; }

        // ——— 模块设置 ———
        private void ShowModuleSettings(int gid) {
            rightKeysModuleSettings.Visibility = Visibility.Visible;
            tbModuleName.Text = _groupNames.TryGetValue(gid, out var n) ? n : ("模块 " + gid);
            var gk = _keyVM.Keys.Where(k => k.GroupId == gid).ToList();
            var pressMode = gk.GroupBy(k => k.PressSendMode).OrderByDescending(g2 => g2.Count()).Select(g2 => g2.Key).FirstOrDefault() ?? "packet";
            var releaseMode = gk.GroupBy(k => k.ReleaseSendMode).OrderByDescending(g2 => g2.Count()).Select(g2 => g2.Key).FirstOrDefault() ?? "packet";
            cbModulePressMode.SelectedValue = pressMode;
            cbModuleReleaseMode.SelectedValue = releaseMode;
        }
        private void tbModuleName_LostFocus(object sender, RoutedEventArgs e) { if (_selectedModuleGroupId == null) return; string nn = tbModuleName.Text?.Trim(); if (!string.IsNullOrEmpty(nn)) { _groupNames[_selectedModuleGroupId.Value] = nn; RefreshKeysUI(); } }
        private void cbModulePressMode_Changed(object sender, SelectionChangedEventArgs e) {
            if (_selectedModuleGroupId == null || cbModulePressMode.SelectedValue == null) return;
            string m = cbModulePressMode.SelectedValue.ToString(); int gid = _selectedModuleGroupId.Value;
            foreach (var k in _keyVM.Keys.Where(k => k.GroupId == gid))
            { k.PressSendMode = m; if (m == "text" && string.IsNullOrEmpty(k.PressSendValue)) k.PressSendValue = k.Name; }
            RefreshKeysUI();
        }
        private void cbModuleReleaseMode_Changed(object sender, SelectionChangedEventArgs e) {
            if (_selectedModuleGroupId == null || cbModuleReleaseMode.SelectedValue == null) return;
            string m = cbModuleReleaseMode.SelectedValue.ToString(); int gid = _selectedModuleGroupId.Value;
            foreach (var k in _keyVM.Keys.Where(k => k.GroupId == gid))
            { k.ReleaseSendMode = m; if (m == "text" && string.IsNullOrEmpty(k.ReleaseSendValue)) k.ReleaseSendValue = k.Name; }
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
                    if (isPress) k.PressSendMode = "none"; else k.ReleaseSendMode = "none";
                    break;
                case "name":
                    if (isPress) { k.PressSendMode = "text"; k.PressSendValue = k.Name; }
                    else        { k.ReleaseSendMode = "text"; k.ReleaseSendValue = k.Name; }
                    break;
                default: // up, down, on, off
                    if (isPress) { k.PressSendMode = "text"; k.PressSendValue = tag; }
                    else        { k.ReleaseSendMode = "text"; k.ReleaseSendValue = tag; }
                    break;
            }
        }
        private void btnModuleGenRelease_Click(object sender, RoutedEventArgs e) {
            if (_selectedModuleGroupId == null) return; int gid = _selectedModuleGroupId.Value;
            foreach (var k in _keyVM.Keys.Where(k => k.GroupId == gid && !k.IsShiftToggle))
            {
                k.ReleaseSendMode = "packet";
                k.ReleaseSendValue = "";  // 默认=up
            }
            RefreshKeysUI(); RefreshKeysSidePanel();
        }
        private void btnModuleDelete_Click(object sender, RoutedEventArgs e) {
            if (_selectedModuleGroupId == null) return; int gid = _selectedModuleGroupId.Value;
            var mk = _keyVM.Keys.Where(k => k.GroupId == gid).ToList(); if (mk.Count == 0) return;
            if (_keysConfirmButton == btnModuleDelete) {
                foreach (var k in mk) _keyVM.RemoveKey(k); _groupNames.Remove(gid);
                _selectedModuleGroupId = null; _selectedKeys.Clear();
                CancelKeysConfirm(); RefreshKeysUI(); RefreshKeysSidePanel();
            } else {
                string moduleName = _groupNames.TryGetValue(gid, out var nm) ? nm : "未命名";
                StartKeysConfirm(btnModuleDelete, string.Format("⚠ 删除「{0}」", moduleName));
            }
        }

        // ——— 二次确认 ———
        private Button _keysConfirmButton;
        private string _keysConfirmOriginalText;
        private DispatcherTimer _keysConfirmTimer;
        private void StartKeysConfirm(Button btn, string confirmText) {
            CancelKeysConfirm();
            _keysConfirmButton = btn;
            _keysConfirmOriginalText = btn.Content?.ToString() ?? "";
            btn.Content = confirmText;
            _keysConfirmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _keysConfirmTimer.Tick += (s2, e2) => CancelKeysConfirm();
            _keysConfirmTimer.Start();
        }
        private void CancelKeysConfirm() {
            if (_keysConfirmButton != null) _keysConfirmButton.Content = _keysConfirmOriginalText;
            _keysConfirmTimer?.Stop(); _keysConfirmTimer = null;
            _keysConfirmButton = null; _keysConfirmOriginalText = null;
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
            var arr = new List<object>();
            foreach (var d in _keyVM.SerializeKeys()) arr.Add(d);
            _prefsData["keys"] = arr;
            var nameDict = new Dictionary<string, object>();
            foreach (var kv in _groupNames) nameDict[kv.Key.ToString()] = kv.Value;
            _prefsData["keyGroupNames"] = nameDict;
            _prefs.Save(_prefsData);
        }
    }
}
