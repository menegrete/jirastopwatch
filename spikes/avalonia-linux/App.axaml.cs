using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AvaloniaSpike;

public partial class App : Application
{
    public ControlWindow? Control { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Control = new ControlWindow();
            desktop.MainWindow = Control;
            try
            {
                var icons = TrayIcon.GetIcons(this);
                Log.Write($"Tray: created from App.axaml, icons={icons?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                Log.Write($"Tray: FAILED {ex}");
            }
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void OnTrayClicked(object? s, EventArgs e) { Log.Write("Tray: icon clicked"); Control?.ShowAndActivate(); }
    private void OnShowControl(object? s, EventArgs e) { Log.Write("Tray: Show control window"); Control?.ShowAndActivate(); }
    private void OnToggleMini(object? s, EventArgs e) { Log.Write("Tray: Toggle mini"); Control?.ToggleMini(); }

    private void OnExit(object? s, EventArgs e)
    {
        Log.Write("Tray: Exit");
        Control?.AllowClose();
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }
}
