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
        if (args.Length > 1 && args[1] == "backup_check")
        {
            var root = Path.Combine(Environment.CurrentDirectory, "dist", "backup-check-" + Guid.NewGuid().ToString("N"));
            var source = Path.Combine(root, "source"); var destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(Path.Combine(source, "geode")); Directory.CreateDirectory(destination);
            try
            {
                File.WriteAllText(Path.Combine(source, "CCGameManager.dat"), "save");
                File.WriteAllText(Path.Combine(source, "geode", "settings.json"), "settings");
                File.WriteAllText(Path.Combine(source, "song.mp3"), "music");
                File.WriteAllText(Path.Combine(source, "geode", "sound.ogg"), "music");
                var result = GameBackupService.CreateBackupAsync(source, destination, CancellationToken.None).GetAwaiter().GetResult();
                if (result.FilesCopied != 2 || result.AudioFilesSkipped != 2 || !File.Exists(Path.Combine(result.Folder, "CCGameManager.dat")) || !File.Exists(Path.Combine(result.Folder, "geode", "settings.json")) || File.Exists(Path.Combine(result.Folder, "song.mp3")))
                    throw new InvalidOperationException("Backup did not preserve non-audio files and skip music.");
                try { GameBackupService.CreateBackupAsync(source, source, CancellationToken.None).GetAwaiter().GetResult(); throw new InvalidOperationException("Backup into source was accepted."); }
                catch (InvalidOperationException error) when (error.Message.Contains("вне локальных данных")) { }
                Console.WriteLine("backup=2 files copied; 2 audio files skipped; source destination rejected");
            }
            finally
            {
                var expected = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "dist")) + Path.DirectorySeparatorChar;
                if (Path.GetFullPath(root).StartsWith(expected, StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
            }
            return;
        }
        var app = new App();
        var previewTestStore = args.Length > 1 && args[1] is "video_ui" or "video_all_ui" or "video_bad_ui" or "video_toggle_ui" or "steam_flyout_video_ui"
            ? new PreviewSettingsStore(Path.Combine(Path.GetTempPath(), "opengd-preview-test-" + Guid.NewGuid().ToString("N")))
            : null;
        var window = new MainWindow(null, previewTestStore) { Width = 1000, Height = 670, WindowStartupLocation = WindowStartupLocation.Manual, Left = args.Length > 1 && args[1] == "video_ui" ? 20 : -2000, Top = args.Length > 1 && args[1] == "video_ui" ? 20 : -2000 };
        app.DispatcherUnhandledException += (_, error) => { Console.Error.WriteLine(error.Exception); Environment.ExitCode = 1; error.Handled = true; window.Close(); };
        window.Show();
        // Screenshots need the final frame; the real launcher keeps its 300 ms startup fade.
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
        if (args.Length > 1 && args[1] == "sound_ui")
        {
            var originalSettings = SettingsStore.Load();
            var check = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            check.Tick += (_, _) =>
            {
                check.Stop();
                try
                {
                    var service = typeof(MainWindow).Assembly.GetType("Launcher.UI.ButtonSoundService")!;
                    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
                    foreach (var name in new[] { "launch", "hover", "click" })
                        if (service.GetField(name, flags)!.GetValue(null) == null) throw new InvalidOperationException($"WAV failed to load: {name}");
                    var gain = (NAudio.Wave.SampleProviders.VolumeSampleProvider)service.GetField("gain", flags)!.GetValue(null)!;
                    var output = (NAudio.Wave.WaveOutEvent)service.GetField("output", flags)!.GetValue(null)!;
                    if (gain.Volume != .5f || output.PlaybackState != NAudio.Wave.PlaybackState.Playing ||
                        !(bool)service.GetField("launchPlayed", flags)!.GetValue(null)! ||
                        !(bool)service.GetField("launchQueued", flags)!.GetValue(null)!)
                        throw new InvalidOperationException("Startup sound or 50% audio output did not start.");
                    var navNames = new[] { "HomeNav", "GameNav", "GeodeNav", "ThemesNav", "SettingsNav", "AboutNav" };
                    var hoverCount = (int)service.GetField("hoverPlayCount", flags)!.GetValue(null)!;
                    foreach (var name in navNames)
                        ((System.Windows.Controls.Button)window.FindName(name)).RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
                    if ((int)service.GetField("hoverPlayCount", flags)!.GetValue(null)! != hoverCount + navNames.Length)
                        throw new InvalidOperationException("Different buttons did not start overlapping hover sounds.");
                    var button = (System.Windows.Controls.Button)window.FindName("HomeNav");
                    var rapid = System.Diagnostics.Stopwatch.StartNew();
                    for (var i = 0; i < 30; i++)
                        button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
                    rapid.Stop();
                    if ((int)service.GetField("hoverPlayCount", flags)!.GetValue(null)! != hoverCount + navNames.Length || rapid.ElapsedMilliseconds > 500)
                        throw new InvalidOperationException("Repeated hover on one button caused excessive playback or delay.");
                    button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    ((System.Windows.Controls.Button)window.FindName("GameNav")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    if ((int)service.GetField("clickPlayCount", flags)!.GetValue(null)! < 2)
                        throw new InvalidOperationException("Rapid click sounds did not overlap.");
                    ((System.Windows.Controls.Button)window.FindName("SettingsNav")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    var page = (System.Windows.Controls.ContentControl)window.FindName("Page");
                    var panel = (System.Windows.Controls.StackPanel)((System.Windows.Controls.ScrollViewer)((System.Windows.Controls.Border)page.Content).Child).Content;
                    var english = originalSettings.Language == "en";
                    var toggleText = originalSettings.SoundsEnabled
                        ? english ? "Turn sounds off" : "Отключить звуки"
                        : english ? "Turn sounds on" : "Включить звуки";
                    var toggle = panel.Children.OfType<System.Windows.Controls.Button>().Single(b => Equals(b.Content, toggleText));
                    toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    if (SettingsStore.Load().SoundsEnabled == originalSettings.SoundsEnabled ||
                        (bool)service.GetField("enabled", flags)!.GetValue(null)! == originalSettings.SoundsEnabled)
                        throw new InvalidOperationException("The sound setting did not save or apply.");
                    Console.WriteLine("sound=launch played; volume=50%; six hover sounds overlap without lag; toggle saved");
                }
                finally { SettingsStore.Save(originalSettings); window.Close(); }
            };
            check.Start(); app.Run(window); return;
        }
        if (args.Length > 1 && args[1] == "startup_animation_check")
        {
            var shell = (FrameworkElement)window.FindName("Shell")!;
            var slide = (TranslateTransform)shell.RenderTransform;
            if (slide.X >= 0) throw new InvalidOperationException("Startup entrance did not begin left of its final position.");
            foreach (var name in new[] { "LanguageButton", "MinimizeButton", "CloseButton" })
            {
                var button = (System.Windows.Controls.Button)window.FindName(name)!;
                button.ApplyTemplate();
                var layer = (System.Windows.Controls.Border)button.Template.FindName("HoverFillLayer", button)!;
                button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
                if (((RectangleGeometry)layer.Clip).Rect.Width != 0) throw new InvalidOperationException(name + " still has hover animation.");
            }
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(390) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (Math.Abs(slide.X) > 1 || Math.Abs(window.Opacity - 1) > .01) throw new InvalidOperationException("Startup entrance did not finish at full size and opacity.");
                Console.WriteLine("startup=left-to-right, 300 ms; header buttons have no hover animation");
                window.Close();
            };
            timer.Start(); app.Run(window); return;
        }
        window.BeginAnimation(Window.OpacityProperty, null);
        window.Opacity = 1;
        if (((FrameworkElement)window.FindName("Shell")!).RenderTransform is TranslateTransform previewSlide) { previewSlide.BeginAnimation(TranslateTransform.XProperty, null); previewSlide.X = 0; }
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
        if (args.Length > 1 && args[1] == "video_ui")
        {
            Console.WriteLine("video-test:start");
            var field = typeof(MainWindow).GetField("homeVideoPlayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var indexField = typeof(MainWindow).GetField("homeVideoIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var tintField = typeof(MainWindow).GetField("homeVideoTint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var first = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            first.Tick += (_, _) =>
            {
                first.Stop();
                var video = (LibVLCSharp.Shared.MediaPlayer?)field.GetValue(window);
                var tint = (System.Windows.Controls.Border?)tintField.GetValue(window);
                var lastError = typeof(MainWindow).GetField("homeVideoLastError", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(window);
                Console.WriteLine($"video-test:first={video?.Media?.Mrl}, playing={video?.IsPlaying}, error={lastError}");
                if (video == null || !video.IsPlaying || video.Volume != 0 || video.Media?.Mrl.EndsWith("01-GRIEF.mp4") != true || (int)indexField.GetValue(window)! != 1 || tint?.Opacity != .5)
                { Environment.ExitCode = 1; window.Close(); return; }
                var frame = new RenderTargetBitmap(1000, 670, 96, 96, PixelFormats.Pbgra32); frame.Render(window);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(frame));
                using (var file = File.Create(args[0])) png.Save(file);
                var second = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(9) };
                second.Tick += (_, _) =>
                {
                    second.Stop();
                    video = (LibVLCSharp.Shared.MediaPlayer?)field.GetValue(window);
                    Console.WriteLine($"video-test:second={video?.Media?.Mrl}, playing={video?.IsPlaying}");
                    if (video?.Media?.Mrl.EndsWith("02-Artemis-13.mp4") != true || tint?.Opacity != .5)
                        throw new InvalidOperationException("Video did not switch after 10 seconds.");
                    Console.WriteLine("video=50% opacity, muted, switched after 10 seconds");
                    ((System.Windows.Controls.Button)window.FindName("GeodeNav")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    if (field.GetValue(window) != null) throw new InvalidOperationException("Video playback continued after leaving Home.");
                    Console.WriteLine("video=stopped on page change");
                    window.Close();
                };
                second.Start();
            };
            first.Start(); app.Run(window); return;
        }
        if (args.Length > 1 && args[1] == "video_all_ui")
        {
            var names = new[] { "01-GRIEF.mp4", "02-Artemis-13.mp4", "03-Society.mp4", "04-Thinking-Space-II.mp4", "05-Amethyst.mp4", "09-ORBIT.mp4", "20-andromeda.mp4" };
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var playerField = typeof(MainWindow).GetField("homeVideoPlayer", flags)!;
            var homeTimer = (System.Windows.Threading.DispatcherTimer?)typeof(MainWindow).GetField("homeVideoTimer", flags)!.GetValue(window);
            if (homeTimer != null) homeTimer.Interval = TimeSpan.FromMinutes(1);
            var advance = typeof(MainWindow).GetMethod("PlayNextHomeVideo", flags)!;
            var index = 0;
            var check = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            check.Tick += (_, _) =>
            {
                var player = (LibVLCSharp.Shared.MediaPlayer?)playerField.GetValue(window);
                if (player?.IsPlaying != true || player.Media?.Mrl.EndsWith(names[index], StringComparison.OrdinalIgnoreCase) != true)
                    throw new InvalidOperationException($"Video failed to play: {names[index]}");
                if (++index == names.Length)
                {
                    check.Stop(); Console.WriteLine("video=all seven clips decoded"); window.Close(); return;
                }
                advance.Invoke(window, null);
            };
            check.Start(); app.Run(window); return;
        }
        if (args.Length > 1 && args[1] == "video_bad_ui")
        {
            var check = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            check.Tick += (_, _) =>
            {
                check.Stop();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var player = typeof(MainWindow).GetField("homeVideoPlayer", flags)!.GetValue(window);
                var disabled = (bool)typeof(MainWindow).GetField("homeVideoDisabled", flags)!.GetValue(window)!;
                if (player != null || !disabled) throw new InvalidOperationException("Broken video did not fall back to the normal card.");
                Console.WriteLine("video=fallback to static card");
                window.Close();
            };
            check.Start(); app.Run(window); return;
        }
        if (args.Length > 1 && args[1] == "video_toggle_ui")
        {
            var check = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            check.Tick += (_, _) =>
            {
                check.Stop();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var buttonField = typeof(MainWindow).GetField("homePreviewButton", flags)!;
                var playerField = typeof(MainWindow).GetField("homeVideoPlayer", flags)!;
                var firstButton = (System.Windows.Controls.Button)buttonField.GetValue(window)!;
                if (previewTestStore!.Load().EnableVideoPreview != true || playerField.GetValue(window) == null)
                    throw new InvalidOperationException("Video did not start with the default configuration.");
                firstButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                if (previewTestStore.Load().EnableVideoPreview || playerField.GetValue(window) != null)
                    throw new InvalidOperationException("Disable preview did not persist and stop playback.");
                var secondButton = (System.Windows.Controls.Button)buttonField.GetValue(window)!;
                if (!Equals(secondButton.Content, "Возобновить предпросмотр"))
                    throw new InvalidOperationException("Resume preview label was not shown.");
                secondButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                if (!previewTestStore.Load().EnableVideoPreview || playerField.GetValue(window) == null)
                    throw new InvalidOperationException("Resume preview did not persist and restart playback.");
                Console.WriteLine("preview=disabled, persisted, resumed");
                window.Close();
            };
            check.Start(); app.Run(window); return;
        }
        if (args.Length > 1 && args[1] == "steam_flyout_video_ui")
        {
            var check = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            check.Tick += (_, _) =>
            {
                check.Stop();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var playerField = typeof(MainWindow).GetField("homeVideoPlayer", flags)!;
                var suspendedField = typeof(MainWindow).GetField("homeVideoSuspendedForSteam", flags)!;
                var flyout = (System.Windows.Controls.Border)window.FindName("SteamFlyout")!;
                if (playerField.GetValue(window) == null) throw new InvalidOperationException("Video was not active before Steam sign-in.");
                typeof(MainWindow).GetMethod("ShowSteamFlyout", flags)!.Invoke(window, null);
                if (playerField.GetValue(window) != null || !(bool)suspendedField.GetValue(window)! || flyout.Visibility != Visibility.Visible)
                    throw new InvalidOperationException("Video was not suspended behind the Steam flyout.");
                window.UpdateLayout();
                var frame = new RenderTargetBitmap(1000, 670, 96, 96, PixelFormats.Pbgra32); frame.Render(window);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(frame));
                using (var file = File.Create(args[0])) png.Save(file);
                typeof(MainWindow).GetMethod("CloseSteamFlyout", flags)!.Invoke(window, [true]);
                if (playerField.GetValue(window) == null || (bool)suspendedField.GetValue(window)! || flyout.Visibility != Visibility.Collapsed)
                    throw new InvalidOperationException("Video did not resume after closing the Steam flyout.");
                Console.WriteLine("steam-flyout=video suspended and resumed");
                window.Close();
            };
            check.Start(); app.Run(window); return;
        }
        if (args.Length > 1 && args[1] is "steam_qr_window_ui" or "steam_qr_fallback_ui")
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var style = (System.Windows.Style)typeof(MainWindow).GetField("actionStyle", flags)!.GetValue(window)!;
            var background = (Brush)typeof(MainWindow).GetField("contentCardBrush", flags)!.GetValue(window)!;
            var accent = (Brush)typeof(MainWindow).GetField("accentBrush", flags)!.GetValue(window)!;
            var qrWindow = new SteamQrWindow("preview", true, window.FontFamily, background, window.Foreground, accent, style, startOnLoad: false)
            { Owner = window, WindowStartupLocation = WindowStartupLocation.Manual, Left = -2000, Top = -2000 };
            qrWindow.Show();
            var website = (System.Windows.Controls.Button)typeof(SteamQrWindow).GetField("websiteButton", flags)!.GetValue(qrWindow)!;
            if (website.Visibility != Visibility.Collapsed) throw new InvalidOperationException("Website fallback should be hidden before QR failure.");
            if (args[1] == "steam_qr_fallback_ui")
            {
                var timer = (System.Windows.Threading.DispatcherTimer)typeof(SteamQrWindow).GetField("qrWaitTimer", flags)!.GetValue(qrWindow)!;
                timer.Interval = TimeSpan.FromMilliseconds(5);
                timer.Start();
                var frame = new System.Windows.Threading.DispatcherFrame();
                var end = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
                end.Tick += (_, _) => { end.Stop(); frame.Continue = false; };
                end.Start();
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                if (website.Visibility != Visibility.Visible) throw new InvalidOperationException("Website fallback did not appear after QR failure.");
            }
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
