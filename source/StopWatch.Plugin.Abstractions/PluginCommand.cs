using System;

namespace StopWatch.Plugin
{
    /// <summary>Where the host may show a command.</summary>
    [Flags]
    public enum PluginCommandLocation
    {
        None = 0,

        /// <summary>The tray icon context menu.</summary>
        Tray = 1,

        /// <summary>The "Plugins" menu of the main window.</summary>
        MainWindow = 2,

        Both = Tray | MainWindow,
    }


    /// <summary>
    /// A command a plugin contributes. It is data plus an action: the host
    /// decides how to draw it.
    /// </summary>
    public sealed class PluginCommand
    {
        public PluginCommand(string id, string title, Action execute, PluginCommandLocation location = PluginCommandLocation.Both, string icon = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A command needs an id.", nameof(id));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("A command needs a title.", nameof(title));

            Id = id;
            Title = title;
            Execute = execute ?? throw new ArgumentNullException(nameof(execute));
            Location = location;
            Icon = icon;
        }

        /// <summary>Unique within the plugin.</summary>
        public string Id { get; }

        public string Title { get; }

        /// <summary>Where the host may show it.</summary>
        public PluginCommandLocation Location { get; }

        /// <summary>
        /// Optional short glyph (an emoji or symbol character) shown before
        /// the title; null for none.
        /// </summary>
        public string Icon { get; }

        /// <summary>Runs on the UI thread when the user picks the command.</summary>
        public Action Execute { get; }
    }
}
