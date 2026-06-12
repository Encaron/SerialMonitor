using System.Collections.Generic;

namespace 串口助手
{
    /// <summary>
    /// 单个按键的数据模型 —— Phase 4 实现。
    /// 每个按键独立配置：名字、发送模式（文本/HEX/数据包）、发送值、锁定状态、外观。
    /// </summary>
    public class KeyViewModel
    {
        /// <summary>按键名字，同时也是协议中 [key,Name,state] 的标识符</summary>
        public string Name { get; set; }

        /// <summary>
        /// 发送模式："文本" / "HEX" / "数据包"
        /// 数据包模式：发送 [key,Name,state] 格式的完整协议消息
        /// </summary>
        public string SendMode { get; set; } = "数据包";

        /// <summary>发送值：文本模式=文本内容，HEX模式=HEX字符串，数据包模式=down/up 对应的发送内容</summary>
        public string SendValue { get; set; } = "";

        /// <summary>锁定：true 时按键只显示状态不收不发</summary>
        public bool IsLocked { get; set; }

        /// <summary>按键宽度（像素），默认 80</summary>
        public double Width { get; set; } = 80;

        /// <summary>按键高度（像素），默认 36</summary>
        public double Height { get; set; } = 36;

        /// <summary>
        /// 按键颜色：预设色块名称
        /// "默认" / "红色" / "绿色" / "蓝色" / "黄色" / "白色" / "灰色"
        /// </summary>
        public string Color { get; set; } = "默认";

        /// <summary>Shift 大小写切换键（仅键盘布局预设）—— 不发送数据，只控制创建时的大小写模式</summary>
        public bool IsShiftToggle { get; set; }

        /// <summary>键盘布局中 X 坐标位置（仅键盘布局预设使用）</summary>
        public int LayoutX { get; set; }

        /// <summary>键盘布局中 Y 坐标位置（仅键盘布局预设使用）</summary>
        public int LayoutY { get; set; }

        /// <summary>从 STM32 收到的当前按压状态：true=down, false=up</summary>
        public bool IsDown { get; set; }

        /// <summary>布局分组 ID：同一批次创建的按键共享同一个 GroupId，渲染时隔离</summary>
        public int GroupId { get; set; }

        /// <summary>
        /// 创建深拷贝
        /// </summary>
        public KeyViewModel Clone()
        {
            return new KeyViewModel
            {
                Name = this.Name,
                SendMode = this.SendMode,
                SendValue = this.SendValue,
                IsLocked = this.IsLocked,
                Width = this.Width,
                Height = this.Height,
                Color = this.Color,
                IsShiftToggle = this.IsShiftToggle,
                LayoutX = this.LayoutX,
                LayoutY = this.LayoutY,
                IsDown = this.IsDown,
                GroupId = this.GroupId,
            };
        }

        /// <summary>
        /// 序列化为 JSON 存储格式
        /// </summary>
        public Dictionary<string, object> ToDict()
        {
            return new Dictionary<string, object>
            {
                ["name"] = Name,
                ["sendMode"] = SendMode,
                ["sendValue"] = SendValue,
                ["isLocked"] = IsLocked,
                ["width"] = Width,
                ["height"] = Height,
                ["color"] = Color,
                ["isShiftToggle"] = IsShiftToggle,
                ["layoutX"] = LayoutX,
                ["layoutY"] = LayoutY,
                ["groupId"] = GroupId,
            };
        }

        /// <summary>
        /// 从 JSON 字典反序列化。
        /// JavaScriptSerializer 将整数读为 int、浮点数读为 double——统一转换。
        /// </summary>
        public static KeyViewModel FromDict(Dictionary<string, object> d)
        {
            return new KeyViewModel
            {
                Name = GetStr(d, "name", ""),
                SendMode = GetStr(d, "sendMode", "数据包"),
                SendValue = GetStr(d, "sendValue", ""),
                IsLocked = d.TryGetValue("isLocked", out var v) && v is bool b && b,
                Width = GetDouble(d, "width", 80),
                Height = GetDouble(d, "height", 36),
                Color = GetStr(d, "color", "默认"),
                IsShiftToggle = d.TryGetValue("isShiftToggle", out v) && v is bool s && s,
                LayoutX = GetInt(d, "layoutX", 0),
                LayoutY = GetInt(d, "layoutY", 0),
                GroupId = GetInt(d, "groupId", 0),
            };
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
