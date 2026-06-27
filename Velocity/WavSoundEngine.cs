using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace Pseudo3DRacer;

public enum Sfx { Go, Drift, RankUp, PickUp, WallHit, Finish, Achievement, DailyComplete, Engine }

public static class WavSoundEngine
{
    private static readonly Dictionary<Sfx, byte[]> _cache = new();
    private static bool _enabled = true;
    private static float _lastEngineTime;

    public static bool Enabled { get => _enabled; set => _enabled = value; }

    public static byte[] GenerateTone(float freq, float duration, float volume = 0.3f, bool noise = false)
    {
        int sampleRate = 22050;
        int samples = (int)(sampleRate * duration);
        var data = new byte[44 + samples * 2];
        WriteWavHeader(data, samples, sampleRate);

        var rng = new Random(42);
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float sample;
            if (noise)
                sample = ((float)rng.NextDouble() * 2 - 1) * volume * (1 - t / duration);
            else
                sample = (float)Math.Sin(2 * Math.PI * freq * t) * volume * (1 - t / duration);
            short s = (short)(sample * 32767);
            data[44 + i * 2] = (byte)(s & 0xFF);
            data[44 + i * 2 + 1] = (byte)(s >> 8);
        }
        return data;
    }

    private static void WriteWavHeader(byte[] data, int samples, int sampleRate)
    {
        void W(int o, string s) { for (int i = 0; i < 4; i++) data[o + i] = (byte)s[i]; }
        void I(int o, int v) { data[o] = (byte)v; data[o + 1] = (byte)(v >> 8); data[o + 2] = (byte)(v >> 16); data[o + 3] = (byte)(v >> 24); }
        W(0, "RIFF"); I(4, 36 + samples * 2); W(8, "WAVE"); W(12, "fmt ");
        I(16, 16); I(20, 1); I(22, 1); I(24, sampleRate); I(28, sampleRate * 2);
        I(32, 2); I(34, 16); W(36, "data"); I(40, samples * 2);
    }

    private static byte[] Get(Sfx sfx)
    {
        if (_cache.TryGetValue(sfx, out var cached)) return cached;
        byte[] wav = sfx switch
        {
            Sfx.Go => GenerateTone(880, 0.15f, 0.4f),
            Sfx.Drift => GenerateTone(200, 0.1f, 0.2f, true),
            Sfx.RankUp => GenerateTone(660, 0.2f, 0.35f),
            Sfx.PickUp => GenerateTone(1200, 0.08f, 0.3f),
            Sfx.WallHit => GenerateTone(120, 0.15f, 0.4f, true),
            Sfx.Finish => GenerateTone(523, 0.4f, 0.35f),
            Sfx.Achievement => GenerateTone(784, 0.3f, 0.35f),
            Sfx.DailyComplete => GenerateTone(988, 0.35f, 0.35f),
            Sfx.Engine => GenerateTone(80, 0.05f, 0.1f, true),
            _ => GenerateTone(440, 0.1f)
        };
        _cache[sfx] = wav;
        return wav;
    }

    public static void Play(Sfx sfx)
    {
        if (!_enabled) return;
        Task.Run(() =>
        {
            try
            {
                using var ms = new MemoryStream(Get(sfx));
                using var player = new SoundPlayer(ms);
                player.PlaySync();
            }
            catch { }
        });
    }

    public static void PlayEngine(float speed)
    {
        if (!_enabled || speed < 0.05f) return;
        float now = Environment.TickCount / 1000f;
        if (now - _lastEngineTime < 0.08f) return;
        _lastEngineTime = now;
        Play(Sfx.Engine);
    }
}

public static class SoundManager
{
    public static void Play(Sfx sfx) => WavSoundEngine.Play(sfx);
}
