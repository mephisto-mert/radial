using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RadialLauncher.UI.Animations
{
    public static class RadialMotionSystem
    {
        // Premium soft easing definitions
        public static readonly IEasingFunction SoftCubic = new CubicEase
        {
            EasingMode = EasingMode.EaseOut
        };

        public static readonly IEasingFunction SoftQuartic = new QuarticEase
        {
            EasingMode = EasingMode.EaseOut
        };

        public static readonly IEasingFunction GentleSine = new SineEase
        {
            EasingMode = EasingMode.EaseInOut
        };

        public static readonly IEasingFunction SubtleSpring = new BackEase
        {
            Amplitude = 0.18,
            EasingMode = EasingMode.EaseOut
        };

        public static double CalculateDurationScale(double velocityPxPerMs)
        {
            if (velocityPxPerMs <= 0.2) return 1.0;
            if (velocityPxPerMs >= 2.0) return 0.4;
            
            // 2.5x range (0.4 to 1.0)
            double t = (velocityPxPerMs - 0.2) / (2.0 - 0.2);
            return 1.0 - (t * 0.6);
        }

        public static void AnimateWindowOpen(FrameworkElement windowRoot, bool reduceMotion = false, double cursorVelocity = 0.0)
        {
            if (reduceMotion)
            {
                windowRoot.Opacity = 1.0;
                return;
            }

            double speedScale = CalculateDurationScale(cursorVelocity);
            int scaleDuration = Math.Max(120, (int)(280 * speedScale));
            int opacityDuration = Math.Max(90, (int)(220 * speedScale));

            var transformGroup = new TransformGroup();
            var scale = new ScaleTransform(0.85, 0.85);
            transformGroup.Children.Add(scale);
            windowRoot.RenderTransform = transformGroup;
            windowRoot.RenderTransformOrigin = new Point(0.5, 0.5);

            var opacityAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(opacityDuration)) { EasingFunction = SoftQuartic };
            var scaleXAnim = new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(scaleDuration)) { EasingFunction = SubtleSpring };
            var scaleYAnim = new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(scaleDuration)) { EasingFunction = SubtleSpring };

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
            var scale = new ScaleTransform(0.5, 0.5);
            transformGroup.Children.Clear();
            transformGroup.Children.Add(scale);
            element.RenderTransform = transformGroup;
            element.RenderTransformOrigin = new Point(0.5, 0.5);

            int delayMs = Math.Min(index * 16, 260); // Staggered gentle entrance

            var opacityAnim = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = SoftQuartic
            };

            var scaleAnim = new DoubleAnimation
            {
                From = 0.5,
                To = 1.0,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = SubtleSpring
            };

            element.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        public static void AnimateHover(FrameworkElement button, FrameworkElement? label, bool isHovered, bool reduceMotion = false)
        {
            double targetScaleBtn = isHovered ? 1.10 : 1.0;
            double targetScaleLbl = isHovered ? 1.06 : 1.0;
            var duration = TimeSpan.FromMilliseconds(reduceMotion ? 1 : 180);
            var easing = isHovered ? SubtleSpring : SoftCubic;

            if (button.RenderTransform is TransformGroup group)
            {
                ScaleTransform? st = null;
                foreach (var child in group.Children)
                {
                    if (child is ScaleTransform scale)
                    {
                        st = scale;
                        break;
                    }
                }
                if (st == null)
                {
                    st = new ScaleTransform(1.0, 1.0);
                    group.Children.Add(st);
                }
                st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(targetScaleBtn, duration) { EasingFunction = easing });
                st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(targetScaleBtn, duration) { EasingFunction = easing });
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

        public static void AnimatePageTransition(FrameworkElement container, int direction, bool reduceMotion = false)
        {
            if (reduceMotion)
            {
                container.Opacity = 1.0;
                return;
            }

            double offsetDistance = direction >= 0 ? 32.0 : -32.0;

            var transformGroup = container.RenderTransform as TransformGroup ?? new TransformGroup();
            TranslateTransform? translate = null;
            foreach (var child in transformGroup.Children)
            {
                if (child is TranslateTransform tt)
                {
                    translate = tt;
                    break;
                }
            }
            if (translate == null)
            {
                translate = new TranslateTransform();
                transformGroup.Children.Add(translate);
            }
            container.RenderTransform = transformGroup;

            translate.BeginAnimation(TranslateTransform.XProperty, null);
            container.BeginAnimation(UIElement.OpacityProperty, null);

            var translateAnim = new DoubleAnimation
            {
                From = offsetDistance,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = SoftQuartic
            };

            var opacityAnim = new DoubleAnimation
            {
                From = 0.3,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = SoftCubic
            };

            translate.BeginAnimation(TranslateTransform.XProperty, translateAnim);
            container.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }

        public static void AnimateQuickActionCard(FrameworkElement card, bool isVisible, bool reduceMotion = false)
        {
            if (reduceMotion)
            {
                card.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                card.Opacity = isVisible ? 1.0 : 0.0;
                return;
            }

            if (isVisible)
            {
                card.Visibility = Visibility.Visible;
                var opacityAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(160)) { EasingFunction = SoftQuartic };
                card.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            }
            else
            {
                var opacityAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(120)) { EasingFunction = SoftCubic };
                opacityAnim.Completed += (s, e) =>
                {
                    if (card.Opacity <= 0.05) card.Visibility = Visibility.Collapsed;
                };
                card.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            }
        }
    }
}
