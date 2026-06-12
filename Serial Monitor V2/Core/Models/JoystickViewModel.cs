using System.Collections.Generic;

namespace 串口助手
{
    /// <summary>
    /// 单个摇杆轴数据模型 —— Phase 4。
    /// 每个摇杆有 X/Y 两个轴，值范围 [-1, 1]。
    /// </summary>
    public class JoystickViewModel
    {
        /// <summary>摇杆编号（1 或 2）</summary>
        public int Id { get; set; } = 1;

        /// <summary>X 轴（水平），范围 [-1, 1]</summary>
        public double X { get; set; }

        /// <summary>Y 轴（垂直），范围 [-1, 1]</summary>
        public double Y { get; set; }

        /// <summary>拖拽时的发送间隔（毫秒）</summary>
        public int SendIntervalMs { get; set; } = 100;

        /// <summary>摇杆圆座尺寸（像素）</summary>
        public int PadSize { get; set; } = 140;

        public JoystickViewModel Clone()
        {
            return new JoystickViewModel
            {
                Id = Id, X = X, Y = Y,
                SendIntervalMs = SendIntervalMs, PadSize = PadSize,
            };
        }

        public Dictionary<string, object> ToDict()
        {
            return new Dictionary<string, object>
            {
                ["id"] = Id, ["x"] = X, ["y"] = Y,
                ["sendIntervalMs"] = SendIntervalMs, ["padSize"] = PadSize,
            };
        }

        public static JoystickViewModel FromDict(Dictionary<string, object> d)
        {
            return new JoystickViewModel
            {
                Id = GetInt(d, "id", 1),
                X = GetDouble(d, "x", 0),
                Y = GetDouble(d, "y", 0),
                SendIntervalMs = GetInt(d, "sendIntervalMs", 100),
                PadSize = GetInt(d, "padSize", 140),
            };
        }

        private static double GetDouble(Dictionary<string, object> d, string key, double def)
        {
            if (!d.TryGetValue(key, out var v) || v == null) return def;
            if (v is double dv) return dv;
            if (v is int iv) return iv;
            if (double.TryParse(v.ToString(), out double pv)) return pv;
            return def;
        }

        private static int GetInt(Dictionary<string, object> d, string key, int def)
        {
            if (!d.TryGetValue(key, out var v) || v == null) return def;
            if (v is int iv) return iv;
            if (v is long lv) return (int)lv;
            if (int.TryParse(v.ToString(), out int pv)) return pv;
            return def;
        }
    }
}
