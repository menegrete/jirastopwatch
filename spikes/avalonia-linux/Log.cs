namespace AvaloniaSpike;

internal static class Log
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "avalonia-spike.log");
    private static readonly object Gate = new();

    public static event Action<string>? Line;

    public static void Write(string msg)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {msg}";
        try { lock (Gate) File.AppendAllText(FilePath, line + Environment.NewLine); } catch { }
        Line?.Invoke(line);
    }
}
