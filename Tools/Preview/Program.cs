using Launcher.UI;
using Launcher.Core;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 1 && args[1] == "steam_qr_probe")
        {
            using var cancellation = new CancellationTokenSource();
            var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var progress = new Progress<SteamQrProgress>(item => { if (item.QrText != null) received.TrySetResult(item.QrText); });
            var signIn = new DepotDownloaderService().SignInAsync(SteamAuthStore.AccountName ?? "qr_probe", progress, cancellation.Token);
            try
            {
                var completed = Task.WhenAny(received.Task, signIn, Task.Delay(TimeSpan.FromSeconds(30))).GetAwaiter().GetResult();
                if (completed == signIn) signIn.GetAwaiter().GetResult();
                if (completed != received.Task) throw new TimeoutException("Steam did not provide a QR code within 30 seconds.");
                var qr = received.Task.GetAwaiter().GetResult();
                var renderer = typeof(MainWindow).Assembly.GetType("Launcher.UI.SteamQrBitmap")!;
                var bitmap = (BitmapSource?)renderer.GetMethod("TryCreate")!.Invoke(null, [qr]);
                if (bitmap == null || bitmap.PixelWidth != bitmap.PixelHeight) throw new InvalidOperationException("Steam QR output could not be rendered as a square bitmap.");
                Console.WriteLine($"steam-qr-renderable=True; modules={bitmap.PixelWidth}");
            }
            finally
            {
                cancellation.Cancel();
                try { signIn.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
            }
            return;
        }
        if (args.Length > 1 && args[1] == "depot_cache_check")
        {
            var root = Path.Combine(Path.GetTempPath(), "opengd-depot-cache-test-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(root, "old");
            var newDir = Path.Combine(root, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var existing = Path.Combine(oldDir, "account.config");
            var created = Path.Combine(newDir, "account.config");
            try
            {
                File.WriteAllText(existing, "original");
                var type = typeof(DepotDownloaderService).Assembly.GetType("Launcher.Core.DepotAccountCacheSnapshot")!;
                var capture = type.GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Single(m => m.Name == "Capture" && m.GetParameters().Length == 1);
                var snapshot = capture.Invoke(null, [new[] { root }])!;
                File.WriteAllText(existing, "new token");
                File.WriteAllText(created, "new token");
                type.GetMethod("Restore")!.Invoke(snapshot, null);
                if (File.ReadAllText(existing) != "original" || File.Exists(created)) throw new InvalidOperationException("DepotDownloader token cache was not restored.");
                Console.WriteLine("depot-cache-restored=True");
            }
            finally
            {
                if (File.Exists(existing)) File.Delete(existing);
                if (File.Exists(created)) File.Delete(created);
                Directory.Delete(oldDir);
                Directory.Delete(newDir);
                Directory.Delete(root);
            }
            return;
        }
        if (args.Length > 1 && args[1] == "depot_tool")
        {
            var tool = DepotDownloaderService.EnsureToolAsync(null, CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine($"tool={File.Exists(tool)}; manifest-2.2081={DepotDownloaderService.ManifestFor("2.2081")}; unsupported-2.1={DepotDownloaderService.ManifestFor("2.1") == null}");
            return;
        }
        if (args.Length > 1 && args[1] == "steam_auth_check")
        {
            if (SteamAuthStore.AccountName != null) throw new InvalidOperationException("Do not overwrite an existing Steam sign-in for this test.");
            var cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IsolatedStorage", "opengd-test", "account.config");
            try
            {
                SteamAuthStore.Save("sample_account", cachePath, System.Text.Encoding.UTF8.GetBytes("test token marker"));
                var loaded = SteamAuthStore.Load();
                if (loaded?.Username != "sample_account" || System.Text.Encoding.UTF8.GetString(loaded.CacheBytes) != "test token marker") throw new InvalidOperationException("Steam auth roundtrip failed.");
                var file = Path.Combine(SettingsStore.DirectoryPath, "steam-auth.json");
                if (File.ReadAllText(file).Contains("test token marker")) throw new InvalidOperationException("Steam auth was saved without encryption.");
                Console.WriteLine("steam-auth-encrypted=True");
            }
            finally { SteamAuthStore.Clear(); }
            return;
        }
        if (args.Length > 1 && args[1] == "scan")
        {
            var source = GeodeModScanner.ChooseModsDirectory(GameService.Detect() ?? "", "");
            var runtime = GeodeCatalogService.DetectRuntime(source);
            Console.WriteLine($"{source}|{(source == null ? 0 : GeodeModScanner.ScanModsDirectory(source).Count)}|{runtime.GameVersion}|{runtime.GeodeVersion}");
            return;
        }
        if (args.Length > 1 && args[1] == "catalog")
        {
            var service = new GeodeCatalogService();
            var source = GeodeModScanner.ChooseModsDirectory(GameService.Detect() ?? "", "");
            var runtime = GeodeCatalogService.DetectRuntime(source);
            var result = service.SearchAsync("", 1, runtime.GameVersion, runtime.GeodeVersion).GetAwaiter().GetResult();
            var logo = service.GetLogoAsync(result.Mods[0].Id).GetAwaiter().GetResult();
            Console.WriteLine($"{result.Mods.Count}|{result.HasMore}|{result.Mods[0].Name}|{result.Mods[0].Version}|{result.Mods[0].Hash.Length}|{logo?.Length ?? 0}|{GeodeCatalogService.IsCompatible(result.Mods[0], runtime)}");
            return;
        }
        if (args.Length > 1 && args[1] == "download")
        {
            Directory.CreateDirectory(args[0]);
            var service = new GeodeCatalogService();
            var result = service.SearchAsync("Node IDs", 1, "2.206", "3.2.0").GetAwaiter().GetResult();
            var mod = result.Mods.First(m => m.Id == "geode.node-ids");
            var saved = service.InstallAsync(mod, args[0]).GetAwaiter().GetResult();
            Console.WriteLine($"{mod.Name}|{mod.Version}|{File.Exists(saved.FilePath)}|{saved.Bytes}|{GeodeModScanner.ScanModsDirectory(args[0]).Count}");
            return;
        }
        if (args.Length > 1 && args[1] == "steamcheck")
        {
            var checkedAccount = SteamClientService.Check("nonexistent_account_for_test", GameService.Detect() ?? "");
            Console.WriteLine($"{checkedAccount.AccountMatches}|{checkedAccount.HasInstallRecord}");
            return;
        }
        var app = new App();
        var window = new MainWindow { Width = 1000, Height = 670, WindowStartupLocation = WindowStartupLocation.Manual, Left = -2000, Top = -2000 };
        app.DispatcherUnhandledException += (_, error) => { Console.Error.WriteLine(error.Exception); Environment.ExitCode = 1; error.Handled = true; window.Close(); };
        window.Show();
        // Screenshots need the final frame; the real launcher keeps its 300 ms startup fade.
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
        window.BeginAnimation(Window.OpacityProperty, null);
        window.Opacity = 1;
        if (args.Length > 1 && args[1] == "language_toggle_check")
        {
            var original = SettingsStore.Load();
            try
            {
                var button = (System.Windows.Controls.Button)window.FindName("LanguageButton")!;
                button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                var expected = original.Language == "en" ? "ru" : "en";
                var updated = SettingsStore.Load();
                var home = (System.Windows.Controls.Button)window.FindName("HomeNav")!;
                if (updated.Language != expected || !Equals(home.Content, expected == "ru" ? "Главная" : "Home"))
                    throw new InvalidOperationException("Language button did not update the saved language and visible navigation.");
                window.UpdateLayout();
                var preview = new RenderTargetBitmap(1000, 670, 96, 96, PixelFormats.Pbgra32); preview.Render(window);
                var languagePng = new PngBitmapEncoder(); languagePng.Frames.Add(BitmapFrame.Create(preview));
                using var file = File.Create(args[0]); languagePng.Save(file);
                Console.WriteLine($"language-toggle={original.Language}->{updated.Language}; navigation={home.Content}");
            }
            finally { SettingsStore.Save(original); window.Close(); }
            return;
        }
        if (args.Length > 1 && args[1] == "button_hover_check")
        {
            var button = (System.Windows.Controls.Button)window.FindName("HomeNav")!;
            window.Dispatcher.BeginInvoke(() =>
            {
                button.ApplyTemplate();
                var layer = (System.Windows.Controls.Border)button.Template.FindName("HoverFillLayer", button)!;
                button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
                var clip = (RectangleGeometry)layer.Clip;
                var middle = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(110) };
                middle.Tick += (_, _) =>
                {
                    middle.Stop();
                    if (clip.Rect.Width <= 0 || clip.Rect.Width >= layer.ActualWidth)
                        throw new InvalidOperationException($"Hover animation did not reach a partial fill: {clip.Rect.Width}/{layer.ActualWidth}.");
                    var frame = new RenderTargetBitmap(1000, 670, 96, 96, PixelFormats.Pbgra32); frame.Render(window);
                    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(frame));
                    using (var file = File.Create(args[0])) png.Save(file);
                    var end = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(190) };
                    end.Tick += (_, _) =>
                    {
                        end.Stop();
                        if (Math.Abs(clip.Rect.Width - layer.ActualWidth) > 1)
                            throw new InvalidOperationException("Hover fill did not reach the right edge.");
                        button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseLeaveEvent });
                        var reset = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(230) };
                        reset.Tick += (_, _) =>
                        {
                            reset.Stop();
                            if (clip.Rect.Width > 1) throw new InvalidOperationException("Hover fill did not retract after mouse leave.");
                            Console.WriteLine("button-hover=partial->full->empty");
                            window.Close();
                        };
                        reset.Start();
                    };
                    end.Start();
                };
                middle.Start();
            });
            app.Run(window); return;
        }
        if (args.Length > 1 && args[1] == "steam_qr_window_ui")
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var style = (System.Windows.Style)typeof(MainWindow).GetField("actionStyle", flags)!.GetValue(window)!;
            var background = (Brush)typeof(MainWindow).GetField("contentCardBrush", flags)!.GetValue(window)!;
            var accent = (Brush)typeof(MainWindow).GetField("accentBrush", flags)!.GetValue(window)!;
            var qrWindow = new SteamQrWindow("preview", true, window.FontFamily, background, window.Foreground, accent, style, startOnLoad: false)
            { Owner = window, WindowStartupLocation = WindowStartupLocation.Manual, Left = -2000, Top = -2000 };
            qrWindow.Show();
            qrWindow.UpdateLayout();
            var qrPreview = new RenderTargetBitmap(520, 625, 96, 96, PixelFormats.Pbgra32);
            qrPreview.Render(qrWindow);
            var qrPng = new PngBitmapEncoder(); qrPng.Frames.Add(BitmapFrame.Create(qrPreview));
            using var qrFile = File.Create(args[0]); qrPng.Save(qrFile);
            qrWindow.Close(); window.Close(); return;
        }
        if (args.Length > 1 && args[1] == "steam_signin_ui")
        {
            typeof(MainWindow).GetMethod("ShowSteamFlyout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, null);
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1000, 670, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(args[0]); png.Save(file);
            window.Close(); return;
        }
        if (args.Length > 1 && args[1] == "font")
        {
            var candidates = new[]
            {
                window.FontFamily,
                new FontFamily(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Lemon/Milk(RUS BY LYAJKA)"),
                new FontFamily(new Uri("pack://application:,,,/"), "/OpenGDLauncher;component/Assets/Fonts/#Lemon/Milk(RUS BY LYAJKA)"),
                new FontFamily(new Uri("pack://application:,,,/"), "./OpenGDLauncher;component/Assets/Fonts/#Lemon/Milk(RUS BY LYAJKA)"),
                new FontFamily("/OpenGDLauncher;component/Assets/Fonts/#Lemon/Milk(RUS BY LYAJKA)")
            };
            foreach (var candidate in candidates)
            {
                var typeface = new Typeface(candidate, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                Console.WriteLine($"{candidate.Source}|{(typeface.TryGetGlyphTypeface(out var glyphs) ? glyphs.FontUri + "|" + glyphs.EmbeddingRights : "unresolved")}");
            }
            foreach (var family in Fonts.GetFontFamilies(new Uri("pack://application:,,,/"), "/OpenGDLauncher;component/Assets/Fonts/")) Console.WriteLine("FOUND " + family.Source);
            window.Close(); return;
        }
        if (args.Length > 1 && args[1] == "version_ui")
        {
            var selected = SettingsStore.Load().SelectedVersion;
            var picker = new VersionPickerWindow(GameVersionCatalog.GetEntries(), selected.Length > 0 ? selected : GameVersionCatalog.CurrentSteamVersion() ?? "", true, window.FontFamily, new Launcher.Themes.ThemeService(Path.Combine(SettingsStore.DirectoryPath, "Themes")), SettingsStore.Load().Theme)
            { Owner = window, WindowStartupLocation = WindowStartupLocation.Manual, Left = -2000, Top = -2000 };
            picker.Show(); picker.UpdateLayout();
            var bitmap = new RenderTargetBitmap(720, 630, 96, 96, PixelFormats.Pbgra32); bitmap.Render(picker);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(args[0]); png.Save(file);
            picker.Close(); window.Close(); return;
        }
        if (args.Length > 1 && args[1] == "version_cancel_ui")
        {
            var picker = new VersionPickerWindow(GameVersionCatalog.GetEntries(), GameVersionCatalog.CurrentSteamVersion() ?? "", true, window.FontFamily, new Launcher.Themes.ThemeService(Path.Combine(SettingsStore.DirectoryPath, "Themes")), SettingsStore.Load().Theme)
            { Owner = window, WindowStartupLocation = WindowStartupLocation.Manual, Left = -2000, Top = -2000 };
            picker.Show(); picker.UpdateLayout();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var cancel = (System.Windows.Controls.Button)typeof(VersionPickerWindow).GetField("cancelButton", flags)!.GetValue(picker)!;
            if (cancel.Visibility != Visibility.Collapsed) throw new InvalidOperationException("Cancel button was visible before a download started.");
            using var cancellation = new CancellationTokenSource();
            typeof(VersionPickerWindow).GetField("busy", flags)!.SetValue(picker, true);
            typeof(VersionPickerWindow).GetField("downloadCancellation", flags)!.SetValue(picker, cancellation);
            cancel.Visibility = Visibility.Visible;
            picker.UpdateLayout();
            var frame = new RenderTargetBitmap(720, 630, 96, 96, PixelFormats.Pbgra32); frame.Render(picker);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(frame));
            using (var file = File.Create(args[0])) png.Save(file);
            cancel.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            if (!cancellation.IsCancellationRequested || cancel.IsEnabled) throw new InvalidOperationException("Cancel button did not signal cancellation.");
            Console.WriteLine("version-cancel=hidden-before-download; token-cancelled-on-click");
            picker.Close(); window.Close(); return;
        }
        if (args.Length > 1 && args[1] == "version_skin_ui")
        {
            var root = Path.Combine(Environment.CurrentDirectory, "dist", "theme-preview-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var service = new Launcher.Themes.ThemeService(root);
                new Launcher.Workshop.LocalWorkshop(service).Import(Path.Combine(Environment.CurrentDirectory, "ThemeKit", "GraphicDemo.zip"));
                var picker = new VersionPickerWindow(GameVersionCatalog.GetEntries(), GameVersionCatalog.CurrentSteamVersion() ?? "", true, window.FontFamily, service, "GraphicDemo")
                { Owner = window, WindowStartupLocation = WindowStartupLocation.Manual, Left = -2000, Top = -2000 };
                picker.Show(); picker.UpdateLayout();
                var bitmap = new RenderTargetBitmap(720, 630, 96, 96, PixelFormats.Pbgra32); bitmap.Render(picker);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(args[0]); png.Save(file);
                picker.Close(); window.Close();
            }
            finally
            {
                var expected = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "dist")) + Path.DirectorySeparatorChar;
                if (Path.GetFullPath(root).StartsWith(expected, StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
            }
            return;
        }
        if (args.Length > 1 && args[1] is "home_skin_ui" or "geode_skin_ui")
        {
            var root = Path.Combine(Environment.CurrentDirectory, "dist", "theme-preview-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var service = new Launcher.Themes.ThemeService(root);
                new Launcher.Workshop.LocalWorkshop(service).Import(Path.Combine(Environment.CurrentDirectory, "ThemeKit", "GraphicDemo.zip"));
                var themedWindow = new MainWindow(service) { Width = 1000, Height = 670, WindowStartupLocation = WindowStartupLocation.Manual, Left = -2000, Top = -2000 };
                var settings = (AppSettings)typeof(MainWindow).GetField("settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(themedWindow)!;
                settings.Theme = "GraphicDemo";
                typeof(MainWindow).GetMethod("ApplyTheme", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(themedWindow, null);
                var sound = (System.Media.SoundPlayer?)typeof(MainWindow).GetField("heroSound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(themedWindow);
                if (sound == null || !sound.SoundLocation.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Theme sound was not applied.");
                var page = args[1] == "geode_skin_ui" ? "ShowGeode" : "ShowHome";
                typeof(MainWindow).GetMethod(page, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(themedWindow, null);
                themedWindow.Show(); themedWindow.UpdateLayout();
                var bitmap = new RenderTargetBitmap(1000, 670, 96, 96, PixelFormats.Pbgra32); bitmap.Render(themedWindow);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(args[0]); png.Save(file);
                themedWindow.Close(); window.Close();
            }
            finally
            {
                var expected = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "dist")) + Path.DirectorySeparatorChar;
                if (Path.GetFullPath(root).StartsWith(expected, StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
            }
            return;
        }
        if (args.Length > 1 && args[1] == "version_check")
        {
            var entries = GameVersionCatalog.GetEntries();
            var steam = GameVersionCatalog.CurrentSteamVersion();
            if (!entries.Any(v => v.Version == steam && v.IsCurrentSteamVersion)) throw new InvalidOperationException("Steam version is absent from catalog.");
            var picker = new VersionPickerWindow(entries, steam ?? "", true, window.FontFamily, new Launcher.Themes.ThemeService(Path.Combine(SettingsStore.DirectoryPath, "Themes")), SettingsStore.Load().Theme);
            var cards = (Dictionary<string, System.Windows.Controls.Button>)typeof(VersionPickerWindow).GetField("cards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(picker)!;
            cards["2.2081"].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            if (picker.SelectedVersion != "2.2081") throw new InvalidOperationException("Version selection failed.");
            Console.WriteLine($"steam={steam}; local={entries.Count(v => v.LocalDirectory != null)}; selected={picker.SelectedVersion}");
            window.Close(); return;
        }
        if (args.Length > 1 && args[1] == "english_ui")
        {
            var settings = (AppSettings)typeof(MainWindow).GetField("settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(window)!;
            settings.Language = "en";
            typeof(MainWindow).GetMethod("Localize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, null);
            typeof(MainWindow).GetMethod("ShowHome", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, null);
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1000, 670, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(args[0]); png.Save(file);
            window.Close(); return;
        }
        if (args.Length > 1 && args[1] == "hero_ui")
        {
            if (typeof(MainWindow).GetField("heroSound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(window) == null)
                throw new InvalidOperationException("Bundled hero sound did not load.");
            window.Dispatcher.BeginInvoke(() =>
            {
                window.UpdateLayout();
                var hero = FindHero(window) ?? throw new InvalidOperationException("Hero image not found.");
                hero.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(75) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    var bitmap = new RenderTargetBitmap(1000, 670, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
                    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(args[0]); png.Save(file);
                    window.Close();
                };
                timer.Start();
            });
            app.Run(window); return;
        }
        if (args.Length > 1 && args[1] == "catalog_ui")
        {
            window.Dispatcher.BeginInvoke(() => typeof(MainWindow).GetMethod("ShowGeodeCatalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, new object[] { 1, "", true }));
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            timer.Tick += (_, _) =>
            {
                timer.Stop(); window.UpdateLayout();
                var bitmap = new RenderTargetBitmap(1000, 670, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(args[0]); png.Save(file);
                window.Close();
            };
            timer.Start(); app.Run(window); return;
        }
        if (args.Length > 1)
        {
            var method = args[1].ToLowerInvariant() switch { "game" => "ShowGame", "geode" => "ShowGeode", "themes" => "ShowThemes", "settings" => "ShowSettings", "about" => "ShowAbout", "steam" => "ShowSteamFlyout", "purchase" => "LaunchGame", _ => "" };
            if (method.Length > 0) typeof(MainWindow).GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, null);
        }
        window.UpdateLayout();
        var image = new RenderTargetBitmap(1000, 670, 96, 96, PixelFormats.Pbgra32);
        image.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = File.Create(args[0]); encoder.Save(output);
        window.Close();
    }
    private static System.Windows.Controls.Image? FindHero(DependencyObject element)
    {
        if (element is System.Windows.Controls.Image image && image.Width == 240 && image.Height == 240) return image;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var found = FindHero(VisualTreeHelper.GetChild(element, i));
            if (found != null) return found;
        }
        return null;
    }
}
