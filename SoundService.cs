using System.Media;
using System.IO;

namespace Waterline;

public static class SoundService
{
    public static void PlayLog() => Play([520, 760], 75, 300, .16);
    public static void PlayReminder() => Play([620, 820, 1040], 140, 650, .2);

    private static void Play(int[] frequencies, int spacingMs, int durationMs, double volume)
    {
        _ = Task.Run(() =>
        {
            try
            {
                const int sampleRate = 44100;
                var samples = sampleRate * durationMs / 1000;
                using var memory = new MemoryStream();
                using var writer = new BinaryWriter(memory);
                writer.Write("RIFF"u8.ToArray()); writer.Write(36 + samples * 2); writer.Write("WAVEfmt "u8.ToArray());
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate); writer.Write(sampleRate * 2);
                writer.Write((short)2); writer.Write((short)16); writer.Write("data"u8.ToArray()); writer.Write(samples * 2);
                for (var i = 0; i < samples; i++)
                {
                    var time = i / (double)sampleRate;
                    var envelope = Math.Sin(Math.PI * Math.Clamp(i / (double)samples, 0, 1));
                    var value = 0d;
                    for (var note = 0; note < frequencies.Length; note++)
                    {
                        var noteStart = note * spacingMs / 1000d;
                        var noteTime = time - noteStart;
                        if (noteTime is >= 0 and <= .23)
                            value += Math.Sin(2 * Math.PI * frequencies[note] * noteTime) * Math.Sin(Math.PI * noteTime / .23);
                    }
                    writer.Write((short)(short.MaxValue * volume * envelope * Math.Clamp(value, -1, 1)));
                }
                memory.Position = 0;
                using var player = new SoundPlayer(memory);
                player.PlaySync();
            }
            catch { }
        });
    }
}
