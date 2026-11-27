using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace AvaloniaSpike;

public partial class MiniWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _seconds = 5;

    public MiniWindow()
    {
        InitializeComponent();
        TimerText.Text = Format();
        _timer.Tick += (_, _) => { _seconds++; TimerText.Text = Format(); };
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
        PointerPressed += OnPointerPressed;
        PointerReleased += (_, _) => LogPosition("drag end", null);
    }

    private string Format() => $"ABC-123 {TimeSpan.FromSeconds(_seconds).ToString(@"hh\:mm\:ss")}";

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Log.Write("Mini: BeginMoveDrag");
            try { BeginMoveDrag(e); } catch (Exception ex) { Log.Write($"Mini: BeginMoveDrag failed {ex.Message}"); }
        }
    }

    public void MoveTo(PixelPoint p, string what)
    {
        Position = p;
        LogPosition(what, p);
    }

    public void LogPosition(string what, PixelPoint? requested)
    {
        var req = requested is { } r ? $"({r.X},{r.Y})" : "(n/a)";
        Log.Write($"Mini {what}: requested {req} → actual Position ({Position.X},{Position.Y}) [immediate]");
        DispatcherTimer.RunOnce(
            () => Log.Write($"Mini {what}: requested {req} → actual Position ({Position.X},{Position.Y}) [after 200ms]"),
            TimeSpan.FromMilliseconds(200));
    }

    public void ApplyTransparent(bool on)
    {
        if (on)
        {
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Background = Brushes.Transparent;
            Root.CornerRadius = new CornerRadius(12);
        }
        else
        {
            TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
            Background = null;
            Root.CornerRadius = new CornerRadius(0);
        }
        Log.Write($"Mini: transparent+rounded={on}, ActualTransparencyLevel={ActualTransparencyLevel}");
        DispatcherTimer.RunOnce(
            () => Log.Write($"Mini: ActualTransparencyLevel after 200ms={ActualTransparencyLevel}"),
            TimeSpan.FromMilliseconds(200));
    }
}
