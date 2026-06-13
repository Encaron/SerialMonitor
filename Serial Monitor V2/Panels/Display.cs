using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace 串口助手
{
    public partial class MainWindow
    {
        // ——— OLED 面板 ———
        private DisplayPanelViewModel _displayVM;
        private Dictionary<string, TextBlock> _oledTexts = new Dictionary<string, TextBlock>();

        // ——— 初始化 ———
        private void InitOLEDPanel()
        {
            if (_displayVM != null) return;
            _displayVM = new DisplayPanelViewModel();

            // 恢复尺寸
            if (_prefsData != null)
            {
                if (_prefsData.TryGetValue("oledWidth", out var wObj) && int.TryParse(wObj?.ToString(), out int pw))
                    _displayVM.CanvasWidth = pw;
                if (_prefsData.TryGetValue("oledHeight", out var hObj) && int.TryParse(hObj?.ToString(), out int ph))
                    _displayVM.CanvasHeight = ph;
            }

            // 初始化预设尺寸下拉
            cbOLEDPreset.Items.Clear();
            string[] presets = { "128×64", "128×128", "128×160", "240×240", "256×64", "256×128", "320×240", "480×320", "640×320", "800×480" };
            string[] tags    = { "128,64","128,128","128,160","240,240","256,64","256,128","320,240","480,320","640,320","800,480" };
            for (int i = 0; i < presets.Length; i++)
                cbOLEDPreset.Items.Add(new ComboBoxItem { Content = presets[i], Tag = tags[i] });
            string curTag = _displayVM.CanvasWidth + "," + _displayVM.CanvasHeight;
            cbOLEDPreset.SelectedItem = null;
            for (int i = 0; i < tags.Length; i++)
                if (tags[i] == curTag) { cbOLEDPreset.SelectedIndex = i; break; }

            // 初始化尺寸输入框
            tbOLEDWidth.Text = _displayVM.CanvasWidth.ToString();
            tbOLEDHeight.Text = _displayVM.CanvasHeight.ToString();

            RefreshOLEDUI();
        }

        // ═══════════════════════════════════════
        //  OLED 渲染
        // ═══════════════════════════════════════

        private void RefreshOLEDUI()
        {
            if (_displayVM == null) return;
            int w = _displayVM.CanvasWidth, h = _displayVM.CanvasHeight;
            oledCanvas.Width = w; oledCanvas.Height = h;
            oledCanvas.Children.Clear(); _oledTexts.Clear();

            bool hasItems = _displayVM.Items.Count > 0;
            oledEmptyHint.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;

            // ── 坐标轴刻度 ──
            var axisBrush = (Brush)FindResource("TextMutedBrush");
            var axisFont = new FontFamily("Sarasa Mono SC, Consolas, Courier New");

            oledXAxis.Children.Clear();
            oledXAxis.ColumnDefinitions.Clear();
            oledXAxis.ColumnDefinitions.Add(new ColumnDefinition());
            {
                var x0 = new TextBlock { Text = "0", FontSize = 9, Foreground = axisBrush, FontFamily = axisFont, HorizontalAlignment = HorizontalAlignment.Left };
                oledXAxis.Children.Add(x0);
                var xMid = new TextBlock { Text = (w / 2).ToString(), FontSize = 9, Foreground = axisBrush, FontFamily = axisFont, HorizontalAlignment = HorizontalAlignment.Center };
                oledXAxis.Children.Add(xMid);
                var xW = new TextBlock { Text = w.ToString(), FontSize = 9, Foreground = axisBrush, FontFamily = axisFont, HorizontalAlignment = HorizontalAlignment.Right };
                oledXAxis.Children.Add(xW);
            }

            oledYAxis.Children.Clear();
            oledYAxis.RowDefinitions.Clear();
            oledYAxis.RowDefinitions.Add(new RowDefinition());
            {
                var y0 = new TextBlock { Text = "0", FontSize = 9, Foreground = axisBrush, FontFamily = axisFont, VerticalAlignment = VerticalAlignment.Top };
                oledYAxis.Children.Add(y0);
                var yMid = new TextBlock { Text = (h / 2).ToString(), FontSize = 9, Foreground = axisBrush, FontFamily = axisFont, VerticalAlignment = VerticalAlignment.Center };
                oledYAxis.Children.Add(yMid);
                var yH = new TextBlock { Text = h.ToString(), FontSize = 9, Foreground = axisBrush, FontFamily = axisFont, VerticalAlignment = VerticalAlignment.Bottom };
                oledYAxis.Children.Add(yH);
            }

            // ── 用户数据文本 ──
            foreach (var item in _displayVM.Items)
            {
                Brush textBrush = ParseColorBrush(item.Color) ?? Brushes.White;
                var tb = new TextBlock {
                    Text = item.Text, FontSize = item.FontSize,
                    FontFamily = axisFont, Foreground = textBrush,
                };
                Canvas.SetLeft(tb, item.X);
                Canvas.SetTop(tb, item.Y);
                oledCanvas.Children.Add(tb);
                _oledTexts[item.X + "," + item.Y] = tb;
            }
        }

        private static Brush ParseColorBrush(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return null;
            try
            {
                if (!hex.StartsWith("#")) hex = "#" + hex;
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }
            catch { return null; }
        }

        // ——— 操作 ———
        private void btnOLEDClear_Click(object sender, RoutedEventArgs e)
        {
            if (_displayVM == null) return;
            _displayVM.Clear();
            RefreshOLEDUI();
            if (_session != null && _session.IsOpen)
                SendRaw("[display-clear]", appendLineEnding: true);
        }

        private void cbOLEDPreset_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_displayVM == null || cbOLEDPreset.SelectedItem == null) return;
            var tag = (cbOLEDPreset.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (tag == null) return;
            var parts = tag.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
            {
                tbOLEDWidth.Text = w.ToString(); tbOLEDHeight.Text = h.ToString();
                ApplyOLEDSize();
            }
        }

        private void tbOLEDSize_LostFocus(object sender, RoutedEventArgs e) { ApplyOLEDSize(); }
        private void btnOLEDSizeApply_Click(object sender, RoutedEventArgs e) { ApplyOLEDSize(); }

        private void ApplyOLEDSize()
        {
            if (_displayVM == null) { InitOLEDPanel(); if (_displayVM == null) return; }
            if (!int.TryParse(tbOLEDWidth.Text, out int w) || w < 64 || w > 2000)
            { tbOLEDWidth.Text = _displayVM.CanvasWidth.ToString(); w = _displayVM.CanvasWidth; }
            if (!int.TryParse(tbOLEDHeight.Text, out int h) || h < 32 || h > 2000)
            { tbOLEDHeight.Text = _displayVM.CanvasHeight.ToString(); h = _displayVM.CanvasHeight; }
            _displayVM.CanvasWidth = w; _displayVM.CanvasHeight = h;
            oledCanvas.Width = w; oledCanvas.Height = h;
            RefreshOLEDUI(); SaveOLEDPrefs();
        }

        // ═══════════════════════════════════════
        //  协议处理 & 持久化
        // ═══════════════════════════════════════

        private void HandleDisplayMessage(int x, int y, string text, int fontSize, string color = null)
        {
            InitOLEDPanel();
            _displayVM.SetText(x, y, text, fontSize, color);
            Dispatcher.InvokeAsync(() => { RefreshOLEDUI(); });
        }

        private void HandleDisplayClear()
        {
            InitOLEDPanel();
            _displayVM.Clear();
            Dispatcher.InvokeAsync(() => { RefreshOLEDUI(); });
        }

        private void SaveOLEDPrefs()
        {
            if (_displayVM == null || _prefsData == null) return;
            _prefsData["oledWidth"] = _displayVM.CanvasWidth;
            _prefsData["oledHeight"] = _displayVM.CanvasHeight;
            _prefs.Save(_prefsData);
        }
    }
}
