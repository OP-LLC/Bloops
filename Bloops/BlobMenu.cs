using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Bloops;

// Small popup: hue wheel + dismiss/quit. Closes when it loses focus.
public class BlobMenu : Window
{
    const int Size = 140;
    static int lastTab; // reopen on the tab you used last
    static readonly SolidColorBrush Bg = new(Color.FromRgb(0x1a, 0x16, 0x24)), Accent = new(Color.FromRgb(0x8b, 0x5c, 0xf6));

    public BlobMenu(Blob blob, int hatRoom)
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; SizeToContent = SizeToContent.WidthAndHeight;
        Left = -9999; // placed above the blob once our size is known

        var wheel = new System.Windows.Controls.Image { Source = Wheel(), Width = Size, Height = Size, Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 8) };
        wheel.MouseLeftButtonDown += (_, e) =>
        {
            var p = e.GetPosition(wheel);
            double dx = p.X - Size / 2.0, dy = p.Y - Size / 2.0;
            if (Math.Sqrt(dx * dx + dy * dy) > Size / 2.0) return;
            blob.PickColor(Blob.FromHue((Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360));
        };

        // Tabs: each page is built once; the tab bar swaps which one shows. Same width for all so the popup doesn't jump.
        var pages = new (string name, UIElement page)[]
        {
            ("Color", new StackPanel { Children = {
                // black + white sit in the wheel's empty bottom corners, above purple and teal
                new Grid { Children = { wheel, Dot(blob, "#3A3A42", HorizontalAlignment.Left), Dot(blob, "#FFFFFF", HorizontalAlignment.Right) } },
                Swatches(blob) } }),
            ("Hats", HatRow(blob)),
            ("Settings", new StackPanel { Children = { SpeedRow(blob), SoundRow(), StartupBox(), HooksBtn(),
                Btn("Dismiss Bloop", () => { blob.Dismiss(); Close(); }), Btn("Quit Bloops", () => blob.QuitAll()),
                new TextBlock { Text = $"v{typeof(App).Assembly.GetName().Version!.ToString(3)}", FontSize = 9, Foreground = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) } } }),
        };
        var body = new ContentControl { Width = Size + 10, MinHeight = Size + 40 };
        var bar = new UniformGrid { Rows = 1, Margin = new Thickness(0, 0, 0, 10) };
        void Show(int i)
        {
            lastTab = i;
            body.Content = pages[i].page;
            for (int j = 0; j < bar.Children.Count; j++)
            {
                var t = (TextBlock)bar.Children[j];
                t.Foreground = j == i ? Brushes.White : Brushes.Gray;
                t.TextDecorations = j == i ? TextDecorations.Underline : null;
            }
        }
        for (int i = 0; i < pages.Length; i++)
        {
            int k = i;
            var t = new TextBlock { Text = pages[i].name, FontSize = 11, Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(4, 2, 4, 2) };
            t.MouseLeftButtonDown += (_, _) => Show(k);
            bar.Children.Add(t);
        }
        Show(lastTab);

        Content = new Border
        {
            Background = Bg, CornerRadius = new CornerRadius(10), Padding = new Thickness(12), BorderBrush = Accent, BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Children =
                {
                    bar,
                    body,
                    new TextBlock { Text = "Bloops by Online Perseverance (｡•ᴗ•｡)", FontSize = 9, Foreground = Brushes.Gray, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center }
                }
            }
        };
        // Closing the menu deactivates it; closing again mid-close throws and took the whole app down.
        bool closing = false;
        Closing += (_, _) => closing = true;
        Deactivated += (_, _) => { if (!closing) Close(); };
        Loaded += (_, _) =>
        {
            var wa = SystemParameters.WorkArea;
            Left = Math.Clamp(blob.Left + (blob.ActualWidth - ActualWidth) / 2, wa.Left, wa.Right - ActualWidth);
            Top = blob.Top + 30 + (blob.Hat is null or "none" && Blob.Holiday(DateTime.Today) == null ? hatRoom : 0) - ActualHeight; // skip the empty headroom above the blob
            if (Top < wa.Top) Top = blob.Top + blob.ActualHeight; // no room above -> go below
            Activate();
        };
    }

    // One-click exact colors.
    static readonly string[] Presets = { "#A166F7", "#4ADE80", "#3B82F6", "#FCAB45", "#F472B6", "#2DD4BF" }; // purple, green, blue, orange, pink, teal

    static WrapPanel Swatches(Blob blob)
    {
        var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var hex in Presets)
        {
            row.Children.Add(Dot(blob, hex));
        }
        return row;
    }

    static Border Dot(Blob blob, string hex, HorizontalAlignment? corner = null)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        var dot = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), Margin = new Thickness(2), Background = new SolidColorBrush(c), Cursor = Cursors.Hand, ToolTip = hex,
            BorderBrush = Brushes.Gray, BorderThickness = new Thickness(corner != null ? 1 : 0) };
        if (corner is { } h) { dot.HorizontalAlignment = h; dot.VerticalAlignment = VerticalAlignment.Bottom; dot.Margin = new Thickness(6, 0, 6, 8); }
        dot.MouseLeftButtonDown += (_, _) => blob.PickColor(c);
        return dot;
    }

    // Hat picker: "none" + every hat. Holiday hats show by date unless a hat (or none) is picked.
    static WrapPanel HatRow(Blob blob)
    {
        var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, MaxWidth = Size + 10 };
        foreach (var h in Blob.Hats.Keys.Prepend("none"))
        {
            var cell = new Border { Width = 26, Height = 26, Margin = new Thickness(1), CornerRadius = new CornerRadius(4), Cursor = Cursors.Hand, ToolTip = h,
                Background = new SolidColorBrush(Color.FromRgb(0x2a, 0x24, 0x38)), BorderBrush = Accent, BorderThickness = new Thickness(blob.Hat == h ? 1 : 0) };
            if (h == "none") cell.Child = new TextBlock { Text = "×", Foreground = Brushes.Gray, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            else
            {
                var img = new System.Windows.Controls.Image { Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/hat_{h}.png")), Width = 22, Height = 22, Stretch = Stretch.Uniform };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
                cell.Child = img;
            }
            cell.MouseLeftButtonDown += (_, _) =>
            {
                blob.PickHat(h);
                foreach (Border b in row.Children) b.BorderThickness = new Thickness(b == cell ? 1 : 0);
            };
            row.Children.Add(cell);
        }
        return row;
    }

    static StackPanel SpeedRow(Blob blob)
    {
        var label = new TextBlock { Foreground = Brushes.White, FontSize = 11, Text = $"Speed {App.Speed:0.0}x" };
        var slider = new Slider { Minimum = 0.3, Maximum = 1.5, Value = App.Speed, TickFrequency = 0.1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 2, 0, 6) };
        slider.ValueChanged += (_, e) => { label.Text = $"Speed {e.NewValue:0.0}x"; blob.SetSpeed(e.NewValue); };
        return new StackPanel { Children = { label, slider } };
    }

    // Volume slider + "All sounds" (off = only the done ding) + Mute.
    static StackPanel SoundRow()
    {
        var label = new TextBlock { Foreground = Brushes.White, FontSize = 11, Text = $"Volume {Sound.Volume * 100:0}%" };
        var slider = new Slider { Minimum = 0, Maximum = 1, Value = Sound.Volume, TickFrequency = 0.05, IsSnapToTickEnabled = true, Margin = new Thickness(0, 2, 0, 4) };
        slider.ValueChanged += (_, e) => { label.Text = $"Volume {e.NewValue * 100:0}%"; Sound.Volume = e.NewValue; Sound.Save(); };
        slider.PreviewMouseLeftButtonUp += (_, _) => { if (!Sound.Mute) { bool a = Sound.All; Sound.All = true; Sound.Play("done"); Sound.All = a; } }; // preview
        CheckBox Box(string text, bool on, Action<bool> set, string? tip = null)
        {
            var c = new CheckBox { Content = text, ToolTip = tip, IsChecked = on, Foreground = Brushes.White, FontSize = 11, Margin = new Thickness(0, 0, 0, 4) };
            c.Click += (_, _) => { set(c.IsChecked == true); Sound.Save(); };
            return c;
        }
        return new StackPanel { Children = { label, slider,
            Box("All sounds", Sound.All, v => Sound.All = v, "Off = only the done ding plays"), Box("Mute", Sound.Mute, v => Sound.Mute = v) } };
    }

    static CheckBox StartupBox()
    {
        var c = new CheckBox { Content = "Start with Windows", IsChecked = App.StartsWithWindows, Foreground = Brushes.White, FontSize = 11, Margin = new Thickness(0, 0, 0, 6) };
        c.Click += (_, _) => App.StartsWithWindows = c.IsChecked == true;
        return c;
    }

    // One button that flips between connect / disconnect.
    static Button HooksBtn()
    {
        Button b = null!;
        b = Btn(App.HooksInstalled ? "Disconnect from Claude" : "Connect to Claude", () =>
        {
            App.RunHooks(App.HooksInstalled ? "uninstall" : "install");
            b.Content = App.HooksInstalled ? "Disconnect from Claude" : "Connect to Claude";
        });
        return b;
    }

    static Button Btn(string text, Action click)
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 2, 0, 2), Padding = new Thickness(6, 3, 6, 3), Background = Accent, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
        b.Click += (_, _) => click();
        return b;
    }

    // Hue ring: angle = hue, full saturation, transparent outside the circle.
    static BitmapSource Wheel()
    {
        var px = new byte[Size * Size * 4];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                double dx = x - Size / 2.0, dy = y - Size / 2.0, r = Math.Sqrt(dx * dx + dy * dy);
                if (r > Size / 2.0 || r < Size / 4.0) continue;
                double h = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360, c = 1, xx = 1 - Math.Abs(h / 60 % 2 - 1);
                var (R, G, B) = h < 60 ? (c, xx, 0d) : h < 120 ? (xx, c, 0d) : h < 180 ? (0d, c, xx) : h < 240 ? (0d, xx, c) : h < 300 ? (xx, 0d, c) : (c, 0d, xx);
                int i = (y * Size + x) * 4;
                px[i] = (byte)(B * 255); px[i + 1] = (byte)(G * 255); px[i + 2] = (byte)(R * 255); px[i + 3] = 255;
            }
        return BitmapSource.Create(Size, Size, 96, 96, PixelFormats.Bgra32, null, px, Size * 4);
    }
}
