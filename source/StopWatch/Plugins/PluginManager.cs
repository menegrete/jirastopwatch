using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using StopWatch.Logging;
using StopWatch.Plugin;

namespace StopWatch.Plugins
{
    /// <summary>
    /// Owns the plugins of this run: finds the plugins folder, loads what is
    /// in it once at startup, and answers what the UI needs (commands per
    /// location, per-plugin status).
    /// </summary>
    internal class PluginManager
    {
        private PluginManager(IReadOnlyList<PluginInfo> plugins)
        {
            Plugins = plugins;
        }

        /// <summary>No plugins at all - what the app has when there is no plugins folder.</summary>
        public static PluginManager Empty
        {
            get { return new PluginManager(new List<PluginInfo>()); }
        }

        public IReadOnlyList<PluginInfo> Plugins { get; private set; }

        public bool HasPlugins
        {
            get { return Plugins.Count > 0; }
        }

        /// <summary>The commands of loaded plugins offered at <paramref name="location"/>, in plugin order.</summary>
        public IEnumerable<PluginCommandItem> GetCommands(PluginCommandLocation location)
        {
            return Plugins
                .Where(p => p.IsLoaded)
                .SelectMany(p => p.Commands)
                .Where(c => (c.Command.Location & location) != 0);
        }


        /// <summary>
        /// Where plugins are looked for, in priority order: a plugins folder
        /// beside the executable, then %LocalAppData%\StopWatch\plugins. The
        /// second is per user and outside the install directory, so updates
        /// never touch it; the first is covered by the update script too.
        /// </summary>
        public static IReadOnlyList<string> DefaultPluginsFolders
        {
            get
            {
                return new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "plugins"),
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "StopWatch", "plugins"),
                };
            }
        }

        /// <summary>Where each plugin keeps its own files: %LocalAppData%\StopWatch\plugin-data.</summary>
        public static string DefaultDataFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StopWatch", "plugin-data");
            }
        }


        /// <summary>
        /// Loads the plugins in <paramref name="pluginsFolders"/>. Never throws:
        /// whatever goes wrong is contained and logged, and the app carries on
        /// without plugins. With no folder, does nothing at all.
        /// </summary>
        public static PluginManager Load(IReadOnlyList<string> pluginsFolders, string dataFolder, Window mainWindow, AppComposition composition)
        {
            try
            {
                if (!pluginsFolders.Any(Directory.Exists))
                    return Empty;

                Action<string, Exception> log = (message, ex) => Logger.Instance.Log(message, ex);

                var issues = new PluginIssueList(composition.Issues, composition.ActiveTimer, log);

                var loader = new PluginLoader(
                    pluginsFolders,
                    PluginContract.ContractVersion,
                    id => new PluginHost(id, dataFolder, mainWindow, issues, composition.PluginJira, new PluginTimeLoad(id, composition.TimeLoadRegistry), new PluginTimeLoader(id, composition.TimeLoad), log),
                    log);

                return new PluginManager(loader.LoadAll());
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Plugins: loading failed, continuing without plugins", ex);
                return Empty;
            }
        }
    }
}
