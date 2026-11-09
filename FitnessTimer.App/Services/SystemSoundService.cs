using System.IO;
using System.Media;
using System.Text;

namespace FitnessTimer.App.Services;

public interface ISoundService : IDisposable
{
    void PlayTick();

    void PlayPreparationStart();

    void PlayTrainingStart();

    void PlayRestStart();

    void PlayCompleted();
}

public sealed class SystemSoundService : ISoundService
{
    private readonly object playLock = new();
    private readonly object stateLock = new();
    private bool disposed;
    private SoundPlayer? currentPlayer;

    public void PlayTick()
    {
        PlaySequence(SystemSounds.Beep, new Tone(1100, 80, 0.22));
    }

    public void PlayPreparationStart()
    {
        PlaySequence(
            SystemSounds.Asterisk,
            new Tone(660, 95, 0.20),
            new Tone(880, 130, 0.22));
    }

    public void PlayTrainingStart()
    {
        PlaySequence(
            SystemSounds.Asterisk,
            new Tone(740, 80, 0.22),
            new Tone(980, 85, 0.23),
            new Tone(1240, 150, 0.24));
    }

    public void PlayRestStart()
    {
        PlaySequence(
            SystemSounds.Question,
            new Tone(760, 95, 0.20),
            new Tone(540, 170, 0.19));
    }

    public void PlayCompleted()
    {
        PlaySequence(
            SystemSounds.Exclamation,
            new Tone(784, 90, 0.21),
            new Tone(988, 90, 0.22),
            new Tone(1175, 120, 0.23),
            new Tone(1568, 220, 0.22));
    }

    private void PlaySequence(SystemSound fallback, params Tone[] tones)
    {
        if (IsDisposed())
        {
            return;
        }

        byte[] wave = BuildWave(tones);

        _ = Task.Run(() =>
        {
            try
            {
                lock (playLock)
                {
                    if (IsDisposed())
                    {
                        return;
                    }

                    using MemoryStream stream = new(wave);
                    using SoundPlayer player = new(stream);
                    SetCurrentPlayer(player);

                    try
                    {
                        player.Load();

                        if (!IsDisposed())
                        {
                            player.PlaySync();
                        }
                    }
                    finally
                    {
                        ClearCurrentPlayer(player);
                    }
                }
            }
            catch (Exception)
            {
                if (!IsDisposed())
                {
                    fallback.Play();
                }
            }
        });
    }

    public void Dispose()
    {
        lock (stateLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            currentPlayer?.Stop();
        }
    }

    private bool IsDisposed()
    {
        lock (stateLock)
        {
            return disposed;
        }
    }

    private void SetCurrentPlayer(SoundPlayer player)
    {
        lock (stateLock)
        {
            currentPlayer = player;
        }
    }

    private void ClearCurrentPlayer(SoundPlayer player)
    {
        lock (stateLock)
        {
            if (ReferenceEquals(currentPlayer, player))
            {
                currentPlayer = null;
            }
        }
    }

    private static byte[] BuildWave(IReadOnlyList<Tone> tones)
    {
        using MemoryStream audioData = new();
        using BinaryWriter audioWriter = new(audioData, Encoding.ASCII, leaveOpen: true);

        for (int index = 0; index < tones.Count; index++)
        {
            WriteTone(audioWriter, tones[index]);

            if (index < tones.Count - 1)
            {
                WriteSilence(audioWriter, 35);
            }
        }

        byte[] data = audioData.ToArray();
        using MemoryStream wave = new();
        using BinaryWriter writer = new(wave, Encoding.ASCII, leaveOpen: true);

        WriteAscii(writer, "RIFF");
        writer.Write(36 + data.Length);
        WriteAscii(writer, "WAVE");
        WriteAscii(writer, "fmt ");
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(44100);
        writer.Write(44100 * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        WriteAscii(writer, "data");
        writer.Write(data.Length);
        writer.Write(data);

        return wave.ToArray();
    }

    private static void WriteTone(BinaryWriter writer, Tone tone)
    {
        int sampleCount = DurationToSampleCount(tone.DurationMilliseconds);
        int fadeSamples = Math.Min(sampleCount / 2, DurationToSampleCount(8));
        double phaseStep = 2 * Math.PI * tone.Frequency / 44100;

        for (int index = 0; index < sampleCount; index++)
        {
            double fadeIn = fadeSamples == 0 ? 1 : Math.Min(1, index / (double)fadeSamples);
            double fadeOut = fadeSamples == 0 ? 1 : Math.Min(1, (sampleCount - index) / (double)fadeSamples);
            double envelope = Math.Min(fadeIn, fadeOut);
            double wave = Math.Sin(index * phaseStep) + (Math.Sin(index * phaseStep * 2) * 0.15);
            short sample = (short)Math.Clamp(
                wave / 1.15 * short.MaxValue * tone.Volume * envelope,
                short.MinValue,
                short.MaxValue);

            writer.Write(sample);
        }
    }

    private static void WriteSilence(BinaryWriter writer, int durationMilliseconds)
    {
        int sampleCount = DurationToSampleCount(durationMilliseconds);

        for (int index = 0; index < sampleCount; index++)
        {
            writer.Write((short)0);
        }
    }

    private static int DurationToSampleCount(int durationMilliseconds)
    {
        return Math.Max(1, 44100 * durationMilliseconds / 1000);
    }

    private static void WriteAscii(BinaryWriter writer, string text)
    {
        writer.Write(Encoding.ASCII.GetBytes(text));
    }

    private readonly record struct Tone(double Frequency, int DurationMilliseconds, double Volume);
}
