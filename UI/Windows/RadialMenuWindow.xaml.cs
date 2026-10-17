using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using RadialLauncher.Models;
using RadialLauncher.Services.Context;
using RadialLauncher.Services.Icons;
using RadialLauncher.Services.Localization;
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
        private readonly IContextualActionService _contextualActionService;
        private readonly List<(Button btn, Border labelBorder, LauncherItem item)> _visibleButtons = new();
        private readonly List<IntPtr> _activeDwmThumbs = new();
        private int _keyboardFocusIndex = -1;

        // Middle & Left Mouse Drag Navigation
        private Point _dragStartPoint;
        private bool _isDragNavigating = false;
        private bool _hasNavigatedInCurrentDrag = false;

        // Circular Bubble Drag & Drop Reordering
        private Button? _draggedBubbleButton = null;
        private Border? _draggedBubbleLabel = null;
        private int _draggedBubbleIndex = -1;
        private LauncherItem? _draggedBubbleItem = null;
        private Point _bubbleDragStartPoint;
        private bool _isDraggingBubble = false;
        private bool _isPotentialBubbleDrag = false;

        // Press-Hold-Aim-Release (Rovyl-style gesture mode)
        public bool IsHoldGestureActive { get; set; } = false;
        public string AimMode { get; set; } = "angle"; // "angle" or "cursor"
        private int _currentAimSlot = -1;

        // Auto-close on mouse leave
        private readonly DispatcherTimer _autoCloseGraceTimer;
        private bool _isInteractingWithQuickActions = false;

        public RadialMenuViewModel ViewModel => _viewModel;

        public RadialMenuWindow() : this(
            App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<RadialMenuViewModel>(App.ServiceProvider) 
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."),
            App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IIconExtractor>(App.ServiceProvider) 
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."),
            App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IContextualActionService>(App.ServiceProvider) 
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."))
        {
        }

        public RadialMenuWindow(RadialMenuViewModel viewModel, IIconExtractor iconExtractor, IContextualActionService contextualActionService)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _iconExtractor = iconExtractor ?? throw new ArgumentNullException(nameof(iconExtractor));
            _contextualActionService = contextualActionService ?? throw new ArgumentNullException(nameof(contextualActionService));

            InitializeComponent();

            DataContext = _viewModel;

            _autoCloseGraceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(320)
            };
            _autoCloseGraceTimer.Tick += AutoCloseGraceTimer_Tick;

            _viewModel.RequestClose += () => 
            {
                _autoCloseGraceTimer.Stop();
                ClearActiveDwmThumbnails();
                Dispatcher.Invoke(this.Hide);
            };
            _viewModel.RequestLayoutUpdate += () => Dispatcher.Invoke(() => RenderLayout(false));

            this.PreviewMouseDown += Window_PreviewMouseDown;
            this.PreviewMouseMove += Window_PreviewMouseMove;
            this.PreviewMouseUp += Window_PreviewMouseUp;
            this.MouseRightButtonUp += Window_MouseRightButtonUp;
            this.MouseLeave += Window_MouseLeave;
            this.MouseEnter += Window_MouseEnter;

            if (ContextActionCard != null)
            {
                ContextActionCard.MouseEnter += (s, e) => { _isInteractingWithQuickActions = true; _autoCloseGraceTimer.Stop(); };
                ContextActionCard.MouseLeave += (s, e) => { _isInteractingWithQuickActions = false; };
            }

            Closed += (s, e) => 
            {
                _autoCloseGraceTimer.Stop();
                ClearActiveDwmThumbnails();
            };
            IsVisibleChanged += (s, e) =>
            {
                if (!IsVisible)
                {
                    _autoCloseGraceTimer.Stop();
                    ClearActiveDwmThumbnails();
                    HideContextActionCard();
                }
            };

            RadialLauncher.Services.Localization.LocalizationService.Instance.OnLanguageChanged += () =>
            {
                Dispatcher.Invoke(ApplyLocalization);
            };
            ApplyLocalization();
        }

        public void ApplyLocalization()
        {
            var loc = RadialLauncher.Services.Localization.LocalizationService.Instance;
            if (TxtTutorialHeader != null)
                TxtTutorialHeader.Text = loc.GetString("TutorialHeader", "🚀 Welcome to Radial Launcher!");
            if (TxtTutorialBody != null)
                TxtTutorialBody.Text = loc.GetString("TutorialBody", "• Middle Click: Open / close radial menu\n• Click any icon: Launch application or shortcut\n• Mouse wheel / Drag: Navigate pages and categories\n• Type anytime: Fast search across all apps");
            if (BtnDismissTutorial != null)
                BtnDismissTutorial.Content = loc.GetString("TutorialDismiss", "Got it! Start using");
        }

        public void HandleGlobalTriggerUp(int screenX, int screenY)
        {
            if (!this.IsVisible) return;

            IsHoldGestureActive = false;

            // Resolve aim slot against canvas coordinates
            try
            {
                var source = PresentationSource.FromVisual(this);
                double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                double logicalX = screenX / dpiX;
                double logicalY = screenY / dpiY;

                Point relToWindow = new Point(logicalX - this.Left, logicalY - this.Top);
                Point canvasPos = this.TranslatePoint(relToWindow, ItemsCanvas);

                int count = _viewModel.CurrentPageItems.Count;
                int slot = RadialLayoutCalculator.ResolveAimSlot(canvasPos, count, AimMode, 250, 250, 48.0, 320.0);

                if (slot >= 0 && slot < _visibleButtons.Count)
                {
                    var itemToLaunch = _visibleButtons[slot].item;
                    Log.Information("Hold-and-Aim gesture: Launching item {Name} at slot {Slot}", itemToLaunch.Name, slot);
                    _viewModel.LaunchItem(itemToLaunch);
                }
                else if (slot == -1)
                {
                    // Released in dead center or outside -> dismiss without launching
                    Log.Debug("Hold-and-Aim gesture: Released in deadzone/outside, dismissing");
                    this.Hide();
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error resolving hold-and-aim release target");
                this.Hide();
            }
        }

        public void ShowAt(int screenX, int screenY, double cursorVelocity = 0.0)
        {
            try
            {
                _autoCloseGraceTimer.Stop();
                _isDragNavigating = false;
                _hasNavigatedInCurrentDrag = false;
                _isInteractingWithQuickActions = false;
                _isDraggingBubble = false;
                _isPotentialBubbleDrag = false;
                _draggedBubbleButton = null;
                _draggedBubbleLabel = null;
                _draggedBubbleIndex = -1;
                _draggedBubbleItem = null;
                _currentAimSlot = -1;
                IsHoldGestureActive = true;

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
                UpdateHudStatus();

                ApplyThemeVisuals(_viewModel.ActiveTheme);
                RenderLayout(true);
                HideContextActionCard();

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

        private void UpdateHudStatus()
        {
            try
            {
                if (HudClockText != null)
                    HudClockText.Text = DateTime.Now.ToString("HH:mm");

                if (HudBatteryText != null)
                {
                    var ps = System.Windows.Forms.SystemInformation.PowerStatus;
                    int pct = (int)Math.Round(ps.BatteryLifePercent * 100);
                    if (pct >= 0 && pct <= 100)
                        HudBatteryText.Text = $"⚡ {pct}%";
                    else
                        HudBatteryText.Text = "⚡ AC";
                }
            }
            catch { }
        }

        private static Color GetReadableForeground(Color bg)
        {
            double lum = (0.2126 * bg.R + 0.7152 * bg.G + 0.0722 * bg.B) / 255.0;
            return lum > 0.56 ? Color.FromRgb(10, 10, 12) : Colors.White;
        }

        public void ApplyThemeVisuals(Theme theme)
        {
            // Keep BaseFill transparent so floating squircle tiles sit directly over the desktop
            BaseFill.Color = Colors.Transparent;

            var stroke = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            stroke.GradientStops.Add(new GradientStop(Color.FromArgb(40, 255, 255, 255), 0.0));
            stroke.GradientStops.Add(new GradientStop(Color.FromArgb(14, 255, 255, 255), 0.5));
            stroke.GradientStops.Add(new GradientStop(Color.FromArgb(40, 255, 255, 255), 1.0));
            BaseCircle.Stroke = stroke;

            // Rovyl 7-stop smoothstep radial scrim
            var scrim = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.5, 0.5),
                Center = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            scrim.GradientStops.Add(new GradientStop(Color.FromArgb(128, 4, 5, 7), 0.0));
            scrim.GradientStops.Add(new GradientStop(Color.FromArgb(112, 4, 5, 7), 0.18));
            scrim.GradientStops.Add(new GradientStop(Color.FromArgb(88, 4, 5, 7), 0.36));
            scrim.GradientStops.Add(new GradientStop(Color.FromArgb(56, 4, 5, 7), 0.54));
            scrim.GradientStops.Add(new GradientStop(Color.FromArgb(28, 4, 5, 7), 0.72));
            scrim.GradientStops.Add(new GradientStop(Color.FromArgb(8, 4, 5, 7), 0.88));
            scrim.GradientStops.Add(new GradientStop(Color.FromArgb(0, 4, 5, 7), 1.0));
            GlowEllipse.Fill = scrim;

            HoverInfoText.Foreground = new SolidColorBrush(theme.TextColor);

            CategoryPill.Background = new SolidColorBrush(Color.FromArgb(238, 6, 7, 9));
            CategoryPill.BorderBrush = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255));
            CategoryTitleText.Foreground = new SolidColorBrush(Color.FromArgb(215, 255, 255, 255));

            if (ContextActionCard != null)
            {
                ContextActionCard.Background = new SolidColorBrush(Color.FromArgb(242, 8, 9, 12));
                ContextActionCard.BorderBrush = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255));
            }

            SearchBorder.Background = new SolidColorBrush(Color.FromArgb(245, 10, 11, 14));
            SearchBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(90, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B));
            SearchText.Foreground = new SolidColorBrush(theme.TextColor);
        }

        public void RenderLayout(bool animateBloom = false)
        {
            ApplyThemeVisuals(_viewModel.ActiveTheme);
            ClearActiveDwmThumbnails();
            HideContextActionCard();

            ItemsCanvas.Children.Clear();
            _visibleButtons.Clear();

            if (CentralHubLogo != null && CentralHubBackIcon != null)
            {
                CentralHubLogo.Visibility = _viewModel.IsSubmenu ? Visibility.Collapsed : Visibility.Visible;
                CentralHubBackIcon.Visibility = _viewModel.IsSubmenu ? Visibility.Visible : Visibility.Collapsed;
            }

            if (CategoryShortcutText != null)
            {
                CategoryShortcutText.Text = _viewModel.IsSubmenu ? "↩ Back" : $"Key {_viewModel.CurrentCategoryIndex + 1}";
            }

            var items = _viewModel.CurrentPageItems;
            if (items.Count == 0) return;

            bool isCompact = _viewModel.ActiveTheme.DensityMode == "Compact";
            var placements = RadialLayoutCalculator.CalculatePlacements(items.Count, 250, 250, isCompact);
            if (placements.Count > 0 && BaseCircle != null)
            {
                BaseCircle.Width = placements[0].DynamicRadius * 2.0;
                BaseCircle.Height = placements[0].DynamicRadius * 2.0;
            }

            var theme = _viewModel.ActiveTheme;
            var idleTileBg = new SolidColorBrush(Color.FromArgb(248, 18, 19, 23));
            var regularBorderBrush = new SolidColorBrush(Color.FromArgb(72, 255, 255, 255));
            var activeReadableFg = new SolidColorBrush(GetReadableForeground(theme.AccentColor));
            var pillBg = theme.AccentBrush;
            var pillBorder = new SolidColorBrush(theme.AccentColor);
            var textBrush = new SolidColorBrush(theme.TextColor);

            for (int i = 0; i < placements.Count; i++)
            {
                var p = placements[i];
                var item = items[i];
                IntPtr currentHoverThumb = IntPtr.Zero;

                double tileSize = p.DynamicTileSize > 0 ? p.DynamicTileSize : p.CircleSize;
                double iconSize = p.DynamicIconSize > 0 ? p.DynamicIconSize : p.IconSize;
                double tileX = p.DynamicTileSize > 0 ? p.DynamicButtonX : p.ButtonX;
                double tileY = p.DynamicTileSize > 0 ? p.DynamicButtonY : p.ButtonY;
                double squircleRadius = Math.Round(tileSize * 0.32);

                var btn = new Button
                {
                    Width = tileSize,
                    Height = tileSize,
                    Tag = item,
                    BorderThickness = new Thickness(1.2),
                    BorderBrush = regularBorderBrush,
                    Background = idleTileBg,
                    Cursor = Cursors.Hand,
                    UseLayoutRounding = true,
                    SnapsToDevicePixels = true,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 18,
                        ShadowDepth = 4,
                        Color = Colors.Black,
                        Opacity = 0.58
                    }
                };
                Canvas.SetLeft(btn, tileX);
                Canvas.SetTop(btn, tileY);
                Panel.SetZIndex(btn, 15);

                btn.Resources.Add(typeof(Border), new Style(typeof(Border))
                {
                    Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(squircleRadius)) }
                });

                // Icon content
                var iconImg = ResolveItemIcon(item);
                if (iconImg != null)
                {
                    var img = new Image
                    {
                        Source = iconImg,
                        Width = iconSize,
                        Height = iconSize,
                        Stretch = Stretch.Uniform,
                        UseLayoutRounding = true,
                        SnapsToDevicePixels = true
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    btn.Content = img;
                }
                else
                {
                    btn.Content = new TextBlock
                    {
                        Text = item.Name.Length > 0 ? item.Name.Substring(0, 1).ToUpper() : "?",
                        FontSize = 15,
                        FontWeight = FontWeights.Bold,
                        Foreground = textBrush
                    };
                }

                // Rovyl-style Dynamic Outer Pill Label (never truncates, opens outward from slice)
                var txt = new TextBlock
                {
                    Text = item.Name,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = activeReadableFg,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var lblBorder = new Border
                {
                    CornerRadius = new CornerRadius(13),
                    Padding = new Thickness(12, 5, 12, 5),
                    Background = pillBg,
                    BorderBrush = pillBorder,
                    BorderThickness = new Thickness(1),
                    IsHitTestVisible = false,
                    Opacity = 0.0,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    Child = txt,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 14,
                        ShadowDepth = 3,
                        Color = Colors.Black,
                        Opacity = 0.55
                    }
                };
                lblBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double pillW = Math.Max(44.0, lblBorder.DesiredSize.Width);
                double pillH = Math.Max(26.0, lblBorder.DesiredSize.Height);

                var pillSide = RadialLayoutCalculator.GetOuterPillSide(p.AngleRadians);
                double pillX = tileX + (tileSize - pillW) / 2.0;
                double pillY = tileY + tileSize + 10.0;
                if (pillSide == OuterPillSide.Right)
                {
                    pillX = tileX + tileSize + 10.0;
                    pillY = tileY + (tileSize - pillH) / 2.0;
                }
                else if (pillSide == OuterPillSide.Left)
                {
                    pillX = tileX - pillW - 10.0;
                    pillY = tileY + (tileSize - pillH) / 2.0;
                }
                else if (pillSide == OuterPillSide.Top)
                {
                    pillX = tileX + (tileSize - pillW) / 2.0;
                    pillY = tileY - pillH - 10.0;
                }

                Panel.SetZIndex(lblBorder, 250);
                Canvas.SetLeft(lblBorder, Math.Round(pillX));
                Canvas.SetTop(lblBorder, Math.Round(pillY));

                // Wire interactions
                int localIndex = i;

                btn.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    _isPotentialBubbleDrag = true;
                    _isDraggingBubble = false;
                    _draggedBubbleButton = btn;
                    _draggedBubbleLabel = lblBorder;
                    _draggedBubbleIndex = localIndex;
                    _draggedBubbleItem = item;
                    _bubbleDragStartPoint = e.GetPosition(ItemsCanvas);
                };

                lblBorder.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    _isPotentialBubbleDrag = true;
                    _isDraggingBubble = false;
                    _draggedBubbleButton = btn;
                    _draggedBubbleLabel = lblBorder;
                    _draggedBubbleIndex = localIndex;
                    _draggedBubbleItem = item;
                    _bubbleDragStartPoint = e.GetPosition(ItemsCanvas);
                };

                btn.Click += (s, e) =>
                {
                    if (!_isDraggingBubble)
                    {
                        _viewModel.LaunchItem(item);
                    }
                };

                lblBorder.MouseLeftButtonUp += (s, e) =>
                {
                    if (!_isDraggingBubble)
                    {
                        _viewModel.LaunchItem(item);
                    }
                };

                if (item.Type == "WINDOW" && long.TryParse(item.Target, out long hWndVal))
                {
                    void ShowDesktopMoveMenu(object sender, MouseButtonEventArgs e)
                    {
                        e.Handled = true;
                        var cm = new ContextMenu();
                        var desktops = _viewModel.DesktopService.GetDesktops();
                        if (desktops == null || desktops.Count == 0)
                        {
                            var disabledItem = new MenuItem { Header = LocalizationService.Instance.GetString("Desktops_Unavailable", "⚠️ Virtual Desktops Unavailable"), IsEnabled = false };
                            cm.Items.Add(disabledItem);
                        }
                        else
                        {
                            for (int d = 0; d < desktops.Count; d++)
                            {
                                int targetDesktop = d;
                                string dName = !string.IsNullOrEmpty(desktops[d].Name) 
                                    ? desktops[d].Name 
                                    : string.Format(LocalizationService.Instance.GetString("Desktop_Name_Format", "Desktop {0}"), d + 1);
                                var mi = new MenuItem { Header = string.Format(LocalizationService.Instance.GetString("Move_To_Desktop", "🪟 Move to {0}"), dName) };
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
                    _currentAimSlot = localIndex;
                    HighlightKeyboardFocused();

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
                            double previewX = Math.Clamp(btnLogicalX + (tileSize / 2.0) - (previewW / 2.0), 20.0, this.Width - previewW - 20.0);
                            double previewY = (btnLogicalY > 300) 
                                ? Math.Max(20.0, btnLogicalY - previewH - 12.0) 
                                : Math.Min(this.Height - previewH - 20.0, btnLogicalY + tileSize + 12.0);

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
                    if (!IsHoldGestureActive)
                    {
                        _keyboardFocusIndex = -1;
                        _currentAimSlot = -1;
                        HighlightKeyboardFocused();
                    }

                    if (currentHoverThumb != IntPtr.Zero)
                    {
                        DwmThumbnailHelper.UnregisterThumbnail(currentHoverThumb);
                        _activeDwmThumbs.Remove(currentHoverThumb);
                        currentHoverThumb = IntPtr.Zero;
                    }
                }

                btn.MouseEnter += OnEnter;
                btn.MouseLeave += OnLeave;

                ItemsCanvas.Children.Add(btn);
                ItemsCanvas.Children.Add(lblBorder);
                _visibleButtons.Add((btn, lblBorder, item));

                if (animateBloom)
                {
                    double tileCenterX = tileX + (tileSize / 2.0);
                    double tileCenterY = tileY + (tileSize / 2.0);
                    RadialMotionSystem.AnimateItemBloomFromCenter(
                        btn,
                        i,
                        tileCenterX - 250.0,
                        tileCenterY - 250.0,
                        theme.ReduceMotion);
                }
            }

            RenderCategoryDots();
            RenderPageDots();
        }

        public void ShowContextActions(LauncherItem item)
        {
            if (ContextActionCard == null || ContextActionPanel == null) return;
            ContextActionPanel.Children.Clear();

            var actions = _contextualActionService.GetItemQuickActions(item);
            if (actions.Count == 0)
            {
                HideContextActionCard();
                return;
            }

            var theme = _viewModel.ActiveTheme;
            var textBrush = new SolidColorBrush(theme.TextColor);
            var btnBg = new SolidColorBrush(Color.FromArgb(190, theme.IconBgR, theme.IconBgG, theme.IconBgB));
            var btnBorder = new SolidColorBrush(Color.FromArgb(90, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B));

            foreach (var act in actions)
            {
                var actionBtn = new Button
                {
                    Margin = new Thickness(1.5, 0, 1.5, 0),
                    Padding = new Thickness(4, 1, 4, 1),
                    Height = 20,
                    Background = btnBg,
                    BorderBrush = btnBorder,
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    ToolTip = act.Title,
                    SnapsToDevicePixels = true
                };

                actionBtn.Resources.Add(typeof(Border), new Style(typeof(Border))
                {
                    Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(3.5)) }
                });

                var stack = new StackPanel { Orientation = Orientation.Horizontal };
                if (!string.IsNullOrEmpty(act.Icon))
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = act.Icon,
                        FontSize = 9.0,
                        Margin = new Thickness(0, 0, 2.5, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = theme.AccentBrush
                    });
                }
                stack.Children.Add(new TextBlock
                {
                    Text = act.Title,
                    FontSize = 9.0,
                    FontWeight = FontWeights.Medium,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = textBrush
                });

                actionBtn.Content = stack;
                actionBtn.Click += (s, e) =>
                {
                    e.Handled = true;
                    _contextualActionService.ExecuteItemQuickAction(item, act);
                    this.Hide();
                };

                ContextActionPanel.Children.Add(actionBtn);
            }

            RadialMotionSystem.AnimateQuickActionCard(ContextActionCard, true, theme.ReduceMotion);
        }

        private void HideContextActionCard()
        {
            if (ContextActionCard == null) return;
            RadialMotionSystem.AnimateQuickActionCard(ContextActionCard, false, _viewModel?.ActiveTheme?.ReduceMotion ?? false);
        }

        private void RenderPageDots()
        {
            if (PageDots == null) return;
            PageDots.Children.Clear();
            if (_viewModel.TotalPages <= 1)
            {
                PageDots.Visibility = Visibility.Collapsed;
                return;
            }

            PageDots.Visibility = Visibility.Visible;
            var theme = _viewModel.ActiveTheme;
            for (int i = 0; i < _viewModel.TotalPages; i++)
            {
                int pageIndex = i;
                bool isActive = (i == _viewModel.CurrentPageIndex);
                var dot = new Ellipse
                {
                    Width = isActive ? 7 : 5,
                    Height = isActive ? 7 : 5,
                    Margin = new Thickness(2.5, 0, 2.5, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Fill = isActive 
                        ? theme.AccentBrush 
                        : new SolidColorBrush(Color.FromArgb(90, theme.TextR, theme.TextG, theme.TextB)),
                    ToolTip = string.Format(LocalizationService.Instance.GetString("Page_Format", "Page {0} / {1}"), i + 1, _viewModel.TotalPages),
                    Cursor = Cursors.Hand
                };
                System.Windows.Automation.AutomationProperties.SetName(dot, string.Format(LocalizationService.Instance.GetString("Page_Name", "Page {0}"), i + 1));
                dot.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    int dir = pageIndex > _viewModel.CurrentPageIndex ? 1 : -1;
                    _viewModel.CurrentPageIndex = pageIndex;
                    _viewModel.RefreshPageItems();
                    RadialMotionSystem.AnimatePageTransition(ItemsCanvas, dir, _viewModel.ActiveTheme.ReduceMotion);
                };
                PageDots.Children.Add(dot);
            }
        }

        private ImageSource? ResolveItemIcon(LauncherItem item)
        {
            if (item.Type == "WINDOW")
            {
                if (RadialMenuViewModel.WindowIcons.TryGetValue(item.Target, out var winIcon) && winIcon != null)
                    return winIcon;
                return _iconExtractor.CreateMonogramIcon(item.Name, Color.FromRgb(155, 89, 182));
            }

            var actionIcon = _iconExtractor.GetActionIcon(item.Target) ?? _iconExtractor.GetActionIcon(item.Name);
            if (actionIcon != null) return actionIcon;

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

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                _dragStartPoint = e.GetPosition(this);
                _isDragNavigating = true;
                _hasNavigatedInCurrentDrag = false;
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (!_isPotentialBubbleDrag && !_isDraggingBubble)
                {
                    _dragStartPoint = e.GetPosition(this);
                    _isDragNavigating = true;
                    _hasNavigatedInCurrentDrag = false;
                }
            }
        }

        private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(this);
            double centerX = this.Width / 2.0;
            double centerY = this.Height / 2.0;
            double distFromCenter = Math.Sqrt(Math.Pow(pos.X - centerX, 2) + Math.Pow(pos.Y - centerY, 2));

            if (distFromCenter > 330.0 && !_isInteractingWithQuickActions && !_isDraggingBubble)
            {
                if (!_autoCloseGraceTimer.IsEnabled)
                {
                    _autoCloseGraceTimer.Start();
                }
            }
            else
            {
                _autoCloseGraceTimer.Stop();
            }

            var canvasPos = e.GetPosition(ItemsCanvas);

            // 1. Bubble Drag & Drop
            if (_isPotentialBubbleDrag && _draggedBubbleButton != null && e.LeftButton == MouseButtonState.Pressed)
            {
                double dragDist = Math.Sqrt(Math.Pow(canvasPos.X - _bubbleDragStartPoint.X, 2) + Math.Pow(canvasPos.Y - _bubbleDragStartPoint.Y, 2));
                if (dragDist > 6.0)
                {
                    _isDraggingBubble = true;
                    _isDragNavigating = false;
                    _hasNavigatedInCurrentDrag = false;

                    Panel.SetZIndex(_draggedBubbleButton, 500);
                    if (_draggedBubbleLabel != null) Panel.SetZIndex(_draggedBubbleLabel, 501);

                    double btnW = _draggedBubbleButton.Width;
                    double btnH = _draggedBubbleButton.Height;
                    Canvas.SetLeft(_draggedBubbleButton, canvasPos.X - (btnW / 2.0));
                    Canvas.SetTop(_draggedBubbleButton, canvasPos.Y - (btnH / 2.0));

                    if (_draggedBubbleLabel != null)
                    {
                        double lblW = _draggedBubbleLabel.Width;
                        Canvas.SetLeft(_draggedBubbleLabel, canvasPos.X - (lblW / 2.0));
                        Canvas.SetTop(_draggedBubbleLabel, canvasPos.Y + (btnH / 2.0) + 2.0);
                    }
                    return;
                }
            }

            // 1b. Hold-and-Aim gesture & Central Hub deadzone resolution (Rovyl style)
            double dxCenter = canvasPos.X - 250.0;
            double dyCenter = canvasPos.Y - 250.0;
            double distCenter = Math.Sqrt(dxCenter * dxCenter + dyCenter * dyCenter);
            bool inCenterHub = distCenter < 48.0;

            if (CentralHubBorder != null)
            {
                var theme = _viewModel.ActiveTheme;
                CentralHubBorder.Background = inCenterHub
                    ? theme.AccentBrush
                    : new SolidColorBrush(Color.FromArgb(232, 6, 7, 9));
                CentralHubBorder.BorderBrush = inCenterHub
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Color.FromArgb(76, 255, 255, 255));
            }

            if (IsHoldGestureActive)
            {
                int count = _viewModel.CurrentPageItems.Count;
                int slot = RadialLayoutCalculator.ResolveAimSlot(canvasPos, count, AimMode, 250, 250, 48.0, 320.0);
                if (slot != _currentAimSlot)
                {
                    _currentAimSlot = slot;
                    _keyboardFocusIndex = slot;
                    HighlightKeyboardFocused();
                }
            }

            // 2. Global Swipe Navigation
            if (_isDragNavigating && !_isDraggingBubble && (e.MiddleButton == MouseButtonState.Pressed || e.LeftButton == MouseButtonState.Pressed))
            {
                double dx = pos.X - _dragStartPoint.X;
                if (!_hasNavigatedInCurrentDrag && Math.Abs(dx) > 35)
                {
                    if (dx > 35)
                    {
                        _viewModel.NavigateNextGlobal();
                        RenderLayout(true);
                    }
                    else if (dx < -35)
                    {
                        _viewModel.NavigatePrevGlobal();
                        RenderLayout(true);
                    }
                    _hasNavigatedInCurrentDrag = true;
                    _dragStartPoint = pos;
                }
            }

            // 3. Magnetic Hover (only if not dragging a bubble)
            if (!_isDraggingBubble)
            {
                foreach (var (btn, _, _) in _visibleButtons)
                {
                    double bx = Canvas.GetLeft(btn) + (btn.Width / 2.0);
                    double by = Canvas.GetTop(btn) + (btn.Height / 2.0);
                    var offset = RadialLayoutCalculator.CalculateMagneticHoverOffset(new Point(bx, by), canvasPos, 6.0);
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
        }

        private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingBubble)
            {
                var canvasPos = e.GetPosition(ItemsCanvas);
                int count = _viewModel.CurrentPageItems.Count;
                int targetSlot = RadialLayoutCalculator.CalculateNearestSlot(canvasPos, count, 250, 250);
                int sourceIdx = _draggedBubbleIndex;

                _isDraggingBubble = false;
                _isPotentialBubbleDrag = false;
                _draggedBubbleButton = null;
                _draggedBubbleLabel = null;
                _draggedBubbleIndex = -1;
                _draggedBubbleItem = null;

                if (sourceIdx >= 0 && targetSlot >= 0 && sourceIdx != targetSlot && targetSlot < count)
                {
                    _viewModel.ReorderCurrentPageItems(sourceIdx, targetSlot);
                }
                else
                {
                    RenderLayout();
                }

                e.Handled = true;
                return;
            }

            _isPotentialBubbleDrag = false;
            _draggedBubbleButton = null;
            _draggedBubbleLabel = null;
            _draggedBubbleIndex = -1;
            _draggedBubbleItem = null;

            if (_isDragNavigating)
            {
                _isDragNavigating = false;
                if (_hasNavigatedInCurrentDrag)
                {
                    e.Handled = true;
                }
            }
        }

        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == RootGrid || e.OriginalSource == BaseCircle || e.OriginalSource == GlowEllipse || e.OriginalSource == ItemsCanvas)
            {
                this.Hide();
                ((App)Application.Current).OpenSettings();
            }
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isInteractingWithQuickActions && !_autoCloseGraceTimer.IsEnabled)
            {
                _autoCloseGraceTimer.Start();
            }
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            _autoCloseGraceTimer.Stop();
        }

        private void AutoCloseGraceTimer_Tick(object? sender, EventArgs e)
        {
            _autoCloseGraceTimer.Stop();
            if (this.IsVisible && !_isDragNavigating && !_isInteractingWithQuickActions)
            {
                ClearActiveDwmThumbnails();
                this.Hide();
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                _viewModel.NavigatePrevGlobal();
                RenderLayout(true);
            }
            else if (e.Delta < 0)
            {
                _viewModel.NavigateNextGlobal();
                RenderLayout(true);
            }
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
                RenderLayout(true);
                e.Handled = true;
                return;
            }

            // Number keys 1-9 switch category/workspace directly (inspired by Rovyl)
            int categoryNum = -1;
            if (e.Key >= Key.D1 && e.Key <= Key.D9)
            {
                categoryNum = (int)e.Key - (int)Key.D1;
            }
            else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9)
            {
                categoryNum = (int)e.Key - (int)Key.NumPad1;
            }

            if (categoryNum >= 0 && categoryNum < _viewModel.Categories.Count && !_viewModel.IsSearchMode)
            {
                _viewModel.SwitchCategory(categoryNum);
                RenderLayout(true);
                e.Handled = true;
                return;
            }
        }

        private void HighlightKeyboardFocused()
        {
            var theme = _viewModel.ActiveTheme;
            var idleTileBg = new SolidColorBrush(Color.FromArgb(248, 18, 19, 23));
            var regularBorder = new SolidColorBrush(Color.FromArgb(72, 255, 255, 255));
            var activeReadableFg = new SolidColorBrush(GetReadableForeground(theme.AccentColor));

            if (_keyboardFocusIndex < 0)
            {
                _viewModel.HoveredItemTitle = string.Empty;
                HideContextActionCard();
            }

            for (int i = 0; i < _visibleButtons.Count; i++)
            {
                var (btn, lbl, item) = _visibleButtons[i];
                var txt = lbl.Child as TextBlock;
                if (i == _keyboardFocusIndex)
                {
                    Panel.SetZIndex(btn, 180);
                    Panel.SetZIndex(lbl, 260);
                    btn.Background = theme.AccentBrush;
                    btn.BorderBrush = new SolidColorBrush(Colors.White);
                    btn.BorderThickness = new Thickness(1.6);
                    btn.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 26,
                        ShadowDepth = 0,
                        Color = theme.AccentColor,
                        Opacity = 0.82
                    };
                    if (txt != null)
                    {
                        txt.Foreground = activeReadableFg;
                        txt.FontWeight = FontWeights.SemiBold;
                    }
                    _viewModel.HoveredItemTitle = item.Name;
                    RadialMotionSystem.AnimateHover(btn, lbl, true, theme.ReduceMotion);
                    ShowContextActions(item);
                }
                else
                {
                    Panel.SetZIndex(btn, 15);
                    Panel.SetZIndex(lbl, 250);
                    btn.Background = idleTileBg;
                    btn.BorderBrush = regularBorder;
                    btn.BorderThickness = new Thickness(1.2);
                    btn.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 18,
                        ShadowDepth = 4,
                        Color = Colors.Black,
                        Opacity = 0.58
                    };
                    RadialMotionSystem.AnimateHover(btn, lbl, false, theme.ReduceMotion);
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

        private void CentralHub_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            bool wasSubmenu = _viewModel.IsSubmenu;
            _viewModel.CenterButtonClick();
            if (wasSubmenu)
            {
                RenderLayout(true);
            }
        }

        private void CategoryPill_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            int nextCat = (_viewModel.CurrentCategoryIndex + 1) % Math.Max(1, _viewModel.Categories.Count);
            _viewModel.SwitchCategory(nextCat);
            RenderLayout(true);
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            _autoCloseGraceTimer.Stop();
            _isDragNavigating = false;
            _isDraggingBubble = false;
            _isPotentialBubbleDrag = false;
            _draggedBubbleButton = null;
            _draggedBubbleLabel = null;
            _draggedBubbleIndex = -1;
            _draggedBubbleItem = null;
            ClearActiveDwmThumbnails();
            this.Hide();
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    int added = 0;
                    foreach (var f in files)
                    {
                        var (ok, _, item) = RadialLauncher.Services.Import.LauncherDropParser.BuildItem(f);
                        if (ok && item != null)
                        {
                            if (_viewModel.CurrentCategory != null && _viewModel.CurrentCategory.Id > 0)
                            {
                                item.CategoryId = _viewModel.CurrentCategory.Id;
                            }
                            _viewModel.ItemRepository.Insert(item);
                            added++;
                        }
                    }
                    if (added > 0)
                    {
                        _viewModel.ReloadCategoriesAndItems();
                        RenderLayout();
                    }
                }
            }
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
