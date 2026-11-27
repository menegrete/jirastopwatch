using Avalonia;

namespace AvaloniaSpike;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--selftest"))
            return SelfTest();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont();

    // Headless check of settings persistence (task 1.3) and libsecret (task 1.4).
    private static int SelfTest()
    {
        var failed = false;
        try
        {
            var s = SpikeSettings.Instance;
            s.RunCount++;
            s.LastValue = $"selftest-{s.RunCount}";
            s.Save();
            var path = System.Configuration.ConfigurationManager
                .OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
            Log.Write($"SelfTest settings: user.config={path} exists={File.Exists(path)} RunCount={s.RunCount} LastValue='{s.LastValue}'");
        }
        catch (Exception ex) { failed = true; Log.Write($"SelfTest settings FAILED: {ex}"); }

        if (LibSecret.IsSupported)
        {
            try
            {
                const string secret = "selftest-secret-value";
                var st = LibSecret.Store(secret);
                Log.Write($"SelfTest secret store: ok={st.Ok} {st.Error}");
                var lk = LibSecret.Lookup();
                Log.Write($"SelfTest secret lookup: ok={lk.Ok} found={lk.Value != null} length={lk.Value?.Length ?? 0} matches={lk.Value == secret} {lk.Error}");
                var cl = LibSecret.Clear();
                Log.Write($"SelfTest secret clear: ok={cl.Ok} removed={cl.Removed} {cl.Error}");
                var after = LibSecret.Lookup();
                Log.Write($"SelfTest secret lookup after clear: ok={after.Ok} found={after.Value != null} {after.Error}");
                failed |= !(st.Ok && lk.Ok && lk.Value == secret && cl.Ok && cl.Removed && after.Value == null);
            }
            catch (Exception ex) { failed = true; Log.Write($"SelfTest secret FAILED: {ex}"); }
        }

        Log.Write($"SelfTest result: {(failed ? "FAIL" : "PASS")}");
        return failed ? 1 : 0;
    }
}
