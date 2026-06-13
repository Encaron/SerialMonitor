using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace 串口助手
{
    public partial class MainWindow : Window
    {
        // ==================================================================
        //  快捷发送按钮 + 发送历史
        // ==================================================================

        private string QuickSendsFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SerialMonitor", "quick_sends.cfg");

        // ————————————————————————————————————————
        //  发送历史
        // ————————————————————————————————————————

        private void RecordSendHistory(string text)
        {
            sendHistory.RemoveAll(s => s == text);
            sendHistory.Insert(0, text);
            if (sendHistory.Count > MaxSendHistory)
                sendHistory.RemoveAt(sendHistory.Count - 1);
        }

        private void btnSendHistory_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            if (sendHistory.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "（暂无历史记录）", IsEnabled = false });
            }
            else
            {
                foreach (var item in sendHistory)
                {
                    string display = item.Length > 60 ? item.Substring(0, 60) + "…" : item;
                    var menuItem = new MenuItem { Header = display.Replace("_", "__"), Tag = item };
                    menuItem.Click += (s, args) =>
                    {
                        tbSend.Text = (s as MenuItem)?.Tag?.ToString() ?? "";
                        tbSend.Focus();
                        tbSend.CaretIndex = tbSend.Text.Length;
                    };
                    menu.Items.Add(menuItem);
                }
            }
            menu.IsOpen = true;
        }

        // ==================================================================
        //  快捷发送按钮
        // ==================================================================

        private void LoadQuickSends()
        {
            try
            {
                if (!File.Exists(QuickSendsFilePath))
                {
                    // 首次启动：加载预置 AT 指令模板
                    quickSends = new Dictionary<string, string>
                    {
                        ["AT"]          = "AT\\r\\n",
                        ["AT+CWLAP"]    = "AT+CWLAP\\r\\n",
                        ["AT+CWMODE=1"] = "AT+CWMODE=1\\r\\n",
                        ["AT+CIFSR"]    = "AT+CIFSR\\r\\n",
                        ["AT+RST"]      = "AT+RST\\r\\n",
                    };
                    SaveQuickSends();
                    return;
                }

                quickSends.Clear();
                foreach (var line in File.ReadAllLines(QuickSendsFilePath))
                {
                    int idx = line.IndexOf('\t');
                    if (idx > 0)
                    {
                        string label = line.Substring(0, idx);
                        string content = line.Substring(idx + 1);
                        quickSends[label] = content;
                    }
                }
            }
            catch { /* 静默失败 */ }
        }

        private void SaveQuickSends()
        {
            try
            {
                string dir = Path.GetDirectoryName(QuickSendsFilePath);
                Directory.CreateDirectory(dir);

                var lines = new List<string>();
                foreach (var kv in quickSends)
                    lines.Add($"{kv.Key}\t{kv.Value}");
                File.WriteAllLines(QuickSendsFilePath, lines);
            }
            catch { /* 静默失败 */ }
        }

        /// <summary>
        /// 重建快捷发送按钮面板
        /// </summary>
        private void RefreshQuickSendButtons()
        {
            quickSendPanel.Children.Clear();

            foreach (var kv in quickSends)
            {
                var btn = new Button
                {
                    Content = kv.Key,
                    Tag = kv.Value,
                    Style = FindResource("QuickSendChipStyle") as Style,
                };

                // 左键 → 立即发送
                btn.Click += (s, e) =>
                {
                    string content = (s as Button)?.Tag?.ToString();
                    if (string.IsNullOrEmpty(content)) return;
                    // 还原 \r\n 字面量为真实换行
                    content = content.Replace("\\r\\n", "\r\n").Replace("\\n", "\r\n");
                    SendRaw(content);
                };

                // 右键 → 编辑 / 删除
                btn.MouseRightButtonDown += (s, e) =>
                {
                    var menu = new ContextMenu();

                    var editItem = new MenuItem { Header = "编辑标签" };
                    string oldLabel = kv.Key;
                    editItem.Click += (s2, e2) =>
                    {
                        string newLabel = ShowInputDialog("编辑标签", "按钮名称：", oldLabel);
                        if (!string.IsNullOrEmpty(newLabel) && newLabel != oldLabel)
                        {
                            string val = quickSends[oldLabel];
                            quickSends.Remove(oldLabel);
                            quickSends[newLabel] = val;
                            SaveQuickSends();
                            RefreshQuickSendButtons();
                        }
                    };
                    menu.Items.Add(editItem);

                    var deleteItem = new MenuItem { Header = "删除" };
                    deleteItem.Click += (s2, e2) =>
                    {
                        quickSends.Remove(oldLabel);
                        SaveQuickSends();
                        RefreshQuickSendButtons();
                        LogSystem($"---- 快捷发送「{oldLabel}」已删除 ----");
                    };
                    menu.Items.Add(deleteItem);

                    menu.IsOpen = true;
                    e.Handled = true;
                };

                quickSendPanel.Children.Add(btn);
            }

            // "+" 添加按钮
            var addBtn = new Button
            {
                Content = "＋",
                Width = 24, Height = 24,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 4),
                Background = Brushes.Transparent,
                BorderBrush = FindResource("CardBorderBrush") as Brush,
                BorderThickness = new Thickness(1),
                Foreground = FindResource("TextMutedBrush") as Brush,
                Cursor = Cursors.Hand,
                ToolTip = "将当前输入区内容添加为快捷发送按钮",
                SnapsToDevicePixels = true,
            };
            // "+" 按钮模板（虚线边框风格表示"添加"语义）
            var addTemplate = new ControlTemplate(typeof(Button));
            var addBorder = new FrameworkElementFactory(typeof(Border));
            addBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            addBorder.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            addBorder.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            addBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            addBorder.SetValue(Border.SnapsToDevicePixelsProperty, true);
            var addCP = new FrameworkElementFactory(typeof(ContentPresenter));
            addCP.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            addCP.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            addBorder.AppendChild(addCP);
            addTemplate.VisualTree = addBorder;

            var addHoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            addHoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, FindResource("SecondaryHoverBgBrush") as Brush));
            addHoverTrigger.Setters.Add(new Setter(Button.BorderBrushProperty, FindResource("PrimaryBrush") as Brush));
            addHoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, FindResource("PrimaryBrush") as Brush));
            addTemplate.Triggers.Add(addHoverTrigger);
            addBtn.Template = addTemplate;

            addBtn.Click += (s, e) =>
            {
                string content = tbSend.Text;
                if (string.IsNullOrEmpty(content))
                {
                    LogSystem("---- 快捷发送：请先在发送区输入内容再添加 ----");
                    return;
                }

                string defaultLabel = content.Length > 15 ? content.Substring(0, 15) + "…" : content;
                string label = ShowInputDialog("添加快捷发送", "按钮名称：", defaultLabel);
                if (!string.IsNullOrEmpty(label))
                {
                    // 以字面量存储换行
                    content = content.Replace("\r\n", "\\r\\n").Replace("\n", "\\r\\n");
                    quickSends[label] = content;
                    SaveQuickSends();
                    RefreshQuickSendButtons();
                    LogSystem($"---- 快捷发送「{label}」已添加 ----");
                }
            };

            quickSendPanel.Children.Add(addBtn);

            // 有按钮时显示面板
            quickSendPanel.Visibility = quickSends.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 简易输入对话框（ToolWindow，居中于主窗口）
        /// </summary>
        private string ShowInputDialog(string title, string prompt, string defaultText)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 320,
                Height = 170,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                FontFamily = new FontFamily("Microsoft YaHei"),
                FontSize = 12,
                Background = FindResource("CardBgBrush") as Brush,
                Foreground = FindResource("TextPrimaryBrush") as Brush,
            };

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var promptText = new TextBlock
            {
                Text = prompt,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = FindResource("TextSecondaryBrush") as Brush,
            };
            Grid.SetRow(promptText, 0);
            grid.Children.Add(promptText);

            var input = new TextBox
            {
                Text = defaultText,
                Height = 28,
                Margin = new Thickness(0, 0, 0, 12),
                Background = FindResource("CardBgBrush") as Brush,
                Foreground = FindResource("TextPrimaryBrush") as Brush,
                BorderBrush = FindResource("InputBorderBrush") as Brush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 2, 6, 2),
                FontFamily = new FontFamily("Microsoft YaHei"),
                FontSize = 12,
            };
            input.SelectAll();
            input.Loaded += (s, e2) => input.Focus();
            Grid.SetRow(input, 1);
            grid.Children.Add(input);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            var cancelBtn = new Button
            {
                Content = "取消",
                Width = 64,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0),
                Foreground = FindResource("TextSecondaryBrush") as Brush,
                Background = FindResource("CardBgBrush") as Brush,
                BorderBrush = FindResource("CardBorderBrush") as Brush,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FontSize = 12,
                SnapsToDevicePixels = true,
            };
            cancelBtn.Click += (s, e2) => { dialog.DialogResult = false; dialog.Close(); };
            // 悬停效果（不依赖 DynamicResource 样式，避免弹窗视觉树找不到资源）
            var cancelFgHover = FindResource("TextPrimaryBrush") as Brush;
            var cancelBgHover = FindResource("SecondaryHoverBgBrush") as Brush;
            var cancelBorderHover = FindResource("TextFaintBrush") as Brush;
            var cancelFgNormal = cancelBtn.Foreground;
            var cancelBgNormal = cancelBtn.Background;
            var cancelBorderNormal = cancelBtn.BorderBrush;
            cancelBtn.MouseEnter += (s, e2) =>
            {
                cancelBtn.Foreground = cancelFgHover;
                cancelBtn.Background = cancelBgHover;
                cancelBtn.BorderBrush = cancelBorderHover;
            };
            cancelBtn.MouseLeave += (s, e2) =>
            {
                cancelBtn.Foreground = cancelFgNormal;
                cancelBtn.Background = cancelBgNormal;
                cancelBtn.BorderBrush = cancelBorderNormal;
            };
            buttonPanel.Children.Add(cancelBtn);

            var okBtn = new Button
            {
                Content = "确定",
                Width = 64,
                Height = 28,
                Foreground = Brushes.White,
                Background = FindResource("PrimaryBrush") as Brush,
                BorderBrush = FindResource("PrimaryBrush") as Brush,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FontSize = 12,
                SnapsToDevicePixels = true,
            };
            okBtn.Click += (s, e2) => { dialog.DialogResult = true; dialog.Close(); };
            var okBgHover = FindResource("PrimaryHoverBrush") as Brush;
            var okBgNormal = okBtn.Background;
            var okBorderNormal = okBtn.BorderBrush;
            okBtn.MouseEnter += (s, e2) =>
            {
                okBtn.Background = okBgHover;
                okBtn.BorderBrush = okBgHover;
            };
            okBtn.MouseLeave += (s, e2) =>
            {
                okBtn.Background = okBgNormal;
                okBtn.BorderBrush = okBorderNormal;
            };
            buttonPanel.Children.Add(okBtn);

            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            input.KeyDown += (s, e2) =>
            {
                if (e2.Key == Key.Enter) { dialog.DialogResult = true; dialog.Close(); }
            };

            if (dialog.ShowDialog() == true)
                return input.Text.Trim();
            else
                return null;
        }
    }
}
