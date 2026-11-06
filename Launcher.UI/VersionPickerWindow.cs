using Launcher.Core;
using Launcher.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Launcher.UI;

public sealed class VersionPickerWindow : Window
{
    private readonly bool ru;
    private readonly IReadOnlyList<GameVersionEntry> versions;
    private readonly Dictionary<string, Button> cards = new();
    private readonly Brush cardNormal;
    private readonly Brush cardHover;
    private readonly Brush cardSelected;
    private readonly Color selectedColor;
    private readonly TextBlock selectionNote;
    private readonly TextBlock fileNote;
    private readonly Button downloadButton;
    private readonly Button cancelButton;
    private readonly Button chooseButton;
    private readonly ProgressBar progressBar;
    private readonly DepotDownloaderService downloader = new();
    private CancellationTokenSource? downloadCancellation;
    private bool busy;
    private string selectedVersion;
    public string SelectedVersion => selectedVersion;

    private string T(string russian, string english) => ru ? russian : english;
    private bool IsVersionDownloaded(string version) => DepotDownloaderService.IsDownloaded(version) || versions.Any(entry => entry.Version == version && entry.IsCurrentSteamVersion);

    public VersionPickerWindow(IReadOnlyList<GameVersionEntry> versions, string selectedVersion, bool ru, FontFamily font, ThemeService themes, string themeName)
    {
        this.ru = ru;
        this.versions = versions;
        this.selectedVersion = selectedVersion;
        var theme = themes.Load(themeName);
        var standard = themes.Load("Default");
        Color Pick(string? value, string fallback) => (Color)ColorConverter.ConvertFromString(value ?? fallback);
        Brush Asset(string? custom, string? fallback, Color baseColor)
        {
            var file = themes.ResolveImage(themeName, custom, fallback);
            if (file == null) return new SolidColorBrush(baseColor);
            try
            {
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(file); image.EndInit(); image.Freeze();
                return new ImageBrush(image) { Stretch = Stretch.UniformToFill };
            }
            catch { return new SolidColorBrush(baseColor); }
        }
        var text = Pick(null, theme.TextColor);
        var muted = Pick(null, theme.MutedTextColor);
        selectedColor = Pick(theme.VersionSelectedColor, theme.ButtonColor);
        cardNormal = Asset(theme.VersionCardNormalImage, standard.VersionCardNormalImage, Pick(theme.VersionCardColor, theme.PanelColor));
        cardHover = Asset(theme.VersionCardHoverImage, standard.VersionCardHoverImage, Color.Multiply(Pick(theme.VersionCardColor, theme.PanelColor), 1.25f));
        cardSelected = Asset(theme.VersionCardSelectedImage, standard.VersionCardSelectedImage, Color.Multiply(selectedColor, .5f));
        var buttonNormal = Asset(theme.VersionButtonNormalImage, standard.VersionButtonNormalImage, Pick(null, theme.ButtonColor));
        var buttonHover = Asset(theme.VersionButtonHoverImage, standard.VersionButtonHoverImage, Color.Multiply(Pick(null, theme.ButtonColor), 1.2f));
        var buttonPressed = Asset(theme.VersionButtonPressedImage, standard.VersionButtonPressedImage, Color.Multiply(Pick(null, theme.ButtonColor), .8f));
        var cardStyle = ButtonFillStyle.Create(160, 49, text, cardNormal, cardHover, cardSelected, cardNormal);
        var footerStyle = ButtonFillStyle.Create(166, 45, text, buttonNormal, buttonHover, buttonPressed, Brushes.Gray);
        var closeStyle = ButtonFillStyle.Create(30, 30, text, Brushes.Transparent, new SolidColorBrush(Color.Multiply(selectedColor, .6f)), new SolidColorBrush(selectedColor), Brushes.Gray);
        Title = T("Каталог версий", "Version catalog");
        Width = 720; Height = 630;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Asset(theme.VersionBackgroundImage, standard.VersionBackgroundImage, Pick(theme.VersionBackgroundColor, theme.BackgroundColor));
        Foreground = new SolidColorBrush(text);
        FontFamily = font;

        var frame = new Border { BorderBrush = new SolidColorBrush(selectedColor), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(22) };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(95) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) });
        frame.Child = root;
        Content = frame;

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        top.Children.Add(new TextBlock { Text = T("Geometry Dash Launcher  /  КАТАЛОГ ВЕРСИЙ", "Geometry Dash Launcher  /  VERSIONS"), FontSize = 11, Foreground = new SolidColorBrush(muted), VerticalAlignment = VerticalAlignment.Center });
        var close = new Button { Content = "×", Style = closeStyle, Width = 30, Height = 30, Margin = new Thickness(0), FontSize = 20, FontFamily = new FontFamily("Segoe UI") };
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 1); top.Children.Add(close); root.Children.Add(top);

        var heading = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        heading.Children.Add(new TextBlock { Text = T("Выберите версию", "Choose a version"), FontSize = 27, FontWeight = FontWeights.Bold });
        heading.Children.Add(new TextBlock { Text = T("Загрузка через Steam DepotDownloader", "Download through Steam DepotDownloader"), FontSize = 12, Foreground = new SolidColorBrush(muted), Margin = new Thickness(0, 7, 0, 0) });
        Grid.SetRow(heading, 1); root.Children.Add(heading);

        var list = new UniformGrid { Columns = 4 };
        foreach (var version in versions)
        {
            var contents = new StackPanel { Margin = new Thickness(10, 3, 4, 2) };
            contents.Children.Add(new TextBlock { Text = version.Version, FontSize = 15, FontWeight = FontWeights.Bold });
            var status = IsVersionDownloaded(version.Version) ? T("Загружено", "Downloaded") : T("Не загружено", "Not downloaded");
            contents.Children.Add(new TextBlock { Text = status, FontSize = 9, Foreground = new SolidColorBrush(muted), Margin = new Thickness(0, 2, 0, 0) });
            var card = new Button { Content = contents, Style = cardStyle, Width = 160, Height = 49, Margin = new Thickness(4), Padding = new Thickness(0), BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            card.Click += (_, _) => { this.selectedVersion = version.Version; RefreshSelection(); };
            cards.Add(version.Version, card);
            list.Children.Add(card);
        }
        var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 2); root.Children.Add(scroll);

        var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var statusPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 15, 0) };
        selectionNote = new TextBlock { FontSize = 10, Foreground = new SolidColorBrush(muted), TextWrapping = TextWrapping.Wrap };
        progressBar = new ProgressBar { Height = 5, IsIndeterminate = true, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 7, 0, 0), Foreground = new SolidColorBrush(selectedColor), Background = cardNormal };
        fileNote = new TextBlock { FontSize = 9, Foreground = new SolidColorBrush(muted), TextTrimming = TextTrimming.CharacterEllipsis, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 4, 0, 0) };
        statusPanel.Children.Add(selectionNote);
        statusPanel.Children.Add(progressBar);
        statusPanel.Children.Add(fileNote);
        footer.Children.Add(statusPanel);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        downloadButton = new Button { Content = T("↓  Скачать", "↓  Download"), Style = footerStyle, Width = 136, Height = 45, Margin = new Thickness(2), FontWeight = FontWeights.Bold };
        downloadButton.Click += async (_, _) => await DownloadSelectedAsync();
        buttons.Children.Add(downloadButton);
        cancelButton = new Button { Content = T("Отменить", "Cancel"), Style = footerStyle, Width = 126, Height = 45, Margin = new Thickness(2), FontWeight = FontWeights.Bold, Visibility = Visibility.Collapsed };
        cancelButton.Click += (_, _) => CancelDownload();
        buttons.Children.Add(cancelButton);
        chooseButton = new Button { Content = T("Выбрать  →", "Select  →"), Style = footerStyle, Width = 166, Height = 45, Margin = new Thickness(2), FontWeight = FontWeights.Bold };
        chooseButton.Click += (_, _) => { DialogResult = true; Close(); };
        buttons.Children.Add(chooseButton);
        Grid.SetColumn(buttons, 1); footer.Children.Add(buttons);
        Grid.SetRow(footer, 3); root.Children.Add(footer);
        Closing += (_, _) => downloadCancellation?.Cancel();
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        foreach (var (version, card) in cards)
        {
            var selected = version == selectedVersion;
            card.Background = selected ? cardSelected : cardNormal;
            card.BorderBrush = selected ? new SolidColorBrush(selectedColor) : Brushes.Transparent;
            if (card.Content is StackPanel details && details.Children[1] is TextBlock state) state.Text = IsVersionDownloaded(version) ? T("Загружено", "Downloaded") : T("Не загружено", "Not downloaded");
        }
        var chosen = versions.FirstOrDefault(v => v.Version == selectedVersion);
        downloadButton.IsEnabled = !busy && SteamAuthStore.AccountName != null && DepotDownloaderService.ManifestFor(selectedVersion) != null && !IsVersionDownloaded(selectedVersion);
        selectionNote.Text = DepotDownloaderService.IsDownloaded(selectedVersion)
            ? T("Загружена через Steam. Можно выбрать и играть.", "Downloaded through Steam. Select to play.")
            : SteamAuthStore.AccountName == null
            ? T("Войдите по QR в панели Steam слева внизу.", "Sign in with QR from the bottom-left Steam panel.")
            : chosen?.IsCurrentSteamVersion == true
            ? T("Текущая установка Steam. Запуск доступен.", "Current Steam installation. Ready to play.")
            : chosen?.LocalDirectory != null
                ? T("Локальная копия найдена. Запуск через Steam для неё пока недоступен.", "Local copy found. Steam launch for this copy is not available yet.")
                : DepotDownloaderService.ManifestFor(selectedVersion) != null
                    ? T("Нажмите «Скачать» для загрузки через Steam.", "Click Download to fetch this version through Steam.")
                    : T("Эта версия пока недоступна для загрузки.", "This version is not available for download yet.");
    }

    private void CancelDownload()
    {
        if (!busy || downloadCancellation == null || downloadCancellation.IsCancellationRequested) return;
        cancelButton.IsEnabled = false;
        selectionNote.Text = T("Отменяем загрузку...", "Cancelling download...");
        downloadCancellation.Cancel();
    }

    private async Task DownloadSelectedAsync()
    {
        if (busy || SteamAuthStore.AccountName == null || DepotDownloaderService.ManifestFor(selectedVersion) == null || IsVersionDownloaded(selectedVersion)) return;
        busy = true; downloadButton.IsEnabled = false; chooseButton.IsEnabled = false;
        cancelButton.IsEnabled = true; cancelButton.Visibility = Visibility.Visible;
        progressBar.Visibility = Visibility.Visible;
        progressBar.IsIndeterminate = true;
        fileNote.Visibility = Visibility.Visible; fileNote.Text = "";
        foreach (var card in cards.Values) card.IsEnabled = false;
        using var cancellation = new CancellationTokenSource();
        downloadCancellation = cancellation;
        var version = selectedVersion;
        try
        {
            var progress = new Progress<DepotDownloadProgress>(item =>
            {
                if (!busy || cancellation.IsCancellationRequested) return;
                selectionNote.Text = item.Percent is { } percent ? T($"Загрузка: {percent:0}%", $"Downloading: {percent:0}%") : T("Загрузка файлов игры...", item.Message);
                if (item.Percent is { } value) { progressBar.IsIndeterminate = false; progressBar.Value = value; }
                if (!string.IsNullOrWhiteSpace(item.FileName)) fileNote.Text = item.FileName;
            });
            await downloader.DownloadAsync(version, progress, cancellation.Token);
            RefreshSelection();
        }
        catch (OperationCanceledException) { selectionNote.Text = T("Загрузка отменена.", "Download cancelled."); }
        catch (Exception error) { selectionNote.Text = T("Ошибка загрузки: ", "Download failed: ") + error.Message; }
        finally
        {
            busy = false;
            cancelButton.Visibility = Visibility.Collapsed;
            progressBar.Visibility = Visibility.Collapsed;
            fileNote.Visibility = Visibility.Collapsed;
            chooseButton.IsEnabled = true;
            foreach (var card in cards.Values) card.IsEnabled = true;
            downloadCancellation = null;
            downloadButton.IsEnabled = SteamAuthStore.AccountName != null && DepotDownloaderService.ManifestFor(selectedVersion) != null && !IsVersionDownloaded(selectedVersion);
        }
    }
}
