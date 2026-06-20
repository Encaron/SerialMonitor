using System.Collections.Generic;
using System.Linq;

namespace 串口助手
{
    /// <summary>
    /// 单个按键的数据模型 —— Phase 4。
    /// 按下/松开各自独立配置发送模式和内容。自锁模式：点一下保持按下，再点松开。
    /// </summary>
    public class KeyViewModel
    {
        /// <summary>按键名字，也是协议 [key,Name,state] 的标识符</summary>
        public string Name { get; set; }

        // ── 按下（Press）──
        /// <summary>按下时的发送模式："text" / "hex" / "packet" / "none"</summary>
        public string PressSendMode { get; set; } = "packet";
        /// <summary>按下时的发送内容（数据包模式可留空，自动生成 [key,Name,down]）</summary>
        public string PressSendValue { get; set; } = "";

        // ── 松开（Release）──
        /// <summary>松开时的发送模式："text" / "hex" / "packet" / "none"</summary>
        public string ReleaseSendMode { get; set; } = "packet";
        /// <summary>松开时的发送内容（数据包模式可留空，自动生成 [key,Name,up]）</summary>
        public string ReleaseSendValue { get; set; } = "";

        // ── 自锁 ──
        /// <summary>自锁模式：true=点一下保持按下再点松开；false=点按即松（瞬发）</summary>
        public bool IsSelfLock { get; set; }

        // ── 运行时状态 ──
        /// <summary>自锁模式下当前是否处于按下状态</summary>
        public bool IsPressed { get; set; }
        /// <summary>从 STM32 收到的 [key,name,down] 反馈状态</summary>
        public bool IsDown { get; set; }

        // ── 外观 ──
        /// <summary>按键宽度（像素），默认 80</summary>
        public double Width { get; set; } = 80;
        /// <summary>按键高度（像素），默认 36</summary>
        public double Height { get; set; } = 36;
        /// <summary>预设色块名称："default"/"red"/"green"/"blue"/"yellow"/"white"/"gray"</summary>
        public string Color { get; set; } = "default";

        // ── 布局 ──
        /// <summary>Shift 大小写切换键（仅键盘布局）</summary>
        public bool IsShiftToggle { get; set; }
        public int LayoutX { get; set; }
        public int LayoutY { get; set; }
        /// <summary>布局分组 ID（同批次创建共享，渲染时隔离）</summary>
        public int GroupId { get; set; }

        // ── 工具 ──

        /// <summary>HEX 模式内容空 → 取名字首字符 ASCII 码</summary>
        private string GetHexValue(string sendValue)
        {
            if (!string.IsNullOrEmpty(sendValue)) return sendValue;
            if (Name.Length > 0 && Name[0] <= 127) return ((int)Name[0]).ToString("X2");
            return "";
        }

        /// <summary>生成实际的按下发送字符串。数据包格式：[key,名字,内容]，内容空则默认"down"</summary>
        public string GetPressContent()
        {
            if (string.IsNullOrEmpty(PressSendMode) || PressSendMode == "none") return "";
            switch (PressSendMode)
            {
                case "text": return PressSendValue ?? "";
                case "hex":  return GetHexValue(PressSendValue);
                default:     return string.Format("[key,{0},{1}]", Name,
                                 string.IsNullOrEmpty(PressSendValue) ? "down" : PressSendValue);
            }
        }

        /// <summary>生成实际的松开发送字符串。数据包格式：[key,名字,内容]，内容空则默认"up"</summary>
        public string GetReleaseContent()
        {
            if (string.IsNullOrEmpty(ReleaseSendMode) || ReleaseSendMode == "none") return "";
            switch (ReleaseSendMode)
            {
                case "text": return ReleaseSendValue ?? "";
                case "hex":  return GetHexValue(ReleaseSendValue);
                default:     return string.Format("[key,{0},{1}]", Name,
                                 string.IsNullOrEmpty(ReleaseSendValue) ? "up" : ReleaseSendValue);
            }
        }

        public KeyViewModel Clone()
        {
            return new KeyViewModel
            {
                Name = Name, PressSendMode = PressSendMode, PressSendValue = PressSendValue,
                ReleaseSendMode = ReleaseSendMode, ReleaseSendValue = ReleaseSendValue,
                IsSelfLock = IsSelfLock, IsPressed = IsPressed, IsDown = IsDown,
                Width = Width, Height = Height, Color = Color,
                IsShiftToggle = IsShiftToggle, LayoutX = LayoutX, LayoutY = LayoutY, GroupId = GroupId,
            };
        }

        public Dictionary<string, object> ToDict()
        {
            return new Dictionary<string, object>
            {
                ["name"] = Name,
                ["pressSendMode"] = PressSendMode, ["pressSendValue"] = PressSendValue,
                ["releaseSendMode"] = ReleaseSendMode, ["releaseSendValue"] = ReleaseSendValue,
                ["isSelfLock"] = IsSelfLock,
                ["width"] = Width, ["height"] = Height, ["color"] = Color,
                ["isShiftToggle"] = IsShiftToggle, ["layoutX"] = LayoutX, ["layoutY"] = LayoutY, ["groupId"] = GroupId,
            };
        }

        public static KeyViewModel FromDict(Dictionary<string, object> d)
        {
            var k = new KeyViewModel
            {
                Name = GetStr(d, "name", ""),
                PressSendMode  = LogicValueMaps.MigrateSendMode(GetStr(d, "pressSendMode", "packet")),
                PressSendValue  = GetStr(d, "pressSendValue", ""),
                ReleaseSendMode = LogicValueMaps.MigrateSendMode(GetStr(d, "releaseSendMode", "packet")),
                ReleaseSendValue = GetStr(d, "releaseSendValue", ""),
                IsSelfLock = d.TryGetValue("isSelfLock", out var v) && v is bool b && b,
                Width  = GetDouble(d, "width", 80),
                Height = GetDouble(d, "height", 36),
                Color  = LogicValueMaps.MigrateColor(GetStr(d, "color", "default")),
                IsShiftToggle = d.TryGetValue("isShiftToggle", out v) && v is bool s && s,
                LayoutX = GetInt(d, "layoutX", 0),
                LayoutY = GetInt(d, "layoutY", 0),
                GroupId = GetInt(d, "groupId", 0),
            };

            // 迁移旧版数据（sendMode/sendValue/isLocked → press/release）
            if (string.IsNullOrEmpty(k.PressSendMode) && string.IsNullOrEmpty(k.ReleaseSendMode))
            {
                string oldMode = LogicValueMaps.MigrateSendMode(GetStr(d, "sendMode", ""));
                string oldValue = GetStr(d, "sendValue", "");
                bool oldLocked = d.TryGetValue("isLocked", out v) && v is bool lb && lb;
                if (!string.IsNullOrEmpty(oldMode))
                {
                    k.PressSendMode = oldMode;
                    k.ReleaseSendMode = oldMode;
                }
                if (!string.IsNullOrEmpty(oldValue))
                {
                    k.PressSendValue = oldValue;
                    k.ReleaseSendValue = oldValue;
                }
                if (oldLocked) k.IsSelfLock = true;
            }

            // 确保有默认值
            if (string.IsNullOrEmpty(k.PressSendMode))  k.PressSendMode = "packet";
            if (string.IsNullOrEmpty(k.ReleaseSendMode)) k.ReleaseSendMode = "packet";

            // 迁移：键盘布局按键的发送值从小写修正为匹配名称（如 Name="F9", Value="f9" → Value="F9"）
            if (!string.IsNullOrEmpty(k.PressSendValue) && k.Name.Any(char.IsUpper)
                && k.PressSendValue == k.Name.ToLowerInvariant())
            {
                k.PressSendValue = k.Name;
            }

            return k;
        }

        private static string GetStr(Dictionary<string, object> d, string key, string def)
            => d.TryGetValue(key, out var v) && v != null ? v.ToString() : def;

        private static double GetDouble(Dictionary<string, object> d, string key, double def)
        {
            if (!d.TryGetValue(key, out var v) || v == null) return def;
            if (v is double dv) return dv;
            if (v is int iv) return iv;
            if (v is long lv) return lv;
            if (v is decimal mv) return (double)mv;
            if (double.TryParse(v.ToString(), out double pv)) return pv;
            return def;
        }

        private static int GetInt(Dictionary<string, object> d, string key, int def)
        {
            if (!d.TryGetValue(key, out var v) || v == null) return def;
            if (v is int iv) return iv;
            if (v is long lv) return (int)lv;
            if (v is double dv) return (int)dv;
            if (int.TryParse(v.ToString(), out int pv)) return pv;
            return def;
        }
    }
}
