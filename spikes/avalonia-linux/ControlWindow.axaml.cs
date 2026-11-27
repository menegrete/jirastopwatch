using System.Configuration;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AvaloniaSpike;

public partial class ControlWindow : Window
{
    private MiniWindow? _mini;
    private bool _allowClose;

    public ControlWindow()
    {
        InitializeComponent();
        Log.Line += l => Dispatcher.UIThread.Post(() =>
        {
            LogBox.Text += l + Environment.NewLine;
            LogBox.CaretIndex = LogBox.Text.Length;
        });
        Opened += (_, _) => LogEnvironment();
        Closing += (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            Hide();
            Log.Write("Control window hidden (close intercepted)");
        };
    }

    public void AllowClose() { _allowClose = true; _mini?.Close(); }

    public void ShowAndActivate() { Show(); WindowState = WindowState.Normal; Activate(); }

    private void LogEnvironment()
    {
        static string E(string n) => Environment.GetEnvironmentVariable(n) ?? "(unset)";
        var display = Environment.GetEnvironmentVariable("DISPLAY");
        var platform = !string.IsNullOrEmpty(display) ? "X11 (inferred: DISPLAY set; Wayland not requested)"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Win32" : "unknown";
        var sb = new StringBuilder();
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        sb.AppendLine($"XDG_SESSION_TYPE={E("XDG_SESSION_TYPE")}");
        sb.AppendLine($"XDG_CURRENT_DESKTOP={E("XDG_CURRENT_DESKTOP")}");
        sb.AppendLine($"WAYLAND_DISPLAY={E("WAYLAND_DISPLAY")}");
        sb.AppendLine($"DISPLAY={E("DISPLAY")}");
        sb.AppendLine($"Platform: {platform}; PlatformHandle descriptor={TryGetPlatformHandle()?.HandleDescriptor ?? "null"}");
        foreach (var (s, i) in Screens.All.Select((s, i) => (s, i)))
            sb.AppendLine($"Screen {i}: Bounds={s.Bounds} WorkingArea={s.WorkingArea} Scaling={s.Scaling} IsPrimary={s.IsPrimary}");
        EnvBox.Text = sb.ToString();
        foreach (var l in sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
            Log.Write(l.TrimEnd());
        InitSettings();
    }

    public void ToggleMini()
    {
        if (_mini == null) OpenMini(); else CloseMini();
    }

    private void OpenMini()
    {
        if (_mini != null) return;
        _mini = new MiniWindow { Topmost = TopmostBox.IsChecked == true };
        _mini.Closed += (_, _) => { _mini = null; Log.Write("Mini closed"); };
        _mini.Show();
        if (TranspBox.IsChecked == true) _mini.ApplyTransparent(true);
        Log.Write($"Mini opened, Position=({_mini.Position.X},{_mini.Position.Y}) Topmost={_mini.Topmost}");
    }

    private void CloseMini() { _mini?.Close(); }

    private void OnOpenMini(object? s, RoutedEventArgs e) => OpenMini();
    private void OnCloseMini(object? s, RoutedEventArgs e) => CloseMini();

    private void OnCorner(object? s, RoutedEventArgs e)
    {
        if (_mini == null) { Log.Write("Corner: mini not open"); return; }
        var screen = _mini.Screens.ScreenFromWindow(_mini) ?? _mini.Screens.Primary;
        if (screen == null) { Log.Write("Corner: no screen"); return; }
        var wa = screen.WorkingArea;
        var w = (int)Math.Round(_mini.Width * screen.Scaling);
        var h = (int)Math.Round(_mini.Height * screen.Scaling);
        var tag = (string)((Button)s!).Tag!;
        var x = tag.EndsWith('L') ? wa.X : wa.Right - w;
        var y = tag.StartsWith('T') ? wa.Y : wa.Bottom - h;
        Log.Write($"Corner {tag}: screen WorkingArea={wa} scaling={screen.Scaling}");
        _mini.MoveTo(new PixelPoint(x, y), $"corner {tag}");
    }

    private void OnMoveXY(object? s, RoutedEventArgs e)
    {
        if (_mini == null) { Log.Write("Move: mini not open"); return; }
        _mini.MoveTo(new PixelPoint((int)(XBox.Value ?? 0), (int)(YBox.Value ?? 0)), "move XY");
    }

    private void OnTopmostChanged(object? s, RoutedEventArgs e)
    {
        var on = TopmostBox.IsChecked == true;
        if (_mini != null) _mini.Topmost = on;
        Log.Write($"Topmost={on} (mini open={_mini != null})");
    }

    private void OnTranspChanged(object? s, RoutedEventArgs e)
    {
        var on = TranspBox.IsChecked == true;
        if (_mini != null) _mini.ApplyTransparent(on);
        else Log.Write($"Transparent+rounded={on} (will apply when mini opens)");
    }

    // ---- Settings ----
    private string? _stored;

    private void InitSettings()
    {
        try
        {
            var s = SpikeSettings.Instance;
            s.Reload();
            s.RunCount++;
            s.Save();
            LogSettings("startup");
            SettingBox.Text = s.LastValue;
        }
        catch (Exception ex) { Log.Write($"Settings: FAILED {ex}"); }
        if (!LibSecret.IsSupported)
        {
            StoreBtn.IsEnabled = LookupBtn.IsEnabled = ClearBtn.IsEnabled = false;
            SecretNote.Text = "(Linux only)";
        }
    }

    private void LogSettings(string when)
    {
        try
        {
            var path = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
            var s = SpikeSettings.Instance;
            Log.Write($"Settings [{when}]: user.config={path} exists={File.Exists(path)} RunCount={s.RunCount} LastValue='{s.LastValue}'");
            SettingInfo.Text = $"RunCount={s.RunCount}";
        }
        catch (Exception ex) { Log.Write($"Settings [{when}]: FAILED {ex}"); }
    }

    private void OnSaveSetting(object? s, RoutedEventArgs e)
    {
        try { SpikeSettings.Instance.LastValue = SettingBox.Text ?? ""; SpikeSettings.Instance.Save(); LogSettings("save"); }
        catch (Exception ex) { Log.Write($"Settings save FAILED {ex}"); }
    }

    private void OnReloadSetting(object? s, RoutedEventArgs e)
    {
        try { SpikeSettings.Instance.Reload(); SettingBox.Text = SpikeSettings.Instance.LastValue; LogSettings("reload"); }
        catch (Exception ex) { Log.Write($"Settings reload FAILED {ex}"); }
    }

    // ---- Secrets ----
    private void OnStoreSecret(object? s, RoutedEventArgs e)
    {
        try
        {
            var v = SecretBox.Text ?? "";
            var r = LibSecret.Store(v);
            if (r.Ok) _stored = v;
            Log.Write(r.Ok ? $"Secret store OK (length {v.Length})" : $"Secret store FAILED: {r.Error}");
        }
        catch (Exception ex) { Log.Write($"Secret store EXCEPTION {ex.GetType().Name}: {ex.Message}"); }
    }

    private void OnLookupSecret(object? s, RoutedEventArgs e)
    {
        try
        {
            var r = LibSecret.Lookup();
            if (!r.Ok) Log.Write($"Secret lookup FAILED: {r.Error}");
            else if (r.Value == null) Log.Write("Secret lookup OK: not found");
            else Log.Write($"Secret lookup OK (length {r.Value.Length}); matches stored this session={(_stored == null ? "n/a (nothing stored this session)" : (_stored == r.Value).ToString())}; matches textbox={(r.Value == (SecretBox.Text ?? ""))}");
        }
        catch (Exception ex) { Log.Write($"Secret lookup EXCEPTION {ex.GetType().Name}: {ex.Message}"); }
    }

    private void OnClearSecret(object? s, RoutedEventArgs e)
    {
        try
        {
            var r = LibSecret.Clear();
            Log.Write(r.Ok ? $"Secret clear OK (removed={r.Removed})" : $"Secret clear FAILED: {r.Error}");
            if (r.Ok) _stored = null;
        }
        catch (Exception ex) { Log.Write($"Secret clear EXCEPTION {ex.GetType().Name}: {ex.Message}"); }
    }
}
