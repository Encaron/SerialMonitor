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

            // 用按键自身颜色做闪烁——自定义色按键闪自己的颜色，默认按键闪蓝色
            Color flashColor;
            if (originalBg is SolidColorBrush bgBrush && bgBrush.Color != Colors.Transparent)
            {
                flashColor = bgBrush.Color;
            }
            else
            {
                flashColor = Color.FromRgb(0x00, 0x78, 0xD4); // 默认蓝
            }
            // 亮度 > 140 的浅色背景 → 深色闪烁（加深 40%），否则亮色闪烁（加亮）
            double lum = 0.299 * flashColor.R + 0.587 * flashColor.G + 0.114 * flashColor.B;
            Color brightFlash;
            if (lum > 140)
            {
                brightFlash = Color.FromRgb(
                    (byte)Math.Max(0, flashColor.R - 60),
                    (byte)Math.Max(0, flashColor.G - 60),
                    (byte)Math.Max(0, flashColor.B - 60));
            }
            else
            {
                brightFlash = Color.FromRgb(
                    (byte)Math.Min(255, flashColor.R + 40),
                    (byte)Math.Min(255, flashColor.G + 40),
                    (byte)Math.Min(255, flashColor.B + 40));
            }
            btn.Background = new SolidColorBrush(brightFlash);
            btn.Foreground = lum > 140 ? Brushes.Black : Brushes.White;
            btn.BorderBrush = new SolidColorBrush(Color.FromRgb(
                (byte)Math.Min(255, brightFlash.R + 60),
                (byte)Math.Min(255, brightFlash.G + 60),
                (byte)Math.Min(255, brightFlash.B + 60)));
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
