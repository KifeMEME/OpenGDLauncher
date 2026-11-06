using Launcher.Core;
using Launcher.Themes;
using Launcher.Workshop;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using Polygon = System.Windows.Shapes.Polygon;

namespace Launcher.UI;

public partial class MainWindow : Window
{
    private static readonly FontFamily BundledFont = new(new Uri("pack://application:,,,/"), "/OpenGDLauncher;component/Assets/Fonts/#Lemon/Milk(RUS BY LYAJKA)");
    private readonly AppSettings settings = SettingsStore.Load();
    private readonly ThemeService themes;
    private LocalWorkshop workshop = null!;
    private readonly GeodeCatalogService geodeCatalog = new();
    private int catalogRequest;
    private Style actionStyle = null!;
    private Brush cardBrush = Brushes.Transparent;
    private Brush contentCardBrush = Brushes.Transparent;
    private Brush modRowBrush = Brushes.Transparent;
    private Brush mutedBrush = Brushes.Gray;
    private Brush accentBrush = Brushes.MediumPurple;
    private Border? steamWarning;
    private System.Media.SoundPlayer? heroSound;
    private Action? refreshPage;
    private bool startupAnimated;
    private bool Ru => settings.Language != "en";
    private string T(string ru, string en) => Ru ? ru : en;
    public MainWindow() : this(null) { }
    public MainWindow(ThemeService? themeService)
    {
        themes = themeService ?? new ThemeService(Path.Combine(SettingsStore.DirectoryPath, "Themes"));
        InitializeComponent();
        WorkshopNav.Visibility = Visibility.Collapsed;
        workshop = new LocalWorkshop(themes);
        Closed += (_, _) => heroSound?.Dispose();
        if (!GameService.IsValid(settings.GameDirectory)) settings.GameDirectory = GameService.Detect() ?? "";
        if (string.IsNullOrWhiteSpace(settings.SelectedVersion)) settings.SelectedVersion = GameVersionCatalog.CurrentSteamVersion() ?? "";
        ApplyTheme(); Localize(); ShowHome();
    }
    private void Localize()
    {
        HomeNav.Content = T("Главная", "Home"); GameNav.Content = "Geometry Dash"; GeodeNav.Content = "Geode";
        WorkshopNav.Content = T("Мастерская", "Workshop"); ThemesNav.Content = T("Мои темы", "My themes");
        SettingsNav.Content = T("Настройки", "Settings"); AboutNav.Content = T("Описание", "Description");
        LanguageButton.ToolTip = T("Switch to English", "Переключить на русский");
        System.Windows.Automation.AutomationProperties.SetName(LanguageButton, T("Switch to English", "Переключить на русский"));
        UpdateLanguageFlag();
        UpdateSteamStatus();
    }
    private void ApplyTheme()
    {
        ThemeManifest theme;
        try { theme = themes.Load(settings.Theme); } catch { settings.Theme = "Default"; theme = themes.Load("Default"); }
        var standard = themes.Load("Default");
        var bg = (Color)ColorConverter.ConvertFromString(theme.BackgroundColor);
        var panel = (Color)ColorConverter.ConvertFromString(theme.PanelColor);
        var text = (Color)ColorConverter.ConvertFromString(theme.TextColor);
        var button = (Color)ColorConverter.ConvertFromString(theme.ButtonColor);
        accentBrush = new SolidColorBrush(button);
        Resources["ThemePanelBrush"] = new SolidColorBrush(panel);
        Resources["ThemeTextBrush"] = new SolidColorBrush(text);
        Resources["ThemeAccentBrush"] = accentBrush;
        Brush Asset(string? custom, string? fallback, Color baseColor, double opacity = 1) => ThemeImage(custom, fallback, baseColor, opacity);
        Shell.Background = Asset(theme.BackgroundImage, standard.BackgroundImage, bg);
        Sidebar.Background = Asset(theme.SidebarImage, standard.SidebarImage, panel, theme.PanelOpacity);
        HeaderPanel.Background = Asset(theme.HeaderImage, standard.HeaderImage, panel, theme.PanelOpacity);
        MainPanel.Background = Asset(theme.PanelImage, standard.PanelImage, panel, theme.PanelOpacity);
        cardBrush = Asset(theme.CardImage, standard.CardImage, panel, theme.PanelOpacity);
        contentCardBrush = Asset(theme.ContentCardImage ?? theme.CardImage, standard.ContentCardImage ?? standard.CardImage, panel, theme.PanelOpacity);
        modRowBrush = Asset(theme.ModRowImage, standard.ModRowImage, Color.Multiply(panel, .8f), theme.PanelOpacity);
        Foreground = new SolidColorBrush(text);
        mutedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.MutedTextColor));
        Status.Foreground = mutedBrush;
        Subtitle.Foreground = Status.Foreground;
        HeaderTitle.Foreground = Status.Foreground;
        SteamCard.Background = Asset(theme.SteamCardImage ?? theme.CardImage, standard.SteamCardImage ?? standard.CardImage, panel, theme.PanelOpacity); SteamFlyout.Background = contentCardBrush;
        SteamCard.BorderBrush = accentBrush; SteamFlyout.BorderBrush = accentBrush;
        SteamIndicator.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.SteamIndicatorColor ?? "#43D79B"));
        try { FontFamily = theme.FontFamily == "LemonMilk" ? BundledFont : new FontFamily(theme.FontFamily); }
        catch { FontFamily = BundledFont; }
        var nav = CreateButtonStyle(164, 40, text,
            Asset(theme.NavigationNormalImage, standard.NavigationNormalImage, button),
            Asset(theme.NavigationHoverImage, standard.NavigationHoverImage, Color.Multiply(button, 1.2f)),
            Asset(theme.NavigationPressedImage, standard.NavigationPressedImage, Color.Multiply(button, 0.8f)),
            Asset(theme.NavigationDisabledImage, standard.NavigationDisabledImage, Colors.Gray));
        actionStyle = CreateButtonStyle(220, 46, text,
            Asset(theme.ButtonNormalImage, standard.ButtonNormalImage, button),
            Asset(theme.ButtonHoverImage, standard.ButtonHoverImage, Color.Multiply(button, 1.2f)),
            Asset(theme.ButtonPressedImage, standard.ButtonPressedImage, Color.Multiply(button, 0.8f)),
            Asset(theme.ButtonDisabledImage, standard.ButtonDisabledImage, Colors.Gray));
        foreach (var item in new[] { HomeNav, GameNav, GeodeNav, WorkshopNav, ThemesNav, SettingsNav, AboutNav }) item.Style = nav;
        var headerButton = ButtonFillStyle.Create(28, 25, text, Brushes.Transparent, new SolidColorBrush(Color.Multiply(button, .6f)), accentBrush, Brushes.Gray);
        foreach (var item in new[] { LanguageButton, MinimizeButton, CloseButton }) { item.Style = headerButton; item.Margin = new Thickness(0); item.FontFamily = new FontFamily("Segoe UI"); }
        UpdateLanguageFlag();
        UpdateSteamStatus();
        heroSound?.Dispose(); heroSound = null;
        if (theme.HeroSoundEnabled)
        {
            var soundFile = themes.ResolveSound(settings.Theme, theme.HeroSound, standard.HeroSound) ?? Path.Combine(AppContext.BaseDirectory, "Assets", "awee.wav");
            if (File.Exists(soundFile))
                try { heroSound = new System.Media.SoundPlayer(soundFile); heroSound.Load(); }
                catch (Exception) { heroSound?.Dispose(); heroSound = null; }
        }
    }
    private void UpdateLanguageFlag()
    {
        LanguageFlagImage.Source = null;
        LanguageFlagImage.Visibility = Visibility.Collapsed;
        RussianFlag.Visibility = Ru ? Visibility.Visible : Visibility.Collapsed;
        EnglishFlag.Visibility = Ru ? Visibility.Collapsed : Visibility.Visible;
        try
        {
            var theme = themes.Load(settings.Theme);
            var standard = themes.Load("Default");
            var file = themes.ResolveImage(settings.Theme,
                Ru ? theme.LanguageRuImage : theme.LanguageEnImage,
                Ru ? standard.LanguageRuImage : standard.LanguageEnImage);
            if (file == null) return;
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(file); image.EndInit(); image.Freeze();
            LanguageFlagImage.Source = image;
            LanguageFlagImage.Visibility = Visibility.Visible;
            RussianFlag.Visibility = Visibility.Collapsed;
            EnglishFlag.Visibility = Visibility.Collapsed;
        }
        catch (Exception error) when (error is IOException or FormatException or NotSupportedException)
        {
            // A missing or damaged optional flag falls back to the built-in vector icon.
        }
    }
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (startupAnimated) return;
        startupAnimated = true;
        Opacity = 1;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
        {
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
    }
    private Brush ThemeImage(string? custom, string? fallback, Color baseColor, double opacity = 1)
    {
        var file = themes.ResolveImage(settings.Theme, custom, fallback);
        if (file == null) return new SolidColorBrush(baseColor) { Opacity = opacity };
        try
        {
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(file); image.EndInit(); image.Freeze();
            return new ImageBrush(image) { Stretch = Stretch.UniformToFill, Opacity = opacity };
        }
        catch { return new SolidColorBrush(baseColor) { Opacity = opacity }; }
    }
    private static Style CreateButtonStyle(double width, double height, Color foreground, Brush normal, Brush hover, Brush pressed, Brush disabled) =>
        ButtonFillStyle.Create(width, height, foreground, normal, hover, pressed, disabled);
    private StackPanel Stack() => new() { Margin = new Thickness(6) };
    private void SelectNav(Button selected)
    {
        foreach (var item in new[] { HomeNav, GameNav, GeodeNav, WorkshopNav, ThemesNav, SettingsNav, AboutNav })
        {
            item.FontWeight = item == selected ? FontWeights.Bold : FontWeights.Normal;
            item.BorderThickness = item == selected ? new Thickness(3, 0, 0, 0) : new Thickness(0);
            item.BorderBrush = accentBrush;
        }
    }
    private TextBlock Label(string text) => new() { Text = text, FontSize = 16, Margin = new Thickness(5, 8, 5, 8), TextWrapping = TextWrapping.Wrap };
    private Button Action(string text, Action action) { var b = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Left, Style = actionStyle }; b.Click += (_, _) => Run(action); return b; }
    private void Run(Action action) { try { var before = Status.Text; action(); if (Status.Text == before) Status.Text = T("Готово", "Done"); } catch (Exception ex) { Status.Text = ex.Message; MessageBox.Show(ex.Message, T("Ошибка", "Error"), MessageBoxButton.OK, MessageBoxImage.Error); } }
    private void LaunchGame()
    {
        var steamVersion = GameVersionCatalog.CurrentSteamVersion();
        if (!string.IsNullOrWhiteSpace(settings.SelectedVersion) && DepotDownloaderService.IsDownloaded(settings.SelectedVersion))
        {
            var version = settings.SelectedVersion;
            var process = GameService.LaunchDownloadedVersion(version);
            Status.Text = T($"Запускается версия {version} из папки {DepotDownloaderService.VersionDirectory(version)}", $"Starting version {version} from {DepotDownloaderService.VersionDirectory(version)}");
            _ = ObserveDownloadedGameAsync(process, version);
            return;
        }
        if (!string.IsNullOrWhiteSpace(settings.SelectedVersion) && settings.SelectedVersion != steamVersion)
        {
            Status.Text = T("Выбранная версия пока недоступна для запуска через Steam.", "The selected version cannot be launched through Steam yet.");
            MessageBox.Show(T("Выбрана версия " + settings.SelectedVersion + ". Скачайте её в каталоге версий через DepotDownloader или выберите текущую версию Steam" + (steamVersion is null ? "." : " " + steamVersion + "."),
                "Version " + settings.SelectedVersion + " is selected. Download it through DepotDownloader in the version catalog or select the current Steam version" + (steamVersion is null ? "." : " " + steamVersion + ".")),
                T("Версия недоступна", "Version unavailable"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var hasInstallRecord = GameService.HasSteamInstallRecord(settings.GameDirectory);
        // A missing local manifest does not mean the signed-in Steam account lacks the game.
        // Steam is the authority for launch access; always hand the request to its client.
        GameService.LaunchThroughSteam();
        if (!hasInstallRecord)
        {
            ShowHome();
            if (steamWarning != null) steamWarning.Visibility = Visibility.Visible;
            Status.Text = T("Запрос отправлен в Steam. Локальная запись установки не найдена; право запуска проверит Steam.", "Launch request sent to Steam. No local install record was found; Steam will check access.");
        }
    }
    private async Task ObserveDownloadedGameAsync(System.Diagnostics.Process process, string version)
    {
        try
        {
            await Task.Delay(3000);
            if (!process.HasExited) return;
            var code = process.ExitCode;
            Status.Text = T($"Версия {version} завершилась сразу (код {code}). Проверьте совместимость этой сборки с Windows и Steam.",
                $"Version {version} exited immediately (code {code}). Check this build's compatibility with Windows and Steam.");
        }
        catch (InvalidOperationException) { }
        finally { process.Dispose(); }
    }
    private void SetPage(string heading, UIElement content, Button? navigation = null) { Heading.Text = heading; Subtitle.Text = T("Управление игрой и оформлением", "Manage game and appearance"); Page.Content = new Border { Background = contentCardBrush, CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Child = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } }; Status.Text = ""; if (navigation != null) SelectNav(navigation); }
    private void ShowHome()
    {
        refreshPage = ShowHome;
        Heading.Text = "Geometry Dash";
        Subtitle.Text = T("Игра и моды — в одном месте.", "Your game and mods in one place.");
        SelectNav(HomeNav);
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(68) });
        var card = new Border { Background = cardBrush, BorderBrush = accentBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(13), Height = 360, VerticalAlignment = VerticalAlignment.Top };
        var contents = new Grid(); contents.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }); contents.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        var displayFont = FontFamily;
        var activeTheme = themes.Load(settings.Theme); var defaultTheme = themes.Load("Default");
        var ribbon = new Canvas { Width = 374, Height = 52, HorizontalAlignment = HorizontalAlignment.Left };
        ribbon.Children.Add(new Polygon { Points = new PointCollection { new(0, 0), new(374, 0), new(374, 36), new(322, 52), new(0, 52) }, Fill = ThemeImage(activeTheme.RibbonImage, defaultTheme.RibbonImage, (Color)ColorConverter.ConvertFromString(activeTheme.RibbonColor ?? activeTheme.ButtonColor)) });
        ribbon.Children.Add(new TextBlock { Text = T("OPENGDL ЭТО -", "OPENGDL IS -"), FontFamily = displayFont, FontSize = 37, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(activeTheme.RibbonTextColor ?? activeTheme.TextColor)), Margin = new Thickness(8, 1, 0, 0) });
        info.Children.Add(ribbon);
        info.Children.Add(new TextBlock { Text = T("ОТКРЫТЫЙ\nGEOMETRY DASH LAUNCHER.", "AN OPEN SOURCE\nGEOMETRY DASH LAUNCHER."), FontFamily = displayFont, FontSize = 20, FontWeight = FontWeights.Bold, LineHeight = 25, Margin = new Thickness(8, 11, 0, 0) });
        info.Children.Add(new TextBlock { Text = T("БЕЗ ПОКУПОК, БЕЗ\nЛИШНЕГО МУСОРА,\nЗАПУСКАЕШЬ И ИГРАЕШЬ.", "NO PAID FEATURES,\nNO CLUTTER,\nJUST LAUNCH AND PLAY."), FontFamily = displayFont, FontSize = 20, FontWeight = FontWeights.Bold, LineHeight = 25, Margin = new Thickness(8, 13, 0, 0) });
        info.Children.Add(new TextBlock { Text = T("ВЫБРАННАЯ ВЕРСИЯ GD", "SELECTED GD VERSION"), FontFamily = displayFont, FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(8, 15, 0, 0) });
        var selectedGameVersion = settings.SelectedVersion.Length > 0 ? settings.SelectedVersion : "—";
        info.Children.Add(new TextBlock { Text = selectedGameVersion, FontFamily = displayFont, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(8, 8, 0, 0) });
        contents.Children.Add(info);
        var cube = new Canvas { Width = 240, Height = 240, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var heroFile = themes.ResolveImage(settings.Theme, activeTheme.HeroImage, defaultTheme.HeroImage);
        BitmapImage? heroBitmap = null;
        foreach (var file in new[] { heroFile, themes.ImagePath("Default", defaultTheme.HeroImage) }.Where(x => x != null).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try { heroBitmap = new BitmapImage(); heroBitmap.BeginInit(); heroBitmap.CacheOption = BitmapCacheOption.OnLoad; heroBitmap.UriSource = new Uri(file!); heroBitmap.EndInit(); heroBitmap.Freeze(); break; }
            catch { heroBitmap = null; }
        }
        if (heroBitmap != null)
        {
            var hero = new Image { Source = heroBitmap, Width = 240, Height = 240, Stretch = Stretch.Uniform, Cursor = System.Windows.Input.Cursors.Hand, RenderTransformOrigin = new Point(.5, .5) };
            hero.RenderTransform = new ScaleTransform(1, 1);
            hero.MouseLeftButtonDown += (_, e) =>
            {
                AnimateHero(hero);
                try { heroSound?.Play(); } catch (InvalidOperationException) { }
                e.Handled = true;
            };
            cube.Children.Add(hero);
        }
        Grid.SetColumn(cube, 1); contents.Children.Add(cube); card.Child = contents; layout.Children.Add(card);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(Action(T("Выбрать версию", "Choose version"), ShowVersionPicker));
        actions.Children.Add(Action(T("▶  Играть", "▶  Play"), LaunchGame));
        Grid.SetRow(actions, 1); layout.Children.Add(actions);
        var warningRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        warningRow.Children.Add(new TextBlock { Text = T("Нет подтверждённой установки Steam. Если игры нет в библиотеке, купите её.", "No confirmed Steam installation. Buy the game if it is not in your library."), Width = 360, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
        var steamAction = Action(T("Открыть Steam", "Open Steam"), GameService.LaunchThroughSteam); steamAction.Width = 165; warningRow.Children.Add(steamAction);
        var storeAction = Action(T("Купить игру", "Buy game"), GameService.OpenSteamStore); storeAction.Width = 155; warningRow.Children.Add(storeAction);
        steamWarning = new Border { Background = contentCardBrush, BorderBrush = accentBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(7), Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Bottom, Child = warningRow };
        layout.Children.Add(steamWarning);
        Page.Content = layout; Status.Text = "";
    }
    private static void AnimateHero(Image hero)
    {
        if (hero.RenderTransform is not ScaleTransform scale) return;
        static DoubleAnimation Squash(double target) => new()
        {
            From = 1,
            To = target,
            Duration = new Duration(TimeSpan.FromMilliseconds(105)),
            AutoReverse = true,
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, Squash(1.07));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, Squash(.88));
    }
    private void ShowVersionPicker()
    {
        var picker = new VersionPickerWindow(GameVersionCatalog.GetEntries(), settings.SelectedVersion, Ru, FontFamily, themes, settings.Theme) { Owner = this };
        if (picker.ShowDialog() != true) return;
        settings.SelectedVersion = picker.SelectedVersion;
        SettingsStore.Save(settings);
        ShowHome();
    }
    private void ChooseGame()
    {
        var dialog = new OpenFolderDialog { Title = T("Папка Geometry Dash", "Geometry Dash folder") };
        if (dialog.ShowDialog() == true) { if (!GameService.IsValid(dialog.FolderName)) throw new InvalidDataException("GeometryDash.exe not found in this folder."); settings.GameDirectory = dialog.FolderName; SettingsStore.Save(settings); ShowHome(); }
    }
    private void ShowGame()
    {
        refreshPage = ShowGame;
        var p = Stack(); p.Children.Add(Label(T("Путь: ", "Path: ") + (settings.GameDirectory.Length > 0 ? settings.GameDirectory : "—")));
        p.Children.Add(Label(T("Статус: ", "Status: ") + (GameService.IsValid(settings.GameDirectory) ? T("установлена", "installed") : T("не найдена", "not found"))));
        p.Children.Add(Action(T("Найти автоматически", "Detect automatically"), () => { settings.GameDirectory = GameService.Detect() ?? ""; SettingsStore.Save(settings); ShowGame(); }));
        p.Children.Add(Action(T("Выбрать папку", "Choose folder"), ChooseGame)); p.Children.Add(Action(T("Запустить", "Launch"), LaunchGame));
        p.Children.Add(Action(T("Установить через Steam", "Install with Steam"), GameService.OpenSteamInstall));
        p.Children.Add(Action(T("Открыть Steam для обновления", "Open Steam for updates"), GameService.OpenSteamUpdate)); SetPage("Geometry Dash", p, GameNav);
    }
    private void ShowGeode()
    {
        refreshPage = ShowGeode;
        var p = Stack();
        var source = GeodeModScanner.ChooseModsDirectory(settings.GameDirectory, settings.GeodeModsDirectory);
        var mods = source == null ? Array.Empty<GeodeMod>() : GeodeModScanner.ScanModsDirectory(source);
        p.Children.Add(Label(T($"Установлено модов: {mods.Count}", $"Installed mods: {mods.Count}")));
        p.Children.Add(new TextBlock { Text = T("Папка модов (сюда также будут скачиваться новые):", "Mods folder (new mods will be downloaded here):"), FontSize = 12, Opacity = .7, Margin = new Thickness(5, 0, 5, 3) });
        var folders = GeodeModScanner.FindModsDirectories(settings.GameDirectory);
        if (folders.Count > 1)
        {
            var selector = new ComboBox { ItemsSource = folders, SelectedItem = source, Width = 650, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(5, 0, 5, 8) };
            selector.SelectionChanged += (_, _) =>
            {
                if (selector.SelectedItem is not string selected || string.Equals(selected, settings.GeodeModsDirectory, StringComparison.OrdinalIgnoreCase)) return;
                settings.GeodeModsDirectory = selected; SettingsStore.Save(settings); ShowGeode();
            };
            p.Children.Add(selector);
        }
        else p.Children.Add(new TextBlock { Text = source ?? T("Папка Geode не найдена.", "Geode mod folder not found."), FontSize = 11, Opacity = .65, Margin = new Thickness(5, 0, 5, 8), TextWrapping = TextWrapping.Wrap });
        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        controls.Children.Add(Action(T("Обновить список", "Refresh list"), ShowGeode));
        controls.Children.Add(Action(T("Моды", "Mods"), () => ShowGeodeCatalog()));
        controls.Children.Add(Action(T("Выбрать папку модов", "Choose mods folder"), ChooseModsDirectory));
        p.Children.Add(controls);
        if (mods.Count == 0) p.Children.Add(Label(T("В этой папке пока нет установленных модов Geode.", "No installed Geode mods in this folder.")));
        foreach (var mod in mods)
        {
            var row = new Grid { Margin = new Thickness(4, 5, 4, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            if (mod.Icon != null)
            {
                try
                {
                    var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = new MemoryStream(mod.Icon); image.EndInit(); image.Freeze();
                    row.Children.Add(new Image { Source = image, Width = 48, Height = 48, Stretch = Stretch.Uniform });
                }
                catch { row.Children.Add(new TextBlock { Text = "◆", FontSize = 30, Foreground = accentBrush, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }); }
            }
            else row.Children.Add(new TextBlock { Text = "◆", FontSize = 30, Foreground = accentBrush, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
            var details = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            details.Children.Add(new TextBlock { Text = mod.Name, FontSize = 16, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            details.Children.Add(new TextBlock { Text = string.Join(" · ", new[] { mod.Developer, mod.Version, mod.Id }.Where(x => !string.IsNullOrWhiteSpace(x))), FontSize = 11, Opacity = .65, TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(details, 1); row.Children.Add(details);
            if (!mod.Enabled)
            {
                var state = new TextBlock { Text = T("Отключён", "Disabled"), Foreground = mutedBrush, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(state, 2); row.Children.Add(state);
            }
            p.Children.Add(new Border { Background = modRowBrush, BorderBrush = accentBrush, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(6), Child = row });
        }
        SetPage("Geode", p, GeodeNav);
    }
    private void ChooseModsDirectory()
    {
        var dialog = new OpenFolderDialog { Title = T("Папка geode\\mods", "geode\\mods folder") };
        if (dialog.ShowDialog() != true) return;
        var folder = new DirectoryInfo(dialog.FolderName);
        if (!folder.Name.Equals("mods", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(folder.Parent?.Name, "geode", StringComparison.OrdinalIgnoreCase) ||
            !GameService.IsValid(folder.Parent?.Parent?.FullName))
            throw new InvalidDataException(T("Выберите папку geode\\mods внутри установленной Geometry Dash.", "Select geode\\mods inside an installed Geometry Dash folder."));
        settings.GeodeModsDirectory = folder.FullName;
        SettingsStore.Save(settings);
        ShowGeode();
    }
    private void ShowGeodeCatalog(int page = 1, string query = "", bool compatibleOnly = true)
    {
        var request = ++catalogRequest;
        var p = Stack();
        p.Children.Add(Action(T("← Установленные моды", "← Installed mods"), ShowGeode));
        var search = new StackPanel { Orientation = Orientation.Horizontal };
        var input = new TextBox { Text = query, Width = 330, Height = 34, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(5) };
        refreshPage = () => ShowGeodeCatalog(page, input.Text, compatibleOnly);
        input.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) ShowGeodeCatalog(1, input.Text, compatibleOnly); };
        search.Children.Add(input);
        search.Children.Add(Action(T("Поиск", "Search"), () => ShowGeodeCatalog(1, input.Text, compatibleOnly)));
        p.Children.Add(search);
        p.Children.Add(Action(compatibleOnly ? T("Показать все моды", "Show all mods") : T("Только совместимые", "Compatible only"), () => ShowGeodeCatalog(1, input.Text, !compatibleOnly)));
        if (!compatibleOnly) p.Children.Add(new TextBlock { Text = T("Несовместимые с выбранной игрой моды доступны только для просмотра.", "Mods incompatible with the selected game are view-only."), FontSize = 11, Opacity = .7, Margin = new Thickness(5, 0, 5, 4) });
        var source = GeodeModScanner.ChooseModsDirectory(settings.GameDirectory, settings.GeodeModsDirectory);
        var runtime = GeodeCatalogService.DetectRuntime(source);
        p.Children.Add(new TextBlock { Text = T("Папка загрузки: ", "Download folder: ") + (source ?? T("не выбрана", "not selected")), FontSize = 11, Opacity = .7, Margin = new Thickness(5, 4, 5, 4), TextWrapping = TextWrapping.Wrap });
        p.Children.Add(new TextBlock { Text = T("Версия игры: ", "Game version: ") + (runtime.GameVersion ?? T("не определена", "unknown")) + "; Geode: " + (runtime.GeodeVersion ?? T("не определён", "unknown")), FontSize = 11, Opacity = .7, Margin = new Thickness(5, 0, 5, 4) });
        if (source != null) p.Children.Add(Action(T("Открыть папку модов", "Open mods folder"), () => GameService.OpenFolder(source)));
        var results = new StackPanel();
        results.Children.Add(Label(T("Загрузка каталога Geode…", "Loading Geode catalog…")));
        p.Children.Add(results);
        SetPage(T("Geode — моды", "Geode — mods"), p, GeodeNav);
        _ = LoadGeodeCatalogAsync(page, query, compatibleOnly, request, results);
    }
    private async Task LoadGeodeCatalogAsync(int page, string query, bool compatibleOnly, int request, StackPanel results)
    {
        try
        {
            var currentSource = GeodeModScanner.ChooseModsDirectory(settings.GameDirectory, settings.GeodeModsDirectory);
            var runtime = GeodeCatalogService.DetectRuntime(currentSource);
            var catalog = await geodeCatalog.SearchAsync(query, page, compatibleOnly ? runtime.GameVersion : null, compatibleOnly ? runtime.GeodeVersion : null);
            if (request != catalogRequest) return;
            results.Children.Clear();
            var source = GeodeModScanner.ChooseModsDirectory(settings.GameDirectory, settings.GeodeModsDirectory);
            var installed = source == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) :
                GeodeModScanner.ScanModsDirectory(source).Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in catalog.Mods)
            {
                var row = new Grid { Margin = new Thickness(4, 5, 4, 5) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
                var icon = new Image { Width = 44, Height = 44, Stretch = Stretch.Uniform };
                row.Children.Add(icon);
                _ = LoadCatalogLogoAsync(mod.Id, icon, request);
                var details = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                details.Children.Add(new TextBlock { Text = mod.Name, FontSize = 15, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
                details.Children.Add(new TextBlock { Text = string.Join(" · ", new[] { mod.Developer, mod.Version, mod.Id }.Where(s => !string.IsNullOrWhiteSpace(s))), FontSize = 11, Opacity = .65, TextTrimming = TextTrimming.CharacterEllipsis });
                Grid.SetColumn(details, 1); row.Children.Add(details);
                var compatible = GeodeCatalogService.IsCompatible(mod, runtime);
                var alreadyInstalled = installed.Contains(mod.Id);
                var install = new Button { Content = alreadyInstalled ? T("Установлен", "Installed") : compatible ? T("Скачать", "Download") : T("Не совместим", "Incompatible"), Style = actionStyle, Width = 130, Height = 38, IsEnabled = source != null && !alreadyInstalled && compatible };
                install.Click += async (_, _) =>
                {
                    install.IsEnabled = false; install.Content = T("Загрузка…", "Downloading…");
                    try
                    {
                        var destination = GeodeModScanner.ChooseModsDirectory(settings.GameDirectory, settings.GeodeModsDirectory) ?? throw new DirectoryNotFoundException(T("Выберите папку модов.", "Choose a mod folder."));
                        var saved = await geodeCatalog.InstallAsync(mod, destination);
                        if (!File.Exists(saved.FilePath) || new FileInfo(saved.FilePath).Length != saved.Bytes) throw new IOException(T("Файл мода не найден после загрузки.", "Mod file was not found after download."));
                        install.Content = T("Установлен", "Installed");
                        install.ToolTip = saved.FilePath;
                        details.Children.Add(new TextBlock { Text = T("Сохранён: ", "Saved: ") + saved.FilePath, FontSize = 10, Opacity = .7, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = saved.FilePath });
                        Status.Text = T("Мод скачан в указанную папку. Перезапустите игру.", "Mod saved to the shown folder. Restart the game.");
                    }
                    catch (Exception ex) { install.Content = T("Скачать", "Download"); install.IsEnabled = true; Status.Text = ex.Message; MessageBox.Show(ex.Message, T("Ошибка загрузки", "Download error")); }
                };
                Grid.SetColumn(install, 2); row.Children.Add(install);
                results.Children.Add(new Border { Background = modRowBrush, BorderBrush = accentBrush, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(6), Child = row });
            }
            if (catalog.Mods.Count == 0) results.Children.Add(Label(T("Моды не найдены.", "No mods found.")));
            var navigation = new StackPanel { Orientation = Orientation.Horizontal };
            if (page > 1) navigation.Children.Add(Action(T("← Назад", "← Previous"), () => ShowGeodeCatalog(page - 1, query, compatibleOnly)));
            navigation.Children.Add(Label(T($"Страница {page}", $"Page {page}")));
            if (catalog.HasMore) navigation.Children.Add(Action(T("Далее →", "Next →"), () => ShowGeodeCatalog(page + 1, query, compatibleOnly)));
            results.Children.Add(navigation);
        }
        catch (Exception ex)
        {
            if (request != catalogRequest) return;
            results.Children.Clear();
            results.Children.Add(Label(T("Не удалось загрузить каталог Geode: ", "Could not load Geode catalog: ") + ex.Message));
            results.Children.Add(Action(T("Повторить", "Retry"), () => ShowGeodeCatalog(page, query, compatibleOnly)));
        }
    }
    private async Task LoadCatalogLogoAsync(string id, Image image, int request)
    {
        try
        {
            var bytes = await geodeCatalog.GetLogoAsync(id);
            if (bytes == null || request != catalogRequest) return;
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(bytes); bitmap.EndInit(); bitmap.Freeze(); image.Source = bitmap;
        }
        catch { /* A missing logo must not hide the catalog entry. */ }
    }
    private void ShowWorkshop()
    {
        refreshPage = ShowWorkshop;
        var p = Stack(); p.Children.Add(Label(T("Локальная мастерская: установите пример темы или импортируйте собственный ZIP.", "Local workshop: install the example theme or import your own ZIP.")));
        var sample = Path.Combine(AppContext.BaseDirectory, "Catalog", "GraphicDemo.zip");
        if (File.Exists(sample)) { p.Children.Add(Label("GraphicDemo — OpenGDLauncher · 2.0")); p.Children.Add(Action(T("Установить пример", "Install example"), () => { workshop.Import(sample); ShowThemes(); })); }
        p.Children.Add(Action(T("Установить тему из ZIP", "Install theme from ZIP"), () => { var d = new OpenFileDialog { Filter = "Theme ZIP|*.zip" }; if (d.ShowDialog() == true) { workshop.Import(d.FileName); ShowThemes(); } }));
        SetPage(T("Мастерская", "Workshop"), p, WorkshopNav);
    }
    private void ShowThemes()
    {
        refreshPage = ShowThemes;
        var p = Stack(); foreach (var name in themes.Names())
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal }; ThemeManifest manifest;
            try { manifest = themes.Load(name); } catch { continue; }
            row.Children.Add(Label($"{manifest.Name} — {manifest.Author}"));
            row.Children.Add(Action(T("Применить", "Apply"), () => { settings.Theme = name; SettingsStore.Save(settings); ApplyTheme(); ShowThemes(); }));
            if (name != "Default") row.Children.Add(Action(T("Удалить", "Delete"), () => { if (settings.Theme == name) { settings.Theme = "Default"; SettingsStore.Save(settings); ApplyTheme(); } themes.Delete(name); ShowThemes(); }));
            p.Children.Add(row);
        }
        SetPage(T("Мои темы", "My themes"), p, ThemesNav);
    }
    private void ShowSettings()
    {
        refreshPage = ShowSettings;
        var p = Stack();
        p.Children.Add(new TextBlock { Text = T("Мастерская тем", "Theme workshop"), FontSize = 21, FontWeight = FontWeights.Bold, Margin = new Thickness(5, 0, 5, 8) });
        p.Children.Add(Label(T("Устанавливайте готовые скины и собственные ZIP-архивы. Применение и удаление — в разделе «Мои темы».", "Install built-in skins and your own ZIP archives. Apply or remove them in My themes.")));
        var sample = Path.Combine(AppContext.BaseDirectory, "Catalog", "GraphicDemo.zip");
        if (File.Exists(sample)) p.Children.Add(Action(T("Установить пример", "Install example"), () => { workshop.Import(sample); ShowThemes(); }));
        p.Children.Add(Action(T("Установить ZIP", "Install ZIP"), () => { var dialog = new OpenFileDialog { Filter = "Theme ZIP|*.zip" }; if (dialog.ShowDialog() == true) { workshop.Import(dialog.FileName); ShowThemes(); } }));
        p.Children.Add(Action(T("Открыть мои темы", "Open my themes"), ShowThemes));
        var designerKit = Path.Combine(AppContext.BaseDirectory, "ThemeKit");
        if (Directory.Exists(designerKit)) p.Children.Add(Action(T("Шаблоны и инструкция", "Templates and guide"), () => GameService.OpenFolder(designerKit)));
        SetPage(T("Настройки", "Settings"), p, SettingsNav);
    }
    private void ShowAbout() { refreshPage = ShowAbout; var p = Stack(); p.Children.Add(Label(T("OpenGDLauncher — бесплатный проект с открытым исходным кодом. Geometry Dash и Geode принадлежат их правообладателям. Для установки игры используется Steam.", "OpenGDLauncher is a free, open source project. Geometry Dash and Geode belong to their respective owners. Steam handles game installation."))); p.Children.Add(Action(T("Поддержать автора", "Support the author"), () => GameService.OpenUrl("https://www.donationalerts.com/r/kifememe"))); SetPage(T("Описание", "Description"), p, AboutNav); }
    private void Language_Click(object sender, RoutedEventArgs e)
    {
        settings.Language = Ru ? "en" : "ru";
        SettingsStore.Save(settings);
        SteamFlyout.Visibility = Visibility.Collapsed;
        Localize();
        refreshPage?.Invoke();
    }
    private void Home_Click(object sender, RoutedEventArgs e) => ShowHome();
    private void Game_Click(object sender, RoutedEventArgs e) => ShowGame();
    private void Geode_Click(object sender, RoutedEventArgs e) => ShowGeode();
    private void Workshop_Click(object sender, RoutedEventArgs e) => ShowWorkshop();
    private void Themes_Click(object sender, RoutedEventArgs e) => ShowThemes();
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();
    private void About_Click(object sender, RoutedEventArgs e) => ShowAbout();
    private void SteamCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SteamFlyout.Visibility == Visibility.Visible) SteamFlyout.Visibility = Visibility.Collapsed;
        else ShowSteamFlyout();
        e.Handled = true;
    }
    private void UpdateSteamStatus()
    {
        var account = SteamAuthStore.AccountName;
        SteamNote.Text = account == null ? T("Войти по QR", "Sign in with QR") : T("Вход: ", "Signed in: ") + account;
    }
    private void ShowSteamFlyout()
    {
        var panel = new StackPanel();
        var heading = new DockPanel();
        var close = new Button { Content = "×", Width = 26, Height = 24, HorizontalAlignment = HorizontalAlignment.Right, FontFamily = new FontFamily("Segoe UI"), Foreground = Foreground, Background = accentBrush, BorderThickness = new Thickness(0) };
        close.Click += (_, _) => SteamFlyout.Visibility = Visibility.Collapsed;
        DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        heading.Children.Add(new TextBlock { Text = T("Вход в Steam", "Steam sign-in"), FontSize = 19, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(heading);
        panel.Children.Add(Label(T("Логин Steam (не отображаемое имя)", "Steam login (not display name)")));
        var login = new TextBox { MaxLength = 64, Height = 36, VerticalContentAlignment = VerticalAlignment.Center, Text = SteamAuthStore.AccountName ?? "" };
        panel.Children.Add(login);
        panel.Children.Add(new TextBlock { Text = T("Один раз подтвердите вход в мобильном Steam. Сеанс хранится зашифрованным в Windows. Пароль лаунчер не запрашивает.", "Confirm once in Steam Mobile. The session is encrypted for this Windows user. The launcher does not ask for a password."), TextWrapping = TextWrapping.Wrap, Opacity = .7, Margin = new Thickness(5, 8, 5, 8) });
        var result = new TextBlock { Text = SteamAuthStore.AccountName is { } name ? T("Подключён аккаунт: ", "Connected account: ") + name : T("Вход не выполнен.", "Not signed in."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(5, 8, 5, 8) };
        panel.Children.Add(result);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var signIn = new Button { Content = T("Войти по QR", "Sign in with QR"), Style = actionStyle, Width = 175, Height = 40 };
        signIn.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(login.Text))
            {
                result.Text = T("Введите логин Steam.", "Enter your Steam login.");
                login.Focus();
                return;
            }
            var window = new SteamQrWindow(login.Text.Trim(), Ru, FontFamily, contentCardBrush, Foreground, accentBrush, actionStyle) { Owner = this };
            window.SignedIn += account => result.Text = T("Подключён аккаунт: ", "Connected account: ") + account;
            SteamFlyout.Visibility = Visibility.Collapsed;
            window.ShowDialog();
            UpdateSteamStatus();
        };
        actions.Children.Add(signIn);
        var signOut = new Button { Content = T("Выйти", "Sign out"), Style = actionStyle, Width = 175, Height = 40 };
        signOut.Click += (_, _) => { SteamAuthStore.Clear(); UpdateSteamStatus(); result.Text = T("Вход удалён.", "Sign-in removed."); };
        actions.Children.Add(signOut);
        panel.Children.Add(actions);
        SteamFlyout.Child = new ScrollViewer { Content = panel, MaxHeight = 540, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        SteamFlyout.Visibility = Visibility.Visible;
    }
    private void Header_Drag(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (e.ClickCount == 1) DragMove(); }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
