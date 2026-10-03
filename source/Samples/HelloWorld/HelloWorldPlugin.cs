using System;
using System.Collections.Generic;
using System.IO;
using StopWatch.Plugin;

namespace HelloWorld
{
    /// <summary>
    /// The smallest useful plugin: one command that opens a themed window,
    /// writes a file into the plugin's data directory and logs.
    /// </summary>
    public class HelloWorldPlugin : IPlugin
    {
        private IPluginHost host;

        public void Initialize(IPluginHost host)
        {
            this.host = host;
            host.Logger.Log("HelloWorld initialized");
        }


        public IEnumerable<PluginCommand> GetCommands()
        {
            yield return new PluginCommand("say-hello", "Say hello", SayHello, PluginCommandLocation.Both);
        }


        private void SayHello()
        {
            string file = Path.Combine(host.DataDirectory, "hello.txt");
            File.AppendAllText(file, $"Hello at {DateTime.Now:O}{Environment.NewLine}");

            host.Logger.Log("HelloWorld said hello");

            var window = new HelloWindow(host.Issues.GetIssues().Count, file)
            {
                Owner = host.MainWindow,
            };
            window.ShowDialog();
        }
    }
}
