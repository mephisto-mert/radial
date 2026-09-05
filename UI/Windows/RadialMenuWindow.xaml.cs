using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RadialLauncher.Models;

namespace RadialLauncher.UI.Windows
{
    public partial class RadialMenuWindow : Window
    {
        private const int MaxItemsPerPage = 15;

        private readonly Services.ProcessRunner _processRunner = new();
        private readonly Data.DatabaseManager _dbManager = new();
        private readonly Services.WindowSwitcherService _windowSwitcher = new();
        private readonly Stack<(int parentId, string title)> _navStack = new();
        private readonly Dictionary<string, ImageSource> _windowIcons = new();

        private List<LauncherItem> _allItems = new();
        private List<Category> _categories = new();
        private int _currentCategoryIndex = 0;
        private int _currentPageIndex = 0;
        private string _searchQuery = "";
        private bool _isSearchMode = false;

        private List<(Button btn, LauncherItem item)> _visibleButtons = new();

        public RadialMenuWindow()
        {
            InitializeComponent();
            Services.ThemeManager.OnThemeChanged += (t) =>
            {
                Dispatcher.Invoke(() =>
                {
                    ApplyTheme();
                    if (this.IsVisible)
                    {
                        GenerateItems();
                    }
                });
            };
        }

        public void ShowAt(int x, int y)
        {
            try
            {
                var source = PresentationSource.FromVisual(this);
                double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                double logicalX = x / dpiX;
                double logicalY = y / dpiY;

                this.Left = logicalX - (this.Width / 2);
                this.Top = logicalY - (this.Height / 2);

                if (this.Left < 0) this.Left = 0;
                if (this.Top < 0) this.Top = 0;

                // Reset state
                _searchQuery = "";
                _isSearchMode = false;
                _navStack.Clear();
                SearchBorder.Visibility = Visibility.Collapsed;
                SearchText.Text = "";
                HoverInfoText.Text = "";

                // Load data
                _allItems = _dbManager.GetAllItems();
                var allDbCats = _dbManager.GetAllCategories();
                
                // Hide any empty category tabs from radial wheel so user never sees an empty screen
                _categories = allDbCats.Where(c =>
                    c.Name.Contains("Açık Pencereler", StringComparison.OrdinalIgnoreCase) ||
                    (c.Id <= 1 || c.Name.Contains("Kullanılanlar", StringComparison.OrdinalIgnoreCase)) ||
                    _allItems.Any(i => i.CategoryId == c.Id && i.ParentId == 0)
                ).ToList();

                if (_currentCategoryIndex >= _categories.Count) _currentCategoryIndex = 0;

                ApplyTheme();
                GenerateItems();

                this.Opacity = 1;
                this.Show();
                this.Activate();
                this.Focus();
            }
            catch (Exception ex)
            {
                try
                {
                    var logFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RadialLauncher");
                    System.IO.File.WriteAllText(System.IO.Path.Combine(logFolder, "showat_error.log"), ex.ToString());
                }
                catch { }
            }
        }

        private void ApplyTheme()
        {
            var theme = Services.ThemeManager.GetCurrentTheme();

            byte alpha = (byte)(theme.BackgroundOpacity * 255);
            BaseCircle.Fill = new SolidColorBrush(Color.FromArgb(alpha, theme.BackgroundColor.R, theme.BackgroundColor.G, theme.BackgroundColor.B));

            var strokeBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            strokeBrush.GradientStops.Add(new GradientStop(Color.FromArgb(80, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B), 0.0));
            strokeBrush.GradientStops.Add(new GradientStop(Color.FromArgb(25, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B), 0.5));
            strokeBrush.GradientStops.Add(new GradientStop(Color.FromArgb(80, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B), 1.0));
            BaseCircle.Stroke = strokeBrush;

            var glowBrush = new RadialGradientBrush();
            glowBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 0.82));
            glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(45, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B), 1.0));
            GlowEllipse.Fill = glowBrush;

            GlowEllipse.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 36,
                ShadowDepth = 0,
                Color = theme.AccentColor,
                Opacity = 0.6
            };

            CenterButton.Background = new SolidColorBrush(theme.CenterButtonColor);
            CenterText.Foreground = new SolidColorBrush(theme.TextColor);
            HoverInfoText.Foreground = new SolidColorBrush(theme.TextColor);

            CategoryPill.Background = new SolidColorBrush(Color.FromArgb(225, 18, 18, 24));
            CategoryPill.BorderBrush = new SolidColorBrush(Color.FromArgb(80, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B));
        }

        private List<LauncherItem> GetCurrentFilteredItems()
        {
            if (_isSearchMode && !string.IsNullOrEmpty(_searchQuery))
            {
                return _allItems.Where(i => i.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (_navStack.Count > 0)
            {
                int parentId = _navStack.Peek().parentId;
                return _allItems.Where(i => i.ParentId == parentId).ToList();
            }

            var currentCategory = _currentCategoryIndex < _categories.Count ? _categories[_currentCategoryIndex] : null;

            // Check if user selected Open Windows category
            if (currentCategory != null && currentCategory.Name.Contains("Açık Pencereler", StringComparison.OrdinalIgnoreCase))
            {
                var openWins = _windowSwitcher.GetOpenWindows();
                _windowIcons.Clear();
                var winItems = new List<LauncherItem>();
                for (int w = 0; w < openWins.Count; w++)
                {
                    var win = openWins[w];
                    string targetKey = win.Handle.ToString();
                    if (win.Icon != null) _windowIcons[targetKey] = win.Icon;
                    winItems.Add(new LauncherItem
                    {
                        Id = -1000 - w,
                        Name = win.Title,
                        Type = "WINDOW",
                        Target = targetKey,
                        Position = w
                    });
                }
                return winItems;
            }

            if (currentCategory != null && currentCategory.Id > 1 && 
                !currentCategory.Name.Equals("Hepsi", StringComparison.OrdinalIgnoreCase) && 
                !currentCategory.Name.Contains("Kullanılanlar", StringComparison.OrdinalIgnoreCase))
            {
                return _allItems.Where(i => i.CategoryId == currentCategory.Id && i.ParentId == 0)
                                .OrderBy(i => i.IsFavorite ? 0 : 1)
                                .ThenBy(i => i.Position)
                                .ToList();
            }

            // ⭐ En Çok Kullanılanlar: User's top desktop apps, games, websites, and favorites
            var primaryItems = _allItems.Where(i => (i.CategoryId <= 1 || i.IsUserAdded) && i.ParentId == 0)
                                        .OrderBy(i => i.IsFavorite ? 0 : 1)
                                        .ThenBy(i => i.Type == "URL" ? 0 : 1)
                                        .ThenBy(i => i.Position)
                                        .ToList();

            if (primaryItems.Count == 0)
            {
                primaryItems = _allItems.Where(i => i.ParentId == 0).Take(15).ToList();
            }

            return primaryItems;
        }

        private void GenerateCategoryDots(int totalPages)
        {
            CategoryDots.Children.Clear();

            if (_navStack.Count > 0)
            {
                CategoryTitleText.Text = $"📁 {_navStack.Peek().title} (Merkez: ⬅ Geri)";
                return;
            }

            var currentCategory = _currentCategoryIndex < _categories.Count ? _categories[_currentCategoryIndex] : null;
            string categoryName = currentCategory?.Name ?? "Hepsi";

            if (_isSearchMode)
            {
                CategoryTitleText.Text = $"Arama: \"{_searchQuery}\" ({_visibleButtons.Count} sonuç)";
                return;
            }

            CategoryTitleText.Text = totalPages > 1 
                ? $"{categoryName} (Sayfa {_currentPageIndex + 1}/{totalPages})" 
                : categoryName;

            var theme = Services.ThemeManager.GetCurrentTheme();
            CategoryPill.BorderBrush = new SolidColorBrush(Color.FromArgb(80, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B));

            // Dots for categories
            for (int i = 0; i < _categories.Count; i++)
            {
                var cat = _categories[i];
                bool isCatActive = i == _currentCategoryIndex;

                var dot = new Ellipse
                {
                    Width = isCatActive ? 9 : 6,
                    Height = isCatActive ? 9 : 6,
                    Margin = new Thickness(3, 0, 3, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = cat.Name
                };

                dot.Fill = isCatActive 
                    ? new SolidColorBrush(theme.AccentColor) 
                    : new SolidColorBrush(Color.FromArgb(120, 120, 125, 135));

                int catIndex = i;
                dot.MouseLeftButtonDown += (s, e) =>
                {
                    _currentCategoryIndex = catIndex;
                    _currentPageIndex = 0;
                    GenerateItems();
                };

                CategoryDots.Children.Add(dot);
            }
        }

        private void GenerateItems()
        {
            var toRemove = new List<UIElement>();
            foreach (UIElement child in ItemsCanvas.Children)
            {
                if (child != CenterButton) toRemove.Add(child);
            }
            foreach (var c in toRemove) ItemsCanvas.Children.Remove(c);
            _visibleButtons.Clear();

            // Center button update (Back button or Close button)
            if (_navStack.Count > 0)
            {
                CenterText.Text = "⬅";
                CenterButton.ToolTip = "Üst Menüye Dön (Geri)";
            }
            else
            {
                CenterText.Text = "✕";
                CenterButton.ToolTip = "Sol Tık: Kapat | Sağ Tık: Ayarlar";
            }

            var allCategoryItems = GetCurrentFilteredItems();
            int totalPages = Math.Max(1, (int)Math.Ceiling(allCategoryItems.Count / (double)MaxItemsPerPage));
            if (_currentPageIndex >= totalPages) _currentPageIndex = totalPages - 1;
            if (_currentPageIndex < 0) _currentPageIndex = 0;

            var pageItems = allCategoryItems.Skip(_currentPageIndex * MaxItemsPerPage).Take(MaxItemsPerPage).ToList();

            GenerateCategoryDots(totalPages);

            int count = pageItems.Count;
            if (count == 0) return;

            var theme = Services.ThemeManager.GetCurrentTheme();
            double centerX = 210;
            double centerY = 210;
            double radius = 146;

            for (int i = 0; i < count; i++)
            {
                var item = pageItems[i];
                double angle = i * (2 * Math.PI / count) - (Math.PI / 2);
                double x = centerX + radius * Math.Cos(angle);
                double y = centerY + radius * Math.Sin(angle);

                bool isMissing = item.Type != "URL"
                    && item.Type != "ACTION"
                    && item.Type != "SUBMENU"
                    && item.Type != "WINDOW"
                    && !item.Target.StartsWith("steam://", StringComparison.OrdinalIgnoreCase)
                    && !item.Target.StartsWith("com.epicgames.", StringComparison.OrdinalIgnoreCase)
                    && !File.Exists(item.Target)
                    && !Directory.Exists(item.Target);

                ImageSource? icon = null;

                if (item.Type == "WINDOW")
                {
                    if (_windowIcons.TryGetValue(item.Target, out var wIcon))
                        icon = wIcon;
                }
                else
                {
                    // 1. Sleek vector dark brand icon first
                    icon = Services.IconExtractor.GetBrandIcon(item.Name, item.Target);

                    // 2. Custom icon path
                    if (icon == null && !string.IsNullOrEmpty(item.IconPath) && File.Exists(item.IconPath))
                    {
                        icon = Services.IconExtractor.GetIconForFile(item.IconPath);
                    }

                    // 3. Web favicon or executable icon
                    if (icon == null)
                    {
                        if (item.Type == "URL")
                        {
                            icon = Services.IconExtractor.GetFaviconForUrl(item.Target);
                        }
                        else if (item.Type == "EXE" || item.Type == "FILE" || item.Target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                        {
                            icon = Services.IconExtractor.GetIconForFile(item.Target);
                        }
                    }
                }

                int circleSize = 52;
                int iconSize = 32;

                // Dark-glass circular border
                var iconBorder = new Border
                {
                    Width = circleSize,
                    Height = circleSize,
                    CornerRadius = new CornerRadius(circleSize / 2.0),
                    BorderThickness = new Thickness(1.4),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                    SnapsToDevicePixels = true,
                    ClipToBounds = true
                };

                var bgGrad = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };
                bgGrad.GradientStops.Add(new GradientStop(Color.FromArgb(235, theme.IconBackgroundColor.R, theme.IconBackgroundColor.G, theme.IconBackgroundColor.B), 0.0));
                bgGrad.GradientStops.Add(new GradientStop(Color.FromArgb(250, (byte)Math.Max(0, theme.IconBackgroundColor.R - 15), (byte)Math.Max(0, theme.IconBackgroundColor.G - 15), (byte)Math.Max(0, theme.IconBackgroundColor.B - 15)), 1.0));
                iconBorder.Background = bgGrad;

                var dropShadow = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 14,
                    ShadowDepth = 2,
                    Opacity = 0.5,
                    Color = Colors.Black
                };
                iconBorder.Effect = dropShadow;

                var iconContainer = new Grid
                {
                    Width = circleSize,
                    Height = circleSize
                };
                iconBorder.Child = iconContainer;

                if (icon != null)
                {
                    var img = new Image
                    {
                        Source = icon,
                        Width = iconSize,
                        Height = iconSize,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Opacity = isMissing ? 0.3 : 1.0
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    iconContainer.Children.Add(img);
                }
                else if (item.Type == "ACTION")
                {
                    string symbol = Services.SystemActionService.GetIconForAction(item.Target);
                    iconContainer.Children.Add(new TextBlock
                    {
                        Text = symbol,
                        FontSize = 20,
                        Foreground = new SolidColorBrush(theme.AccentColor),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }
                else if (item.Type == "SUBMENU")
                {
                    iconContainer.Children.Add(new TextBlock
                    {
                        Text = "📁",
                        FontSize = 20,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }
                else if (item.Type == "WINDOW")
                {
                    iconContainer.Children.Add(new TextBlock
                    {
                        Text = "🪟",
                        FontSize = 18,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }
                else
                {
                    var monogram = Services.IconExtractor.CreateMonogramIcon(item.Name, theme.AccentColor);
                    var img = new Image
                    {
                        Source = monogram,
                        Width = iconSize,
                        Height = iconSize,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    iconContainer.Children.Add(img);
                }

                // Wrapper for icon + favorite badge
                var iconWrapper = new Grid
                {
                    Width = circleSize + 2,
                    Height = circleSize + 2,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                iconBorder.HorizontalAlignment = HorizontalAlignment.Center;
                iconBorder.VerticalAlignment = VerticalAlignment.Center;
                iconWrapper.Children.Add(iconBorder);

                // Favorite star indicator
                if (item.IsFavorite && item.Type != "WINDOW")
                {
                    var star = new TextBlock
                    {
                        Text = "⭐",
                        FontSize = 11,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, -3, -3, 0)
                    };
                    iconWrapper.Children.Add(star);
                }

                // Compact name label below icon
                var nameLabel = new TextBlock
                {
                    Text = item.Name,
                    Foreground = new SolidColorBrush(isMissing ? Colors.Red : theme.TextColor),
                    FontSize = 9.5,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 66,
                    MaxHeight = 26,
                    Margin = new Thickness(0, 3, 0, 0)
                };
                nameLabel.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 4,
                    ShadowDepth = 1,
                    Opacity = 0.8,
                    Color = Colors.Black
                };

                var stack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Width = 66
                };
                stack.Children.Add(iconWrapper);
                stack.Children.Add(nameLabel);

                string tooltipText = item.Type switch
                {
                    "WINDOW" => $"{item.Name}\n[Sol Tık: Pencereye Geç | Orta Tık: Pencereyi Kapat]",
                    "SUBMENU" => $"📁 {item.Name} (Alt Menüye Gir)",
                    "ACTION" => $"⚡ {item.Name}",
                    _ => item.Name
                };

                var btn = new Button
                {
                    Content = stack,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = tooltipText
                };
                btn.Style = CreateTransparentButtonStyle();
                btn.RenderTransformOrigin = new Point(0.5, 0.4);

                var scaleTransform = new ScaleTransform(0, 0);
                btn.RenderTransform = scaleTransform;
                btn.Opacity = 0;

                // Hover animation
                var capturedBorder = iconBorder;
                var capturedShadow = dropShadow;
                string itemName = item.Name;
                btn.MouseEnter += (s, e) =>
                {
                    HoverInfoText.Text = itemName;
                    var grow = new DoubleAnimation(1.22, TimeSpan.FromMilliseconds(130))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, grow);

                    capturedBorder.BorderBrush = new SolidColorBrush(theme.AccentColor);
                    capturedShadow.Color = theme.AccentColor;
                    capturedShadow.BlurRadius = 22;
                    capturedShadow.Opacity = 0.8;
                };

                btn.MouseLeave += (s, e) =>
                {
                    if (HoverInfoText.Text == itemName) HoverInfoText.Text = "";
                    var shrink = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);

                    capturedBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                    capturedShadow.Color = Colors.Black;
                    capturedShadow.BlurRadius = 14;
                    capturedShadow.Opacity = 0.5;
                };

                Canvas.SetLeft(btn, x - 33);
                Canvas.SetTop(btn, y - 26);

                // Left click trigger
                btn.Click += (s, e) =>
                {
                    if (item.Type == "SUBMENU")
                    {
                        _navStack.Push((item.Id, item.Name));
                        _currentPageIndex = 0;
                        GenerateItems();
                        return;
                    }
                    if (item.Type == "WINDOW")
                    {
                        if (long.TryParse(item.Target, out long hVal))
                        {
                            _windowSwitcher.SwitchToWindow(new IntPtr(hVal));
                            this.Hide();
                        }
                        return;
                    }
                    if (item.Type == "ACTION")
                    {
                        Services.SystemActionService.ExecuteAction(item.Target);
                        if (!item.Target.StartsWith("VOLUME_", StringComparison.OrdinalIgnoreCase))
                        {
                            this.Hide();
                        }
                        return;
                    }

                    _processRunner.Launch(item);
                    this.Hide();
                };

                // Middle click: Close window if item is a WINDOW
                btn.MouseDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Middle && item.Type == "WINDOW")
                    {
                        if (long.TryParse(item.Target, out long hVal))
                        {
                            _windowSwitcher.CloseWindow(new IntPtr(hVal));
                            System.Threading.Tasks.Task.Delay(150).ContinueWith(_ =>
                            {
                                Dispatcher.Invoke(() => GenerateItems());
                            });
                        }
                        e.Handled = true;
                    }
                };

                // Right click: toggle favorite (only for stored database items)
                if (item.Id > 0)
                {
                    btn.MouseRightButtonUp += (s, e) =>
                    {
                        _dbManager.ToggleFavorite(item.Id);
                        _allItems = _dbManager.GetAllItems();
                        GenerateItems();
                        e.Handled = true;
                    };
                }

                ItemsCanvas.Children.Add(btn);
                _visibleButtons.Add((btn, item));

                // Entry animation
                var sb = new Storyboard();
                var scaleXAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
                {
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(scaleXAnim, btn);
                Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("RenderTransform.ScaleX"));

                var scaleYAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
                {
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(scaleYAnim, btn);
                Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("RenderTransform.ScaleY"));

                var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                Storyboard.SetTarget(fadeAnim, btn);
                Storyboard.SetTargetProperty(fadeAnim, new PropertyPath("Opacity"));

                sb.Children.Add(scaleXAnim);
                sb.Children.Add(scaleYAnim);
                sb.Children.Add(fadeAnim);
                sb.BeginTime = TimeSpan.FromMilliseconds(i * 22);
                sb.Begin();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_isSearchMode)
                {
                    _isSearchMode = false;
                    _searchQuery = "";
                    SearchBorder.Visibility = Visibility.Collapsed;
                    SearchText.Text = "";
                    GenerateItems();
                }
                else if (_navStack.Count > 0)
                {
                    _navStack.Pop();
                    _currentPageIndex = 0;
                    GenerateItems();
                }
                else
                {
                    this.Hide();
                }
                e.Handled = true;
                return;
            }

            // Left / Right arrow navigation: pages & categories
            if (e.Key == Key.Left)
            {
                NavigatePrevious();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Right)
            {
                NavigateNext();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Back && _isSearchMode)
            {
                if (_searchQuery.Length > 0)
                {
                    _searchQuery = _searchQuery.Substring(0, _searchQuery.Length - 1);
                    SearchText.Text = _searchQuery;
                    if (_searchQuery.Length == 0)
                    {
                        _isSearchMode = false;
                        SearchBorder.Visibility = Visibility.Collapsed;
                    }
                    GenerateItems();
                }
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Back && _navStack.Count > 0)
            {
                _navStack.Pop();
                _currentPageIndex = 0;
                GenerateItems();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter && _visibleButtons.Count > 0)
            {
                var item = _visibleButtons[0].item;
                if (item.Type == "SUBMENU")
                {
                    _navStack.Push((item.Id, item.Name));
                    _currentPageIndex = 0;
                    GenerateItems();
                }
                else if (item.Type == "WINDOW")
                {
                    if (long.TryParse(item.Target, out long hVal))
                    {
                        _windowSwitcher.SwitchToWindow(new IntPtr(hVal));
                        this.Hide();
                    }
                }
                else if (item.Type == "ACTION")
                {
                    Services.SystemActionService.ExecuteAction(item.Target);
                    this.Hide();
                }
                else
                {
                    _processRunner.Launch(item);
                    this.Hide();
                }
                e.Handled = true;
                return;
            }
        }

        private void Window_TextInput(object sender, TextCompositionEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Text) && char.IsLetterOrDigit(e.Text[0]))
            {
                if (!_isSearchMode)
                {
                    _isSearchMode = true;
                    _searchQuery = "";
                    SearchBorder.Visibility = Visibility.Visible;
                }
                _searchQuery += e.Text;
                SearchText.Text = _searchQuery;
                GenerateItems();
                e.Handled = true;
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.Delta > 0)
            {
                NavigatePrevious();
            }
            else
            {
                NavigateNext();
            }
        }

        private void NavigateNext()
        {
            _isSearchMode = false;
            _searchQuery = "";
            SearchBorder.Visibility = Visibility.Collapsed;

            var items = GetCurrentFilteredItems();
            int totalPages = Math.Max(1, (int)Math.Ceiling(items.Count / (double)MaxItemsPerPage));

            if (_currentPageIndex < totalPages - 1)
            {
                _currentPageIndex++;
            }
            else
            {
                _currentCategoryIndex = (_currentCategoryIndex + 1) % _categories.Count;
                _currentPageIndex = 0;
            }

            GenerateItems();
        }

        private void NavigatePrevious()
        {
            _isSearchMode = false;
            _searchQuery = "";
            SearchBorder.Visibility = Visibility.Collapsed;

            if (_currentPageIndex > 0)
            {
                _currentPageIndex--;
            }
            else
            {
                _currentCategoryIndex = (_currentCategoryIndex - 1 + _categories.Count) % _categories.Count;
                var items = GetCurrentFilteredItems();
                int totalPages = Math.Max(1, (int)Math.Ceiling(items.Count / (double)MaxItemsPerPage));
                _currentPageIndex = Math.Max(0, totalPages - 1);
            }

            GenerateItems();
        }

        private static Style CreateTransparentButtonStyle()
        {
            var style = new Style(typeof(Button));
            var template = new ControlTemplate(typeof(Button));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            template.VisualTree = presenter;
            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            style.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            return style;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void CenterButton_Click(object sender, RoutedEventArgs e)
        {
            if (_navStack.Count > 0)
            {
                _navStack.Pop();
                _currentPageIndex = 0;
                GenerateItems();
            }
            else
            {
                this.Hide();
            }
        }

        private void CenterButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.Hide();
            if (Application.Current is App app)
            {
                app.OpenSettings();
            }
            else
            {
                var mgmt = new ManagementWindow();
                mgmt.Show();
            }
            e.Handled = true;
        }
    }
}
