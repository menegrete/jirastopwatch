using System;
using System.Threading.Tasks;

namespace StopWatch.Plugin
{
    /// <summary>Who started a time load: the user, through the host's dialog, or a plugin.</summary>
    public sealed class TimeLoadSource
    {
        private TimeLoadSource(string pluginId)
        {
            PluginId = pluginId;
        }

        /// <summary>A load the user confirmed in the host's worklog dialog.</summary>
        public static TimeLoadSource User { get; } = new TimeLoadSource(null);

        /// <summary>A load a plugin started through <see cref="ITimeLoader"/>.</summary>
        public static TimeLoadSource FromPlugin(string pluginId)
        {
            if (string.IsNullOrEmpty(pluginId))
                throw new ArgumentException("A plugin id is required.", nameof(pluginId));

            return new TimeLoadSource(pluginId);
        }

        public bool IsUser
        {
            get { return PluginId == null; }
        }

        /// <summary>The id of the plugin that started the load, or null for the user.</summary>
        public string PluginId { get; }

        public override string ToString()
        {
            return IsUser ? "user" : "plugin:" + PluginId;
        }
    }


    /// <summary>A time load as a replacement handler receives it.</summary>
    public sealed class TimeLoadRequest
    {
        public TimeLoadRequest(string issueKey, DateTimeOffset startTime, TimeSpan timeElapsed, string comment, PluginEstimateUpdate estimateUpdate, string estimateValue, TimeLoadSource source, IJiraApi jira)
        {
            IssueKey = issueKey;
            StartTime = startTime;
            TimeElapsed = timeElapsed;
            Comment = comment;
            EstimateUpdate = estimateUpdate;
            EstimateValue = estimateValue;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Jira = jira;
        }

        public string IssueKey { get; }

        public DateTimeOffset StartTime { get; }

        /// <summary>The total the load was confirmed with.</summary>
        public TimeSpan TimeElapsed { get; }

        public string Comment { get; }

        public PluginEstimateUpdate EstimateUpdate { get; }

        public string EstimateValue { get; }

        public TimeLoadSource Source { get; }

        /// <summary>
        /// The Jira API to write with while handling this request. The host
        /// counts the writes made through it to decide whether a failure can
        /// fall back to the standard load. Writes made through
        /// <see cref="IPluginHost.Jira"/> are not counted.
        /// </summary>
        public IJiraApi Jira { get; }
    }


    /// <summary>What a replacement handler decided.</summary>
    public enum InsteadOfKind
    {
        /// <summary>Not for this handler; the next one, or the host, loads the time.</summary>
        Declined,

        /// <summary>The handler loaded the time itself.</summary>
        Handled,

        /// <summary>The user closed the handler's own dialog; nothing is loaded.</summary>
        Cancelled,

        /// <summary>The handler could not load the time.</summary>
        Failed
    }


    /// <summary>The answer of a replacement handler. Built with the static factories.</summary>
    public sealed class InsteadOfResult
    {
        private InsteadOfResult(InsteadOfKind kind, TimeSpan timeLoaded, string reason)
        {
            Kind = kind;
            TimeLoaded = timeLoaded;
            Reason = reason;
        }

        public InsteadOfKind Kind { get; }

        /// <summary>For <see cref="InsteadOfKind.Handled"/>, the time the handler says it loaded.</summary>
        public TimeSpan TimeLoaded { get; }

        /// <summary>For <see cref="InsteadOfKind.Failed"/>, an optional explanation.</summary>
        public string Reason { get; }

        /// <summary>The load is not for this handler. It must not have written anything.</summary>
        public static InsteadOfResult Declined()
        {
            return new InsteadOfResult(InsteadOfKind.Declined, TimeSpan.Zero, null);
        }

        /// <summary>
        /// The handler loaded the time, comment included. <paramref name="timeLoaded"/>
        /// must equal the total in the request, or the host does not reset the timer.
        /// </summary>
        public static InsteadOfResult Handled(TimeSpan timeLoaded)
        {
            return new InsteadOfResult(InsteadOfKind.Handled, timeLoaded, null);
        }

        /// <summary>The user cancelled in the handler's own dialog. It must not have written anything.</summary>
        public static InsteadOfResult Cancelled()
        {
            return new InsteadOfResult(InsteadOfKind.Cancelled, TimeSpan.Zero, null);
        }

        public static InsteadOfResult Failed(string reason = null)
        {
            return new InsteadOfResult(InsteadOfKind.Failed, TimeSpan.Zero, reason);
        }
    }


    /// <summary>How a time load ended.</summary>
    public enum TimeLoadOutcome
    {
        Succeeded,
        Failed,
        Cancelled
    }


    /// <summary>What an observer is told after a time load.</summary>
    public sealed class TimeLoadedEventArgs : EventArgs
    {
        public TimeLoadedEventArgs(string issueKey, DateTimeOffset startTime, TimeSpan timeElapsed, TimeSpan timeLoaded, TimeLoadSource source, string handledBy, TimeLoadOutcome outcome, string reason, int writesMade)
        {
            IssueKey = issueKey;
            StartTime = startTime;
            TimeElapsed = timeElapsed;
            TimeLoaded = timeLoaded;
            Source = source;
            HandledBy = handledBy;
            Outcome = outcome;
            Reason = reason ?? "";
            WritesMade = writesMade;
        }

        public string IssueKey { get; }

        public DateTimeOffset StartTime { get; }

        /// <summary>The total the load was confirmed with.</summary>
        public TimeSpan TimeElapsed { get; }

        /// <summary>The time actually loaded: zero when nothing was.</summary>
        public TimeSpan TimeLoaded { get; }

        public TimeLoadSource Source { get; }

        /// <summary>The id of the plugin that handled the load, or null when the host did.</summary>
        public string HandledBy { get; }

        public TimeLoadOutcome Outcome { get; }

        /// <summary>Why the load did not succeed; empty when it did.</summary>
        public string Reason { get; }

        /// <summary>How many writes were made through the handler's Jira API.</summary>
        public int WritesMade { get; }
    }


    /// <summary>The result of a load a plugin started through <see cref="ITimeLoader"/>.</summary>
    public sealed class TimeLoadResult
    {
        public TimeLoadResult(bool success, string reason)
        {
            Success = success;
            Reason = reason ?? "";
        }

        public bool Success { get; }

        public string Reason { get; }
    }


    /// <summary>How a plugin takes part in time loading.</summary>
    public interface IPluginTimeLoad
    {
        /// <summary>
        /// Registers the plugin's one replacement handler. It runs after the
        /// user confirmed the load in the host dialog, on the UI thread, and
        /// may take as long as it needs: it is never timed out. Calling this a
        /// second time throws.
        /// </summary>
        void RegisterInsteadOf(Func<TimeLoadRequest, Task<InsteadOfResult>> handler);

        /// <summary>
        /// Raised after every time load, whoever handled it. Observers cannot
        /// veto or change the load, and one that throws does not affect it.
        /// </summary>
        event EventHandler<TimeLoadedEventArgs> After;
    }


    /// <summary>Lets a plugin load time through the host's pipeline.</summary>
    public interface ITimeLoader
    {
        /// <summary>
        /// Loads time with the plugin as its source: other plugins' handlers
        /// are consulted and observers notified, the plugin's own handler is
        /// not. Called from inside a handler, it skips the handlers altogether.
        /// It does not touch the timer of any issue in the list.
        /// </summary>
        Task<TimeLoadResult> LoadAsync(string issueKey, DateTimeOffset startTime, TimeSpan timeElapsed, string comment, PluginEstimateUpdate estimateUpdate, string estimateValue);
    }
}
