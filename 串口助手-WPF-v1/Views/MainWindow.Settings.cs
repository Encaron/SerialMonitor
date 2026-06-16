using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace 串口助手
{
    public partial class MainWindow : Window
    {
        // ==================================================================
        //  窗口位置记忆 & 偏好持久化
        // ==================================================================

        private string SettingsFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "串口助手WPF", "window.cfg");

        private void LoadWindowSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath)) return;

                var dict = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(SettingsFilePath))
                {
                    int idx = line.IndexOf('=');
                    if (idx > 0) dict[line.Substring(0, idx)] = line.Substring(idx + 1);
                }

                if (dict.TryGetValue("Left",   out string l) && double.TryParse(l, out double left))
                    this.Left = Math.Max(0, Math.Min(left, SystemParameters.PrimaryScreenWidth - 100));
                if (dict.TryGetValue("Top",    out string t) && double.TryParse(t, out double top))
                    this.Top  = Math.Max(0, Math.Min(top,  SystemParameters.PrimaryScreenHeight - 100));
                if (dict.TryGetValue("Width",  out string w) && double.TryParse(w, out double width))
                    this.Width  = Math.Max(MinWidth,  Math.Min(width,  SystemParameters.PrimaryScreenWidth));
                if (dict.TryGetValue("Height", out string h) && double.TryParse(h, out double height))
                    this.Height = Math.Max(MinHeight, Math.Min(height, SystemParameters.PrimaryScreenHeight));
                if (dict.TryGetValue("State",  out string s) && s == "Maximized")
                    this.WindowState = WindowState.Maximized;

                // 加载主题偏好
                if (dict.TryGetValue("Theme", out string theme) && theme == "Dark")
                {
                    ApplyTheme(true);
                }

                // 加载上次成功打开的串口号
                if (dict.TryGetValue("LastPort", out string lastPort))
                    _lastSuccessfulPort = lastPort;
            }
            catch { /* 静默失败，使用默认值 */ }
        }

        private void SaveWindowSettings()
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsFilePath);
                Directory.CreateDirectory(dir);

                var lines = new[]
                {
                    $"Left={this.Left}",
                    $"Top={this.Top}",
                    $"Width={this.Width}",
                    $"Height={this.Height}",
                    $"State={this.WindowState}",
                    $"Theme={(isDarkTheme ? "Dark" : "Light")}",
                    $"LastPort={_lastSuccessfulPort}",
                    $"LineEnding={cbLineEnding.SelectedItem}",
                    $"AutoClear={(chkAutoClear.IsChecked == true ? "1" : "0")}",
                    $"AutoReconnect={(chkAutoReconnect.IsChecked == true ? "1" : "0")}",
                    $"ShowEcho={(chkShowEcho.IsChecked == true ? "1" : "0")}",
                    $"ShowLineNumbers={(chkShowLineNumbers.IsChecked == true ? "1" : "0")}",
                    $"PersistTraffic={(chkPersistTraffic.IsChecked == true ? "1" : "0")}",
                    $"SeparateSystemLog={(chkSeparateSystemLog.IsChecked == true ? "1" : "0")}",
                };
                File.WriteAllLines(SettingsFilePath, lines);
            }
            catch { /* 静默失败，不影响关闭 */ }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowSettings();
        }

        /// <summary>
        /// 从配置文件加载 UI 偏好（ComboBox 选择 + CheckBox 状态）
        /// 需在 InitComboBoxItems + SetDefaultValues 之后调用
        /// </summary>
        private void LoadPreferences()
        {
            try
            {
                if (!File.Exists(SettingsFilePath)) return;

                var dict = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(SettingsFilePath))
                {
                    int idx = line.IndexOf('=');
                    if (idx > 0) dict[line.Substring(0, idx)] = line.Substring(idx + 1);
                }

                if (dict.TryGetValue("LineEnding", out string le))
                {
                    int idx = cbLineEnding.Items.IndexOf(le);
                    if (idx >= 0) cbLineEnding.SelectedIndex = idx;
                }
                if (dict.TryGetValue("AutoClear", out string ac))
                    chkAutoClear.IsChecked = ac == "1";
                if (dict.TryGetValue("AutoReconnect", out string ar))
                    chkAutoReconnect.IsChecked = ar == "1";
                if (dict.TryGetValue("PersistTraffic", out string pt))
                    chkPersistTraffic.IsChecked = pt == "1";
                if (dict.TryGetValue("ShowEcho", out string se))
                    chkShowEcho.IsChecked = se == "1";
                if (dict.TryGetValue("ShowLineNumbers", out string sln))
                    chkShowLineNumbers.IsChecked = sln == "1";
                if (dict.TryGetValue("SeparateSystemLog", out string ssl))
                    chkSeparateSystemLog.IsChecked = ssl == "1";
            }
            catch { /* 静默失败 */ }
        }
    }
}
