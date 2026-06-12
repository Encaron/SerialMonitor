using System.Collections.Generic;
using System.Linq;

namespace 串口助手
{
    /// <summary>
    /// 摇杆面板 ViewModel —— Phase 4 实现。
    /// 管理 1-2 个摇杆的配置和状态。
    /// </summary>
    public class JoystickPanelViewModel
    {
        public string Title { get; set; } = "摇杆";

        /// <summary>摇杆列表（默认包含 1 号和 2 号摇杆）</summary>
        public List<JoystickViewModel> Joysticks { get; set; } = new List<JoystickViewModel>();

        /// <summary>是否处于编辑模式</summary>
        public bool IsEditMode { get; set; }

        public JoystickPanelViewModel()
        {
            // 默认双摇杆
            Joysticks.Add(new JoystickViewModel { Id = 1 });
            Joysticks.Add(new JoystickViewModel { Id = 2 });
        }

        public JoystickViewModel GetJoystick(int id)
            => Joysticks.FirstOrDefault(j => j.Id == id);

        /// <summary>更新摇杆位置（来自 STM32 协议消息）</summary>
        public void SetJoystickValues(int id, double x, double y)
        {
            var j = GetJoystick(id);
            if (j != null) { j.X = x; j.Y = y; }
        }

        // ——— 序列化 ———

        public List<Dictionary<string, object>> SerializeJoysticks()
            => Joysticks.Select(j => j.ToDict()).ToList();

        public void DeserializeJoysticks(List<Dictionary<string, object>> data)
        {
            Joysticks.Clear();
            if (data == null || data.Count == 0)
            {
                Joysticks.Add(new JoystickViewModel { Id = 1 });
                Joysticks.Add(new JoystickViewModel { Id = 2 });
                return;
            }
            foreach (var d in data)
                Joysticks.Add(JoystickViewModel.FromDict(d));
        }
    }
}
