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
using Microsoft.Extensions.DependencyInjection;
using RadialLauncher.Models;
using RadialLauncher.Services.Audio;
using RadialLauncher.Services.Clipboard;
using RadialLauncher.Services.Context;
using RadialLauncher.Services.Icons;
using RadialLauncher.Services.Input;
using RadialLauncher.Services.Localization;
using RadialLauncher.Services.SystemTools;
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
        private List<RadialItemPlacement> _currentPlacements = new();
        private readonly List<Color> _itemAccentColors = new();
        private readonly Dictionary<string, ImageSource?> _resolvedIconCache = new();
        private readonly Dictionary<string, Color> _dominantColorCache = new();
        private int _keyboardFocusIndex = -1;
        private int _lastSoundHoverIndex = -1;

        private INowPlayingAndAudioService AudioService => App.ServiceProvider?.GetService<INowPlayingAndAudioService>() ?? _fallbackAudioService;
        private IUiSoundEffectsService SoundService => App.ServiceProvider?.GetService<IUiSoundEffectsService>() ?? _fallbackSoundService;
        private IGamepadRadialService GamepadService => App.ServiceProvider?.GetService<IGamepadRadialService>() ?? _fallbackGamepadService;
        private IPlaytimeTrackerService PlaytimeService => App.ServiceProvider?.GetService<IPlaytimeTrackerService>() ?? _fallbackPlaytimeService;
        private ISystemTelemetryService TelemetryService => App.ServiceProvider?.GetService<ISystemTelemetryService>() ?? _fallbackTelemetryService;
        private IPerAppAudioAndDeviceService PerAppAudioService => App.ServiceProvider?.GetService<IPerAppAudioAndDeviceService>() ?? _fallbackPerAppAudioService;
        private IRichClipboardPreviewService RichClipboardService => App.ServiceProvider?.GetService<IRichClipboardPreviewService>() ?? _fallbackRichClipboardService;

        private static readonly INowPlayingAndAudioService _fallbackAudioService = new NowPlayingAndAudioService();
        private static readonly IUiSoundEffectsService _fallbackSoundService = new UiSoundEffectsService();
        private static readonly IGamepadRadialService _fallbackGamepadService = new GamepadRadialService();
        private static readonly IPlaytimeTrackerService _fallbackPlaytimeService = new PlaytimeTrackerService();
        private static readonly ISystemTelemetryService _fallbackTelemetryService = new SystemTelemetryService();
        private static readonly IPerAppAudioAndDeviceService _fallbackPerAppAudioService = new PerAppAudioAndDeviceService();
        private static readonly IRichClipboardPreviewService _fallbackRichClipboardService = new RichClipboardPreviewService();

        private readonly DispatcherTimer _toastHideTimer;
        private readonly DispatcherTimer _markingDwellTimer;
        private readonly DispatcherTimer _trailDecayTimer;
        private readonly List<Point> _cursorTrailPoints = new();
        private DateTime _markingDwellStartUtc;
        private int _markingDwellSlot = -1;

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

            _toastHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(2100)
            };
            _toastHideTimer.Tick += (s, e) =>
            {
                _toastHideTimer.Stop();
                if (ActionToastBadge != null)
                {
                    RadialMotionSystem.AnimateToastBadge(ActionToastBadge, false, _viewModel.ActiveTheme.ReduceMotion);
                }
                if (HubVolumeArc != null && _markingDwellSlot < 0)
                {
                    HubVolumeArc.Opacity = 0;
                }
            };

            _markingDwellTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _markingDwellTimer.Tick += MarkingDwellTimer_Tick;

            _trailDecayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(22)
            };
            _trailDecayTimer.Tick += TrailDecayTimer_Tick;

            _viewModel.RequestShowToast += (msg, hexColor) =>
            {
                Dispatcher.Invoke(() => ShowActionToast(msg, ParseHexColor(hexColor)));
            };

            _viewModel.RequestClose += () => 
            {
                _autoCloseGraceTimer.Stop();
                _markingDwellTimer.Stop();
                _trailDecayTimer.Stop();
                GamepadService.StopPolling();
                ClearActiveDwmThumbnails();
                Dispatcher.Invoke(this.Hide);
            };
            _viewModel.RequestLayoutUpdate += () => Dispatcher.Invoke(() => RenderLayout(false));

            WireGamepadEvents();

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
                _markingDwellTimer.Stop();
                _trailDecayTimer.Stop();
                GamepadService.StopPolling();
                ClearActiveDwmThumbnails();
            };
            IsVisibleChanged += (s, e) =>
            {
                if (!IsVisible)
                {
                    _autoCloseGraceTimer.Stop();
                    _markingDwellTimer.Stop();
                    _trailDecayTimer.Stop();
                    GamepadService.StopPolling();
                    ClearActiveDwmThumbnails();
                    HideContextActionCard();
                }
            };

            RadialLauncher.Services.Localization.LocalizationService.Instance.OnLanguageChanged += () =>
            {
                Dispatcher.Invoke(ApplyLocalization);
            };
            ApplyLocalization();

            // Pre-warm icons, dominant colors, and visual tree in idle time so first middle-click opens in <5ms
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    _viewModel.InitializeForDisplay();
                    RenderLayout(false);
                }
                catch { }
            }));
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
                var dpi = VisualTreeHelper.GetDpi(this);
                double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

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
                // If slot == -1 (user tapped middle mouse button in center deadzone), keep menu open in interactive mode!
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error resolving hold-and-aim release target");
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

                var dpi = VisualTreeHelper.GetDpi(this);
                double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

                double logicalX = screenX / dpiX;
                double logicalY = screenY / dpiY;

                var clamped = MultiMonitorHelper.ClampWindowToCursorScreen(this.Width, this.Height, logicalX, logicalY, dpiX, dpiY);
                this.Left = clamped.X;
                this.Top = clamped.Y;

                _keyboardFocusIndex = -1;
                _lastSoundHoverIndex = -1;
                _markingDwellSlot = -1;
                _markingDwellTimer.Stop();
                _cursorTrailPoints.Clear();
                if (AimTrailGlowPolyline != null) AimTrailGlowPolyline.Opacity = 0;
                if (AimTrailCorePolyline != null) AimTrailCorePolyline.Opacity = 0;
                if (OrbitalPeekCanvas != null) OrbitalPeekCanvas.Children.Clear();
                if (HubVolumeArc != null) HubVolumeArc.Opacity = 0;

                _viewModel.InitializeForDisplay();
                UpdateHudStatus();
                RenderLayout(true);

                this.Opacity = 1;
                this.Show();
                this.Activate();
                this.Focus();

                SoundService.PlayBloomOpen();
                GamepadService.StartPolling();

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

                var snap = TelemetryService.GetLatestSnapshot();
                if (HudBatteryText != null)
                {
                    HudBatteryText.Text = snap.BatteryPercent >= 0 && snap.BatteryPercent <= 100
                        ? $"⚡ {snap.BatteryPercent}%"
                        : "⚡ AC";
                }

                if (HudTelemetryText != null)
                {
                    HudTelemetryText.Text = $"CPU %{snap.CpuPercent} · RAM %{snap.RamPercent}";
                }

                UpdateTelemetryRings(snap);

                if (HudProfileBadgeText != null)
                {
                    HudProfileBadgeText.Text = _viewModel.ActiveProfileTitle;
                }

                _ = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        var media = AudioService.GetCurrentMediaInfo();
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            if (HudNowPlayingPanel != null && HudMediaTitleText != null)
                            {
                                HudNowPlayingPanel.Visibility = Visibility.Visible;
                                HudMediaTitleText.Text = media.HasMedia
                                    ? $"🎵 {(string.IsNullOrWhiteSpace(media.Artist) ? media.Title : $"{media.Artist} - {media.Title}")}"
                                    : "🎵 Medya";
                            }
                        }));
                    }
                    catch { }
                });
            }
            catch { }
        }

        private void UpdateTelemetryRings(SystemTelemetrySnapshot snap)
        {
            DrawRingArc(TelemetryCpuArc, 35.8, Math.Max(0.05, snap.CpuPercent / 100.0), -Math.PI / 2.0);
            DrawRingArc(TelemetryRamArc, 38.2, Math.Max(0.05, snap.RamPercent / 100.0), -Math.PI / 2.0);
            DrawRingArc(TelemetryBatArc, 40.6, Math.Max(0.05, snap.BatteryPercent / 100.0), -Math.PI / 2.0);
        }

        private static void DrawRingArc(System.Windows.Shapes.Path? path, double radius, double ratio0To1, double startAngleRad)
        {
            if (path == null) return;
            double clamped = Math.Clamp(ratio0To1, 0.02, 0.995);
            double cx = 250.0, cy = 250.0;
            double endAngle = startAngleRad + (clamped * 2.0 * Math.PI);
            Point pStart = new Point(cx + radius * Math.Cos(startAngleRad), cy + radius * Math.Sin(startAngleRad));
            Point pEnd = new Point(cx + radius * Math.Cos(endAngle), cy + radius * Math.Sin(endAngle));

            var fig = new PathFigure { StartPoint = pStart, IsClosed = false, IsFilled = false };
            fig.Segments.Add(new ArcSegment(pEnd, new Size(radius, radius), 0, clamped > 0.5, SweepDirection.Clockwise, true));
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            path.Data = geo;
        }

        private void TrailDecayTimer_Tick(object? sender, EventArgs e)
        {
            if (_cursorTrailPoints.Count > 1)
            {
                _cursorTrailPoints.RemoveAt(0);
                RefreshTrailPolylinePoints();
            }
            else
            {
                _cursorTrailPoints.Clear();
                if (AimTrailGlowPolyline != null) AimTrailGlowPolyline.Opacity = 0;
                if (AimTrailCorePolyline != null) AimTrailCorePolyline.Opacity = 0;
                _trailDecayTimer.Stop();
            }
        }

        private void RefreshTrailPolylinePoints()
        {
            if (AimTrailGlowPolyline == null || AimTrailCorePolyline == null) return;
            if (_cursorTrailPoints.Count < 2)
            {
                AimTrailGlowPolyline.Opacity = 0;
                AimTrailCorePolyline.Opacity = 0;
                return;
            }

            var pts = new PointCollection(_cursorTrailPoints);
            AimTrailGlowPolyline.Points = pts;
            AimTrailCorePolyline.Points = pts;
            AimTrailGlowPolyline.Opacity = 0.42;
            AimTrailCorePolyline.Opacity = 0.88;
        }

        private void BtnHudMediaPrev_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            SoundService.PlayHoverTick();
            AudioService.PreviousTrack();
            ShowActionToast("⏮ Önceki Parça", Color.FromRgb(16, 185, 129));
        }

        private void BtnHudMediaPlayPause_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            SoundService.PlayLaunchConfirm();
            AudioService.PlayPause();
            ShowActionToast("⏯ Oynat / Duraklat", Color.FromRgb(16, 185, 129));
        }

        private void BtnHudMediaNext_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            SoundService.PlayHoverTick();
            AudioService.NextTrack();
            ShowActionToast("⏭ Sonraki Parça", Color.FromRgb(16, 185, 129));
        }

        public void ShowActionToast(string text, Color borderColor)
        {
            if (ActionToastBadge == null || ActionToastText == null) return;
            ActionToastText.Text = text;
            ActionToastBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(200, borderColor.R, borderColor.G, borderColor.B));
            ActionToastBadge.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 2,
                Color = borderColor,
                Opacity = 0.72
            };
            RadialMotionSystem.AnimateToastBadge(ActionToastBadge, true, _viewModel.ActiveTheme.ReduceMotion);
            _toastHideTimer.Stop();
            _toastHideTimer.Start();
        }

        public void UpdateHubArcProgress(double ratio0To1, Color arcColor)
        {
            if (HubVolumeArc == null) return;
            double clamped = Math.Clamp(ratio0To1, 0.0, 0.999);
            if (clamped <= 0.01)
            {
                HubVolumeArc.Opacity = 0;
                return;
            }

            double cx = 250.0, cy = 250.0, r = 34.0;
            double startAngle = -Math.PI / 2.0;
            double endAngle = startAngle + (clamped * 2.0 * Math.PI);

            Point pStart = new Point(cx + r * Math.Cos(startAngle), cy + r * Math.Sin(startAngle));
            Point pEnd = new Point(cx + r * Math.Cos(endAngle), cy + r * Math.Sin(endAngle));

            var fig = new PathFigure { StartPoint = pStart, IsClosed = false, IsFilled = false };
            fig.Segments.Add(new ArcSegment(pEnd, new Size(r, r), 0, clamped > 0.5, SweepDirection.Clockwise, true));
            var geo = new PathGeometry();
            geo.Figures.Add(fig);

            HubVolumeArc.Data = geo;
            HubVolumeArc.Stroke = new SolidColorBrush(arcColor);
            HubVolumeArc.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12,
                ShadowDepth = 0,
                Color = arcColor,
                Opacity = 0.85
            };
            HubVolumeArc.Opacity = 1.0;
        }

        private void MarkingDwellTimer_Tick(object? sender, EventArgs e)
        {
            if (!IsHoldGestureActive || _markingDwellSlot < 0 || _markingDwellSlot >= _visibleButtons.Count)
            {
                _markingDwellTimer.Stop();
                _markingDwellSlot = -1;
                if (HubVolumeArc != null) HubVolumeArc.Opacity = 0;
                return;
            }

            double elapsedMs = (DateTime.UtcNow - _markingDwellStartUtc).TotalMilliseconds;
            double progress = Math.Clamp(elapsedMs / 240.0, 0.0, 1.0);
            UpdateHubArcProgress(progress, _viewModel.ActiveTheme.AccentColor);

            if (progress >= 1.0)
            {
                int slotToOpen = _markingDwellSlot;
                _markingDwellTimer.Stop();
                _markingDwellSlot = -1;
                if (HubVolumeArc != null) HubVolumeArc.Opacity = 0;

                if (slotToOpen >= 0 && slotToOpen < _visibleButtons.Count)
                {
                    var folderItem = _visibleButtons[slotToOpen].item;
                    ShowActionToast($"📂 {folderItem.Name} Açıldı", _viewModel.ActiveTheme.AccentColor);
                    _viewModel.LaunchItem(folderItem);
                    RenderLayout(true);
                }
            }
        }

        private void WireGamepadEvents()
        {
            GamepadService.OnStickAimChanged += (angleRad, isActive) =>
            {
                if (!this.IsVisible) return;
                Dispatcher.Invoke(() =>
                {
                    if (!isActive || _visibleButtons.Count == 0)
                    {
                        return;
                    }
                    double fakeX = 250.0 + 150.0 * Math.Cos(angleRad);
                    double fakeY = 250.0 + 150.0 * Math.Sin(angleRad);
                    int slot = RadialLayoutCalculator.ResolveAimSlot(new Point(fakeX, fakeY), _visibleButtons.Count, "angle", 250, 250, 48.0, 320.0);
                    if (slot >= 0 && slot != _keyboardFocusIndex)
                    {
                        _keyboardFocusIndex = slot;
                        _currentAimSlot = slot;
                        HighlightKeyboardFocused();
                    }
                });
            };

            GamepadService.OnConfirmPressed += () =>
            {
                if (!this.IsVisible) return;
                Dispatcher.Invoke(() =>
                {
                    if (_keyboardFocusIndex >= 0 && _keyboardFocusIndex < _visibleButtons.Count)
                    {
                        _viewModel.LaunchItem(_visibleButtons[_keyboardFocusIndex].item);
                    }
                });
            };

            GamepadService.OnCancelPressed += () =>
            {
                if (!this.IsVisible) return;
                Dispatcher.Invoke(() =>
                {
                    bool wasSubmenu = _viewModel.IsSubmenu;
                    _viewModel.CenterButtonClick();
                    if (wasSubmenu) RenderLayout(true);
                });
            };

            GamepadService.OnShoulderPressed += (dir) =>
            {
                if (!this.IsVisible) return;
                Dispatcher.Invoke(() =>
                {
                    if (dir > 0) _viewModel.NavigateNextGlobal();
                    else _viewModel.NavigatePrevGlobal();
                    SoundService.PlaySubmenuDive();
                    RenderLayout(true);
                });
            };
        }

        private static Color ParseHexColor(string hex)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hex) && ColorConverter.ConvertFromString(hex) is Color c)
                {
                    return c;
                }
            }
            catch { }
            return Color.FromRgb(16, 185, 129);
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
            OrbitalPeekCanvas?.Children.Clear();
            _visibleButtons.Clear();
            _itemAccentColors.Clear();
            _currentPlacements.Clear();
            UpdateAimIndicator(-1, _viewModel.ActiveTheme.AccentColor);

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
            _currentPlacements = placements;
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
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                Canvas.SetLeft(btn, tileX);
                Canvas.SetTop(btn, tileY);
                Panel.SetZIndex(btn, 15);

                var borderFactory = new FrameworkElementFactory(typeof(Border));
                borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
                borderFactory.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
                borderFactory.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
                borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(squircleRadius));
                borderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);

                var contentFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
                contentFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                contentFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                borderFactory.AppendChild(contentFactory);

                btn.Template = new System.Windows.Controls.ControlTemplate(typeof(Button))
                {
                    VisualTree = borderFactory
                };

                // Icon content + Rounded squircle clipping + Cached Dominant color extraction
                var iconImg = ResolveItemIcon(item);
                string colorKey = $"{item.Id}:{item.Target}:{theme.AccentColor}";
                if (!_dominantColorCache.TryGetValue(colorKey, out Color itemAccent))
                {
                    itemAccent = RadialLayoutCalculator.ExtractDominantAccentColor(iconImg, theme.AccentColor);
                    _dominantColorCache[colorKey] = itemAccent;
                }
                _itemAccentColors.Add(itemAccent);

                if (iconImg != null)
                {
                    double iconCorner = Math.Round(iconSize * 0.28);
                    var img = new Image
                    {
                        Source = iconImg,
                        Width = iconSize,
                        Height = iconSize,
                        Stretch = Stretch.Uniform,
                        UseLayoutRounding = true,
                        SnapsToDevicePixels = true,
                        Clip = new RectangleGeometry(new Rect(0, 0, iconSize, iconSize), iconCorner, iconCorner)
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
                var itemReadableFg = new SolidColorBrush(GetReadableForeground(itemAccent));
                var itemPillBg = new SolidColorBrush(itemAccent);
                var itemPillBorder = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));

                var txt = new TextBlock
                {
                    Text = item.Name,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = itemReadableFg,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var lblBorder = new Border
                {
                    CornerRadius = new CornerRadius(13),
                    Padding = new Thickness(12, 5, 12, 5),
                    Background = itemPillBg,
                    BorderBrush = itemPillBorder,
                    BorderThickness = new Thickness(1),
                    IsHitTestVisible = false,
                    Opacity = 0.0,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    Child = txt
                };
                lblBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double pillW = Math.Max(44.0, lblBorder.DesiredSize.Width);
                double pillH = Math.Max(26.0, lblBorder.DesiredSize.Height);

                var pillSide = RadialLayoutCalculator.GetOuterPillSide(p.AngleRadians);
                double pillX = tileX + (tileSize - pillW) / 2.0;
                double pillY = tileY + tileSize + 10.0;
                if (pillSide == OuterPillSide.Right)
                {
                    pillX = Math.Min(tileX + tileSize + 10.0, 582.0 - pillW);
                    pillY = tileY + (tileSize - pillH) / 2.0;
                }
                else if (pillSide == OuterPillSide.Left)
                {
                    pillX = Math.Max(-82.0, tileX - pillW - 10.0);
                    pillY = tileY + (tileSize - pillH) / 2.0;
                }
                else if (pillSide == OuterPillSide.Top)
                {
                    pillX = Math.Clamp(tileX + (tileSize - pillW) / 2.0, -82.0, 582.0 - pillW);
                    pillY = tileY - pillH - 10.0;
                }
                else
                {
                    pillX = Math.Clamp(pillX, -82.0, 582.0 - pillW);
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

            string cacheKey = $"{item.Id}:{item.Type}:{item.Name}:{item.Target}:{item.IconPath}";
            if (_resolvedIconCache.TryGetValue(cacheKey, out var cachedIcon) && cachedIcon != null)
            {
                return cachedIcon;
            }

            var resolved = ResolveItemIconUncached(item);
            if (resolved != null)
            {
                _resolvedIconCache[cacheKey] = resolved;
            }
            return resolved;
        }

        private ImageSource? ResolveItemIconUncached(LauncherItem item)
        {
            if (string.Equals(item.Type, "CLIPBOARD", StringComparison.OrdinalIgnoreCase))
            {
                var richClipIcon = RichClipboardService.CreateRichIcon(item.Target);
                if (richClipIcon != null) return richClipIcon;
            }

            var actionIcon = _iconExtractor.GetActionIcon(item.Target)
                          ?? _iconExtractor.GetActionIcon(item.Name)
                          ?? _iconExtractor.GetActionIcon(item.Type);
            if (actionIcon != null) return actionIcon;

            if (item.Type == "URL" && (item.Name.Contains("gmail", StringComparison.OrdinalIgnoreCase) || item.Target.Contains("mail.google", StringComparison.OrdinalIgnoreCase)))
            {
                var gmailIcon = _iconExtractor.GetBrandIcon(item.Name, item.Target);
                if (gmailIcon != null) return gmailIcon;
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

            // 1c. Dynamic Cursor Neon Ribbon Trail
            if (!inCenterHub && distCenter < 245.0 && !_viewModel.ActiveTheme.ReduceMotion)
            {
                if (_cursorTrailPoints.Count == 0 ||
                    (canvasPos - _cursorTrailPoints[^1]).Length >= 3.5)
                {
                    _cursorTrailPoints.Add(canvasPos);
                    if (_cursorTrailPoints.Count > 14)
                    {
                        _cursorTrailPoints.RemoveAt(0);
                    }

                    Color trailColor = (_keyboardFocusIndex >= 0 && _keyboardFocusIndex < _itemAccentColors.Count)
                        ? _itemAccentColors[_keyboardFocusIndex]
                        : _viewModel.ActiveTheme.AccentColor;
                    if (AimTrailGlowPolyline != null)
                        AimTrailGlowPolyline.Stroke = new SolidColorBrush(Color.FromArgb(145, trailColor.R, trailColor.G, trailColor.B));
                    if (AimTrailCorePolyline != null)
                        AimTrailCorePolyline.Stroke = new SolidColorBrush(Color.FromArgb(245, trailColor.R, trailColor.G, trailColor.B));

                    RefreshTrailPolylinePoints();
                    if (!_trailDecayTimer.IsEnabled)
                    {
                        _trailDecayTimer.Start();
                    }
                }
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
            if (_keyboardFocusIndex >= 0 && _keyboardFocusIndex < _visibleButtons.Count)
            {
                var (focusedBtn, focusedLbl, focusedItem) = _visibleButtons[_keyboardFocusIndex];
                if (string.Equals(focusedItem.Type, "APP_AUDIO_SESSION", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(focusedItem.Target, out int appPid))
                {
                    int appDelta = e.Delta > 0 ? 5 : -5;
                    int newAppVol = PerAppAudioService.AdjustAppVolume(appPid, appDelta);
                    SoundService.PlayHoverTick();
                    Color mixerColor = Color.FromRgb(236, 72, 153);
                    UpdateHubArcProgress(newAppVol / 100.0, mixerColor);

                    string baseName = focusedItem.Name;
                    int parenIdx = baseName.LastIndexOf("(%", StringComparison.Ordinal);
                    if (parenIdx > 0)
                    {
                        baseName = baseName[..parenIdx].TrimEnd();
                    }
                    focusedItem.Name = $"{baseName} (%{newAppVol})";
                    if (focusedLbl.Child is TextBlock lblTxt)
                    {
                        lblTxt.Text = focusedItem.Name;
                    }
                    _viewModel.HoveredItemTitle = focusedItem.Name;
                    ShowActionToast($"🎚️ {focusedItem.Name}", mixerColor);
                    e.Handled = true;
                    return;
                }
            }

            var canvasPos = e.GetPosition(ItemsCanvas);
            double distCenter = Math.Sqrt(Math.Pow(canvasPos.X - 250.0, 2) + Math.Pow(canvasPos.Y - 250.0, 2));
            bool isVolumeScroll = distCenter < 72.0 ||
                                  (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != ModifierKeys.None;

            if (isVolumeScroll)
            {
                int delta = e.Delta > 0 ? 4 : -4;
                int vol = AudioService.AdjustMasterVolume(delta);
                SoundService.PlayHoverTick();
                UpdateHubArcProgress(vol / 100.0, Color.FromRgb(16, 185, 129));
                ShowActionToast($"🔊 Sistem Sesi: %{vol}", Color.FromRgb(16, 185, 129));
                e.Handled = true;
                return;
            }

            if (e.Delta > 0)
            {
                SoundService.PlaySubmenuDive();
                _viewModel.NavigatePrevGlobal();
                UpdateHudStatus();
                RenderLayout(true);
            }
            else if (e.Delta < 0)
            {
                SoundService.PlaySubmenuDive();
                _viewModel.NavigateNextGlobal();
                UpdateHudStatus();
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
            var neighborBorder = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255));

            if (_keyboardFocusIndex != _lastSoundHoverIndex)
            {
                _lastSoundHoverIndex = _keyboardFocusIndex;
                if (_keyboardFocusIndex >= 0 && _keyboardFocusIndex < _visibleButtons.Count)
                {
                    SoundService.PlayHoverTick();
                    var fItem = _visibleButtons[_keyboardFocusIndex].item;
                    bool isFolder = string.Equals(fItem.Type, "SUBMENU", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(fItem.Type, "FOLDER", StringComparison.OrdinalIgnoreCase);
                    if (IsHoldGestureActive && isFolder)
                    {
                        _markingDwellSlot = _keyboardFocusIndex;
                        _markingDwellStartUtc = DateTime.UtcNow;
                        _markingDwellTimer.Start();
                    }
                    else
                    {
                        _markingDwellTimer.Stop();
                        _markingDwellSlot = -1;
                    }
                }
                else
                {
                    _markingDwellTimer.Stop();
                    _markingDwellSlot = -1;
                }
            }

            if (_keyboardFocusIndex < 0)
            {
                _viewModel.HoveredItemTitle = string.Empty;
                HideContextActionCard();
                UpdateAimIndicator(-1, theme.AccentColor);
                RenderOrbitalSubRingPeek(-1, theme.AccentColor);
            }

            int total = _visibleButtons.Count;
            for (int i = 0; i < total; i++)
            {
                var (btn, lbl, item) = _visibleButtons[i];
                var txt = lbl.Child as TextBlock;
                Color itemAccent = (i < _itemAccentColors.Count) ? _itemAccentColors[i] : theme.AccentColor;

                if (i == _keyboardFocusIndex)
                {
                    Panel.SetZIndex(btn, 180);
                    Panel.SetZIndex(lbl, 260);
                    var accentBrush = new SolidColorBrush(itemAccent);
                    var readableFg = new SolidColorBrush(GetReadableForeground(itemAccent));
                    btn.Background = accentBrush;
                    btn.BorderBrush = new SolidColorBrush(Colors.White);
                    btn.BorderThickness = new Thickness(1.6);
                    btn.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 28,
                        ShadowDepth = 0,
                        Color = itemAccent,
                        Opacity = 0.88
                    };
                    lbl.Background = accentBrush;
                    lbl.BorderBrush = new SolidColorBrush(Color.FromArgb(225, 255, 255, 255));
                    lbl.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 16,
                        ShadowDepth = 2,
                        Color = itemAccent,
                        Opacity = 0.65
                    };
                    if (txt != null)
                    {
                        txt.Foreground = readableFg;
                        txt.FontWeight = FontWeights.SemiBold;
                    }
                    string playtimeBadge = PlaytimeService.GetPlaytimeBadge(item.Name, item.Target);
                    _viewModel.HoveredItemTitle = string.IsNullOrWhiteSpace(playtimeBadge)
                        ? item.Name
                        : $"{item.Name}  ·  {playtimeBadge}";
                    UpdateAimIndicator(i, itemAccent);
                    RenderOrbitalSubRingPeek(i, itemAccent);
                    RadialMotionSystem.AnimateHoverState(btn, lbl, 1.16, 1.0, true, theme.ReduceMotion);
                    ShowContextActions(item);
                }
                else
                {
                    Panel.SetZIndex(btn, 15);
                    Panel.SetZIndex(lbl, 250);
                    btn.Background = idleTileBg;
                    btn.Effect = null;
                    lbl.Effect = null;

                    if (_keyboardFocusIndex < 0 || total <= 2)
                    {
                        btn.BorderBrush = regularBorder;
                        btn.BorderThickness = new Thickness(1.2);
                        RadialMotionSystem.AnimateHoverState(btn, lbl, 1.0, 1.0, false, theme.ReduceMotion);
                    }
                    else
                    {
                        int diff = Math.Abs(i - _keyboardFocusIndex);
                        int dist = Math.Min(diff, total - diff);
                        if (dist == 1)
                        {
                            btn.BorderBrush = neighborBorder;
                            btn.BorderThickness = new Thickness(1.35);
                            RadialMotionSystem.AnimateHoverState(btn, lbl, 1.06, 0.94, false, theme.ReduceMotion);
                        }
                        else
                        {
                            btn.BorderBrush = regularBorder;
                            btn.BorderThickness = new Thickness(1.1);
                            RadialMotionSystem.AnimateHoverState(btn, lbl, 0.97, 0.76, false, theme.ReduceMotion);
                        }
                    }
                }
            }
        }

        private void RenderOrbitalSubRingPeek(int focusedIndex, Color activeColor)
        {
            if (OrbitalPeekCanvas == null) return;
            OrbitalPeekCanvas.Children.Clear();

            if (focusedIndex < 0 || focusedIndex >= _visibleButtons.Count || focusedIndex >= _currentPlacements.Count)
                return;

            var p = _currentPlacements[focusedIndex];
            var parentItem = _visibleButtons[focusedIndex].item;
            var subItems = _viewModel.GetSubmenuPreviewItems(parentItem, 5);
            var quickActions = subItems.Count == 0
                ? _contextualActionService.GetItemQuickActions(parentItem)
                : new List<RadialLauncher.Services.Context.ItemContextAction>();

            int count = subItems.Count > 0 ? subItems.Count : Math.Min(3, quickActions.Count);
            if (count == 0) return;

            double cx = 250.0, cy = 250.0;
            double orbitR = Math.Min(232.0, p.DynamicRadius + 46.0);
            double stepRad = 0.21;
            double startRad = p.AngleRadians - ((count - 1) * stepRad / 2.0);
            double endRad = p.AngleRadians + ((count - 1) * stepRad / 2.0);

            if (count > 1)
            {
                Point arcStart = new Point(cx + orbitR * Math.Cos(startRad - 0.08), cy + orbitR * Math.Sin(startRad - 0.08));
                Point arcEnd = new Point(cx + orbitR * Math.Cos(endRad + 0.08), cy + orbitR * Math.Sin(endRad + 0.08));
                var arcFig = new PathFigure { StartPoint = arcStart, IsClosed = false, IsFilled = false };
                arcFig.Segments.Add(new ArcSegment(arcEnd, new Size(orbitR, orbitR), 0, false, SweepDirection.Clockwise, true));
                var arcGeo = new PathGeometry();
                arcGeo.Figures.Add(arcFig);
                var guidePath = new System.Windows.Shapes.Path
                {
                    Data = arcGeo,
                    Stroke = new SolidColorBrush(Color.FromArgb(145, activeColor.R, activeColor.G, activeColor.B)),
                    StrokeThickness = 1.6,
                    StrokeDashArray = new DoubleCollection { 2.5, 2.0 },
                    IsHitTestVisible = false
                };
                OrbitalPeekCanvas.Children.Add(guidePath);
            }

            for (int k = 0; k < count; k++)
            {
                double ang = startRad + (k * stepRad);
                double satSize = 26.0;
                double sx = cx + orbitR * Math.Cos(ang) - (satSize / 2.0);
                double sy = cy + orbitR * Math.Sin(ang) - (satSize / 2.0);

                var satBorder = new Border
                {
                    Width = satSize,
                    Height = satSize,
                    CornerRadius = new CornerRadius(8.5),
                    Background = new SolidColorBrush(Color.FromArgb(242, 14, 16, 22)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(215, activeColor.R, activeColor.G, activeColor.B)),
                    BorderThickness = new Thickness(1.3),
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 10,
                        ShadowDepth = 1,
                        Color = activeColor,
                        Opacity = 0.65
                    }
                };

                if (subItems.Count > 0)
                {
                    var sub = subItems[k];
                    satBorder.Child = new Image
                    {
                        Width = 15,
                        Height = 15,
                        Source = ResolveItemIcon(sub),
                        Stretch = Stretch.Uniform
                    };
                }
                else
                {
                    var qa = quickActions[k];
                    satBorder.Child = new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(qa.Icon) ? "⚡" : qa.Icon,
                        FontSize = 11,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = new SolidColorBrush(Colors.White)
                    };
                }

                Canvas.SetLeft(satBorder, sx);
                Canvas.SetTop(satBorder, sy);
                OrbitalPeekCanvas.Children.Add(satBorder);

                double parentCenterX = cx + p.DynamicRadius * Math.Cos(p.AngleRadians);
                double parentCenterY = cy + p.DynamicRadius * Math.Sin(p.AngleRadians);
                RadialMotionSystem.AnimateItemBloomFromCenter(
                    satBorder,
                    k,
                    (sx + satSize / 2.0) - parentCenterX,
                    (sy + satSize / 2.0) - parentCenterY,
                    _viewModel.ActiveTheme.ReduceMotion);
            }
        }

        private void UpdateAimIndicator(int focusedIndex, Color activeColor)
        {
            if (AimSectorWedge == null || AimRayLine == null || AimHubArc == null) return;

            if (focusedIndex < 0 || focusedIndex >= _currentPlacements.Count)
            {
                AimSectorWedge.Opacity = 0;
                AimRayLine.Opacity = 0;
                AimHubArc.Opacity = 0;
                if (CentralHubBorder != null)
                {
                    CentralHubBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(76, 255, 255, 255));
                }
                return;
            }

            var p = _currentPlacements[focusedIndex];
            double cx = 250.0, cy = 250.0;
            double angle = p.AngleRadians;
            double cosA = Math.Cos(angle), sinA = Math.Sin(angle);
            double hubRadius = 31.0;
            double tileInnerEdge = Math.Max(hubRadius + 10.0, p.DynamicRadius - (p.DynamicTileSize / 2.0) - 4.0);

            AimRayLine.X1 = cx + hubRadius * cosA;
            AimRayLine.Y1 = cy + hubRadius * sinA;
            AimRayLine.X2 = cx + tileInnerEdge * cosA;
            AimRayLine.Y2 = cy + tileInnerEdge * sinA;
            AimRayLine.Stroke = new SolidColorBrush(Color.FromArgb(220, activeColor.R, activeColor.G, activeColor.B));
            AimRayLine.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Color = activeColor,
                Opacity = 0.85
            };
            AimRayLine.Opacity = 1.0;

            double halfSpan = Math.Min(Math.PI / 7.5, Math.PI / Math.Max(4, _currentPlacements.Count));
            double a1 = angle - halfSpan;
            double a2 = angle + halfSpan;

            Point arcStart = new Point(cx + hubRadius * Math.Cos(a1), cy + hubRadius * Math.Sin(a1));
            Point arcEnd = new Point(cx + hubRadius * Math.Cos(a2), cy + hubRadius * Math.Sin(a2));
            var arcFig = new PathFigure { StartPoint = arcStart, IsClosed = false, IsFilled = false };
            arcFig.Segments.Add(new ArcSegment(arcEnd, new Size(hubRadius, hubRadius), 0, false, SweepDirection.Clockwise, true));
            var arcGeo = new PathGeometry();
            arcGeo.Figures.Add(arcFig);
            AimHubArc.Data = arcGeo;
            AimHubArc.Stroke = new SolidColorBrush(activeColor);
            AimHubArc.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12,
                ShadowDepth = 0,
                Color = activeColor,
                Opacity = 0.9
            };
            AimHubArc.Opacity = 1.0;

            double outerWedgeR = p.DynamicRadius;
            Point w1 = new Point(cx + hubRadius * Math.Cos(a1), cy + hubRadius * Math.Sin(a1));
            Point w2 = new Point(cx + outerWedgeR * Math.Cos(a1), cy + outerWedgeR * Math.Sin(a1));
            Point w3 = new Point(cx + outerWedgeR * Math.Cos(a2), cy + outerWedgeR * Math.Sin(a2));
            Point w4 = new Point(cx + hubRadius * Math.Cos(a2), cy + hubRadius * Math.Sin(a2));
            var wedgeFig = new PathFigure { StartPoint = w1, IsClosed = true, IsFilled = true };
            wedgeFig.Segments.Add(new LineSegment(w2, false));
            wedgeFig.Segments.Add(new ArcSegment(w3, new Size(outerWedgeR, outerWedgeR), 0, false, SweepDirection.Clockwise, false));
            wedgeFig.Segments.Add(new LineSegment(w4, false));
            wedgeFig.Segments.Add(new ArcSegment(w1, new Size(hubRadius, hubRadius), 0, false, SweepDirection.Counterclockwise, false));
            var wedgeGeo = new PathGeometry();
            wedgeGeo.Figures.Add(wedgeFig);
            AimSectorWedge.Data = wedgeGeo;
            AimSectorWedge.Fill = new SolidColorBrush(Color.FromArgb(30, activeColor.R, activeColor.G, activeColor.B));
            AimSectorWedge.Opacity = 1.0;

            if (CentralHubBorder != null)
            {
                CentralHubBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(165, activeColor.R, activeColor.G, activeColor.B));
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
