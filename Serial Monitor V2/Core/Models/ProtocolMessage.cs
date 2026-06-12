using System.Collections.Generic;

namespace 串口助手
{
    /// <summary>
    /// 解析后的单条协议消息。
    /// 例如 [plot,P,0.5] → Type="plot", Args=["P","0.5"]
    /// </summary>
    public class ProtocolMessage
    {
        /// <summary>消息类型：plot / slider / key / display / joystick / plot-clear / display-clear</summary>
        public string Type { get; set; }

        /// <summary>参数列表（引号已剥离）</summary>
        public List<string> Args { get; set; } = new List<string>();
    }
}
