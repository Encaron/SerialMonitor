using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace 串口助手
{
    public partial class MainWindow : Window
    {
        // ————————————————————————————————————————
        //  动画辅助
        // ————————————————————————————————————————

        private void AnimateBrushColor(SolidColorBrush brush, Color to)
        {
            var anim = new ColorAnimation(
                to, TimeSpan.FromMilliseconds(280))
            {
                FillBehavior = FillBehavior.HoldEnd
            };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        }

        private void PulseElement(FrameworkElement element)
        {
            var anim = new DoubleAnimation(
                1.0, 0.55, TimeSpan.FromMilliseconds(130))
            {
                AutoReverse = true,
                FillBehavior = FillBehavior.Stop
            };
            element.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        /// <summary>
        /// 滑动开关动画：圆钮左→右 / 右→左（200ms）
        /// </summary>
        private void AnimateToggleSwitch(CheckBox cb)
        {
            if (cb == null) return;
            cb.ApplyTemplate();
            var knob = cb.Template.FindName("Knob", cb) as Ellipse;
            if (knob == null) return;

            double toLeft = cb.IsChecked == true ? 20 : 2;
            var anim = new DoubleAnimation(
                toLeft, TimeSpan.FromMilliseconds(200))
            {
                FillBehavior = FillBehavior.HoldEnd
            };
            knob.BeginAnimation(Canvas.LeftProperty, anim);
        }
    }
}
