using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace 串口助手
{
    /// <summary>
    /// 传感面板顶层 ViewModel——管理组列表、数据路由、编辑状态、持久化。
    /// </summary>
    public class SensorPanelViewModel
    {
        public string Title { get; set; } = "传感面板";
        public ObservableCollection<SensorGroup> Groups { get; } = new();
        public bool IsActive { get; set; } = true;
        public bool IsEditMode { get; set; }

        /// <summary>手动删除的卡片名——下次同名数据不再自动重建。</summary>
        public HashSet<string> DeletedNames { get; } = new();

        // ═══ 组操作 ═══

        public SensorGroup AddGroup(string name = null)
        {
            var group = new SensorGroup { Name = name ?? "" };
            Groups.Add(group);
            return group;
        }

        public void RemoveGroup(SensorGroup group) => Groups.Remove(group);

        // ═══ 卡片操作 ═══

        public void AddCardToGroup(SensorGroup group, string type, string name)
        {
            group.Items.Add(new SensorCardViewModel { Type = type, Name = name });
        }

        public void RemoveCard(SensorCardViewModel card)
        {
            DeletedNames.Add(card.Name);
            foreach (var group in Groups)
                group.Items.Remove(card);
        }

        /// <summary>跨所有组搜索同名卡片。同名 = 同一张卡。</summary>
        public SensorCardViewModel FindByName(string name)
            => Groups.SelectMany(g => g.Cards).FirstOrDefault(c => c.Name == name);

        // ═══ 数据路由 ═══

        /// <summary>
        /// sensor 类消息路由：找同名卡 → 更新值/辅助 → 若不存在且未进删除名单则自动创建。
        /// </summary>
        public SensorCardViewModel OnSensorMessage(string subType, string name,
            string value, string aux)
        {
            if (DeletedNames.Contains(name)) return null;

            var card = FindByName(name);
            if (card == null)
            {
                if (Groups.Count == 0) AddGroup();
                card = new SensorCardViewModel { Type = subType, Name = name, SubType = subType };
                Groups[^1].Items.Add(card);
            }
            card.SubType = subType;
            // 首次出现时 Type 可能为空，用实际子类型更新
            if (card.Type != subType && subType != null)
                card.Type = subType;
            card.Update(value, aux);
            return card;
        }

        /// <summary>
        /// ctrl 类消息路由：开关卡状态更新。
        /// </summary>
        public SensorCardViewModel OnCtrlMessage(string subType, string name, string action)
        {
            var card = FindByName(name);
            if (card == null)
            {
                if (Groups.Count == 0) AddGroup();
                card = new SensorCardViewModel { Type = "control", Name = name, SubType = subType };
                Groups[^1].Items.Add(card);
            }
            card.SubType = subType;
            card.Status = action;  // "on" / "off"
            card.Value = action;
            return card;
        }

        // ═══ 序列化 ═══

        public List<Dictionary<string, object>> Serialize()
            => Groups.Select(g => g.ToDict()).ToList();

        public void Deserialize(List<object> data)
        {
            Groups.Clear();
            if (data == null) return;
            foreach (var item in data)
            {
                if (item is Dictionary<string, object> d)
                    Groups.Add(SensorGroup.FromDict(d));
            }
        }
    }
}
