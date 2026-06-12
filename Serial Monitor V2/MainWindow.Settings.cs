using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace 串口助手
{
    public partial class MainWindow : Window
    {
        // ==================================================================
        //  窗口位置记忆 & 偏好持久化（已迁移至 PreferenceService + prefs.json）
        // ==================================================================

        private PreferenceService _prefs;
        private Dictionary<string, object> _prefsData;

        private void InitPreferences()
        {
            _prefs = new PreferenceService();
            // 首次启动时从旧 window.cfg 迁移
            _prefsData = _prefs.MigrateFromLegacy();
        }

        private void LoadWindowSettings()
        {
            InitPreferences();

            try
            {
                var window = (Dictionary<string, object>)_prefsData["window"];

                double left   = Convert.ToDouble(window["left"]);
                double top    = Convert.ToDouble(window["top"]);
                double width  = Convert.ToDouble(window["width"]);
                double height = Convert.ToDouble(window["height"]);
                bool maximized = Convert.ToBoolean(window["maximized"]);

                this.Left   = Math.Max(0, Math.Min(left,   SystemParameters.PrimaryScreenWidth - 100));
                this.Top    = Math.Max(0, Math.Min(top,    SystemParameters.PrimaryScreenHeight - 100));
                this.Width  = Math.Max(MinWidth,  Math.Min(width,  SystemParameters.PrimaryScreenWidth));
                this.Height = Math.Max(MinHeight, Math.Min(height, SystemParameters.PrimaryScreenHeight));

                if (maximized)
                    this.WindowState = WindowState.Maximized;

                // 加载主题偏好
                string theme = _prefsData["theme"] as string;
                if (theme == "Dark")
                    ApplyTheme(true);

                // 加载上次成功打开的串口号
                string lastPort = _prefsData["lastPort"] as string;
                if (!string.IsNullOrEmpty(lastPort))
                    _lastSuccessfulPort = lastPort;
            }
            catch { /* 静默失败，使用默认值 */ }
        }

        private void SaveWindowSettings()
        {
            try
            {
                var window = (Dictionary<string, object>)_prefsData["window"];
                window["left"]   = this.Left;
                window["top"]    = this.Top;
                window["width"]  = this.Width;
                window["height"] = this.Height;
                window["maximized"] = this.WindowState == WindowState.Maximized;

                _prefsData["theme"] = isDarkTheme ? "Dark" : "Light";
                _prefsData["lastPort"] = _lastSuccessfulPort;

                var prefs = (Dictionary<string, object>)_prefsData["preferences"];
                prefs["lineEnding"]  = cbLineEnding.SelectedItem?.ToString() ?? "\\r\\n";
                prefs["autoClear"]   = chkAutoClear.IsChecked == true;
                prefs["autoReconnect"] = chkAutoReconnect.IsChecked == true;
                prefs["showEcho"]    = chkShowEcho.IsChecked == true;
                prefs["showLineNumbers"] = chkShowLineNumbers.IsChecked == true;
                prefs["persistTraffic"] = chkPersistTraffic.IsChecked == true;
                prefs["separateSystemLog"] = chkSeparateSystemLog.IsChecked == true;

                _prefs.Save(_prefsData);
            }
            catch { /* 静默失败，不影响关闭 */ }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowSettings();
        }

        /// <summary>
        /// 从 prefs.json 加载 UI 偏好（ComboBox 选择 + CheckBox 状态）
        /// 需在 InitComboBoxItems + SetDefaultValues 之后调用
        /// </summary>
        private void LoadPreferences()
        {
            try
            {
                var prefs = (Dictionary<string, object>)_prefsData["preferences"];

                if (prefs.TryGetValue("lineEnding", out object le) && le is string leStr)
                {
                    int idx = cbLineEnding.Items.IndexOf(leStr);
                    if (idx >= 0) cbLineEnding.SelectedIndex = idx;
                }
                if (prefs.TryGetValue("autoClear", out object ac))
                    chkAutoClear.IsChecked = Convert.ToBoolean(ac);
                if (prefs.TryGetValue("autoReconnect", out object ar))
                    chkAutoReconnect.IsChecked = Convert.ToBoolean(ar);
                if (prefs.TryGetValue("persistTraffic", out object pt))
                    chkPersistTraffic.IsChecked = Convert.ToBoolean(pt);
                if (prefs.TryGetValue("showEcho", out object se))
                    chkShowEcho.IsChecked = Convert.ToBoolean(se);
                if (prefs.TryGetValue("showLineNumbers", out object sln))
                    chkShowLineNumbers.IsChecked = Convert.ToBoolean(sln);
                if (prefs.TryGetValue("separateSystemLog", out object ssl))
                    chkSeparateSystemLog.IsChecked = Convert.ToBoolean(ssl);
            }
            catch { /* 静默失败 */ }
        }
    }
}
