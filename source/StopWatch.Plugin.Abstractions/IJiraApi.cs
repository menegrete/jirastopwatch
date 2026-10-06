using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StopWatch.Plugin
{
    /// <summary>The remaining estimate of an issue.</summary>
    public sealed class PluginTimeTracking
    {
        public PluginTimeTracking(string remainingEstimate, int remainingEstimateSeconds)
        {
            RemainingEstimate = remainingEstimate;
            RemainingEstimateSeconds = remainingEstimateSeconds;
        }

        /// <summary>Jira's own notation, or null when there is no estimate.</summary>
        public string RemainingEstimate { get; }

        /// <summary>The same value in seconds, or -1 when there is none.</summary>
        public int RemainingEstimateSeconds { get; }
    }


    /// <summary>How Jira should adjust an issue's remaining estimate when a worklog is added.</summary>
    public enum PluginEstimateUpdate
    {
        /// <summary>Jira reduces the estimate by the time spent.</summary>
        Auto,

        /// <summary>The estimate is left as it is.</summary>
        Leave,

        /// <summary>The estimate is set to the value given with it.</summary>
        SetTo,

        /// <summary>The estimate is reduced by the value given with it.</summary>
        ManualDecrease
    }


    /// <summary>A subtask of an issue.</summary>
    public sealed class PluginSubtask
    {
        public PluginSubtask(string key, string summary)
            : this(key, summary, "")
        {
        }

        public PluginSubtask(string key, string summary, string issueType)
        {
            Key = key;
            Summary = summary;
            IssueType = issueType ?? "";
        }

        public string Key { get; }

        /// <summary>Jira's own summary, not composed with the parent's.</summary>
        public string Summary { get; }

        /// <summary>The name of the subtask's issue type, empty when unknown.</summary>
        public string IssueType { get; }
    }


    /// <summary>
    /// The Jira operations a plugin can use. They run with the host's own
    /// session; the API token is not reachable from here. Each method reports
    /// failure through its result rather than by throwing.
    /// </summary>
    public interface IJiraApi
    {
        /// <summary>The composed summary of an issue, or null when it could not be read.</summary>
        Task<string> GetSummaryAsync(string key);

        /// <summary>The remaining estimate of an issue, or null when it could not be read.</summary>
        Task<PluginTimeTracking> GetTimeTrackingAsync(string key);

        /// <summary>
        /// Adds a worklog; true when Jira accepted it. This is the raw
        /// operation: it does not run time-load handlers nor notify
        /// observers, so a handler can use it without intercepting itself.
        /// The estimate is updated automatically.
        /// </summary>
        Task<bool> AddWorklogAsync(string key, DateTimeOffset startTime, TimeSpan timeSpent, string comment);

        /// <summary>
        /// Adds a worklog with the estimate update the user chose, so a
        /// handler that takes over a load can honor it. Raw, like the
        /// overload without an estimate.
        /// </summary>
        Task<bool> AddWorklogAsync(string key, DateTimeOffset startTime, TimeSpan timeSpent, string comment, PluginEstimateUpdate estimateUpdate, string estimateValue);

        /// <summary>The subtasks of an issue, or null when they could not be read.</summary>
        Task<IReadOnlyList<PluginSubtask>> GetSubtasksAsync(string parentKey);

        /// <summary>
        /// Creates a subtask with only a summary, using the first subtask
        /// type the parent's project offers. Returns the new key, or null
        /// when it could not be created. Keep the key: creating again after
        /// a lost answer creates a second subtask.
        /// </summary>
        Task<string> CreateSubtaskAsync(string parentKey, string summary);

        /// <summary>
        /// Creates a subtask of the named issue type, which must be one of the
        /// subtask types the parent's project offers (see
        /// <see cref="GetSubtaskTypesAsync"/>); the name is matched ignoring
        /// case and surrounding whitespace. A null or empty name behaves like
        /// the overload without a type. Returns the new key, or null when it
        /// could not be created, including when the project does not offer
        /// the type: nothing is created and no other type is used instead.
        /// </summary>
        Task<string> CreateSubtaskAsync(string parentKey, string summary, string issueTypeName);

        /// <summary>
        /// The names of the subtask types a project offers (an empty list when
        /// it has none), or null when they could not be read.
        /// </summary>
        Task<IReadOnlyList<string>> GetSubtaskTypesAsync(string projectKey);

        /// <summary>Adds a comment; true when Jira accepted it.</summary>
        Task<bool> AddCommentAsync(string key, string comment);
    }
}
