using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Launcher.UI;

// WPF class handlers also cover buttons created later in separate windows.
internal static class ButtonSoundService
{
    private static SoundPlayer? hover;
    private static SoundPlayer? click;
    private static SoundPlayer? launch;
    private static bool registered;

    public static void Register()
    {
        if (registered) return;
        registered = true;
        EventManager.RegisterClassHandler(typeof(Button), UIElement.MouseEnterEvent,
            new MouseEventHandler((sender, _) => { if (sender is Button { IsEnabled: true }) Play(hover); }));
        EventManager.RegisterClassHandler(typeof(Button), ButtonBase.ClickEvent,
            new RoutedEventHandler((sender, _) => { if (sender is Button { IsEnabled: true }) Play(click); }));
    }

    public static void Configure(string? hoverPath, string? clickPath, string? launchPath)
    {
        hover?.Dispose();
        click?.Dispose();
        launch?.Dispose();
        hover = Load(hoverPath);
        click = Load(clickPath);
        launch = Load(launchPath);
    }

    public static void PlayLaunch() => Play(launch);

    public static void Dispose()
    {
        hover?.Dispose();
        click?.Dispose();
        launch?.Dispose();
        hover = click = launch = null;
    }

    private static SoundPlayer? Load(string? path)
    {
        if (path is null || !File.Exists(path)) return null;
        SoundPlayer? player = null;
        try
        {
            player = new SoundPlayer(path);
            player.Load();
            return player;
        }
        catch (Exception) { player?.Dispose(); return null; } // Optional audio must not prevent the UI from opening.
    }

    private static void Play(SoundPlayer? player)
    {
        try { player?.Play(); }
        catch (Exception) { } // A broken effect should not interrupt a button click.
    }
}
