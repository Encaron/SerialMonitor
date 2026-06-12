using System.Collections.Generic;
using System.Linq;

namespace 串口助手
{
    /// <summary>
    /// 虚拟 OLED 显示项。
    /// </summary>
    public class DisplayItem
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Text { get; set; }
        public int FontSize { get; set; } = 14;
        /// <summary>文字颜色（hex 如 #FF0000），null 则为白色默认</summary>
        public string Color { get; set; }
    }

    /// <summary>
    /// 虚拟 OLED 面板 ViewModel —— Phase 4 实现。
    /// 管理显示项列表 + 清屏。
    /// </summary>
    public class DisplayPanelViewModel
    {
        public string Title { get; set; } = "OLED";

        /// <summary>显示项列表</summary>
        public List<DisplayItem> Items { get; set; } = new List<DisplayItem>();

        /// <summary>画布宽度</summary>
        public int CanvasWidth { get; set; } = 640;

        /// <summary>画布高度</summary>
        public int CanvasHeight { get; set; } = 320;

        /// <summary>添加或更新显示项（同位置替换）</summary>
        public void SetText(int x, int y, string text, int fontSize, string color = null)
        {
            var existing = Items.FirstOrDefault(i => i.X == x && i.Y == y);
            if (existing != null)
            {
                existing.Text = text;
                existing.FontSize = fontSize;
                existing.Color = color;
            }
            else
            {
                Items.Add(new DisplayItem { X = x, Y = y, Text = text, FontSize = fontSize, Color = color });
            }
        }

        /// <summary>清屏</summary>
        public void Clear() => Items.Clear();
    }
}
