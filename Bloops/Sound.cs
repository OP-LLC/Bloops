using System.IO;
using System.Windows.Media;

namespace Bloops;

// Tiny sound effects. Wavs ship inside the exe and get copied to %APPDATA%\Bloops\sounds (MediaPlayer needs a file).
// Only the "done" ding plays by default; the rest need "All sounds" on.
public static class Sound
{
    static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bloops");
    static readonly string File_ = Path.Combine(Dir, "sound.txt");
    public static double Volume = 0.5;
    public static bool Mute, All;
    static readonly List<MediaPlayer> playing = new(); // keep a reference until done, or it gets collected mid-sound

    public static void Load()
    {
        try { var p = File.ReadAllText(File_).Split(';'); Volume = double.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture); Mute = p[1] == "1"; All = p[2] == "1"; } catch { }
        try
        {
            Directory.CreateDirectory(Path.Combine(Dir, "sounds"));
            foreach (var n in new[] { "spawn", "poof", "done", "needs_you", "error" })
                using (var s = typeof(Sound).Assembly.GetManifestResourceStream($"{n}.wav")!) using (var f = File.Create(Path.Combine(Dir, "sounds", n + ".wav"))) s.CopyTo(f);
        }
        catch { }
    }

    public static void Save()
    {
        try { File.WriteAllText(File_, $"{Volume.ToString(System.Globalization.CultureInfo.InvariantCulture)};{(Mute ? 1 : 0)};{(All ? 1 : 0)}"); } catch { }
    }

    public static void Play(string name)
    {
        if (Mute || Volume <= 0 || (!All && name != "done")) return;
        var p = new MediaPlayer { Volume = Volume };
        p.MediaEnded += (_, _) => { p.Close(); playing.Remove(p); };
        p.MediaFailed += (_, _) => playing.Remove(p);
        playing.Add(p);
        p.Open(new Uri(Path.Combine(Dir, "sounds", name + ".wav")));
        p.Play();
    }
}
