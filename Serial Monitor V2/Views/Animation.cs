using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

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
            // 1. 缩放脉冲（按下回弹）
            if (!(element.RenderTransform is ScaleTransform scale))
            {
                scale = new ScaleTransform(1, 1);
                element.RenderTransformOrigin = new Point(0.5, 0.5);
                element.RenderTransform = scale;
            }
            var scaleAnim = new DoubleAnimation(
                1.0, 0.82, TimeSpan.FromMilliseconds(45))
            {
                AutoReverse = true,
                FillBehavior = FillBehavior.Stop
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

            // 2. 背景闪烁（瞬间亮色 → 渐消）
            if (element is Button btn)
                FlashButtonBg(btn);
        }

        private void FlashButtonBg(Button btn)
        {
            var originalBg = btn.Background;
            var originalBorder = btn.BorderBrush;
            var originalThickness = btn.BorderThickness;
            var originalFg = btn.Foreground;

            // 非常明显的闪烁：亮蓝色底 + 白色字 + 加厚白边框
            btn.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
            btn.Foreground = Brushes.White;
            btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0xFF));
            btn.BorderThickness = new Thickness(3);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            timer.Tick += (s, args) =>
            {
                btn.Background = originalBg;
                btn.BorderBrush = originalBorder;
                btn.BorderThickness = originalThickness;
                btn.Foreground = originalFg;
                timer.Stop();
            };
            timer.Start();
        }

        // ————————————————————————————————————————
        //  Q 弹按压动画（滑杆 / 摇杆）
        // ————————————————————————————————————————

        /// <summary>按下：缩小到 88%，模拟按入感</summary>
        private void SpringPress(FrameworkElement element)
        {
            if (!(element.RenderTransform is ScaleTransform scale))
            {
                scale = new ScaleTransform(1, 1);
                element.RenderTransformOrigin = new Point(0.5, 0.5);
                element.RenderTransform = scale;
            }
            var anim = new DoubleAnimation(1.0, 0.88, TimeSpan.FromMilliseconds(80))
            {
                FillBehavior = FillBehavior.HoldEnd
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        /// <summary>松开：弹性回弹（ElasticEase），Q 弹感</summary>
        private void SpringRelease(FrameworkElement element)
        {
            if (!(element.RenderTransform is ScaleTransform scale))
            {
                scale = new ScaleTransform(1, 1);
                element.RenderTransformOrigin = new Point(0.5, 0.5);
                element.RenderTransform = scale;
            }
            var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(380))
            {
                EasingFunction = new ElasticEase { Oscillations = 3, Springiness = 5 },
                FillBehavior = FillBehavior.HoldEnd
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
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
