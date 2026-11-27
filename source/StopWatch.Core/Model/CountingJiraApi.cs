using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StopWatch.Plugin;

namespace StopWatch
{
    /// <summary>
    /// The Jira API handed to one replacement handler for one invocation. It
    /// forwards every call and counts the writes Jira accepted, which is what
    /// tells the pipeline whether a handler that failed can still be replaced
    /// by the standard load without loading the same time twice. It also adds
    /// up the time of the worklogs Jira accepted: after a partial load that is
    /// the time the host takes off the timer, measured here rather than taken
    /// from what the plugin says.
    ///
    /// A fresh instance is made per invocation, so concurrent loads never share
    /// a count or a time.
    /// </summary>
    internal class CountingJiraApi : IJiraApi
    {
        private readonly IJiraApi inner;
        private int writes;
        private long loadedTicks;

        public CountingJiraApi(IJiraApi inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>The number of worklogs and subtasks Jira accepted through this instance.</summary>
        public int WritesMade
        {
            get { return Volatile.Read(ref writes); }
        }

        /// <summary>The time of the worklogs Jira accepted through this instance.</summary>
        public TimeSpan TimeLoaded
        {
            get { return TimeSpan.FromTicks(Interlocked.Read(ref loadedTicks)); }
        }


        public Task<string> GetSummaryAsync(string key)
        {
            return inner.GetSummaryAsync(key);
        }

        public Task<PluginTimeTracking> GetTimeTrackingAsync(string key)
        {
            return inner.GetTimeTrackingAsync(key);
        }

        public Task<bool> AddCommentAsync(string key, string comment)
        {
            return inner.AddCommentAsync(key, comment);
        }

        public Task<IReadOnlyList<PluginSubtask>> GetSubtasksAsync(string parentKey)
        {
            return inner.GetSubtasksAsync(parentKey);
        }

        public async Task<bool> AddWorklogAsync(string key, DateTimeOffset startTime, TimeSpan timeSpent, string comment)
        {
            return CountWorklog(await inner.AddWorklogAsync(key, startTime, timeSpent, comment), timeSpent);
        }

        public async Task<bool> AddWorklogAsync(string key, DateTimeOffset startTime, TimeSpan timeSpent, string comment, PluginEstimateUpdate estimateUpdate, string estimateValue)
        {
            return CountWorklog(await inner.AddWorklogAsync(key, startTime, timeSpent, comment, estimateUpdate, estimateValue), timeSpent);
        }

        public async Task<string> CreateSubtaskAsync(string parentKey, string summary)
        {
            string created = await inner.CreateSubtaskAsync(parentKey, summary);
            Count(created != null);
            return created;
        }

        public async Task<string> CreateSubtaskAsync(string parentKey, string summary, string issueTypeName)
        {
            string created = await inner.CreateSubtaskAsync(parentKey, summary, issueTypeName);
            Count(created != null);
            return created;
        }

        public Task<IReadOnlyList<string>> GetSubtaskTypesAsync(string projectKey)
        {
            return inner.GetSubtaskTypesAsync(projectKey);
        }


        private bool Count(bool accepted)
        {
            if (accepted)
                Interlocked.Increment(ref writes);

            return accepted;
        }


        private bool CountWorklog(bool accepted, TimeSpan timeSpent)
        {
            if (accepted)
                Interlocked.Add(ref loadedTicks, timeSpent.Ticks);

            return Count(accepted);
        }
    }
}
