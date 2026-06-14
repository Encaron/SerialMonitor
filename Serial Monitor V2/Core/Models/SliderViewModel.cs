using System.Collections.Generic;

namespace 串口助手
{
    /// <summary>
    /// 单个滑杆的数据模型 —— Phase 4。
    /// </summary>
    public class SliderViewModel
    {
        /// <summary>滑杆名字，也是协议 [slider,Name,Value] 的标识符</summary>
        public string Name { get; set; }

        /// <summary>最小值</summary>
        public double MinValue { get; set; } = 0;

        /// <summary>最大值</summary>
        public double MaxValue { get; set; } = 100;

        /// <summary>当前值</summary>
        public double Value { get; set; }

        /// <summary>步长 / 分辨率（如 0.1 表示保留 1 位小数）</summary>
        public double Step { get; set; } = 1;

        /// <summary>拖拽时的发送间隔（毫秒）</summary>
        public int SendIntervalMs { get; set; } = 200;

        /// <summary>预设色块名称</summary>
        public string Color { get; set; } = "默认";

        /// <summary>轨道风格（"默认" / "极简" / 自定义风格名）</summary>
        public string TrackStyle { get; set; } = "默认";

        /// <summary>拇指风格（"默认" / "极简" / 自定义风格名）</summary>
        public string ThumbStyle { get; set; } = "默认";

        // ——— 工具 ———

        /// <summary>小数位数（从 Step 自动推断）</summary>
        public int DecimalPlaces
        {
            get
            {
                if (Step >= 1) return 0;
                if (Step >= 0.1) return 1;
                if (Step >= 0.01) return 2;
                return 3;
            }
        }

        /// <summary>显示用的格式化值</summary>
        public string DisplayValue => Value.ToString("F" + DecimalPlaces);

        public SliderViewModel Clone()
        {
            return new SliderViewModel
            {
                Name = Name, MinValue = MinValue, MaxValue = MaxValue,
                Value = Value, Step = Step, SendIntervalMs = SendIntervalMs, Color = Color,
                TrackStyle = TrackStyle, ThumbStyle = ThumbStyle,
            };
        }

        public Dictionary<string, object> ToDict()
        {
            return new Dictionary<string, object>
            {
                ["name"] = Name,
                ["minValue"] = MinValue, ["maxValue"] = MaxValue, ["value"] = Value,
                ["step"] = Step, ["sendIntervalMs"] = SendIntervalMs, ["color"] = Color,
                ["trackStyle"] = TrackStyle,
                ["thumbStyle"] = ThumbStyle,
            };
        }

        public static SliderViewModel FromDict(Dictionary<string, object> d)
        {
            return new SliderViewModel
            {
                Name  = GetStr(d, "name", ""),
                MinValue = GetDouble(d, "minValue", 0),
                MaxValue = GetDouble(d, "maxValue", 100),
                Value    = GetDouble(d, "value", 0),
                Step     = GetDouble(d, "step", 1),
                SendIntervalMs = GetInt(d, "sendIntervalMs", 200),
                Color    = GetStr(d, "color", "默认"),
                // 迁移旧的 "style" 字段到 trackStyle（兼容旧 prefs.json）
                TrackStyle = GetStr(d, "trackStyle",
                              GetStr(d, "style", "默认")),
                ThumbStyle = GetStr(d, "thumbStyle",
                              GetStr(d, "style", "默认")),
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
