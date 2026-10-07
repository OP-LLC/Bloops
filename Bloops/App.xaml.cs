using System.IO;
using System.Net;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace Bloops;

// Bloops by Online Perseverance. Listens for Claude Code hook events on localhost only.
public partial class App : System.Windows.Application
{
    public const int Port = 47321;
    public static readonly TimeSpan WorkTimeout = TimeSpan.FromMinutes(2);  // no events while "working" -> idle
    public static readonly TimeSpan HappyTime = TimeSpan.FromMinutes(1);    // bounce after a reply, then chill
    public static readonly TimeSpan FlagTime = TimeSpan.FromSeconds(30);   // "done" flag wave, unless clicked or messaged sooner
    public static readonly TimeSpan SleepAt = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan LongTask = TimeSpan.FromMinutes(5);   // working this long -> sweatband
    public static readonly TimeSpan PoofAt = TimeSpan.FromMinutes(60);      // 30 min asleep -> despawn

    readonly Dictionary<string, Blob> blobs = new();
    readonly HttpListener http = new();
    WinForms.NotifyIcon? tray;
    static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bloops");
    static readonly string ColorFile = Path.Combine(DataDir, "colors.json");
    static readonly string SpeedFile = Path.Combine(DataDir, "speed.txt");
    static readonly string HatFile = Path.Combine(DataDir, "hats.json");
    Dictionary<string, string> hats = new(); // session_id -> hat name or "none"
    Dictionary<string, string> colors = new(); // session_id -> "#RRGGBB"
    public static double Speed = 0.8; // animation speed multiplier, set from the blob menu

    protected override void OnStartup(StartupEventArgs e)
    {
        // A bug in one blob must never take every blob down: log it and keep running.
        DispatcherUnhandledException += (_, ex) =>
        {
            ex.Handled = true;
            Log(ex.Exception.ToString());
        };
        Sound.Load(); try { Speed = double.Parse(File.ReadAllText(SpeedFile), System.Globalization.CultureInfo.InvariantCulture); } catch { }
        try
        {
            // Older versions saved a wheel hue (number; body hue was +8 from it). Convert those to hex.
            foreach (var (k, v) in JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(ColorFile)) ?? new())
                colors[k] = v.ValueKind == JsonValueKind.Number ? Hex(Blob.FromHue(v.GetDouble() + 8)) : v.GetString() ?? "";
        }
        catch { }
        try { hats = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(HatFile)) ?? new(); } catch { }

        try
        {
            http.Prefixes.Add($"http://localhost:{Port}/");
            http.Start();
        }
        catch
        {
            System.Windows.MessageBox.Show("Bloops is already running (or port 47321 is busy).", "Bloops");
            Shutdown();
            return;
        }
        _ = Listen();
        if (!HooksInstalled && !File.Exists(Path.Combine(DataDir, "asked-hooks")))
        {
            try { Directory.CreateDirectory(DataDir); File.WriteAllText(Path.Combine(DataDir, "asked-hooks"), ""); } catch { }
            if (System.Windows.MessageBox.Show("Connect Bloops to Claude Code?\n\nThis adds small hooks to ~/.claude/settings.json (a backup is saved). You can remove them any time from a Bloop's Settings tab.",
                "Bloops", MessageBoxButton.YesNo) == MessageBoxResult.Yes) RunHooks("install");
        }

        tray = new WinForms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = "Bloops by Online Perseverance",
            Visible = true,
            ContextMenuStrip = new WinForms.ContextMenuStrip()
        };
        tray.ContextMenuStrip.Items.Add("Test blob", null, (_, _) => Handle("test-" + Guid.NewGuid(), "SessionStart", "test", ""));
        tray.ContextMenuStrip.Items.Add("Open log folder", null, (_, _) => { Directory.CreateDirectory(DataDir); System.Diagnostics.Process.Start("explorer.exe", DataDir); });
        tray.ContextMenuStrip.Items.Add("Quit Bloops", null, (_, _) => Quit());

        var tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        tick.Tick += (_, _) => { foreach (var b in blobs.Values.ToList()) b.UpdateIdle(); HighFives(); };
        tick.Start();
        Watchdog();
    }

    // Freeze watchdog: if the UI thread stops answering for 10s, log it with the last events so a report can say what caused it.
    static readonly Queue<string> recent = new();
    static void Log(string msg) { try { Directory.CreateDirectory(DataDir); File.AppendAllText(Path.Combine(DataDir, "error.log"), $"{DateTime.Now:u} {msg}\n\n"); } catch { } }
    void Watchdog() => new Thread(() =>
    {
        while (true)
        {
            Thread.Sleep(5000);
            var op = Dispatcher.BeginInvoke(() => { });
            if (op.Wait(TimeSpan.FromSeconds(10)) == DispatcherOperationStatus.Completed) continue;
            string[] last; lock (recent) last = recent.ToArray();
            Log($"FROZEN: UI thread not answering for 10s. v{typeof(App).Assembly.GetName().Version!.ToString(3)}. Last events:\n  " + string.Join("\n  ", last));
            op.Wait(); // log once per freeze
        }
    }) { IsBackground = true }.Start();

    public void Quit()
    {
        if (tray != null) tray.Visible = false;
        try { http.Stop(); } catch { }
        Shutdown();
    }

    async Task Listen()
    {
        while (http.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await http.GetContextAsync(); } catch { return; }
            try
            {
                // Browsers send Origin: refuse so web pages can't poke the buddy.
                if (ctx.Request.HttpMethod == "POST" && ctx.Request.Headers["Origin"] == null && ctx.Request.ContentLength64 < 1_000_000)
                {
                    using var doc = await JsonDocument.ParseAsync(ctx.Request.InputStream);
                    var r = doc.RootElement;
                    string? id = r.TryGetProperty("session_id", out var s) ? s.GetString() : null;
                    string ev = r.TryGetProperty("hook_event_name", out var h) ? h.GetString() ?? "" : "";
                    string cwd = r.TryGetProperty("cwd", out var c) ? c.GetString() ?? "" : "";
                    string tool = r.TryGetProperty("tool_name", out var t) ? t.GetString() ?? "" : "";
                    string input = r.TryGetProperty("tool_input", out var ti) ? ti.GetRawText() : "";
                    bool failed = ev == "PostToolUseFailure" || (r.TryGetProperty("tool_response", out var tr) && tr.ValueKind == JsonValueKind.Object
                        && tr.TryGetProperty("exit_code", out var x) && x.ValueKind == JsonValueKind.Number && x.GetInt32() != 0);
                    lock (recent) { recent.Enqueue($"{DateTime.Now:HH:mm:ss} {ev} {tool}"); if (recent.Count > 20) recent.Dequeue(); }
                    if (!string.IsNullOrEmpty(id)) Dispatcher.Invoke(() => Handle(id, ev, cwd, tool, input, failed));
                    ctx.Response.StatusCode = 204;
                }
                else ctx.Response.StatusCode = 403;
            }
            catch { ctx.Response.StatusCode = 400; }
            ctx.Response.Close();
        }
    }

    void Handle(string id, string ev, string cwd, string tool, string input = "", bool failed = false)
    {
        if (!blobs.TryGetValue(id, out var blob))
        {
            if (ev == "SessionEnd") return;
            blob = new Blob(this, id, Path.GetFileName(cwd.TrimEnd('\\', '/')), ChatColor(id), hats.GetValueOrDefault(id), FreeSlot());
            blobs[id] = blob;
            blob.Show();
        }
        blob.OnEvent(ev, tool, input, failed);
    }

    // First spawn spot no blob is standing on (so new blobs don't stack on old ones).
    int FreeSlot()
    {
        int i = 0;
        while (blobs.Values.Any(b => Math.Abs(b.Left - Blob.SlotLeft(i)) < 60)) i++;
        return i;
    }

    static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    // Color belongs to the chat (session_id), never the spawn slot. Auto colors get saved too, so restarts can't reshuffle them.
    Color ChatColor(string id)
    {
        try { return (Color)ColorConverter.ConvertFromString(colors[id]); }
        catch { var c = Blob.FromHue(PickHue()); SaveColor(id, c); return c; }
    }

    // New chat: pick the hue furthest from the blobs on screen, so no two look alike.
    double PickHue()
    {
        var taken = blobs.Values.Select(b => b.Hue).ToList();
        double start = Random.Shared.Next(360);
        double best = start, bestGap = -1;
        for (int i = 0; i < 360; i += 15)
        {
            double h = (start + i) % 360;
            double gap = taken.Count == 0 ? 360 : taken.Min(t => Math.Min(Math.Abs(h - t), 360 - Math.Abs(h - t)));
            if (gap > bestGap + 0.1) { best = h; bestGap = gap; }
        }
        return best;
    }

    public void SaveColor(string id, Color c)
    {
        colors[id] = Hex(c);
        try { Directory.CreateDirectory(DataDir); File.WriteAllText(ColorFile, JsonSerializer.Serialize(colors)); } catch { }
    }

    public void SaveHat(string id, string hat)
    {
        hats[id] = hat;
        try { Directory.CreateDirectory(DataDir); File.WriteAllText(HatFile, JsonSerializer.Serialize(hats)); } catch { }
    }

    // Two idle blobs standing side by side now and then high-five.
    void HighFives()
    {
        var free = blobs.Values.Where(b => b.Free && DateTime.Now > b.NextHighFive).OrderBy(b => b.Left).ToList();
        for (int i = 0; i + 1 < free.Count; i++)
        {
            Blob a = free[i], b = free[i + 1];
            if (b.Left - a.Left < 220 && Math.Abs(a.Top - b.Top) < 60) { a.HighFive(false); b.HighFive(true); i++; }
        }
    }

    public void SetSpeed(double speed)
    {
        Speed = speed;
        foreach (var b in blobs.Values) b.RefreshSpeed();
        try { Directory.CreateDirectory(DataDir); File.WriteAllText(SpeedFile, speed.ToString(System.Globalization.CultureInfo.InvariantCulture)); } catch { }
    }

    public void Remove(string id) => blobs.Remove(id);

    static readonly string ClaudeSettings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");
    public static bool HooksInstalled { get { try { return File.ReadAllText(ClaudeSettings).Contains(Port.ToString()); } catch { return false; } } }

    // The hook scripts ship inside the exe; write one to temp and run it ("install" or "uninstall").
    public static void RunHooks(string which)
    {
        var file = Path.Combine(Path.GetTempPath(), $"bloops-{which}-hooks.ps1");
        using (var s = typeof(App).Assembly.GetManifestResourceStream($"{which}-hooks.ps1")!) using (var f = File.Create(file)) s.CopyTo(f);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("powershell", $"-NoProfile -ExecutionPolicy Bypass -File \"{file}\"") { CreateNoWindow = true })?.WaitForExit(15000);
    }

    // Start with Windows = a value under the per-user Run key.
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool StartsWithWindows
    {
        get { using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue("Bloops") != null; }
        set { using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey); if (value) k.SetValue("Bloops", $"\"{Environment.ProcessPath}\""); else k.DeleteValue("Bloops", false); }
    }
}
