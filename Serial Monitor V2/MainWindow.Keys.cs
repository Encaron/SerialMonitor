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
        // 当前编辑模式选中的按键（单选 + 多选）
        private List<KeyViewModel> _selectedKeys = new List<KeyViewModel>();
        // 映射：WPF Button → KeyViewModel
        private Dictionary<Button, KeyViewModel> _keyButtonMap = new Dictionary<Button, KeyViewModel>();

        // ——— 按键面板初始化 ———
        private void InitKeyPanel()
        {
            if (_keyVM != null) return;
            _keyVM = new KeyPanelViewModel();

            // 从 prefs.json 恢复按键
            if (_prefsData != null && _prefsData.TryGetValue("keys", out var keysObj)
                && keysObj is System.Collections.ArrayList arr && arr.Count > 0)
            {
                var keysList = new List<Dictionary<string, object>>();
                foreach (var item in arr)
                {
                    if (item is Dictionary<string, object> d)
                        keysList.Add(d);
                }
                if (keysList.Count > 0)
                    _keyVM.DeserializeKeys(keysList);
            }

            // 初始化发送模式下拉框
            foreach (var cb in new[] { cbKeySendMode, cbKeySendModeMulti })
            {
                cb.Items.Clear();
                cb.Items.Add("文本");
                cb.Items.Add("HEX");
                cb.Items.Add("数据包");
                cb.SelectedIndex = 2; // 默认数据包
            }

            // 初始化颜色面板
            InitColorPanels();

            // 初始化按键 UI
            RefreshKeysUI();
        }

        // ——— 颜色面板 ———
        private void InitColorPanels()
        {
            string[] colors = { "默认", "红色", "绿色", "蓝色", "黄色", "白色", "灰色" };
            foreach (var colorName in colors)
            {
                // 单选颜色面板
                var chip = CreateColorChip(colorName, (c) =>
                {
                    if (_selectedKeys.Count == 1)
                    {
                        _selectedKeys[0].Color = c;
                        RefreshKeysUI();
                        RefreshKeysSidePanel();
                    }
                });
                keysColorPanel.Children.Add(chip);

                // 多选颜色面板
                var chipMulti = CreateColorChip(colorName, (c) =>
                {
                    foreach (var key in _selectedKeys)
                        key.Color = c;
                    RefreshKeysUI();
                    RefreshKeysSidePanel();
                });
                keysColorPanelMulti.Children.Add(chipMulti);
            }
        }

        private Border CreateColorChip(string colorName, Action<string> onClick)
        {
            var isDark = isDarkTheme;
            string hex = KeyPanelViewModel.GetColorHex(colorName, isDark);
            Brush fillBrush;
            if (hex == null)
            {
                // "默认" 色块：使用主题背景色 + 边框提示
                fillBrush = (Brush)FindResource("CardBgBrush");
            }
            else
            {
                fillBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }
            var border = new Border
            {
                Width = 24, Height = 24, CornerRadius = new CornerRadius(4),
                Background = fillBrush,
                BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 6, 6),
                Cursor = Cursors.Hand,
                ToolTip = colorName,
                Tag = colorName,
            };
            border.MouseLeftButtonDown += (s, e) => onClick((string)((Border)s).Tag);
            return border;
        }

        // ——— 编辑模式切换 ———
        private void btnKeysEdit_Click(object sender, RoutedEventArgs e)
        {
            InitKeyPanel();
            _keyVM.IsEditMode = true;
            _selectedKeys.Clear();
            keysToolbarNormal.Visibility = Visibility.Collapsed;
            keysToolbarEdit.Visibility = Visibility.Visible;
            RefreshKeysUI();
            RefreshKeysSidePanel();
        }

        private void btnKeysDone_Click(object sender, RoutedEventArgs e)
        {
            _keyVM.IsEditMode = false;
            _selectedKeys.Clear();
            keysToolbarNormal.Visibility = Visibility.Visible;
            keysToolbarEdit.Visibility = Visibility.Collapsed;
            RefreshKeysUI();
            RefreshKeysSidePanel();
            // 保存到 prefs.json
            SaveKeysPrefs();
        }

        // ——— 添加按键 ———
        private void btnKeysAdd_Click(object sender, RoutedEventArgs e)
        {
            InitKeyPanel();
            string name = "Key" + (_keyVM.Keys.Count + 1);
            _keyVM.AddKey(name);
            RefreshKeysUI();
            RefreshKeysSidePanel();
        }

        // ——— 键盘布局 ———
        private void btnKeysLayout_Click(object sender, RoutedEventArgs e)
        {
            InitKeyPanel();
            var menu = new ContextMenu
            {
                PlacementTarget = sender as UIElement,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            };

            var itemQwerty = new MenuItem { Header = "键盘布局（QWERTY 标准）" };
            itemQwerty.Click += (s, args) =>
            {
                _keyVM.CreateKeyboardLayout();
                RefreshKeysUI();
                RefreshKeysSidePanel();
            };
            menu.Items.Add(itemQwerty);

            var itemArrows = new MenuItem { Header = "方向键布局（↑ ↓ ← →）" };
            itemArrows.Click += (s, args) =>
            {
                _keyVM.CreateDirectionalLayout();
                RefreshKeysUI();
                RefreshKeysSidePanel();
            };
            menu.Items.Add(itemArrows);

            var itemNumpad = new MenuItem { Header = "数字键盘布局（3×4 小键盘）" };
            itemNumpad.Click += (s, args) =>
            {
                _keyVM.CreateNumpadLayout();
                RefreshKeysUI();
                RefreshKeysSidePanel();
            };
            menu.Items.Add(itemNumpad);

            menu.IsOpen = true;
        }

        // ——— 删除按键 ———
        private void btnKeyDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedKeys.Count == 0) return;
            foreach (var key in _selectedKeys.ToList())
                _keyVM.RemoveKey(key);
            _selectedKeys.Clear();
            RefreshKeysUI();
            RefreshKeysSidePanel();
        }

        // ——— 清空全部 ———
        private void btnKeysClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_keyVM == null || _keyVM.Keys.Count == 0) return;
            var result = MessageBox.Show(
                string.Format("确定要删除全部 {0} 个按键吗？", _keyVM.Keys.Count),
                "清空全部按键", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                _keyVM.ClearAll();
                _selectedKeys.Clear();
                RefreshKeysUI();
                RefreshKeysSidePanel();
            }
        }

        // ——— Ctrl+A 全选（在按键面板按键事件中处理） ———
        private void KeysPanel_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_keyVM.IsEditMode) return;
            if (e.Key == Key.A && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                _selectedKeys.Clear();
                _selectedKeys.AddRange(_keyVM.Keys.Where(k => !k.IsShiftToggle));
                RefreshKeysUI();
                RefreshKeysSidePanel();
                e.Handled = true;
            }
        }

        // ——— 刷新按键 UI（正常模式 + 编辑模式） ———
        private void RefreshKeysUI()
        {
            if (_keyVM == null) return;
            keysPanel.Children.Clear();
            _keyButtonMap.Clear();

            bool hasKeys = _keyVM.Keys.Count > 0;
            bool isEdit = _keyVM.IsEditMode;

            // 空状态提示
            keysEmptyHintNormal.Visibility = (!hasKeys && !isEdit) ? Visibility.Visible : Visibility.Collapsed;
            keysEmptyHintEdit.Visibility = (!hasKeys && isEdit) ? Visibility.Visible : Visibility.Collapsed;

            if (!hasKeys) return;

            // 按行分组（LayoutY），每行一个 WrapPanel，支持键盘布局的行列结构
            var rows = _keyVM.Keys.GroupBy(k => k.LayoutY).OrderBy(g => g.Key);
            // 键盘式行偏移（模拟真实键盘的 stagger 效果）
            double[] rowStagger = { 0, 12, 20, 28 };
            foreach (var row in rows)
            {
                double leftMargin = row.Key < rowStagger.Length ? rowStagger[row.Key] : 0;
                var rowPanel = new WrapPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(leftMargin, 0, 0, 2),
                };
                // 行内按 LayoutX 排序
                var sortedKeys = row.OrderBy(k => k.LayoutX);
                foreach (var keyVM in sortedKeys)
                {
                    var btn = CreateKeyButton(keyVM, isEdit);
                    rowPanel.Children.Add(btn);
                    _keyButtonMap[btn] = keyVM;
                }
                keysPanel.Children.Add(rowPanel);
            }
        }

        private Button CreateKeyButton(KeyViewModel keyVM, bool isEditMode)
        {
            var btn = new Button
            {
                Content = keyVM.Name,
                Width = keyVM.Width,
                Height = keyVM.Height,
                Margin = new Thickness(0, 0, 6, 6),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Padding = new Thickness(4, 0, 4, 0),
                Tag = keyVM,
            };

            // 颜色
            var isDark = isDarkTheme;
            string hex = KeyPanelViewModel.GetColorHex(keyVM.Color, isDark);
            Brush bgBrush;
            if (hex != null)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                bgBrush = new SolidColorBrush(color);
                btn.Foreground = Brushes.White;
            }
            else
            {
                bgBrush = (Brush)FindResource("CardBgBrush");
                btn.Foreground = (Brush)FindResource("TextPrimaryBrush");
            }
            btn.Background = bgBrush;
            btn.BorderBrush = (Brush)FindResource("CardBorderBrush");
            btn.BorderThickness = new Thickness(1);
            btn.Cursor = Cursors.Hand;

            // Down 状态高亮（来自 STM32 的 [key,name,down] 消息）
            if (keyVM.IsDown && !keyVM.IsShiftToggle)
            {
                var accentColor = isDark ? Color.FromRgb(0x0E, 0x63, 0x9C) : Color.FromRgb(0x00, 0x78, 0xD4);
                btn.BorderBrush = new SolidColorBrush(accentColor);
                btn.BorderThickness = new Thickness(2);
                var downBg = new SolidColorBrush(isDark ? Color.FromRgb(0x1A, 0x3A, 0x5C) : Color.FromRgb(0xDE, 0xEC, 0xFC));
                btn.Background = downBg;
            }

            // 锁定状态
            if (keyVM.IsLocked)
            {
                btn.Opacity = 0.5;
                btn.IsEnabled = !isEditMode; // 编辑模式下仍可选中
                btn.ToolTip = "已锁定";
            }

            // Shift 切换键样式
            if (keyVM.IsShiftToggle)
            {
                btn.FontWeight = FontWeights.Bold;
                btn.Background = (Brush)FindResource("SecondaryHoverBgBrush");
                if (_keyVM.ShiftActive)
                {
                    var accentColor = isDark ? Color.FromRgb(0x0E, 0x63, 0x9C) : Color.FromRgb(0x00, 0x78, 0xD4);
                    btn.BorderBrush = new SolidColorBrush(accentColor);
                    btn.BorderThickness = new Thickness(2);
                }
            }

            // 编辑模式：选中高亮
            if (isEditMode)
            {
                bool isSelected = _selectedKeys.Contains(keyVM);
                if (isSelected)
                {
                    var accentColor = isDark ? Color.FromRgb(0x0E, 0x63, 0x9C) : Color.FromRgb(0x00, 0x78, 0xD4);
                    btn.BorderBrush = new SolidColorBrush(accentColor);
                    btn.BorderThickness = new Thickness(2);
                    var selBg = new SolidColorBrush(isDark ? Color.FromRgb(0x1A, 0x2E, 0x4A) : Color.FromRgb(0xE6, 0xF0, 0xFA));
                    btn.Background = keyVM.Color == "默认" ? selBg : bgBrush;
                }
                btn.Click += KeyButtonEdit_Click;
                btn.MouseRightButtonDown += KeyButtonEdit_RightClick;
            }
            else
            {
                btn.Click += KeyButtonSend_Click;
            }

            return btn;
        }

        // ——— 按键点击：发送数据 ———
        private void KeyButtonSend_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var keyVM = btn.Tag as KeyViewModel;
            if (keyVM == null || keyVM.IsLocked || keyVM.IsShiftToggle) return;

            // 视觉反馈 1：脉冲动画（透明度 1.0→0.55→1.0）
            PulseElement(btn);

            // 视觉反馈 2：闪绿色背景 → 200ms 后恢复
            var prevBg = btn.Background;
            var prevFg = btn.Foreground;
            btn.Background = new SolidColorBrush(SuccessColor);
            btn.Foreground = System.Windows.Media.Brushes.White;
            var flashTimer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(200) };
            flashTimer.Tick += (s2, e2) =>
            {
                flashTimer.Stop();
                btn.Background = prevBg;
                btn.Foreground = prevFg;
            };
            flashTimer.Start();

            // 更新侧面板反馈
            ShowKeySendFeedback(keyVM);

            if (_session == null || !_session.IsOpen) return;

            string toSend;
            switch (keyVM.SendMode)
            {
                case "文本":
                    toSend = keyVM.SendValue;
                    break;
                case "HEX":
                    toSend = keyVM.SendValue; // HEX 字符串，让 Session 处理
                    break;
                case "数据包":
                default:
                    // 发送协议格式 [key,Name,down] + [key,Name,up] 模拟一次完整点击
                    toSend = string.Format("[key,{0},down][key,{0},up]", keyVM.Name);
                    break;
            }

            if (!string.IsNullOrEmpty(toSend))
            {
                // 使用 SendRaw（复用主发送逻辑：模式 → 编码 → 字节），加换行符
                SendRaw(toSend, appendLineEnding: true);
            }
        }

        // ——— 侧面板：显示刚发送的按键信息 ———
        private void ShowKeySendFeedback(KeyViewModel keyVM)
        {
            tbKeyFeedbackName.Text = keyVM.Name;
            tbKeyFeedbackMode.Text = keyVM.SendMode;
            // 显示实际发送内容
            string preview;
            switch (keyVM.SendMode)
            {
                case "文本":
                    preview = keyVM.SendValue;
                    break;
                case "HEX":
                    preview = keyVM.SendValue;
                    break;
                case "数据包":
                default:
                    preview = string.Format("[key,{0},down][key,{0},up]", keyVM.Name);
                    break;
            }
            tbKeyFeedbackValue.Text = string.IsNullOrEmpty(preview) ? "（空）" : preview;
        }

        // ——— 编辑模式：选中/取消选中按键 ———
        private void KeyButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var keyVM = btn.Tag as KeyViewModel;
            if (keyVM == null) return;

            bool ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            if (keyVM.IsShiftToggle)
            {
                // Shift 切换键：切换大小写模式
                _keyVM.ShiftActive = !_keyVM.ShiftActive;
                RefreshKeysUI();
                RefreshKeysSidePanel();
                return;
            }

            if (ctrl)
            {
                // Ctrl+点击：切换选中
                if (_selectedKeys.Contains(keyVM))
                    _selectedKeys.Remove(keyVM);
                else
                    _selectedKeys.Add(keyVM);
            }
            else
            {
                // 普通点击：单选
                _selectedKeys.Clear();
                _selectedKeys.Add(keyVM);
            }
            RefreshKeysUI();
            RefreshKeysSidePanel();
        }

        private void KeyButtonEdit_RightClick(object sender, MouseButtonEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var keyVM = btn.Tag as KeyViewModel;
            if (keyVM == null || keyVM.IsShiftToggle) return;

            // 右键：快速删除
            _selectedKeys.Clear();
            _keyVM.RemoveKey(keyVM);
            RefreshKeysUI();
            RefreshKeysSidePanel();
        }

        // ——— 刷新侧面板 ———
        private void RefreshKeysSidePanel()
        {
            if (_keyVM == null) return;

            bool isEdit = _keyVM.IsEditMode;
            int count = _selectedKeys.Count;

            // 隐藏所有面板
            rightKeysSentFeedback.Visibility = Visibility.Collapsed;
            rightKeysNoSelection.Visibility = Visibility.Collapsed;
            rightKeysSingleSelect.Visibility = Visibility.Collapsed;
            rightKeysMultiSelect.Visibility = Visibility.Collapsed;

            // 非编辑模式：显示发送反馈
            if (!isEdit)
            {
                rightKeysSentFeedback.Visibility = Visibility.Visible;
                return;
            }

            // 编辑模式 + 无选中
            if (count == 0)
            {
                rightKeysNoSelection.Visibility = Visibility.Visible;
                return;
            }

            // 编辑模式 + 多选
            if (count > 1)
            {
                rightKeysMultiSelect.Visibility = Visibility.Visible;
                tbKeysSelectedCount.Text = string.Format("已选 {0} 个按键", count);

                // 批量模式下显示共有属性（取第一个的非空值）
                var first = _selectedKeys[0];
                bool sameMode = _selectedKeys.All(k => k.SendMode == first.SendMode);
                cbKeySendModeMulti.SelectedItem = sameMode ? first.SendMode : null;
                tbKeySendValueMulti.Text = first.SendValue;
                return;
            }

            // 编辑模式 + 单选
            var key = _selectedKeys[0];
            rightKeysSingleSelect.Visibility = Visibility.Visible;

            tbKeyName.Text = key.Name;
            cbKeySendMode.SelectedItem = key.SendMode;
            tbKeySendValue.Text = key.SendValue;
            chkKeyLocked.IsChecked = key.IsLocked;
            tbKeyWidth.Text = key.Width.ToString();
            tbKeyHeight.Text = key.Height.ToString();
        }

        // ——— 侧面板属性编辑事件 ———
        private void tbKeyName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_selectedKeys.Count != 1) return;
            _selectedKeys[0].Name = tbKeyName.Text;
            RefreshKeysUI();
        }

        private void cbKeySendMode_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedKeys.Count != 1 || cbKeySendMode.SelectedItem == null) return;
            _selectedKeys[0].SendMode = cbKeySendMode.SelectedItem.ToString();
        }

        private void tbKeySendValue_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_selectedKeys.Count != 1) return;
            _selectedKeys[0].SendValue = tbKeySendValue.Text;
        }

        private void chkKeyLocked_Changed(object sender, RoutedEventArgs e)
        {
            if (_selectedKeys.Count != 1) return;
            _selectedKeys[0].IsLocked = chkKeyLocked.IsChecked == true;
            RefreshKeysUI();
        }

        private void tbKeyWidth_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_selectedKeys.Count != 1) return;
            if (double.TryParse(tbKeyWidth.Text, out double w) && w >= 20 && w <= 400)
            {
                _selectedKeys[0].Width = w;
                RefreshKeysUI();
            }
            else
            {
                tbKeyWidth.Text = _selectedKeys[0].Width.ToString();
            }
        }

        private void tbKeyHeight_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_selectedKeys.Count != 1) return;
            if (double.TryParse(tbKeyHeight.Text, out double h) && h >= 20 && h <= 400)
            {
                _selectedKeys[0].Height = h;
                RefreshKeysUI();
            }
            else
            {
                tbKeyHeight.Text = _selectedKeys[0].Height.ToString();
            }
        }

        // ——— 多选批量编辑 ———
        private void cbKeySendModeMulti_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedKeys.Count <= 1 || cbKeySendModeMulti.SelectedItem == null) return;
            string mode = cbKeySendModeMulti.SelectedItem.ToString();
            foreach (var key in _selectedKeys)
                key.SendMode = mode;
        }

        private void tbKeySendValueMulti_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_selectedKeys.Count <= 1) return;
            foreach (var key in _selectedKeys)
                key.SendValue = tbKeySendValueMulti.Text;
        }

        // ——— 协议处理：收到 [key,name,state] ———
        /// <summary>
        /// 处理 [key,name,down] 或 [key,name,up] 协议消息
        /// </summary>
        private void HandleKeyMessage(string name, string state)
        {
            InitKeyPanel();
            bool isDown = state == "down";
            _keyVM.SetKeyState(name, isDown);

            // 更新对应按键按钮的外观
            Dispatcher.InvokeAsync(() =>
            {
                foreach (var kv in _keyButtonMap)
                {
                    if (kv.Value.Name == name)
                    {
                        kv.Value.IsDown = isDown;
                        break;
                    }
                }
                RefreshKeysUI();
            });
        }

        // ——— 持久化 ———
        private void SaveKeysPrefs()
        {
            if (_keyVM == null || _prefsData == null) return;
            var data = _keyVM.SerializeKeys();
            // 转换为 ArrayList（JavaScriptSerializer 兼容）
            var arr = new System.Collections.ArrayList();
            foreach (var d in data)
                arr.Add(d);
            _prefsData["keys"] = arr;
            _prefs.Save(_prefsData);
        }
    }
}
