using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Launcher.UI;

// One cached, mixed audio stream avoids restarting the Windows sound device on every hover.
internal static class ButtonSoundService
{
    private static readonly WaveFormat Format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    private static WaveOutEvent? output;
    private static MixingSampleProvider? mixer;
    private static VolumeSampleProvider? gain;
    private static AudioClip? hover;
    private static AudioClip? click;
    private static AudioClip? launch;
    private static bool registered;
    private static bool enabled = true;
    private static bool launchPlayed;
    private static long hoverBusyUntil;
    private static long clickBusyUntil;
    private static long startupHoverGateUntil;

    public static void Register()
    {
        if (registered) return;
        registered = true;
        EventManager.RegisterClassHandler(typeof(Button), UIElement.MouseEnterEvent,
            new MouseEventHandler((sender, _) => { if (sender is Button { IsEnabled: true }) PlayHover(); }));
        EventManager.RegisterClassHandler(typeof(Button), ButtonBase.ClickEvent,
            new RoutedEventHandler((sender, _) => { if (sender is Button { IsEnabled: true }) PlayClick(); }));
    }

    public static void Configure(string? hoverPath, string? clickPath, string? launchPath, bool soundsEnabled)
    {
        hover = AudioClip.TryLoad(hoverPath);
        click = AudioClip.TryLoad(clickPath);
        launch = AudioClip.TryLoad(launchPath);
        mixer?.RemoveAllMixerInputs();
        hoverBusyUntil = clickBusyUntil = 0;
        if (hover != null || click != null || launch != null) EnsureOutput();
        SetEnabled(soundsEnabled);
    }

    public static void SetEnabled(bool value)
    {
        enabled = value;
        if (gain != null) gain.Volume = value ? 0.5f : 0f;
        if (!value) mixer?.RemoveAllMixerInputs();
    }

    public static void PlayLaunch()
    {
        if (launchPlayed) return;
        launchPlayed = true;
        if (!enabled || launch == null || !Play(launch)) return;
        startupHoverGateUntil = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 2;
    }

    private static void PlayHover()
    {
        if (!enabled || hover == null) return;
        var now = Stopwatch.GetTimestamp();
        if (now < startupHoverGateUntil || now < hoverBusyUntil) return;
        if (Play(hover)) hoverBusyUntil = now + (long)(hover.Duration.TotalSeconds * Stopwatch.Frequency);
    }

    private static void PlayClick()
    {
        if (!enabled || click == null) return;
        var now = Stopwatch.GetTimestamp();
        if (now < clickBusyUntil) return;
        if (Play(click)) clickBusyUntil = now + (long)(click.Duration.TotalSeconds * Stopwatch.Frequency);
    }

    private static bool Play(AudioClip clip)
    {
        if (mixer == null || output == null) return false;
        try
        {
            if (output.PlaybackState != PlaybackState.Playing) output.Play();
            mixer.AddMixerInput(new ClipSampleProvider(clip));
            return true;
        }
        catch (Exception) { return false; } // Audio device problems must not block the UI.
    }

    private static void EnsureOutput()
    {
        if (output != null) return;
        var device = new WaveOutEvent { DesiredLatency = 80, NumberOfBuffers = 2 };
        try
        {
            var mixed = new MixingSampleProvider(Format) { ReadFully = true };
            var volume = new VolumeSampleProvider(mixed) { Volume = 0.5f };
            device.Init(volume.ToWaveProvider16());
            device.Play();
            mixer = mixed;
            gain = volume;
            output = device;
        }
        catch (Exception) { device.Dispose(); } // Keep the launcher usable without an audio device.
    }

    public static void Dispose()
    {
        output?.Dispose();
        output = null;
        mixer = null;
        gain = null;
        hover = click = launch = null;
    }

    private sealed class AudioClip
    {
        public float[] Samples { get; }
        public TimeSpan Duration => TimeSpan.FromSeconds(Samples.Length / (44100d * 2));

        private AudioClip(float[] samples) => Samples = samples;

        public static AudioClip? TryLoad(string? path)
        {
            if (path == null || !File.Exists(path)) return null;
            try
            {
                if (new FileInfo(path).Length > 5_000_000) return null;
                using var reader = new WaveFileReader(path);
                ISampleProvider samples = reader.ToSampleProvider();
                if (samples.WaveFormat.Channels == 1) samples = new MonoToStereoSampleProvider(samples);
                else if (samples.WaveFormat.Channels != 2) return null;
                if (samples.WaveFormat.SampleRate != 44100) samples = new WdlResamplingSampleProvider(samples, 44100);
                var data = new List<float>();
                var buffer = new float[8192];
                int read;
                while ((read = samples.Read(buffer, 0, buffer.Length)) > 0)
                    for (var i = 0; i < read; i++) data.Add(buffer[i]);
                return data.Count == 0 ? null : new AudioClip(data.ToArray());
            }
            catch (Exception) { return null; }
        }
    }

    private sealed class ClipSampleProvider(AudioClip clip) : ISampleProvider
    {
        private int position;
        public WaveFormat WaveFormat => Format;

        public int Read(float[] buffer, int offset, int count)
        {
            var available = Math.Min(count, clip.Samples.Length - position);
            if (available <= 0) return 0;
            Array.Copy(clip.Samples, position, buffer, offset, available);
            position += available;
            return available;
        }
    }
}
