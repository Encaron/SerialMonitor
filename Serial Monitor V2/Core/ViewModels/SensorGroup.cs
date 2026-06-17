using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace 串口助手
{
    /// <summary>
    /// 一个组 = 一个灰色圆角框。组内 Items 可以是卡片或 "---" 换行标记。
    /// Items 中按顺序排列，渲染时遇 "---" 拆分为多个 WrapPanel。
    /// </summary>
    public class SensorGroup
    {
        /// <summary>组名（用户可命名）。null 或空 = 不显示标签。</summary>
        public string Name { get; set; }

        /// <summary>组内的卡片和换行标记。"---" 字符串 = 排间换行。</summary>
        public ObservableCollection<object> Items { get; set; } = new();

        public IEnumerable<SensorCardViewModel> Cards
            => Items.OfType<SensorCardViewModel>();

        public void AddCard(SensorCardViewModel card) => Items.Add(card);
        public void AddLineBreak() => Items.Add("---");
        public void RemoveItem(object item) => Items.Remove(item);

        public Dictionary<string, object> ToDict() => new()
        {
            ["name"] = Name ?? "",
            ["items"] = Items.Select(item =>
            {
                if (item is SensorCardViewModel card)
                    return (object)card.ToDict();
                return "---";
            }).ToList(),
        };

        public static SensorGroup FromDict(Dictionary<string, object> d)
        {
            var group = new SensorGroup
            {
                Name = d.GetValueOrDefault("name")?.ToString() ?? ""
            };
            if (d.TryGetValue("items", out var itemsObj)
                && itemsObj is System.Collections.IList itemList)
            {
                foreach (var item in itemList)
                {
                    if (item is string s && s == "---")
                        group.Items.Add("---");
                    else if (item is Dictionary<string, object> cd)
                        group.Items.Add(SensorCardViewModel.FromDict(cd));
                }
            }
            return group;
        }
    }
}
