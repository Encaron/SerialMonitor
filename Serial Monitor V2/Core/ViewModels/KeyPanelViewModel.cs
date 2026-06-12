using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace 串口助手
{
    /// <summary>
    /// 按键面板 ViewModel —— Phase 4 实现。
    /// 管理按键集合 + 编辑模式 + 键盘布局预设 + JSON 序列化。
    /// </summary>
    public class KeyPanelViewModel
    {
        public string Title { get; set; } = "按键";

        /// <summary>所有按键</summary>
        public List<KeyViewModel> Keys { get; set; } = new List<KeyViewModel>();

        /// <summary>是否处于编辑模式</summary>
        public bool IsEditMode { get; set; }

        /// <summary>键盘布局 ⇧ 大小写切换是否激活（仅影响随后创建的键）</summary>
        public bool ShiftActive { get; set; }

        /// <summary>
        /// 添加一个按键
        /// </summary>
        public KeyViewModel AddKey(string name, string sendMode = "数据包", string sendValue = "")
        {
            var key = new KeyViewModel
            {
                Name = name,
                SendMode = sendMode,
                SendValue = string.IsNullOrEmpty(sendValue) ? name : sendValue,
            };
            Keys.Add(key);
            return key;
        }

        /// <summary>
        /// 添加一个按键（从已有对象）
        /// </summary>
        public void AddKey(KeyViewModel key)
        {
            Keys.Add(key);
        }

        /// <summary>
        /// 删除指定按键
        /// </summary>
        public bool RemoveKey(KeyViewModel key)
        {
            return Keys.Remove(key);
        }

        /// <summary>
        /// 根据名字查找按键
        /// </summary>
        public KeyViewModel FindByName(string name)
        {
            return Keys.FirstOrDefault(k => k.Name == name);
        }

        /// <summary>
        /// 清空所有按键
        /// </summary>
        public void ClearAll()
        {
            Keys.Clear();
        }

        /// <summary>
        /// 更新指定按键的 down/up 状态（来自 STM32 协议消息）
        /// </summary>
        public void SetKeyState(string name, bool isDown)
        {
            var key = FindByName(name);
            if (key != null)
                key.IsDown = isDown;
        }

        /// <summary>
        /// 创建键盘布局（QWERTY 风格）
        /// 4 行：数字行 + 上排 + 中排 + 下排
        /// 最后一个是 ⇧ Shift 切换键
        /// </summary>
        public List<KeyViewModel> CreateKeyboardLayout()
        {
            var newKeys = new List<KeyViewModel>();

            // Row 0: 数字行
            string[] row0 = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
            // Row 1: QWERTY 上排
            string[] row1 = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" };
            // Row 2: 中排
            string[] row2 = { "A", "S", "D", "F", "G", "H", "J", "K", "L" };
            // Row 3: 下排 + Shift
            string[] row3 = { "Z", "X", "C", "V", "B", "N", "M" };

            double keyW = 46, keyH = 30;

            for (int i = 0; i < row0.Length; i++)
            {
                newKeys.Add(new KeyViewModel
                {
                    Name = row0[i], SendMode = "数据包", SendValue = row0[i],
                    Width = keyW, Height = keyH, LayoutX = i, LayoutY = 0,
                });
            }
            for (int i = 0; i < row1.Length; i++)
            {
                newKeys.Add(new KeyViewModel
                {
                    Name = row1[i], SendMode = "数据包", SendValue = row1[i].ToLowerInvariant(),
                    Width = keyW, Height = keyH, LayoutX = i, LayoutY = 1,
                });
            }
            for (int i = 0; i < row2.Length; i++)
            {
                newKeys.Add(new KeyViewModel
                {
                    Name = row2[i], SendMode = "数据包", SendValue = row2[i].ToLowerInvariant(),
                    Width = keyW, Height = keyH, LayoutX = i, LayoutY = 2,
                });
            }
            for (int i = 0; i < row3.Length; i++)
            {
                newKeys.Add(new KeyViewModel
                {
                    Name = row3[i], SendMode = "数据包", SendValue = row3[i].ToLowerInvariant(),
                    Width = keyW, Height = keyH, LayoutX = i, LayoutY = 3,
                });
            }
            // Shift 切换键（LayoutX 多跳一列，与字母区留间距）
            newKeys.Add(new KeyViewModel
            {
                Name = "⇧", SendMode = "数据包", SendValue = "",
                Width = keyW + 12, Height = keyH, LayoutX = row3.Length + 1, LayoutY = 3,
                IsShiftToggle = true, IsLocked = true,
            });

            Keys.AddRange(newKeys);
            return newKeys;
        }

        /// <summary>
        /// 创建方向键布局（十字排列：↑ ↓ ← →）
        /// </summary>
        public List<KeyViewModel> CreateDirectionalLayout()
        {
            var newKeys = new List<KeyViewModel>();
            double keyW = 52, keyH = 48;

            newKeys.Add(new KeyViewModel
            { Name = "↑", SendMode = "数据包", SendValue = "up", Width = keyW, Height = keyH,
              LayoutX = 1, LayoutY = 0, });
            newKeys.Add(new KeyViewModel
            { Name = "←", SendMode = "数据包", SendValue = "left", Width = keyW, Height = keyH,
              LayoutX = 0, LayoutY = 1, });
            newKeys.Add(new KeyViewModel
            { Name = "↓", SendMode = "数据包", SendValue = "down", Width = keyW, Height = keyH,
              LayoutX = 1, LayoutY = 1, });
            newKeys.Add(new KeyViewModel
            { Name = "→", SendMode = "数据包", SendValue = "right", Width = keyW, Height = keyH,
              LayoutX = 2, LayoutY = 1, });

            Keys.AddRange(newKeys);
            return newKeys;
        }

        /// <summary>
        /// 创建数字键盘布局（3×4 标准电话键盘，含 Enter）
        /// </summary>
        public List<KeyViewModel> CreateNumpadLayout()
        {
            var newKeys = new List<KeyViewModel>();
            double keyW = 56, keyH = 42;

            string[,] numpad = { { "7", "8", "9" }, { "4", "5", "6" }, { "1", "2", "3" }, { "*", "0", "#" } };
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 3; c++)
                    newKeys.Add(new KeyViewModel
                    {
                        Name = numpad[r, c], SendMode = "数据包", SendValue = numpad[r, c],
                        Width = keyW, Height = keyH, LayoutX = c, LayoutY = r,
                    });

            // Enter 键
            newKeys.Add(new KeyViewModel
            { Name = "Enter", SendMode = "数据包", SendValue = "enter", Width = 56, Height = 88,
              LayoutX = 3, LayoutY = 2, });

            Keys.AddRange(newKeys);
            return newKeys;
        }

        /// <summary>
        /// 序列化所有按键为 JSON 存储格式（不含运行时状态 IsDown）
        /// </summary>
        public List<Dictionary<string, object>> SerializeKeys()
        {
            return Keys.Select(k => k.ToDict()).ToList();
        }

        /// <summary>
        /// 从 JSON 恢复按键列表
        /// </summary>
        public void DeserializeKeys(List<Dictionary<string, object>> data)
        {
            Keys.Clear();
            if (data == null) return;
            foreach (var d in data)
                Keys.Add(KeyViewModel.FromDict(d));
        }

        /// <summary>
        /// 获取预设颜色对应的 WPF Brush 十六进制值
        /// </summary>
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
                default: return null; // "默认" → 使用主题默认按键背景
            }
        }
    }
}
