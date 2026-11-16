using System.Media;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Launcher.UI;

// WPF class handlers cover buttons created later by the version picker and other windows.
internal static class ButtonSoundService
{
    private static SoundPlayer? hover;
    private static SoundPlayer? click;
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

    public static void Configure(string? hoverPath, string? clickPath)
    {
        hover?.Dispose();
        click?.Dispose();
        hover = Load(hoverPath);
        click = Load(clickPath);
    }

    public static void Dispose()
    {
        hover?.Dispose();
        click?.Dispose();
        hover = null;
        click = null;
    }

    private static SoundPlayer? Load(string? path)
    {
        if (path is null || !File.Exists(path)) return null;
        try
        {
            var player = new SoundPlayer(path);
            player.Load();
            return player;
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or TimeoutException)
        {
            return null; // A missing or broken optional sound must not block the launcher.
        }
    }

    private static void Play(SoundPlayer? player)
    {
        try { player?.Play(); }
        catch (Exception error) when (error is IOException or InvalidOperationException) { }
    }
}
