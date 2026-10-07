using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Bloops;

// One blob = one Claude chat (session_id). Transparent pixels are click-through for free (layered window).
// A "mode" is a looping activity (reading, sleeping...). One-shots (done, fail, pet...) play on top, then the mode resumes.
public class Blob : Window
{
    static readonly (double h, double s, double v) Base = Hsv(Color.FromRgb(0xFC, 0xAB, 0x45)); // main body color of buddy_base_v1
    const int Scale = 2, Px = 64 * Scale, HatRoom = 20; // frames get 20 empty rows on top so hats fit
    static readonly Dictionary<string, BitmapSource[]> Sheets = new();
    // hat -> rows it sinks into the head
    public static readonly Dictionary<string, int> Hats = new() { ["party"] = 8, ["wizard"] = 7, ["crown"] = 4, ["headphones"] = 22, ["pumpkin"] = 6, ["santa"] = 5, ["halo"] = -4, ["goggles"] = 13, ["duck"] = 4, ["cloud"] = -3, ["moon"] = -3, ["sprout"] = 3, ["orca"] = 4, ["fin"] = 3,
        ["choir"] = 2, ["dino"] = 7, ["pi"] = 3, ["hardhat"] = 8, ["grad"] = 11, ["chef"] = 2, ["cone"] = 3, ["mushroom"] = 3, ["frog"] = 5, ["captain"] = 3, ["donut"] = 5, ["op"] = 3 };
    static readonly Random Rng = new();

    // mode -> (intro strip played once, loop strip, fps)
    static readonly Dictionary<string, (string? intro, string loop, double fps)> Modes = new()
    {
        ["idle"] = (null, "idle", 8), ["chill"] = (null, "chill", 6), ["sleep"] = ("sleep_in", "snooze", 4),
        ["read"] = ("read_in", "read", 8), ["type"] = ("type_in", "type", 12), ["run"] = (null, "run", 10), ["rage"] = ("rage_in", "rage", 10),
        ["browse"] = ("browse_in", "browse", 8), ["think"] = ("think_in", "think", 8), ["sweat"] = ("sweat_in", "sweat", 10),
        ["needs"] = ("ask_in", "ask", 8), ["bucket"] = ("bucket_in", "bucket", 8),
        ["flag"] = ("flag_in", "flag", 8), // chat finished: waves a flag (in its color) until seen
    };
    static readonly string[] Extras = { "yawn", "juggle", "look" };
    static readonly string[] WorkModes = { "read", "type", "run", "rage", "browse", "think", "sweat", "bucket" };

    record struct Fx(double Sx = 1, double Sy = 1, double Ty = 0, double Rot = 0) { public static readonly Fx None = new(1); } // new Fx() would be all zeros
    record Seg(string Strip, bool Loop, double Fps, bool Reverse = false, int Count = 0, Func<int, Fx>? Effect = null, Action<int>? Each = null, Action? End = null);

    readonly App app;
    readonly string id;
    readonly System.Windows.Controls.Image img = new() { Width = Px, Height = Px + HatRoom * Scale, VerticalAlignment = VerticalAlignment.Bottom };
    readonly ScaleTransform scale = new() { CenterX = Px / 2.0, CenterY = Px + HatRoom * Scale };
    readonly RotateTransform rot = new() { CenterX = Px / 2.0, CenterY = 0 };
    readonly TranslateTransform move = new();
    readonly DispatcherTimer anim = new();
    Dictionary<string, BitmapSource[]> frames = new();
    Seg seg = new("idle", true, 8);
    int frame;
    bool oneShot, poofing, mirror;
    string mode = "idle";
    DateTime lastEvent = DateTime.Now, nextExtra = DateTime.Now.AddMinutes(2), nextPet = DateTime.MinValue;
    public DateTime NextHighFive = DateTime.Now.AddMinutes(1);
    DateTime? workStart;
    Color color;
    string? hat;           // picked from the menu (saved per chat)
    bool music, askedPermission;
    int agents;            // subagents running right now
    readonly System.Windows.Controls.Image[] minis = new System.Windows.Controls.Image[3];
    readonly DispatcherTimer orbit = new() { Interval = TimeSpan.FromMilliseconds(40) };
    double orbitAngle;
    public string? Hat => hat;
    public double Hue => Hsv(color).h;
    public bool Free => !oneShot && !poofing && mode is "idle" or "chill";

    public Blob(App app, string id, string label, Color color, string? hat, int index)
    {
        this.app = app; this.id = id; this.hat = hat;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize;
        Width = Px + 48; Height = Px + 24 + HatRoom * Scale; // headroom so squish/hops/hats don't clip
        ToolTip = string.IsNullOrEmpty(label) ? "Bloop" : label;
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
        img.RenderTransform = new TransformGroup { Children = { scale, rot, move } };
        var grid = new System.Windows.Controls.Grid();
        System.Windows.Controls.Panel.SetZIndex(img, 1);
        grid.Children.Add(img);
        for (int i = 0; i < minis.Length; i++)
        {
            minis[i] = new() { Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Collapsed, RenderTransform = new TranslateTransform(), IsHitTestVisible = false };
            RenderOptions.SetBitmapScalingMode(minis[i], BitmapScalingMode.NearestNeighbor);
            grid.Children.Add(minis[i]);
        }
        orbit.Tick += (_, _) => Orbit();
        Content = grid;

        Left = SlotLeft(index);
        Top = SystemParameters.WorkArea.Bottom - Height + 20; // feet rest on the taskbar (sprite has ~9 empty rows under them, x2, plus bounce)

        SetColor(color);
        anim.Tick += (_, _) => NextFrame();
        MouseLeftButtonDown += OnMouseDown;
        MouseEnter += (_, _) => { if (Free && DateTime.Now > nextPet) { nextPet = DateTime.Now.AddSeconds(20); Once("pet", 10); } };
        Spawn(); Sound.Play("spawn");
    }

    // Spawn spots along the bottom-right, one per slot.
    public static double SlotLeft(int i) => SystemParameters.WorkArea.Right - (Px + 10) * (i % 8 + 1) - 24;

    static BitmapSource[] Sheet(string name)
    {
        if (Sheets.TryGetValue(name, out var s)) return s;
        var strip = new BitmapImage(new Uri($"pack://application:,,,/Assets/{name}.png"));
        s = Enumerable.Range(0, strip.PixelWidth / 64).Select(i => (BitmapSource)new CroppedBitmap(strip, new Int32Rect(i * 64, 0, 64, 64))).ToArray();
        return Sheets[name] = s;
    }

    BitmapSource[] Strip(string name)
    {
        if (!frames.TryGetValue(name, out var f)) frames[name] = f = Sheet(name).Select(x => Recolor(x, color, name.StartsWith("bucket") ? null : WornHat, fixedHead: name.StartsWith("flag") ? IdleHead : null)).ToArray();
        // bucket goes where the hat would; the flag is body-colored and touches his head, so it uses the idle head spot (that pose doesn't bounce)
        return f;
    }

    public void SetColor(Color c)
    {
        color = c;
        frames.Clear(); // recolored lazily
        img.Source = Frame(frame);
        var mini = Recolor(Sheet("idle")[0], c, null);
        foreach (var m in minis) m.Source = mini;
    }

    // Menu pick wins; no pick -> holiday hat; "none" = bare. Music work forces headphones.
    string? WornHat => music ? "headphones" : hat == "none" ? null : hat ?? Holiday(DateTime.Today);
    public static string? Holiday(DateTime d) => d is { Month: 10, Day: 31 } ? "pumpkin" : d.Month == 12 ? "santa" : null;

    public void PickHat(string h) { hat = h; frames.Clear(); img.Source = Frame(frame); app.SaveHat(id, h); }
    void SetMusic(bool on) { if (music == on) return; music = on; frames.Clear(); img.Source = Frame(frame); }

    public static (double h, double s, double v) Hsv(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0, max = Math.Max(r, Math.Max(g, b)), d = max - Math.Min(r, Math.Min(g, b));
        double h = d == 0 ? 0 : max == r ? 60 * (((g - b) / d) % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        return ((h + 360) % 360, max == 0 ? 0 : d / max, max);
    }

    public static Color FromHsv(double h, double s, double v)
    {
        h = (h % 360 + 360) % 360;
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        var (r, g, b) = h < 60 ? (c, x, 0d) : h < 120 ? (x, c, 0d) : h < 180 ? (0d, c, x) : h < 240 ? (0d, x, c) : h < 300 ? (x, 0d, c) : (c, 0d, x);
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    // Body-shaded color for a wheel hue (same saturation/brightness as the original orange).
    public static Color FromHue(double hue) => FromHsv(hue, Base.s, Base.v);

    // Palette swap: shift saturated pixels (the body) so its main color becomes `target`, keeping the shading.
    // Eyes/smoke/outline (low saturation) stay as they are.
    // The hat (not recolored) sits on the top of the body, found per frame so it rides along with bounces.
    static BitmapSource Recolor(BitmapSource src, Color target, string? hat, bool room = true, (int, double)? fixedHead = null)
    {
        var (th, ts, tv) = Hsv(target);
        var b = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        var px = new byte[64 * 64 * 4];
        b.CopyPixels(px, 64 * 4, 0);
        var head = hat == null ? null : fixedHead ?? HeadTop(px);
        var orig = hat != null ? (byte[])px.Clone() : null; // pre-recolor, for telling body from props
        for (int i = 0; i < px.Length; i += 4)
        {
            if (px[i + 3] == 0) continue;
            var (h, sat, v) = Hsv(Color.FromRgb(px[i + 2], px[i + 1], px[i]));
            if (sat < 0.3) continue;
            var o2 = FromHsv(h + th - Base.h, Math.Min(1, sat * ts / Base.s), Math.Min(1, v * tv / Base.v));
            px[i + 2] = o2.R; px[i + 1] = o2.G; px[i] = o2.B;
        }
        int top = room ? HatRoom : 0, rows = 64 + top;
        var o = new byte[64 * rows * 4];
        Array.Copy(px, 0, o, top * 64 * 4, px.Length);
        if (head is var (hy, hx) && room)
        {
            var (hw, hh, hp) = HatPixels(hat!);
            int x0 = (int)Math.Round(hx - hw / 2.0), y0 = Math.Max(0, top + hy + Hats[hat!] - hh);
            for (int y = 0; y < hh; y++)
                for (int x = 0; x < hw; x++)
                {
                    int s = (y * hw + x) * 4, dx = x0 + x, dy = y0 + y;
                    if (hp[s + 3] < 128 || dx < 0 || dx >= 64 || dy >= rows) continue;
                    int sy = dy - top;
                    if (sy >= 0 && IsProp(orig!, dx, sy, hy)) continue; // props (bulb, gear, telescope, eyes) stay in front of the hat
                    Array.Copy(hp, s, o, (dy * 64 + dx) * 4, 4);
                }
        }
        var bmp = BitmapSource.Create(64, rows, 96, 96, PixelFormats.Bgra32, null, o, 64 * 4);
        bmp.Freeze();
        return bmp;
    }

    // Top row of the body (orange-ish, saturated, 6+ px wide) and its center x. Props of other hues are ignored.
    static (int y, double x)? HeadTop(byte[] px)
    {
        for (int y = 0; y < 64; y++)
        {
            int n = 0, min = 64, max = -1;
            for (int x = 0; x < 64; x++)
            {
                int i = (y * 64 + x) * 4;
                if (px[i + 3] == 0) continue;
                var (h, s, _) = Hsv(Color.FromRgb(px[i + 2], px[i + 1], px[i]));
                double dh = Math.Abs(h - Base.h);
                if (s < 0.3 || Math.Min(dh, 360 - dh) > 25) continue;
                n++; min = Math.Min(min, x); max = Math.Max(max, x);
            }
            if (n >= 6) return (y, (min + max) / 2.0);
        }
        return null;
    }

    // A prop = anything above the body top, or a non-body-colored pixel inside the body (not the outline: 2px from the edge).
    // Body = orange-ish or the light shine. Hats cover body + outline, but go behind props.
    static bool IsProp(byte[] px, int x, int y, int top)
    {
        bool Solid(int X, int Y) => X >= 0 && X < 64 && Y >= 0 && Y < 64 && px[(Y * 64 + X) * 4 + 3] > 0;
        if (!Solid(x, y)) return false;
        int i = (y * 64 + x) * 4;
        var (h, s, v) = Hsv(Color.FromRgb(px[i + 2], px[i + 1], px[i]));
        if (y < top) return !(y >= top - 2 && v < 0.45); // the head's dark outline just above its top row is body, not a prop (it drew a line through hats)
        double dh = Math.Abs(h - Base.h);
        if ((Math.Min(dh, 360 - dh) <= 30 && s >= 0.12) || v > 0.85) return false;
        return Solid(x + 2, y) && Solid(x - 2, y) && Solid(x, y + 2) && Solid(x, y - 2);
    }

    static (int, double)? idleHead;
    static (int, double)? IdleHead
    {
        get
        {
            if (idleHead == null)
            {
                var b = new FormatConvertedBitmap(Sheet("idle")[0], PixelFormats.Bgra32, null, 0);
                var px = new byte[64 * 64 * 4]; b.CopyPixels(px, 64 * 4, 0);
                idleHead = HeadTop(px);
            }
            return idleHead;
        }
    }

    static readonly Dictionary<string, (int w, int h, byte[] px)> HatCache = new();
    static (int w, int h, byte[] px) HatPixels(string name)
    {
        if (HatCache.TryGetValue(name, out var c)) return c;
        var b = new FormatConvertedBitmap(new BitmapImage(new Uri($"pack://application:,,,/Assets/hat_{name}.png")), PixelFormats.Bgra32, null, 0);
        var px = new byte[b.PixelWidth * b.PixelHeight * 4];
        b.CopyPixels(px, b.PixelWidth * 4, 0);
        return HatCache[name] = (b.PixelWidth, b.PixelHeight, px);
    }

    // ---------- playback ----------

    int Count => seg.Count > 0 ? seg.Count : Strip(seg.Strip).Length;

    BitmapSource Frame(int i)
    {
        var f = Strip(seg.Strip);
        i = Math.Min(i, f.Length - 1);
        return f[seg.Reverse ? f.Length - 1 - i : i];
    }

    void Play(Seg s)
    {
        seg = s; frame = 0;
        RefreshSpeed();
        Show(0);
        anim.Start();
    }

    void Show(int i)
    {
        img.Source = Frame(i);
        var fx = seg.Effect?.Invoke(i) ?? Fx.None;
        scale.ScaleX = fx.Sx * (mirror ? -1 : 1); scale.ScaleY = fx.Sy;
        move.Y = fx.Ty; rot.Angle = fx.Rot;
        seg.Each?.Invoke(i);
    }

    public void RefreshSpeed() => anim.Interval = TimeSpan.FromSeconds(1 / (seg.Fps * App.Speed));

    void NextFrame()
    {
        if (++frame < Count) { Show(frame); return; }
        if (seg.Loop) { frame = 0; Show(0); return; }
        anim.Stop();
        var end = seg.End;
        oneShot = false; mirror = false;
        if (end != null) end(); else StartMode();
    }

    // Busy little hop for hands-on work.
    static Func<int, Fx> Hop(double h) => i => new Fx(Ty: i % 2 == 0 ? -h : 0);
    static readonly Func<int, Fx> Bob = i => new Fx(Rot: i % 4 < 2 ? 4 : -4, Ty: i % 2 == 0 ? -3 : 0); // head bob to the music

    void StartMode()
    {
        if (poofing) return;
        var (intro, loop, fps) = Modes[mode];
        Func<int, Fx>? fx = mode is "type" or "rage" ? Hop(2) : music ? Bob : null;
        var looping = new Seg(loop, true, fps, Effect: fx);
        Play(intro == null ? looping : new Seg(intro, false, fps, End: () => Play(looping)));
    }

    void SetMode(string m)
    {
        if (m == mode) return;
        mode = m;
        if (!oneShot && !poofing) StartMode();
    }

    void Once(string strip, double fps, Func<int, Fx>? fx = null, Action? end = null)
    {
        if (poofing) return;
        oneShot = true;
        Play(new Seg(strip, false, fps, Effect: fx, End: end));
    }

    // Squish on landing (spawn + drop), done in code on the idle frame.
    static readonly Fx[] Squish = { new(1.35, 0.65), new(0.85, 1.2), new(1.1, 0.92), new(0.97, 1.03), Fx.None };
    void Land() { oneShot = true; Play(new Seg("idle", false, 14, Count: Squish.Length, Effect: i => Squish[i])); }

    void Spawn()
    {
        double floor = Top;
        Top = floor - 160;
        oneShot = true;
        // fall in (window moves), then squish
        Play(new Seg("carried", false, 20, Count: 8, Each: i => Top = floor - 160 + 160 * Math.Pow((i + 1) / 8.0, 2), End: Land));
    }

    void Despawn()
    {
        if (poofing) return;
        Once("bye", 8, end: () => { poofing = true; agents = 0; Sound.Play("poof"); Squad(); Play(new Seg("poof", false, 12, End: () => { app.Remove(id); Close(); })); });
    }

    // ---------- events ----------

    static string ToolMode(string tool) => tool switch
    {
        "Read" or "Grep" or "Glob" or "NotebookRead" => "read",
        "Edit" or "Write" or "MultiEdit" or "NotebookEdit" => "type",
        "Bash" => "run",
        "PowerShell" => "rage", // PowerShell 5.1 earns the angry steam face
        "WebFetch" or "WebSearch" => "browse",
        _ => "think"
    };

    string Thinking => workStart != null && DateTime.Now - workStart > App.LongTask ? "sweat" : "think";

    static readonly Func<int, Fx> Pancake = i => i is >= 2 and <= 5 ? new Fx(1.4, 0.45) : Fx.None;
    static readonly Func<int, Fx> BigHop = i => new Fx(Ty: i is 2 or 3 or 4 ? -10 : 0);

    static readonly Func<int, Fx> StressBall = i => i is >= 2 and <= 5 ? new Fx(1.35, 0.55) : i == 6 ? new Fx(0.9, 1.15) : Fx.None;
    static readonly string[] AudioExt = { ".mp3", ".wav", ".mid", ".midi", ".flac", ".ogg", ".m4a", ".aiff" };
    static readonly string[] Downloads = { "curl ", "wget ", "winget ", "invoke-webrequest", "iwr ", "start-bitstransfer", "pip install", "npm install", "npm i ", "git clone", "choco install", "scoop install" };

    // input = raw tool_input JSON (lowercased here), only used to spot commands/file types.
    public void OnEvent(string ev, string tool, string input, bool failed)
    {
        if (poofing) return;
        lastEvent = DateTime.Now;
        input = input.ToLowerInvariant();
        // A permission prompt that ends without the tool running = denied.
        if (askedPermission && ev is "Stop" or "UserPromptSubmit") { askedPermission = false; SetMode("idle"); Once("pout", 8); return; }
        switch (ev)
        {
            case "SessionEnd": Despawn(); break;
            case "Notification": SetMode("needs"); Sound.Play("needs_you"); break;
            case "PermissionRequest": askedPermission = true; Sound.Play("needs_you"); break;
            case "PermissionDenied": Once("pout", 8); break;
            case "PreCompact": Once("squish", 10, StressBall); break;
            case "Stop": workStart = null; SetMusic(false); SetMode("flag"); Once("done", 10, BigHop); Sound.Play("done"); break;
            case "UserPromptSubmit": workStart ??= DateTime.Now; SetMode(Thinking); break;
            case "SubagentStart": agents++; Squad(); Once("split", 10); break;
            case "SubagentStop": agents = Math.Max(0, agents - 1); Squad(); break;
            case "PreToolUse":
                workStart ??= DateTime.Now;
                if (AudioExt.Any(input.Contains)) SetMusic(true);
                var m = ToolMode(tool);
                if (m is "run" or "rage" && Downloads.Any(input.Contains)) m = "bucket";
                SetMode(m == "think" ? Thinking : m);
                if (tool is "Agent" or "Task") Once("split", 10);
                else if (input.Contains("git push")) Once("push", 9);
                else if (input.Contains("fly deploy") || input.Contains("wrangler deploy") || input.Contains("wrangler pages deploy")) Once("rocket", 9);
                break;
            case "PostToolUse" or "PostToolUseFailure":
                askedPermission = false;
                if (failed) { Once("fail", 10, Pancake); Sound.Play("error"); }
                else if (mode is "needs" or "idle" or "chill" or "sleep") SetMode(Thinking); // permission granted, back to work
                break;
        }
    }

    // 3+ subagents at once: mini blobs orbit around him.
    void Squad()
    {
        bool on = agents >= 3;
        foreach (var m in minis) m.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (on) orbit.Start(); else orbit.Stop();
    }

    void Orbit()
    {
        orbitAngle += 0.06 * App.Speed;
        for (int i = 0; i < minis.Length; i++)
        {
            double a = orbitAngle + i * 2 * Math.PI / minis.Length, s = Math.Sin(a);
            var t = (TranslateTransform)minis[i].RenderTransform;
            t.X = Math.Cos(a) * 64; t.Y = -62 + s * 10 + move.Y;
            System.Windows.Controls.Panel.SetZIndex(minis[i], s > 0 ? 2 : 0); // in front on the near side
        }
    }

    // Called every few seconds: idle -> chill -> sleep -> despawn, plus random idle extras.
    public void UpdateIdle()
    {
        if (poofing) return;
        var quiet = DateTime.Now - lastEvent;
        if (quiet >= App.PoofAt) { Despawn(); return; }
        if (quiet >= App.SleepAt) { SetMode("sleep"); return; }
        if (mode == "flag" && quiet >= App.FlagTime) SetMode("idle"); // waved long enough
        else if (mode == "idle" && quiet >= App.HappyTime) SetMode("chill");
        else if (WorkModes.Contains(mode) && quiet >= App.WorkTimeout) { workStart = null; SetMode("chill"); }
        else if (mode == "think" && Thinking == "sweat") SetMode("sweat");
        if (quiet >= App.WorkTimeout) { SetMusic(false); if (agents > 0) { agents = 0; Squad(); } } // missed Stop events can't leave him stuck

        if (Free && DateTime.Now > nextExtra)
        {
            nextExtra = DateTime.Now.AddMinutes(2 + Rng.NextDouble() * 3);
            // 1 in 8: a rare one (dozes off mid-juggle, or a high-five with nobody there)
            if (Rng.Next(8) > 0) Once(Extras[Rng.Next(Extras.Length)], 8);
            else if (Rng.Next(2) == 0) Once("juggle_drop", 8);
            else { mirror = Rng.Next(2) == 0; Once("highfive", 8, end: () => Once("look", 8)); }
        }
    }

    public void HighFive(bool facingLeft) { NextHighFive = DateTime.Now.AddMinutes(3); mirror = facingLeft; Once("highfive", 8); }

    void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        double l = Left, t = Top;
        bool wasOneShot = oneShot;
        if (!poofing) { oneShot = true; Play(new Seg("carried", true, 10, Effect: i => new Fx(Rot: Math.Sin(i * Math.PI / 4) * 10))); }
        DragMove();
        if (poofing) return;
        if (Math.Abs(Left - l) < 3 && Math.Abs(Top - t) < 3) { oneShot = wasOneShot; if (mode == "flag") SetMode("idle"); // first click just puts the flag down
            else { StartMode(); new BlobMenu(this, HatRoom * Scale).Show(); } }
        else Land();
    }

    public void PickColor(Color c) { SetColor(c); app.SaveColor(id, c); }
    public void Dismiss() => Despawn();
    public void QuitAll() => app.Quit();
    public void SetSpeed(double s) => app.SetSpeed(s);
}
