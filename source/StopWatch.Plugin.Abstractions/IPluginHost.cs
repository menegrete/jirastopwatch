using System;
using System.Windows;
using System.Windows.Threading;

namespace StopWatch.Plugin
{
    /// <summary>What the host offers to one plugin.</summary>
    public interface IPluginHost
    {
        /// <summary>The contract version the host implements.</summary>
        Version ContractVersion { get; }

        /// <summary>
        /// The main window, for use as the <see cref="Window.Owner"/> of the
        /// plugin's own windows.
        /// </summary>
        Window MainWindow { get; }

        /// <summary>The UI dispatcher.</summary>
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
    }


    /// <summary>The application log, as seen by a plugin.</summary>
    public interface IPluginLogger
    {
        void Log(string message, Exception exception = null);
    }
}
