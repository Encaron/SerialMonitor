using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace 串口助手
{
    /// <summary>
    /// 偏好存取服务：prefs.json 替代原来的 window.cfg（Key=Value 文本格式）。
    /// Phase 6 升级 .NET 8，已切换为 System.Text.Json。
    /// </summary>
    public class PreferenceService
    {
        private readonly string _filePath;

        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SerialMonitor");
        private static readonly string LegacyDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "串口助手WPF");

        public PreferenceService()
        {
            _filePath = Path.Combine(DataDir, "prefs.json");
            // 首次启动：若新目录不存在但旧目录存在 → 迁移
            if (!Directory.Exists(DataDir) && Directory.Exists(LegacyDir))
            {
                try { MigrateDataDir(); } catch { }
            }
        }

        private static void MigrateDataDir()
        {
            Directory.CreateDirectory(DataDir);
            foreach (var file in Directory.GetFiles(LegacyDir, "*.*", SearchOption.TopDirectoryOnly))
            {
                string dest = Path.Combine(DataDir, Path.GetFileName(file));
                File.Copy(file, dest, overwrite: false);
            }
        }

        /// <summary>
        /// 加载偏好。文件不存在或损坏时返回默认值（不抛异常）。
        /// </summary>
        public Dictionary<string, object> Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return CreateDefaults();

                string json = File.ReadAllText(_filePath);
                using var doc = JsonDocument.Parse(json);
                var prefs = JsonElementToObject(doc.RootElement) as Dictionary<string, object>;
                return prefs ?? CreateDefaults();
            }
            catch
            {
                return CreateDefaults();
            }
        }

        /// <summary>
        /// 保存偏好。序列化失败时静默忽略（不影响程序关闭）。
        /// </summary>
        public void Save(Dictionary<string, object> prefs)
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath);
                Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(prefs, options);
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // 静默失败，不影响程序关闭
            }
        }

        /// <summary>
        /// 递归将 JsonElement 树转换为原生 .NET 类型（Dictionary→string→object / List→object / double / bool / string / null），
        /// 保持与 JavaScriptSerializer 的 Deserialize 行为完全一致。
        /// </summary>
        private static object JsonElementToObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var prop in element.EnumerateObject())
                        dict[prop.Name] = JsonElementToObject(prop.Value);
                    return dict;
                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                        list.Add(JsonElementToObject(item));
                    return list;
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    // 优先用 double（prefs.json 中数值都用 double）
                    if (element.TryGetDouble(out double d)) return d;
                    return element.GetRawText();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                default:
                    return element.GetRawText();
            }
        }

        /// <summary>
        /// 迁移旧的 window.cfg → prefs.json（首次升级时自动执行）
        /// </summary>
        public Dictionary<string, object> MigrateFromLegacy()
        {
            // 优先加载已存在的 prefs.json（迁移已完成的情况）
            if (File.Exists(_filePath))
                return Load();

            string legacyPath = Path.Combine(LegacyDir, "window.cfg");

            var prefs = CreateDefaults();

            if (!File.Exists(legacyPath))
                return prefs;

            try
            {
                foreach (var line in File.ReadAllLines(legacyPath))
                {
                    int idx = line.IndexOf('=');
                    if (idx <= 0) continue;
                    string key = line.Substring(0, idx);
                    string val = line.Substring(idx + 1);

                    switch (key)
                    {
                        case "Left":   SetWindowValue(prefs, "left",   val); break;
                        case "Top":    SetWindowValue(prefs, "top",    val); break;
                        case "Width":  SetWindowValue(prefs, "width",  val); break;
                        case "Height": SetWindowValue(prefs, "height", val); break;
                        case "State":  ((Dictionary<string, object>)prefs["window"])["maximized"] = val == "Maximized"; break;
                        case "Theme":  prefs["theme"] = val; break;
                        case "LastPort": prefs["lastPort"] = val; break;
                        case "LineEnding":
                            ((Dictionary<string, object>)prefs["preferences"])["lineEnding"] = val;
                            break;
                        case "AutoClear":
                            ((Dictionary<string, object>)prefs["preferences"])["autoClear"] = val == "1";
                            break;
                        case "AutoReconnect":
                            ((Dictionary<string, object>)prefs["preferences"])["autoReconnect"] = val == "1";
                            break;
                        case "ShowEcho":
                            ((Dictionary<string, object>)prefs["preferences"])["showEcho"] = val == "1";
                            break;
                        case "ShowLineNumbers":
                            ((Dictionary<string, object>)prefs["preferences"])["showLineNumbers"] = val == "1";
                            break;
                        case "PersistTraffic":
                            ((Dictionary<string, object>)prefs["preferences"])["persistTraffic"] = val == "1";
                            break;
                        case "SeparateSystemLog":
                            ((Dictionary<string, object>)prefs["preferences"])["separateSystemLog"] = val == "1";
                            break;
                    }
                }

                // 迁移后立即保存为 JSON，下次启动直接用 prefs.json
                Save(prefs);

                // 重命名旧文件为 .bak（保留备份）
                try { File.Move(legacyPath, legacyPath + ".bak"); } catch { }
            }
            catch { /* 迁移失败用默认值 */ }

            return prefs;
        }

        private static void SetWindowValue(Dictionary<string, object> prefs, string key, string val)
        {
            var window = (Dictionary<string, object>)prefs["window"];
            if (double.TryParse(val, out double d))
                window[key] = d;
        }

        private static Dictionary<string, object> CreateDefaults()
        {
            return new Dictionary<string, object>
            {
                ["window"] = new Dictionary<string, object>
                {
                    ["left"] = 100.0,
                    ["top"] = 50.0,
                    ["width"] = 920.0,
                    ["height"] = 560.0,
                    ["maximized"] = false,
                },
                ["theme"] = "Light",
                ["lastPort"] = "",
                ["preferences"] = new Dictionary<string, object>
                {
                    ["lineEnding"] = "\\r\\n",
                    ["autoClear"] = false,
                    ["autoReconnect"] = true,
                    ["showEcho"] = true,
                    ["showLineNumbers"] = true,
                    ["persistTraffic"] = false,
                    ["separateSystemLog"] = true,
                },
                ["quickSends"] = new Dictionary<string, object>(),
                ["keys"] = new List<object>(),
                ["sliders"] = new List<object>(),
                ["plotSettings"] = new Dictionary<string, object>(),
            };
        }
    }
}
