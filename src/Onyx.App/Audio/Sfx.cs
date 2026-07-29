using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace Onyx.App.Audio;

/// <summary>
/// Tiny synthesized cockpit sound effects — clicks, beeps, arming chords, a boot
/// sweep and an alarm — generated as in-memory PCM WAV so no asset files ship.
/// </summary>
public static class Sfx
{
    public static bool Enabled = false;
    private const int Rate = 44100;

    private static readonly byte[] ClickWav = Build(30, t => Sine(t, 1500) * Decay(t, 30));
    private static readonly byte[] BeepWav = Build(70, t => Sine(t, 900) * Decay(t, 70));
    private static readonly byte[] ArmWav = Build(170, t => Sine(t, t < 0.08 ? 520 : 880) * Decay(t, 170));
    private static readonly byte[] BootWav = Build(430, t => Sine(t, 300 + 1500 * t) * Decay(t, 430));
    private static readonly byte[] AlarmWav = Build(200, t => Sine(t, ((int)(t / 0.1) % 2 == 0) ? 780 : 500) * Decay(t, 200));

    public static void Click() => Play(ClickWav);
    public static void Beep() => Play(BeepWav);
    public static void Arm() => Play(ArmWav);
    public static void Boot() => Play(BootWav);
    public static void Alarm() => Play(AlarmWav);

    private static double Sine(double t, double f) => Math.Sin(2 * Math.PI * f * t);
    private static double Decay(double t, double ms) => Math.Max(0, 1 - t / (ms / 1000.0));

    private static void Play(byte[] wav)
    {
        if (!Enabled) { return; }
        Task.Run(() =>
        {
            try
            {
                using var p = new SoundPlayer(new MemoryStream(wav));
                p.PlaySync();
            }
            catch { /* audio is best-effort */ }
        });
    }

    private static byte[] Build(int ms, Func<double, double> wave)
    {
        int n = Rate * ms / 1000;
        int dataLen = n * 2;
        using var m = new MemoryStream();
        using var w = new BinaryWriter(m);
        w.Write("RIFF"u8); w.Write(36 + dataLen);
        w.Write("WAVE"u8); w.Write("fmt "u8);
        w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataLen);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate;
            double v = Math.Clamp(wave(t) * 0.35, -1, 1);
            w.Write((short)(v * short.MaxValue));
        }
        w.Flush();
        return m.ToArray();
    }
}
