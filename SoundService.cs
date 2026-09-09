using System.IO;
using System.Media;
using Waterline.Infrastructure;

namespace Waterline;

public static class SoundService
{
    private static readonly object Gate = new();
    private static SoundPlayer? _activePlayer;
    private static Stream? _activeStream;
    private static int _generation;

    public static void PlayLog() => Play("pack://application:,,,/Assets/waterline-log.wav", WaterlineAudio.CreateLogCue, 270);
    public static void PlayReminder() => Play("pack://application:,,,/Assets/waterline-reminder.wav", WaterlineAudio.CreateReminderCue, 660);

    public static void Stop()
    {
        lock (Gate)
        {
            _generation++;
            _activePlayer?.Stop();
            _activePlayer?.Dispose();
            _activeStream?.Dispose();
            _activePlayer = null;
            _activeStream = null;
        }
    }

    private static void Play(string resourceUri, Func<byte[]> fallback, int durationMilliseconds)
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(new Uri(resourceUri));
            var bytes = resource is null ? fallback() : ReadAll(resource.Stream);
            lock (Gate)
            {
                StopActive();
                var generation = ++_generation;
                _activeStream = new MemoryStream(bytes, writable: false);
                _activePlayer = new SoundPlayer(_activeStream);
                _activePlayer.Load();
                _activePlayer.Play();
                _ = ReleaseAfterAsync(generation, durationMilliseconds + 120);
            }
        }
        catch { }
    }

    private static async Task ReleaseAfterAsync(int generation, int delayMilliseconds)
    {
        await Task.Delay(delayMilliseconds).ConfigureAwait(false);
        lock (Gate)
        {
            if (generation != _generation) return;
            StopActive();
        }
    }

    private static byte[] ReadAll(Stream source)
    {
        using (source)
        using (var memory = new MemoryStream())
        {
            source.CopyTo(memory);
            return memory.ToArray();
        }
    }

    private static void StopActive()
    {
        _activePlayer?.Stop();
        _activePlayer?.Dispose();
        _activeStream?.Dispose();
        _activePlayer = null;
        _activeStream = null;
    }
}
