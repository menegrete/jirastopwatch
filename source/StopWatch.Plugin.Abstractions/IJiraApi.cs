using System;
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

        /// <summary>Adds a worklog; true when Jira accepted it.</summary>
        Task<bool> AddWorklogAsync(string key, DateTimeOffset startTime, TimeSpan timeSpent, string comment);

        /// <summary>Adds a comment; true when Jira accepted it.</summary>
        Task<bool> AddCommentAsync(string key, string comment);
    }
}
