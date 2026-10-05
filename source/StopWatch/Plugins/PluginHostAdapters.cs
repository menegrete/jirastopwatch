using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using StopWatch.Logging;
using StopWatch.Plugin;

namespace StopWatch.Plugins
{
    /// <summary>What one plugin is handed in <see cref="IPlugin.Initialize"/>.</summary>
    internal class PluginHost : IPluginHost
    {
        private readonly string pluginId;
        private readonly string dataRoot;
        private readonly Window mainWindow;
        private string dataDirectory;

        public PluginHost(string pluginId, string dataRoot, Window mainWindow, IPluginIssueList issues, IJiraApi jira, IPluginTimeLoad timeLoad, ITimeLoader timeLoader, Action<string, Exception> log)
        {
            this.pluginId = pluginId;
            this.dataRoot = dataRoot;
            this.mainWindow = mainWindow;
            Issues = issues;
            Jira = jira;
            TimeLoad = timeLoad;
            TimeLoader = timeLoader;
            Logger = new PluginLogger(pluginId, log);
        }

        public Version ContractVersion
        {
            get { return PluginContract.ContractVersion; }
        }

        public Window MainWindow
        {
            get { return mainWindow; }
        }

        public Dispatcher Dispatcher
        {
            get { return mainWindow.Dispatcher; }
        }

        public string DataDirectory
        {
            get
            {
                if (dataDirectory == null)
                {
                    string path = Path.Combine(dataRoot, pluginId);
                    Directory.CreateDirectory(path);
                    dataDirectory = path;
                }

                return dataDirectory;
            }
        }

        public IPluginLogger Logger { get; private set; }

        public IPluginIssueList Issues { get; private set; }

        public IJiraApi Jira { get; private set; }

        public IPluginTimeLoad TimeLoad { get; private set; }

        public ITimeLoader TimeLoader { get; private set; }
    }


    internal class PluginLogger : IPluginLogger
    {
        private readonly string pluginId;
        private readonly Action<string, Exception> log;

        public PluginLogger(string pluginId, Action<string, Exception> log)
        {
            this.pluginId = pluginId;
            this.log = log;
        }

        public void Log(string message, Exception exception = null)
        {
            log($"Plugin '{pluginId}': {message}", exception);
        }
    }


    /// <summary>
    /// The read-only issue list plugins see, built over the host's view
    /// models. Events are raised to each subscriber separately, so one that
    /// throws is logged and does not stop the rest.
    /// </summary>
    internal class PluginIssueList : IPluginIssueList
    {
        private readonly IssueListViewModel issues;
        private readonly ActiveTimerViewModel activeTimer;
        private readonly Action<string, Exception> log;

        public PluginIssueList(IssueListViewModel issues, ActiveTimerViewModel activeTimer, Action<string, Exception> log)
        {
            this.issues = issues;
            this.activeTimer = activeTimer;
            this.log = log;

            issues.Issues.CollectionChanged += Issues_CollectionChanged;
        }

        public event EventHandler<PluginIssueEventArgs> IssueAdded;

        public event EventHandler<PluginIssueEventArgs> IssueRemoved;

        public IReadOnlyList<PluginIssue> GetIssues()
        {
            return issues.Issues.Select(Snapshot).ToList();
        }

        public PluginIssue GetActiveIssue()
        {
            activeTimer.Refresh();

            IssueViewModel active = activeTimer.ActiveSource as IssueViewModel;
            return active == null ? null : Snapshot(active);
        }


        private static PluginIssue Snapshot(IssueViewModel issue)
        {
            return new PluginIssue(issue.IssueKey, issue.Summary, issue.ParentKey, issue.TimeElapsed, issue.IsRunning);
        }


        private void Issues_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (IssueViewModel issue in e.NewItems)
                    Raise(IssueAdded, "IssueAdded", Snapshot(issue));
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (IssueViewModel issue in e.OldItems)
                    Raise(IssueRemoved, "IssueRemoved", Snapshot(issue));
            }
        }


        private void Raise(EventHandler<PluginIssueEventArgs> handlers, string name, PluginIssue issue)
        {
            if (handlers == null)
                return;

            var args = new PluginIssueEventArgs(issue);

            foreach (EventHandler<PluginIssueEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception ex)
                {
                    log($"Plugin handler of {name} failed: {ex.Message}", ex);
                }
            }
        }
    }


    /// <summary>
    /// The Jira facade for plugins, over the host's own session. Failures
    /// come back as null/false instead of exceptions.
    /// </summary>
    internal class PluginJiraApi : IJiraApi
    {
        private readonly IssueJiraService service;
        private readonly IJiraOperations jira;
        private readonly Action<string, Exception> log;

        public PluginJiraApi(IssueJiraService service, IJiraOperations jira, Action<string, Exception> log)
        {
            this.service = service;
            this.jira = jira;
            this.log = log;
        }

        public async Task<string> GetSummaryAsync(string key)
        {
            try
            {
                IssueSummaryResult result = await service.GetSummaryAsync(key);
                return result?.Summary;
            }
            catch (Exception ex)
            {
                log($"Plugin Jira call GetSummary({key}) failed: {ex.Message}", ex);
                return null;
            }
        }

        public async Task<PluginTimeTracking> GetTimeTrackingAsync(string key)
        {
            try
            {
                RemainingEstimate estimate = await service.GetRemainingEstimateAsync(key);
                return new PluginTimeTracking(estimate.Text, estimate.Seconds);
            }
            catch (Exception ex)
            {
                log($"Plugin Jira call GetTimeTracking({key}) failed: {ex.Message}", ex);
                return null;
            }
        }

        public Task<bool> AddWorklogAsync(string key, DateTimeOffset startTime, TimeSpan timeSpent, string comment)
        {
            return AddWorklogAsync(key, startTime, timeSpent, comment, PluginEstimateUpdate.Auto, "");
        }

        public Task<bool> AddWorklogAsync(string key, DateTimeOffset startTime, TimeSpan timeSpent, string comment, PluginEstimateUpdate estimateUpdate, string estimateValue)
        {
            return Task.Run(() =>
            {
                try
                {
                    return jira.SessionValid
                        && jira.PostWorklog(key, startTime, timeSpent, comment ?? "", EstimateMapping.ToHost(estimateUpdate), estimateValue ?? "").Success;
                }
                catch (Exception ex)
                {
                    log($"Plugin Jira call AddWorklog({key}) failed: {ex.Message}", ex);
                    return false;
                }
            });
        }

        public Task<IReadOnlyList<PluginSubtask>> GetSubtasksAsync(string parentKey)
        {
            return Task.Run<IReadOnlyList<PluginSubtask>>(() =>
            {
                try
                {
                    if (!jira.SessionValid)
                        return null;

                    JiraResult<IReadOnlyList<JiraIssueInfo>> result = jira.GetSubtasks(parentKey);
                    if (!result.Success)
                        return null;

                    return result.Value.Select(i => new PluginSubtask(i.Key, i.Summary)).ToList();
                }
                catch (Exception ex)
                {
                    log($"Plugin Jira call GetSubtasks({parentKey}) failed: {ex.Message}", ex);
                    return null;
                }
            });
        }

        public Task<string> CreateSubtaskAsync(string parentKey, string summary)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!jira.SessionValid)
                        return null;

                    int dash = parentKey == null ? -1 : parentKey.LastIndexOf('-');
                    if (dash <= 0)
                        return null;

                    // The contract has no issue type: the project's first
                    // subtask type is used, and a project with none cannot
                    // take subtasks at all.
                    JiraResult<IReadOnlyList<JiraIssueType>> types = jira.GetSubtaskTypes(parentKey.Substring(0, dash));
                    if (!types.Success || types.Value.Count == 0)
                        return null;

                    JiraResult<string> created = jira.CreateSubtask(parentKey, summary, types.Value[0].Id);
                    return created.Success ? created.Value : null;
                }
                catch (Exception ex)
                {
                    log($"Plugin Jira call CreateSubtask({parentKey}) failed: {ex.Message}", ex);
                    return null;
                }
            });
        }

        public Task<bool> AddCommentAsync(string key, string comment)
        {
            return Task.Run(() =>
            {
                try
                {
                    return jira.SessionValid && jira.PostComment(key, comment ?? "").Success;
                }
                catch (Exception ex)
                {
                    log($"Plugin Jira call AddComment({key}) failed: {ex.Message}", ex);
                    return false;
                }
            });
        }
    }
}
