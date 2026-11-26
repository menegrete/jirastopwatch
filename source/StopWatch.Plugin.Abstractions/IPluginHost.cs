using System;
using Avalonia.Controls;
using Avalonia.Threading;

namespace StopWatch.Plugin
{
    /// <summary>What the host offers to one plugin.</summary>
    public interface IPluginHost
    {
        /// <summary>The contract version the host implements.</summary>
        Version ContractVersion { get; }

        /// <summary>
        /// The main window, for use as the owner of the plugin's own windows
        /// (<c>window.ShowDialog(MainWindow)</c> or <c>window.Show(MainWindow)</c>).
        /// An Avalonia window: contract 2.0 replaced the WPF types of 1.x.
        /// </summary>
        Window MainWindow { get; }

        /// <summary>The UI dispatcher (<see cref="Dispatcher.UIThread"/>).</summary>
        Dispatcher Dispatcher { get; }

        /// <summary>
        /// A folder private to this plugin, created on first access and not
        /// shared with other plugins. It survives application updates.
        /// </summary>
        string DataDirectory { get; }

        /// <summary>Writes to the application log, tagged with this plugin's id.</summary>
        IPluginLogger Logger { get; }

        /// <summary>Read-only view of the issue list.</summary>
        IPluginIssueList Issues { get; }

        /// <summary>The Jira operations the host offers. Credentials never pass through it.</summary>
        IJiraApi Jira { get; }

        /// <summary>Registers a time-load handler and observes time loads.</summary>
        IPluginTimeLoad TimeLoad { get; }

        /// <summary>Loads time through the host's pipeline.</summary>
        ITimeLoader TimeLoader { get; }
    }


    /// <summary>The application log, as seen by a plugin.</summary>
    public interface IPluginLogger
    {
        void Log(string message, Exception exception = null);
    }
}
