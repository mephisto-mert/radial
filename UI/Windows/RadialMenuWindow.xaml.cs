using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using RadialLauncher.Models;
using RadialLauncher.Services.Icons;
using RadialLauncher.UI.Animations;
using RadialLauncher.UI.Helpers;
using RadialLauncher.UI.ViewModels;
using Serilog;

namespace RadialLauncher.UI.Windows
{
    public partial class RadialMenuWindow : Window
    {
        private readonly RadialMenuViewModel _viewModel;
        private readonly IIconExtractor _iconExtractor;
        private readonly List<(Button btn, Border labelBorder, LauncherItem item)> _visibleButtons = new();
        private readonly List<IntPtr> _activeDwmThumbs = new();
        private int _keyboardFocusIndex = -1;

        public RadialMenuViewModel ViewModel => _viewModel;

        public RadialMenuWindow() : this(
            App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<RadialMenuViewModel>(App.ServiceProvider) 
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."),
            App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IIconExtractor>(App.ServiceProvider) 
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."))
        {
        }

        public RadialMenuWindow(RadialMenuViewModel viewModel, IIconExtractor iconExtractor)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _iconExtractor = iconExtractor ?? throw new ArgumentNullException(nameof(iconExtractor));

            InitializeComponent();

            DataContext = _viewModel;

            _viewModel.RequestClose += () => 
            {
                ClearActiveDwmThumbnails();
                Dispatcher.Invoke(this.Hide);
            };
            _viewModel.RequestLayoutUpdate += () => Dispatcher.Invoke(RenderLayout);

            MouseMove += Window_MouseMove;
        }

        public void ShowAt(int screenX, int screenY, double cursorVelocity = 0.0)
        {
            try
            {
                var source = PresentationSource.FromVisual(this);
                double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                double logicalX = screenX / dpiX;
                double logicalY = screenY / dpiY;

                var clamped = MultiMonitorHelper.ClampWindowToCursorScreen(this.Width, this.Height, logicalX, logicalY);
                this.Left = clamped.X;
                this.Top = clamped.Y;

                _keyboardFocusIndex = -1;
                _viewModel.InitializeForDisplay();

                ApplyThemeVisuals(_viewModel.ActiveTheme);
                RenderLayout();

                this.Opacity = 1;
                this.Show();
                this.Activate();
                this.Focus();

                RadialMotionSystem.AnimateWindowOpen(RootGrid, _viewModel.ActiveTheme.ReduceMotion, cursorVelocity);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to ShowAt {X},{Y}", screenX, screenY);
            }
        }

        private void ApplyThemeVisuals(Theme theme)
        {
            byte alpha = (byte)(theme.BackgroundOpacity * 255);
            BaseFill.Color = Color.FromArgb(alpha, theme.BackgroundColor.R, theme.BackgroundColor.G, theme.BackgroundColor.B);

            var stroke = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            stroke.GradientStops.Add(new GradientStop(Color.FromArgb(80, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B), 0.0));
            stroke.GradientStops.Add(new GradientStop(Color.FromArgb(25, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B), 0.5));
            stroke.GradientStops.Add(new GradientStop(Color.FromArgb(80, theme.Accent2Color.R, theme.Accent2Color.G, theme.Accent2Color.B), 1.0));
            BaseCircle.Stroke = stroke;

            var glow = new RadialGradientBrush();
            glow.GradientStops.Add(new GradientStop(Colors.Transparent, 0.82));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(45, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B), 1.0));
            GlowEllipse.Fill = glow;

            CenterButton.Background = new SolidColorBrush(theme.CenterButtonColor);
            CenterText.Foreground = new SolidColorBrush(theme.TextColor);
            HoverInfoText.Foreground = new SolidColorBrush(theme.TextColor);

            CategoryPill.Background = new SolidColorBrush(Color.FromArgb(225, 18, 18, 24));
            CategoryPill.BorderBrush = new SolidColorBrush(Color.FromArgb(80, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B));
        }

        private void RenderLayout()
        {
            ApplyThemeVisuals(_viewModel.ActiveTheme);
            ClearActiveDwmThumbnails();

            // Clear previous buttons from canvas except CenterButton
            var toRemove = new List<UIElement>();
            foreach (UIElement child in ItemsCanvas.Children)
            {
                if (child != CenterButton) toRemove.Add(child);
            }
            foreach (var el in toRemove) ItemsCanvas.Children.Remove(el);
            _visibleButtons.Clear();

            var items = _viewModel.CurrentPageItems;
            if (items.Count == 0) return;

            bool isCompact = _viewModel.ActiveTheme.DensityMode == "Compact";
            var placements = RadialLayoutCalculator.CalculatePlacements(items.Count, 270, 270, isCompact);

            var theme = _viewModel.ActiveTheme;

            for (int i = 0; i < placements.Count; i++)
            {
                var p = placements[i];
                var item = items[i];
                IntPtr currentHoverThumb = IntPtr.Zero;

                var btn = new Button
                {
                    Width = p.CircleSize,
                    Height = p.CircleSize,
                    Tag = item,
                    BorderThickness = new Thickness(1.5),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                    Background = new SolidColorBrush(theme.IconBackgroundColor),
                    Cursor = Cursors.Hand,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                Canvas.SetLeft(btn, p.ButtonX);
                Canvas.SetTop(btn, p.ButtonY);
                Panel.SetZIndex(btn, 15);

                btn.Resources.Add(typeof(Border), new Style(typeof(Border))
                {
                    Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(p.CircleSize / 2.0)) }
                });

                // Icon content
                var iconImg = ResolveItemIcon(item);
                if (iconImg != null)
                {
                    var img = new Image
                    {
                        Source = iconImg,
                        Width = p.IconSize,
                        Height = p.IconSize,
                        Stretch = Stretch.Uniform
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    btn.Content = img;
                }
                else
                {
                    btn.Content = new TextBlock
                    {
                        Text = item.Name.Length > 0 ? item.Name.Substring(0, 1).ToUpper() : "?",
                        FontSize = 18,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White
                    };
                }

                // Label badge
                var lblBorder = new Border
                {
                    Width = RadialLayoutCalculator.LabelWidth,
                    Height = RadialLayoutCalculator.LabelHeight,
                    CornerRadius = new CornerRadius(5),
                    Background = new SolidColorBrush(Color.FromArgb(200, 16, 16, 20)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(60, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                Panel.SetZIndex(lblBorder, 25);
                Canvas.SetLeft(lblBorder, p.LabelX);
                Canvas.SetTop(lblBorder, p.LabelY);

                var txt = new TextBlock
                {
                    Text = item.Name,
                    FontSize = 11,
                    FontWeight = FontWeights.Normal,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 66
                };
                lblBorder.Child = txt;

                // Wire interactions
                int localIndex = i;
                btn.Click += (s, e) => _viewModel.LaunchItem(item);
                lblBorder.MouseLeftButtonUp += (s, e) => _viewModel.LaunchItem(item);

                if (item.Type == "WINDOW" && long.TryParse(item.Target, out long hWndVal))
                {
                    void ShowDesktopMoveMenu(object sender, MouseButtonEventArgs e)
                    {
                        e.Handled = true;
                        var cm = new ContextMenu();
                        var desktops = _viewModel.DesktopService.GetDesktops();
                        if (desktops == null || desktops.Count == 0)
                        {
                            var disabledItem = new MenuItem { Header = "⚠️ Sanal Masaüstü Kullanılamıyor", IsEnabled = false };
                            cm.Items.Add(disabledItem);
                        }
                        else
                        {
                            for (int d = 0; d < desktops.Count; d++)
                            {
                                int targetDesktop = d;
                                string dName = !string.IsNullOrEmpty(desktops[d].Name) 
                                    ? desktops[d].Name 
                                    : $"Masaüstü {d + 1}";
                                var mi = new MenuItem { Header = $"🪟 {dName}'e Taşı" };
                                mi.Click += (ms, me) =>
                                {
                                    _viewModel.DesktopService.MoveWindowToDesktop((IntPtr)hWndVal, targetDesktop);
                                    _viewModel.RefreshPageItems();
                                };
                                cm.Items.Add(mi);
                            }
                        }
                        cm.PlacementTarget = btn;
                        cm.IsOpen = true;
                    }
                    btn.MouseRightButtonUp += ShowDesktopMoveMenu;
                    lblBorder.MouseRightButtonUp += ShowDesktopMoveMenu;
                }

                void OnEnter(object s, MouseEventArgs e)
                {
                    _keyboardFocusIndex = localIndex;
                    Panel.SetZIndex(btn, 100);
                    Panel.SetZIndex(lblBorder, 101);
                    btn.BorderBrush = theme.AccentBrush;
                    txt.Foreground = theme.AccentBrush;
                    txt.FontWeight = FontWeights.Bold;
                    _viewModel.HoveredItemTitle = item.Name;
                    RadialMotionSystem.AnimateHover(btn, lblBorder, true, theme.ReduceMotion);

                    if (item.Type == "WINDOW" && long.TryParse(item.Target, out long targetHwnd) && targetHwnd != 0)
                    {
                        try
                        {
                            var source = PresentationSource.FromVisual(this);
                            double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                            double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                            Point canvasOffset = ItemsCanvas.TranslatePoint(new Point(0, 0), this);
                            double btnLogicalX = canvasOffset.X + Canvas.GetLeft(btn);
                            double btnLogicalY = canvasOffset.Y + Canvas.GetTop(btn);

                            double previewW = 160;
                            double previewH = 100;
                            double previewX = Math.Clamp(btnLogicalX + (p.CircleSize / 2.0) - (previewW / 2.0), 20.0, this.Width - previewW - 20.0);
                            double previewY = (btnLogicalY > 300) 
                                ? Math.Max(20.0, btnLogicalY - previewH - 12.0) 
                                : Math.Min(this.Height - previewH - 20.0, btnLogicalY + p.CircleSize + 12.0);

                            int destLeft = (int)(previewX * dpiX);
                            int destTop = (int)(previewY * dpiY);
                            int destWidth = (int)(previewW * dpiX);
                            int destHeight = (int)(previewH * dpiY);

                            IntPtr destHwnd = new WindowInteropHelper(this).Handle;
                            if (destHwnd != IntPtr.Zero)
                            {
                                if (currentHoverThumb != IntPtr.Zero)
                                {
                                    DwmThumbnailHelper.UnregisterThumbnail(currentHoverThumb);
                                    _activeDwmThumbs.Remove(currentHoverThumb);
                                    currentHoverThumb = IntPtr.Zero;
                                }

                                currentHoverThumb = DwmThumbnailHelper.RegisterThumbnail(destHwnd, (IntPtr)targetHwnd, destLeft, destTop, destWidth, destHeight);
                                if (currentHoverThumb != IntPtr.Zero)
                                {
                                    _activeDwmThumbs.Add(currentHoverThumb);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Failed registering DWM thumbnail preview for window {Hwnd}", targetHwnd);
                        }
                    }
                }

                void OnLeave(object s, MouseEventArgs e)
                {
                    Panel.SetZIndex(btn, 15);
                    Panel.SetZIndex(lblBorder, 25);
                    btn.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                    txt.Foreground = Brushes.White;
                    txt.FontWeight = FontWeights.Normal;
                    _viewModel.HoveredItemTitle = string.Empty;
                    RadialMotionSystem.AnimateHover(btn, lblBorder, false, theme.ReduceMotion);

                    if (currentHoverThumb != IntPtr.Zero)
                    {
                        DwmThumbnailHelper.UnregisterThumbnail(currentHoverThumb);
                        _activeDwmThumbs.Remove(currentHoverThumb);
                        currentHoverThumb = IntPtr.Zero;
                    }
                }

                btn.MouseEnter += OnEnter;
                btn.MouseLeave += OnLeave;
                lblBorder.MouseEnter += OnEnter;
                lblBorder.MouseLeave += OnLeave;

                ItemsCanvas.Children.Add(btn);
                ItemsCanvas.Children.Add(lblBorder);
                _visibleButtons.Add((btn, lblBorder, item));

                RadialMotionSystem.AnimateItemBloom(btn, i, theme.ReduceMotion);
                RadialMotionSystem.AnimateItemBloom(lblBorder, i, theme.ReduceMotion);
            }

            // Update Center Button Morph
            CenterText.Text = _viewModel.IsSubmenu ? "←" : "✕";
            RadialMotionSystem.AnimateCenterMorph(CenterButton, _viewModel.IsSubmenu, theme.ReduceMotion);

            RenderCategoryDots();
        }

        private ImageSource? ResolveItemIcon(LauncherItem item)
        {
            if (item.Type == "WINDOW")
            {
                if (RadialMenuViewModel.WindowIcons.TryGetValue(item.Target, out var winIcon) && winIcon != null)
                    return winIcon;
                return _iconExtractor.CreateMonogramIcon(item.Name, Color.FromRgb(155, 89, 182));
            }

            if (!string.IsNullOrEmpty(item.IconPath) && File.Exists(item.IconPath))
            {
                var f = _iconExtractor.GetIconForFile(item.IconPath);
                if (f != null) return f;
            }
            if (item.Type == "URL")
            {
                var fav = _iconExtractor.GetFaviconForUrl(item.Target);
                if (fav != null) return fav;
            }
            var brand = _iconExtractor.GetBrandIcon(item.Name, item.Target);
            if (brand != null) return brand;

            if (!string.IsNullOrEmpty(item.Target))
            {
                var t = _iconExtractor.GetIconForFile(item.Target);
                if (t != null) return t;
            }
            return _iconExtractor.CreateMonogramIcon(item.Name, Color.FromRgb(88, 140, 236));
        }

        private void RenderCategoryDots()
        {
            CategoryDots.Children.Clear();
            var cats = _viewModel.Categories;
            for (int i = 0; i < cats.Count; i++)
            {
                var dot = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Margin = new Thickness(2, 0, 2, 0),
                    Fill = (i == _viewModel.CurrentCategoryIndex) 
                        ? _viewModel.ActiveTheme.AccentBrush 
                        : new SolidColorBrush(Color.FromArgb(100, 255, 255, 255))
                };
                CategoryDots.Children.Add(dot);
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            // Magnetic snap subtle pull towards nearest hovered item
            var pos = e.GetPosition(ItemsCanvas);
            foreach (var (btn, _, _) in _visibleButtons)
            {
                double bx = Canvas.GetLeft(btn) + (btn.Width / 2.0);
                double by = Canvas.GetTop(btn) + (btn.Height / 2.0);
                var offset = RadialLayoutCalculator.CalculateMagneticHoverOffset(new Point(bx, by), pos, 6.0);
                if (btn.RenderTransform is TransformGroup group)
                {
                    var translate = group.Children.Count > 1 ? group.Children[1] as TranslateTransform : null;
                    if (translate == null)
                    {
                        translate = new TranslateTransform();
                        group.Children.Add(translate);
                    }
                    translate.X = offset.X;
                    translate.Y = offset.Y;
                }
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) _viewModel.PrevPage();
            else if (e.Delta < 0) _viewModel.NextPage();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_viewModel.IsSearchMode)
                {
                    _viewModel.SearchQuery = string.Empty;
                    _viewModel.IsSearchMode = false;
                    _viewModel.RefreshPageItems();
                }
                else
                {
                    _viewModel.CenterButtonClick();
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Back)
            {
                if (_viewModel.IsSearchMode && !string.IsNullOrEmpty(_viewModel.SearchQuery))
                {
                    string newQ = _viewModel.SearchQuery.Length > 1 
                        ? _viewModel.SearchQuery[..^1] 
                        : string.Empty;
                    _viewModel.ApplySearch(newQ);
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Right || e.Key == Key.Down)
            {
                if (_visibleButtons.Count > 0)
                {
                    _keyboardFocusIndex = (_keyboardFocusIndex + 1) % _visibleButtons.Count;
                    HighlightKeyboardFocused();
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Left || e.Key == Key.Up)
            {
                if (_visibleButtons.Count > 0)
                {
                    _keyboardFocusIndex = (_keyboardFocusIndex - 1 + _visibleButtons.Count) % _visibleButtons.Count;
                    HighlightKeyboardFocused();
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                if (_keyboardFocusIndex >= 0 && _keyboardFocusIndex < _visibleButtons.Count)
                {
                    _viewModel.LaunchItem(_visibleButtons[_keyboardFocusIndex].item);
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Tab)
            {
                int nextCat = (_viewModel.CurrentCategoryIndex + 1) % Math.Max(1, _viewModel.Categories.Count);
                _viewModel.SwitchCategory(nextCat);
                e.Handled = true;
            }
        }

        private void HighlightKeyboardFocused()
        {
            for (int i = 0; i < _visibleButtons.Count; i++)
            {
                var (btn, lbl, item) = _visibleButtons[i];
                if (i == _keyboardFocusIndex)
                {
                    Panel.SetZIndex(btn, 100);
                    Panel.SetZIndex(lbl, 101);
                    btn.BorderBrush = _viewModel.ActiveTheme.AccentBrush;
                    _viewModel.HoveredItemTitle = item.Name;
                    RadialMotionSystem.AnimateHover(btn, lbl, true, _viewModel.ActiveTheme.ReduceMotion);
                }
                else
                {
                    Panel.SetZIndex(btn, 15);
                    Panel.SetZIndex(lbl, 25);
                    btn.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                    RadialMotionSystem.AnimateHover(btn, lbl, false, _viewModel.ActiveTheme.ReduceMotion);
                }
            }
        }

        private void Window_TextInput(object sender, TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;
            char c = e.Text[0];
            if (char.IsLetterOrDigit(c) || char.IsPunctuation(c) || c == ' ')
            {
                _viewModel.ApplySearch(_viewModel.SearchQuery + c);
            }
        }

        private void CenterButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.Hide();
            ((App)Application.Current).OpenSettings();
        }

        private void CategoryPill_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            int nextCat = (_viewModel.CurrentCategoryIndex + 1) % Math.Max(1, _viewModel.Categories.Count);
            _viewModel.SwitchCategory(nextCat);
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            ClearActiveDwmThumbnails();
            this.Hide();
        }

        private void ClearActiveDwmThumbnails()
        {
            try
            {
                foreach (var thumb in _activeDwmThumbs)
                {
                    DwmThumbnailHelper.UnregisterThumbnail(thumb);
                }
                _activeDwmThumbs.Clear();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error clearing active DWM thumbnails");
            }
        }
    }
}
