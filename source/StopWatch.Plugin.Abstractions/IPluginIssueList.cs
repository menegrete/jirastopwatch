using System;
using System.Collections.Generic;

namespace StopWatch.Plugin
{
    /// <summary>A snapshot of one issue row, as plain data.</summary>
    public sealed class PluginIssue
    {
        public PluginIssue(string key, string summary, string parentKey, TimeSpan timeElapsed, bool isRunning)
        {
            Key = key ?? "";
            Summary = summary ?? "";
            ParentKey = parentKey ?? "";
            TimeElapsed = timeElapsed;
            IsRunning = isRunning;
        }

        public string Key { get; }
        public string Summary { get; }
        public string ParentKey { get; }
        public TimeSpan TimeElapsed { get; }
        public bool IsRunning { get; }
    }


    public sealed class PluginIssueEventArgs : EventArgs
    {
        public PluginIssueEventArgs(PluginIssue issue)
        {
            Issue = issue;
        }

        public PluginIssue Issue { get; }
    }


    /// <summary>
    /// Read-only access to the issue list. Every read returns a fresh
    /// snapshot; nothing here lets a plugin change the host state.
    /// </summary>
    public interface IPluginIssueList
    {
        /// <summary>The issues currently in the list, in order.</summary>
        IReadOnlyList<PluginIssue> GetIssues();

        /// <summary>The issue a compact display would show, or null when there is none.</summary>
        PluginIssue GetActiveIssue();

        /// <summary>Raised after an issue row is added to the list.</summary>
        event EventHandler<PluginIssueEventArgs> IssueAdded;

        /// <summary>Raised after an issue row is removed from the list.</summary>
        event EventHandler<PluginIssueEventArgs> IssueRemoved;
    }
}
