using System;
using System.Collections.Generic;
using System.Globalization;

namespace 串口助手
{
    /// <summary>
    /// 单张传感卡片的数据模型。Type 是 string（非 enum）——永远小写英文，prefs.json 存字符串。
    ///
    /// 方向：前 6 个面板是 Array 思维（同类型排列），传感面板是 Cluster 思维（异质混搭）——
    /// 这个 VM 是远期 CardViewModel 基类的原型。保持数据层干净——渲染逻辑放 Panels/Sensors.cs。
    /// </summary>
    public class SensorCardViewModel
    {
        /// <summary>卡片类型：temp / humidity / pressure / status / control / motor / slider / generic</summary>
        public string Type { get; set; }

        /// <summary>协议子类型：led / relay / slider / temp / humidity 等——原样保留 MCU 发来的第二字段</summary>
        public string SubType { get; set; }

        /// <summary>卡片名 = 协议第三字段 = 固件端命名。跨所有组唯一。</summary>
        public string Name { get; set; }

        /// <summary>当前值（字符串形式）。空字符串 → 显示 "--"，不进 History。</summary>
        public string Value { get; set; } = "--";

        /// <summary>辅助参数（可选）。有则显，无则 Collapsed。</summary>
        public string AuxText { get; set; }

        /// <summary>仅 status 类：online / alarm / error / offline</summary>
        public string Status { get; set; }

        /// <summary>仅 status 类告警时：告警原因文字</summary>
        public string AlarmReason { get; set; }

        /// <summary>通用卡自定义颜色 hex。null → 走默认灰。</summary>
        public string ColorHex { get; set; }

        /// <summary>通用卡自定义单位。null/空 → 纯数字无单位。</summary>
        public string CustomUnit { get; set; }

        /// <summary>通用卡是否显示迷你波形。true = 面积图，false = 纯数字卡。</summary>
        public bool ShowWaveform { get; set; } = true;

        /// <summary>仅 slider 卡：最小值</summary>
        public double SliderMin { get; set; }

        /// <summary>仅 slider 卡：最大值</summary>
        public double SliderMax { get; set; } = 100;

        /// <summary>仅 slider 卡：步长</summary>
        public double SliderStep { get; set; } = 1;

        /// <summary>仅 slider 卡：拖拽发送节流间隔（毫秒），>=20</summary>
        public int SendIntervalMs { get; set; } = 200;

        /// <summary>仅 pressure 卡：进度条满格气压基准值（hPa），默认 1013.25（标准海平面）</summary>
        public double PressureBaseline { get; set; } = 1013.25;

        /// <summary>上次收到数据的时间（离线超时检测用）</summary>
        public DateTime LastSeen { get; set; } = DateTime.Now;

        /// <summary>迷你波形历史（30 点）</summary>
        public FixedSizeQueue<double> History { get; } = new(30);

        /// <summary>数值单位。status/control 无单位返回 ""。</summary>
        public string GetUnit()
        {
            return Type switch
            {
                "temp"     => "°C",
                "humidity" => "%",
                "pressure" => "hPa",
                "motor"    => " RPM",
                "generic"  => CustomUnit ?? "",
                _          => "",
            };
        }

        /// <summary>
        /// 卡片竖条色（= 主值色 = 波形色，三处同源）。
        /// 亮/暗主题给不同 hex，避免暗色下过于刺眼。
        /// </summary>
        public string GetAccentHex(bool isDark)
        {
            return Type switch
            {
                "temp"     => isDark ? "#FFC107" : "#F5A623",
                "humidity" => isDark ? "#42A5F5" : "#2196F3",
                "pressure" => isDark ? "#26C6DA" : "#00BCD4",
                "motor"    => isDark ? "#AB47BC" : "#9C27B0",
                "slider"   => isDark ? "#5C6BC0" : "#3F51B5",
                "generic"  => ColorHex ?? (isDark ? "#78909C" : "#607D8B"),
                "control"  => Status switch
                {
                    "on"      => isDark ? "#FFA726" : "#FF9800",
                    _         => isDark ? "#757575" : "#9E9E9E",
                },
                "status"   => Status switch
                {
                    "alarm"   => isDark ? "#EF5350" : "#F44336",
                    "error"   => isDark ? "#EF5350" : "#F44336",
                    "offline" => isDark ? "#EF5350" : "#F44336",
                    _         => isDark ? "#66BB6A" : "#4CAF50",
                },
                _ => isDark ? "#B0B0B0" : "#888888",
            };
        }

        public void Update(string value, string aux)
        {
            LastSeen = DateTime.Now;

            if (Type == "status")
            {
                Status = value;
                Value = string.IsNullOrEmpty(value) ? "--" : value;
                AlarmReason = aux;
            }
            else if (Type == "slider")
            {
                // 滑杆卡：MCU 确认值 → 回填 Value（驱动 Slider 同步）
                Value = string.IsNullOrEmpty(value) ? Value : value;
                AuxText = aux;
            }
            else
            {
                Value = string.IsNullOrEmpty(value) ? "--" : value;
                AuxText = aux;
                if (double.TryParse(value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double v))
                    History.Enqueue(v);
            }
        }

        public Dictionary<string, object> ToDict()
        {
            var d = new Dictionary<string, object>
            {
                ["type"] = Type,
                ["name"] = Name,
            };
            if (SubType != null) d["subtype"] = SubType;
            if (ColorHex != null) d["color"] = ColorHex;
            if (CustomUnit != null) d["unit"] = CustomUnit;
            if (Type == "generic") d["waveform"] = ShowWaveform;
            if (Type == "slider")
            {
                d["min"] = SliderMin;
                d["max"] = SliderMax;
                d["step"] = SliderStep;
                if (SendIntervalMs != 200) d["interval"] = SendIntervalMs;
            }
            if (Type == "pressure")
                d["pBase"] = PressureBaseline;
            return d;
        }

        public static SensorCardViewModel FromDict(Dictionary<string, object> d)
        {
            if (d == null) return null;
            var card = new SensorCardViewModel
            {
                Type = d.GetValueOrDefault("type")?.ToString() ?? "temp",
                Name = d.GetValueOrDefault("name")?.ToString() ?? "Untitled",
            };
            if (d.TryGetValue("subtype", out var su)) card.SubType = su?.ToString();
            if (d.TryGetValue("color", out var co)) card.ColorHex = co?.ToString();
            if (d.TryGetValue("unit", out var un)) card.CustomUnit = un?.ToString();
            if (d.TryGetValue("waveform", out var wf) && wf is bool bWf) card.ShowWaveform = bWf;
            if (d.TryGetValue("min", out var mn) && mn is double dMn) card.SliderMin = dMn;
            if (d.TryGetValue("max", out var mx) && mx is double dMx) card.SliderMax = dMx;
            if (d.TryGetValue("step", out var st) && st is double dSt) card.SliderStep = dSt;
            if (d.TryGetValue("interval", out var iv) && iv is double dIv && dIv >= 20) card.SendIntervalMs = (int)dIv;
            if (d.TryGetValue("pBase", out var pb) && pb is double dPb && dPb > 0) card.PressureBaseline = dPb;
            return card;
        }
    }
}
