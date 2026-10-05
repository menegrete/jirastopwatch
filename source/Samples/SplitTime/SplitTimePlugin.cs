using System.Collections.Generic;
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
            var handler = new SplitTimeHandler(new SplitDialogPrompt(host), message => host.Logger.Log(message));

            host.TimeLoad.RegisterInsteadOf(handler.HandleAsync);
            host.Logger.Log("SplitTime initialized");
        }


        public IEnumerable<PluginCommand> GetCommands()
        {
            yield break;
        }
    }
}
