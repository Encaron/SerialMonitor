using System.Collections.Generic;
using System.Linq;

namespace 串口助手
{
    /// <summary>
    /// 滑杆面板 ViewModel —— Phase 4 实现。
    /// 管理滑杆集合 + 编辑模式 + JSON 序列化。
    /// </summary>
    public class SliderPanelViewModel
    {
        public string Title { get; set; } = "滑杆";

        /// <summary>所有滑杆</summary>
        public List<SliderViewModel> Sliders { get; set; } = new List<SliderViewModel>();

        /// <summary>是否处于编辑模式</summary>
        public bool IsEditMode { get; set; }

        /// <summary>添加一个滑杆</summary>
        public SliderViewModel AddSlider(string name)
        {
            var s = new SliderViewModel { Name = name };
            Sliders.Add(s);
            return s;
        }

        /// <summary>删除指定滑杆</summary>
        public bool RemoveSlider(SliderViewModel s) => Sliders.Remove(s);

        /// <summary>根据名字查找滑杆</summary>
        public SliderViewModel FindByName(string name)
            => Sliders.FirstOrDefault(s => s.Name == name);

        /// <summary>清空所有滑杆</summary>
        public void ClearAll() => Sliders.Clear();

        /// <summary>更新指定滑杆的值（来自 STM32 协议消息）</summary>
        public void SetSliderValue(string name, double value)
        {
            var s = FindByName(name);
            if (s != null) s.Value = value;
        }

        // ——— 序列化 ———

        public List<Dictionary<string, object>> SerializeSliders()
            => Sliders.Select(s => s.ToDict()).ToList();

        public void DeserializeSliders(List<Dictionary<string, object>> data)
        {
            Sliders.Clear();
            if (data == null) return;
            foreach (var d in data)
                Sliders.Add(SliderViewModel.FromDict(d));
        }

        // ——— 颜色 ———

        public static string GetColorHex(string colorName, bool isDark)
        {
            switch (colorName)
            {
                case "红色": return isDark ? "#E74856" : "#C42B1C";
                case "绿色": return isDark ? "#16C60C" : "#107C10";
                case "蓝色": return isDark ? "#3B78FF" : "#0078D4";
                case "黄色": return isDark ? "#F9F1A5" : "#E0C300";
                case "白色": return isDark ? "#CCCCCC" : "#666666";
                case "灰色": return isDark ? "#555555" : "#999999";
                default: return colorName != null && colorName.StartsWith("#") ? colorName : null;
            }
        }
    }
}
