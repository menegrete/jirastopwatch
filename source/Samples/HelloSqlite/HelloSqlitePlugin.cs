using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using StopWatch.Plugin;

namespace HelloSqlite
{
    /// <summary>
    /// Runs a query against an in-memory SQLite database. Executing it forces
    /// the real load of the native e_sqlite3.dll, which is the point.
    /// </summary>
    public class HelloSqlitePlugin : IPlugin
    {
        private IPluginHost host;

        public void Initialize(IPluginHost host)
        {
            this.host = host;

            // Touch the database at initialization too, so a machine that
            // cannot load the native library shows the plugin as not loaded
            // (with the reason) instead of failing at the first click.
            string result = SqliteDemo.Run();
            host.Logger.Log("HelloSqlite initialized: " + result);
        }


        public IEnumerable<PluginCommand> GetCommands()
        {
            yield return new PluginCommand("run-query", "Run SQLite query", RunQuery, PluginCommandLocation.Both);
        }


        private void RunQuery()
        {
            string result = SqliteDemo.Run();
            host.Logger.Log("HelloSqlite query: " + result);

            var ok = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true };
            var window = new Window
            {
                Title = "Hello SQLite",
                Width = 360,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(16),
                    Spacing = 16,
                    Children = { new TextBlock { Text = result, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, ok },
                },
            };
            ok.Click += (s, e) => window.Close();
            _ = window.ShowDialog(host.MainWindow);
        }
    }
}
