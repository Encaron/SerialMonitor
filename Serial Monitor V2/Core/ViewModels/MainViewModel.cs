using System.Windows.Threading;

namespace 串口助手
{
    /// <summary>
    /// 主 ViewModel —— Phase 1 仅持有 SerialPortSession 骨架。
    /// Phase 5 多串口时改为 ObservableCollection&lt;SerialPortSession&gt;。
    /// </summary>
    public class MainViewModel
    {
        public SerialPortSession Session { get; private set; }

        public PlotViewModel Plot { get; } = new PlotViewModel();
        public SliderPanelViewModel SliderPanel { get; } = new SliderPanelViewModel();
        public KeyPanelViewModel KeyPanel { get; } = new KeyPanelViewModel();
        public DisplayPanelViewModel DisplayPanel { get; } = new DisplayPanelViewModel();

        public void Initialize(Dispatcher dispatcher)
        {
            Session = new SerialPortSession(dispatcher);
        }
    }
}
