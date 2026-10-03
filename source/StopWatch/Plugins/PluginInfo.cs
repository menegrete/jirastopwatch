using System;
using System.Collections.Generic;
using StopWatch.Plugin;

namespace StopWatch.Plugins
{
    internal enum PluginState
    {
        Loaded,
        Failed,
    }


    /// <summary>
    /// What the host knows about one discovered plugin: whether it loaded,
    /// and if not, why - plus the commands it contributes when it did.
    /// </summary>
    internal class PluginInfo
    {
        public PluginInfo(string id)
        {
            Id = id;
            Name = id;
            Version = "";
            State = PluginState.Failed;
            Reason = "";
            Commands = new List<PluginCommandItem>();
        }

        public string Id { get; set; }

        public string Name { get; set; }

        public string Version { get; set; }

        public PluginState State { get; set; }

        /// <summary>Why the plugin is not loaded; empty when it is.</summary>
        public string Reason { get; set; }

        public List<PluginCommandItem> Commands { get; private set; }

        public bool IsLoaded
        {
            get { return State == PluginState.Loaded; }
        }

        /// <summary>One line for a status list.</summary>
        public string StatusText
        {
            get
            {
                string label = string.IsNullOrEmpty(Version) ? Name : $"{Name} v{Version}";
                return IsLoaded ? label : $"{label} - not loaded: {Reason}";
            }
        }
    }


    /// <summary>
    /// One command a plugin contributes, with the guard that keeps a plugin
    /// exception from escaping when it is run.
    /// </summary>
    internal class PluginCommandItem
    {
        private readonly Action<string, Exception> log;

        public PluginCommandItem(PluginInfo plugin, PluginCommand command, Action<string, Exception> log)
        {
            Plugin = plugin;
            Command = command;
            this.log = log;
        }

        public PluginInfo Plugin { get; private set; }

        public PluginCommand Command { get; private set; }

        public string Title
        {
            get { return string.IsNullOrEmpty(Command.Icon) ? Command.Title : Command.Icon + " " + Command.Title; }
        }

        /// <summary>
        /// Runs the command. Returns null on success, or a message describing
        /// the failure - the exception never propagates to the caller.
        /// </summary>
        public string Run()
        {
            try
            {
                Command.Execute();
                return null;
            }
            catch (Exception ex)
            {
                log($"Plugin '{Plugin.Id}': command '{Command.Id}' failed: {ex.Message}", ex);
                return $"The plugin \"{Plugin.Name}\" failed running \"{Command.Title}\":{Environment.NewLine}{Environment.NewLine}{ex.Message}";
            }
        }
    }
}
