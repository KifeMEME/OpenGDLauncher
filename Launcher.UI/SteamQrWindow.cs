using Launcher.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Launcher.UI;

public sealed class SteamQrWindow : Window
{
    private readonly string username;
    private readonly bool ru;
    private readonly TextBlock status;
    private readonly TextBlock placeholder;
    private readonly Image codeImage;
    private readonly Button retryButton;
    private readonly Button closeButton;
    private CancellationTokenSource? cancellation;
    private Task? attempt;
    private bool signedIn;

    public event Action<string>? SignedIn;
    private string T(string russian, string english) => ru ? russian : english;

    public SteamQrWindow(string username, bool ru, FontFamily font, Brush background, Brush foreground, Brush accent, Style buttonStyle, bool startOnLoad = true)
    {
        this.username = username;
        this.ru = ru;
        Title = T("Вход в Steam", "Steam sign-in");
        Width = 520;
        Height = 625;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = background;
        Foreground = foreground;
        FontFamily = font;

        var frame = new Border { BorderBrush = accent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(18) };
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(80) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
        frame.Child = layout;
        Content = frame;

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        header.Children.Add(new TextBlock { Text = T("STEAM  /  ВХОД ПО QR", "STEAM  /  QR SIGN-IN"), FontSize = 13, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
        var topClose = new Button { Content = "×", Style = ButtonFillStyle.Create(30, 30, foreground is SolidColorBrush color ? color.Color : Colors.White, Brushes.Transparent, accent, accent, Brushes.Gray), Width = 30, Height = 30, Margin = new Thickness(0), FontSize = 20, FontFamily = new FontFamily("Segoe UI") };
        topClose.Click += (_, _) => Close();
        Grid.SetColumn(topClose, 1);
        header.Children.Add(topClose);
        header.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 1) DragMove(); };
        layout.Children.Add(header);

        var intro = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
        intro.Children.Add(new TextBlock { Text = T("Отсканируйте код в мобильном Steam", "Scan the code in Steam Mobile"), FontSize = 19, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
        status = new TextBlock { Text = T("Подключаемся к Steam...", "Connecting to Steam..."), FontSize = 12, TextWrapping = TextWrapping.Wrap, Opacity = .8, Margin = new Thickness(0, 6, 0, 0) };
        intro.Children.Add(status);
        Grid.SetRow(intro, 1);
        layout.Children.Add(intro);

        var codeFrame = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(6), Padding = new Thickness(12), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Width = 420, Height = 420 };
        var codeSurface = new Grid();
        placeholder = new TextBlock { Text = T("Ожидаем QR-код...", "Waiting for QR code..."), Foreground = Brushes.Black, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        codeImage = new Image { Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed };
        RenderOptions.SetBitmapScalingMode(codeImage, BitmapScalingMode.NearestNeighbor);
        codeSurface.Children.Add(placeholder);
        codeSurface.Children.Add(codeImage);
        codeFrame.Child = codeSurface;
        Grid.SetRow(codeFrame, 2);
        layout.Children.Add(codeFrame);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
        retryButton = new Button { Content = T("Повторить", "Retry"), Style = buttonStyle, Width = 175, Height = 42 };
        retryButton.Click += async (_, _) => await RestartAsync();
        closeButton = new Button { Content = T("Отмена", "Cancel"), Style = buttonStyle, Width = 175, Height = 42 };
        closeButton.Click += (_, _) => Close();
        buttons.Children.Add(retryButton);
        buttons.Children.Add(closeButton);
        Grid.SetRow(buttons, 3);
        layout.Children.Add(buttons);

        if (startOnLoad) Loaded += (_, _) => attempt = SignInAsync();
        Closed += (_, _) => cancellation?.Cancel();
    }

    private async Task RestartAsync()
    {
        retryButton.IsEnabled = false;
        cancellation?.Cancel();
        if (attempt != null) await attempt;
        if (IsVisible && !signedIn) attempt = SignInAsync();
    }

    private async Task SignInAsync()
    {
        using var current = new CancellationTokenSource();
        cancellation = current;
        codeImage.Source = null;
        codeImage.Visibility = Visibility.Collapsed;
        placeholder.Visibility = Visibility.Visible;
        status.Text = T("Подключаемся к Steam...", "Connecting to Steam...");
        retryButton.IsEnabled = true;
        try
        {
            var progress = new Progress<SteamQrProgress>(item =>
            {
                if (current.IsCancellationRequested || !IsVisible || signedIn) return;
                if (item.QrText is { } qrText)
                {
                    var bitmap = SteamQrBitmap.TryCreate(qrText);
                    if (bitmap == null) return;
                    codeImage.Source = bitmap;
                    codeImage.Visibility = Visibility.Visible;
                    placeholder.Visibility = Visibility.Collapsed;
                    status.Text = T("Подтвердите вход в приложении Steam.", "Approve sign-in in the Steam app.");
                }
                else if (item.Message.Contains("Waiting for Steam QR code", StringComparison.OrdinalIgnoreCase))
                {
                    codeImage.Source = null;
                    codeImage.Visibility = Visibility.Collapsed;
                    placeholder.Visibility = Visibility.Visible;
                    status.Text = T("Обновляем QR-код...", "Refreshing QR code...");
                }
                else if (codeImage.Source == null)
                {
                    status.Text = T("Подключаемся к Steam. Если код не появится, нажмите «Повторить».", "Connecting to Steam. If no code appears, click Retry.");
                }
            });
            var account = await new DepotDownloaderService().SignInAsync(username, progress, current.Token);
            if (!IsVisible) return;
            signedIn = true;
            codeImage.Source = null;
            codeImage.Visibility = Visibility.Collapsed;
            placeholder.Text = "✓";
            placeholder.Visibility = Visibility.Visible;
            status.Text = T("Аккаунт подключён: ", "Account connected: ") + account;
            retryButton.IsEnabled = false;
            closeButton.Content = T("Закрыть", "Close");
            SignedIn?.Invoke(account);
        }
        catch (OperationCanceledException)
        {
            if (IsVisible) status.Text = T("Запрос отменён.", "Request cancelled.");
        }
        catch (Exception error)
        {
            if (IsVisible) status.Text = T("Не удалось войти: ", "Sign-in failed: ") + error.Message;
        }
        finally
        {
            current.Cancel();
            if (ReferenceEquals(cancellation, current)) cancellation = null;
            if (IsVisible && !signedIn) retryButton.IsEnabled = true;
        }
    }
}

internal static class SteamQrBitmap
{
    public static BitmapSource? TryCreate(string ascii)
    {
        var lines = ascii.Split('\n').Select(line => line.TrimEnd('\r').Replace('\u00a0', ' ')).ToArray();
        if (lines.Length is < 21 or > 120) return null;
        var size = lines.Length;
        var paired = lines.All(line => line.Length == size * 2 &&
            Enumerable.Range(0, size).All(x => (line[x * 2] == ' ' && line[x * 2 + 1] == ' ') ||
                                              (line[x * 2] == '█' && line[x * 2 + 1] == '█')));
        var modules = new List<bool[]>(lines.Length);
        foreach (var line in lines)
        {
            if (paired)
            {
                modules.Add(Enumerable.Range(0, size).Select(x => line[x * 2] == '█').ToArray());
                continue;
            }
            var row = new List<bool>(line.Length);
            for (var i = 0; i < line.Length;)
            {
                if (line[i] == ' ') { row.Add(false); i++; continue; }
                if (i + 1 >= line.Length || line[i] != '█' || line[i + 1] != '█') return null;
                row.Add(true);
                i += 2;
            }
            modules.Add(row.ToArray());
        }
        if (modules.Any(row => row.Length != size)) return null;
        var stride = size * 4;
        var pixels = new byte[stride * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var value = modules[y][x] ? (byte)0 : (byte)255;
                var offset = y * stride + x * 4;
                pixels[offset] = value;
                pixels[offset + 1] = value;
                pixels[offset + 2] = value;
                pixels[offset + 3] = 255;
            }
        var image = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        image.Freeze();
        return image;
    }
}
