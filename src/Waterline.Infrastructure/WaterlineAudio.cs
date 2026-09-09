namespace Waterline.Infrastructure;

public static class WaterlineAudio
{
    private const int SampleRate = 44_100;

    public static byte[] CreateLogCue() => CreateWave(.27, time =>
    {
        var drop = Note(time, 0, .19, 510 + 210 * time, 9.5);
        var glass = .58 * Note(time, .055, .18, 790, 11);
        var ripple = .22 * Note(time, .025, .22, 245, 8);
        return (drop + glass + ripple) * .17;
    });

    public static byte[] CreateReminderCue() => CreateWave(.66, time =>
    {
        var first = Note(time, 0, .24, 560, 7.8);
        var second = Note(time, .17, .24, 710, 7.8);
        var third = Note(time, .35, .27, 920, 7.2);
        var warmth = .16 * Note(time, 0, .56, 280, 4.5);
        return (first + second * .9 + third * .82 + warmth) * .15;
    });

    private static double Note(double time, double start, double length, double frequency, double decay)
    {
        var local = time - start;
        if (local is < 0 || local > length) return 0;
        var attack = Math.Min(1, local / .009);
        var release = Math.Min(1, (length - local) / .035);
        var envelope = attack * release * Math.Exp(-decay * local);
        var phase = 2 * Math.PI * frequency * local;
        return (Math.Sin(phase) + .12 * Math.Sin(phase * 2.01)) * envelope;
    }

    private static byte[] CreateWave(double durationSeconds, Func<double, double> sample)
    {
        var sampleCount = (int)Math.Round(SampleRate * durationSeconds);
        var values = new double[sampleCount];
        var peak = 0d;
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = sample(index / (double)SampleRate);
            peak = Math.Max(peak, Math.Abs(values[index]));
        }
        var gain = peak > .28 ? .28 / peak : 1;

        using var memory = new MemoryStream(44 + sampleCount * 2);
        using var writer = new BinaryWriter(memory);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + sampleCount * 2);
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(sampleCount * 2);
        foreach (var value in values)
            writer.Write((short)Math.Round(Math.Clamp(value * gain, -1, 1) * short.MaxValue));
        return memory.ToArray();
    }
}
