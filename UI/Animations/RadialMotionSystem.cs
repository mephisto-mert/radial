using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RadialLauncher.UI.Animations
{
    public static class RadialMotionSystem
    {
        // Spring-based easing definitions
        public static readonly IEasingFunction SpringOut = new BackEase
        {
            Amplitude = 0.35,
            EasingMode = EasingMode.EaseOut
        };

        public static readonly IEasingFunction SmoothCubic = new CubicEase
        {
            EasingMode = EasingMode.EaseOut
        };

        public static readonly IEasingFunction SnappyQuintic = new QuinticEase
        {
            EasingMode = EasingMode.EaseOut
        };

        public static void AnimateWindowOpen(FrameworkElement windowRoot, bool reduceMotion = false)
        {
            if (reduceMotion)
            {
                windowRoot.Opacity = 1.0;
                return;
            }

            var transformGroup = new TransformGroup();
            var scale = new ScaleTransform(0.75, 0.75);
            transformGroup.Children.Add(scale);
            windowRoot.RenderTransform = transformGroup;
            windowRoot.RenderTransformOrigin = new Point(0.5, 0.5);

            var opacityAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180)) { EasingFunction = SmoothCubic };
            var scaleXAnim = new DoubleAnimation(0.75, 1.0, TimeSpan.FromMilliseconds(240)) { EasingFunction = SpringOut };
            var scaleYAnim = new DoubleAnimation(0.75, 1.0, TimeSpan.FromMilliseconds(240)) { EasingFunction = SpringOut };

            windowRoot.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
        }

        public static void AnimateItemBloom(FrameworkElement element, int index, bool reduceMotion = false)
        {
            if (reduceMotion)
            {
                element.Opacity = 1.0;
                return;
            }

            var transformGroup = element.RenderTransform as TransformGroup ?? new TransformGroup();
            var scale = new ScaleTransform(0.3, 0.3);
            transformGroup.Children.Add(scale);
            element.RenderTransform = transformGroup;
            element.RenderTransformOrigin = new Point(0.5, 0.5);

            int delayMs = Math.Min(index * 22, 350); // Staggered entrance bloom

            var opacityAnim = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = SmoothCubic
            };

            var scaleAnim = new DoubleAnimation
            {
                From = 0.3,
                To = 1.0,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = SpringOut
            };

            element.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        public static void AnimateHover(FrameworkElement button, FrameworkElement? label, bool isHovered, bool reduceMotion = false)
        {
            double targetScaleBtn = isHovered ? 1.22 : 1.0;
            double targetScaleLbl = isHovered ? 1.15 : 1.0;
            var duration = TimeSpan.FromMilliseconds(reduceMotion ? 1 : 140);
            var easing = isHovered ? SpringOut : SmoothCubic;

            if (button.RenderTransform is TransformGroup group)
            {
                foreach (var child in group.Children)
                {
                    if (child is ScaleTransform st)
                    {
                        st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(targetScaleBtn, duration) { EasingFunction = easing });
                        st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(targetScaleBtn, duration) { EasingFunction = easing });
                    }
                }
            }
            else
            {
                var st = new ScaleTransform(isHovered ? 1.0 : targetScaleBtn, isHovered ? 1.0 : targetScaleBtn);
                button.RenderTransform = st;
                button.RenderTransformOrigin = new Point(0.5, 0.5);
                st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(targetScaleBtn, duration) { EasingFunction = easing });
                st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(targetScaleBtn, duration) { EasingFunction = easing });
            }

            if (label != null)
            {
                if (label.RenderTransform is ScaleTransform lst)
                {
                    lst.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(targetScaleLbl, duration) { EasingFunction = easing });
                    lst.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(targetScaleLbl, duration) { EasingFunction = easing });
                }
                else
                {
                    var lstNew = new ScaleTransform(1.0, 1.0);
                    label.RenderTransform = lstNew;
                    label.RenderTransformOrigin = new Point(0.5, 0.5);
                    lstNew.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(targetScaleLbl, duration) { EasingFunction = easing });
                    lstNew.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(targetScaleLbl, duration) { EasingFunction = easing });
                }
            }
        }

        public static void AnimateCenterMorph(FrameworkElement centerElement, bool isSubmenu, bool reduceMotion = false)
        {
            if (reduceMotion) return;

            var group = centerElement.RenderTransform as TransformGroup ?? new TransformGroup();
            var rotate = new RotateTransform(0);
            var scale = new ScaleTransform(1.0, 1.0);
            group.Children.Clear();
            group.Children.Add(rotate);
            group.Children.Add(scale);
            centerElement.RenderTransform = group;
            centerElement.RenderTransformOrigin = new Point(0.5, 0.5);

            double targetAngle = isSubmenu ? 180.0 : 0.0;
            rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(targetAngle, TimeSpan.FromMilliseconds(260)) { EasingFunction = SpringOut });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = SpringOut });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = SpringOut });
        }
    }
}
