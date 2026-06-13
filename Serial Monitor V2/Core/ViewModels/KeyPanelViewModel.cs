using System.Collections.Generic;
using System.Linq;

namespace 串口助手
{
    /// <summary>
    /// 按键面板 ViewModel —— Phase 4 实现。
    /// 管理按键集合 + 模块隔离（GroupId）+ 键盘布局预设 + JSON 序列化。
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

        /// <summary>手动添加的按键的默认 GroupId</summary>
        public const int ManualGroupId = 1;

        private int _nextGroupId = 10;

        /// <summary>分配一个新的布局组 ID（不同批次的按键互不穿插）</summary>
        public int NewGroupId() { return _nextGroupId++; }

        /// <summary>
        /// 添加一个手动按键
        /// </summary>
        public KeyViewModel AddKey(string name, string pressSendMode = "数据包", string pressSendValue = "")
        {
            var key = new KeyViewModel
            {
                Name = name,
                PressSendMode = pressSendMode,
                PressSendValue = pressSendValue,
                ReleaseSendMode = "无",
                ReleaseSendValue = "",
                GroupId = ManualGroupId,
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
        /// 创建键盘布局（QWERTY 风格）—— Grid 精确行列
        /// 4 行：数字行 + QWERTY上排 + ASDF中排 + ZXCV下排+⇧
        /// </summary>
        public List<KeyViewModel> CreateKeyboardLayout()
        {
            var newKeys = new List<KeyViewModel>();
            int gid = NewGroupId();

            string[] row0 = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
            string[] row1 = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" };
            string[] row2 = { "A", "S", "D", "F", "G", "H", "J", "K", "L" };
            string[] row3 = { "Z", "X", "C", "V", "B", "N", "M" };

            double keyW = 46, keyH = 30;

            // Row 0: 数字行，列 0-9
            for (int i = 0; i < row0.Length; i++)
                newKeys.Add(MakeKey(row0[i], "", gid, i, 0, keyW, keyH));

            // Row 1: QWERTY，列 0-9
            for (int i = 0; i < row1.Length; i++)
                newKeys.Add(MakeKey(row1[i], "", gid, i, 1, keyW, keyH));

            // Row 2: ASDF，列 0-8
            for (int i = 0; i < row2.Length; i++)
                newKeys.Add(MakeKey(row2[i], "", gid, i, 2, keyW, keyH));

            // Row 3: ZXCV + Shift，列 0-6，Shift 在列 8
            for (int i = 0; i < row3.Length; i++)
                newKeys.Add(MakeKey(row3[i], "", gid, i, 3, keyW, keyH));

            newKeys.Add(new KeyViewModel
            {
                Name = "⇧", PressSendMode = "数据包", PressSendValue = "",
                Width = keyW + 8, Height = keyH, GroupId = gid,
                LayoutX = 8, LayoutY = 3,
                IsShiftToggle = true,
            });

            Keys.AddRange(newKeys);
            return newKeys;
        }

        /// <summary>
        /// 创建方向键布局（Grid 3列×2行，↑居中对齐于↓上方）
        /// 列0=←, 列1=↑↓, 列2=→
        /// </summary>
        public List<KeyViewModel> CreateDirectionalLayout()
        {
            var newKeys = new List<KeyViewModel>();
            int gid = NewGroupId();
            double keyW = 50, keyH = 46;

            //      col0  col1  col2
            // row0:        ↑
            // row1:  ←     ↓     →
            newKeys.Add(MakeKey("↑", "up",    gid, 1, 0, keyW, keyH));
            newKeys.Add(MakeKey("←", "left",  gid, 0, 1, keyW, keyH));
            newKeys.Add(MakeKey("↓", "down",  gid, 1, 1, keyW, keyH));
            newKeys.Add(MakeKey("→", "right", gid, 2, 1, keyW, keyH));

            Keys.AddRange(newKeys);
            return newKeys;
        }

        /// <summary>
        /// 创建数字键盘布局（4列×5行 = 数字区 + 运算符区）
        /// </summary>
        public List<KeyViewModel> CreateNumpadLayout()
        {
            var newKeys = new List<KeyViewModel>();
            int gid = NewGroupId();
            double keyW = 48, keyH = 38;

            // Row 0: 7 8 9 ÷
            newKeys.Add(MakeKey("7", "", gid, 0, 0, keyW, keyH));
            newKeys.Add(MakeKey("8", "", gid, 1, 0, keyW, keyH));
            newKeys.Add(MakeKey("9", "", gid, 2, 0, keyW, keyH));
            newKeys.Add(MakeKey("÷", "div", gid, 3, 0, keyW, keyH));
            // Row 1: 4 5 6 ×
            newKeys.Add(MakeKey("4", "", gid, 0, 1, keyW, keyH));
            newKeys.Add(MakeKey("5", "", gid, 1, 1, keyW, keyH));
            newKeys.Add(MakeKey("6", "", gid, 2, 1, keyW, keyH));
            newKeys.Add(MakeKey("×", "mul", gid, 3, 1, keyW, keyH));
            // Row 2: 1 2 3 -
            newKeys.Add(MakeKey("1", "", gid, 0, 2, keyW, keyH));
            newKeys.Add(MakeKey("2", "", gid, 1, 2, keyW, keyH));
            newKeys.Add(MakeKey("3", "", gid, 2, 2, keyW, keyH));
            newKeys.Add(MakeKey("−", "sub", gid, 3, 2, keyW, keyH));
            // Row 3: * 0 # +
            newKeys.Add(MakeKey("*", "", gid, 0, 3, keyW, keyH));
            newKeys.Add(MakeKey("0", "", gid, 1, 3, keyW, keyH));
            newKeys.Add(MakeKey("#", "", gid, 2, 3, keyW, keyH));
            newKeys.Add(MakeKey("+", "add", gid, 3, 3, keyW, keyH));
            // Row 4: Enter（跨全宽）
            newKeys.Add(new KeyViewModel
            {
                Name = "Enter", PressSendMode = "数据包", PressSendValue = "enter",
                ReleaseSendMode = "无", ReleaseSendValue = "",
                Width = keyW * 2, Height = keyH,
                GroupId = gid, LayoutX = 1, LayoutY = 4,
            });

            Keys.AddRange(newKeys);
            return newKeys;
        }

        /// <summary>
        /// 创建游戏键位布局（W A S D + Q E）—— 2 行
        /// Row 0: Q W E (col 0,1,2)  Row 1: A S D (col 0,1,2)
        /// </summary>
        public List<KeyViewModel> CreateWASDLayout()
        {
            var newKeys = new List<KeyViewModel>();
            int gid = NewGroupId();
            double keyW = 48, keyH = 34;

            // Row 0: Q W E
            newKeys.Add(MakeKey("Q", "", gid, 0, 0, keyW, keyH, upper: true));
            newKeys.Add(MakeKey("W", "", gid, 1, 0, keyW, keyH, upper: true));
            newKeys.Add(MakeKey("E", "", gid, 2, 0, keyW, keyH, upper: true));
            // Row 1: A S D
            newKeys.Add(MakeKey("A", "", gid, 0, 1, keyW, keyH, upper: true));
            newKeys.Add(MakeKey("S", "", gid, 1, 1, keyW, keyH, upper: true));
            newKeys.Add(MakeKey("D", "", gid, 2, 1, keyW, keyH, upper: true));

            Keys.AddRange(newKeys);
            return newKeys;
        }

        /// <summary>
        /// 创建功能键布局（F1 - F12）—— 2 行 × 6 列
        /// Row 0: F1 F2 F3 F4  F5 F6   Row 1: F7 F8 F9 F10 F11 F12
        /// </summary>
        public List<KeyViewModel> CreateFunctionKeyLayout()
        {
            var newKeys = new List<KeyViewModel>();
            int gid = NewGroupId();
            double keyW = 44, keyH = 30;

            for (int i = 1; i <= 6; i++)
                newKeys.Add(MakeKey("F" + i, "", gid, i - 1, 0, keyW, keyH, upper: true));
            for (int i = 7; i <= 12; i++)
                newKeys.Add(MakeKey("F" + i, "", gid, i - 7, 1, keyW, keyH, upper: true));

            Keys.AddRange(newKeys);
            return newKeys;
        }

        /// <summary>
        /// 创建逻辑按键对布局 —— 6 对常用反义词，2 行 × 6 列横排
        /// Row 0: up    on    open    start    left    lock
        /// Row 1: down  off   close   stop     right   unlock
        /// </summary>
        public List<KeyViewModel> CreateLogicPairLayout()
        {
            var newKeys = new List<KeyViewModel>();
            int gid = NewGroupId();
            double keyW = 62, keyH = 30;

            string[] row0 = { "up", "on", "open", "start", "left", "lock" };
            string[] row1 = { "down", "off", "close", "stop", "right", "unlock" };

            for (int col = 0; col < row0.Length; col++)
            {
                newKeys.Add(MakeKey(row0[col], row0[col], gid, col, 0, keyW, keyH));
                newKeys.Add(MakeKey(row1[col], row1[col], gid, col, 1, keyW, keyH));
            }

            Keys.AddRange(newKeys);
            return newKeys;
        }

        private static KeyViewModel MakeKey(string name, string pressSendValue, int gid, int lx, int ly, double w, double h, bool upper = false)
        {
            string finalName = upper ? name.ToUpperInvariant() : name.ToLowerInvariant();
            return new KeyViewModel
            {
                Name = finalName,
                PressSendMode = "数据包",
                PressSendValue = string.IsNullOrEmpty(pressSendValue) ? finalName : pressSendValue,
                ReleaseSendMode = "无", ReleaseSendValue = "",
                GroupId = gid, LayoutX = lx, LayoutY = ly, Width = w, Height = h,
            };
        }

        /// <summary>
        /// 序列化所有按键为 JSON 存储格式
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
            // 恢复 _nextGroupId（取最大 GroupId + 10）
            if (Keys.Count > 0)
                _nextGroupId = Keys.Max(k => k.GroupId) + 10;
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
                default: return null;
            }
        }
    }
}
