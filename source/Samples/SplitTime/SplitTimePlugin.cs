using System.Collections.Generic;
using System.Windows;
using StopWatch.Plugin;

namespace SplitTime
{
    /// <summary>
    /// Sample for the time-load pipeline: when the user posts the time of an
    /// issue that has subtasks, asks how to share it among them.
    /// </summary>
    public class SplitTimePlugin : IPlugin
    {
        public void Initialize(IPluginHost host)
        {
            var handler = new SplitTimeHandler(
                new SplitDialogPrompt(host),
                new SplitLedger(host.DataDirectory),
                message => host.Logger.Log(message),
                message => MessageBox.Show(host.MainWindow, message, "Split Time", MessageBoxButton.OK, MessageBoxImage.Warning));

            host.TimeLoad.RegisterInsteadOf(handler.HandleAsync);
            host.Logger.Log("SplitTime initialized");
        }


        public IEnumerable<PluginCommand> GetCommands()
        {
            yield break;
        }
    }
}
