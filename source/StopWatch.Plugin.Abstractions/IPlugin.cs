using System.Collections.Generic;

namespace StopWatch.Plugin
{
    /// <summary>
    /// The entry point of a plugin. The plugin assembly must contain exactly
    /// one public, non-abstract class implementing this interface, with a
    /// public parameterless constructor.
    ///
    /// The host calls <see cref="Initialize"/> once, then
    /// <see cref="GetCommands"/>. Plugins must not depend on each other or on
    /// the order in which they are loaded.
    /// </summary>
    public interface IPlugin
    {
        /// <summary>Called once, on the UI thread, right after loading.</summary>
        void Initialize(IPluginHost host);

        /// <summary>The commands this plugin contributes, as data.</summary>
        IEnumerable<PluginCommand> GetCommands();
    }
}
