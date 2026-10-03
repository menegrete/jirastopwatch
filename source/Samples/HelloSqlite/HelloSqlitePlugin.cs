using System.Collections.Generic;
using System.Windows;
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

            MessageBox.Show(host.MainWindow, result, "Hello SQLite");
        }
    }
}
