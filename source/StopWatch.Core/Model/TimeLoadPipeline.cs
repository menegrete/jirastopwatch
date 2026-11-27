using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StopWatch.Plugin;

namespace StopWatch
{
    /// <summary>The standard load: what the pipeline falls back to, and runs when nobody takes the load.</summary>
    internal interface IWorklogPoster
    {
        Task<PostWorklogResult> PostWorklogAsync(string key, DateTimeOffset startTime, TimeSpan timeElapsed, string comment, EstimateUpdateMethods estimateUpdateMethod, string estimateUpdateValue);
    }


    /// <summary>Tells the user about a load. The pipeline decides what is worth saying; this decides how.</summary>
    internal interface ITimeLoadNotifier
    {
        /// <summary>Something happened that needs no action; shown without interrupting.</summary>
        void Info(string message);

        /// <summary>Time may be half loaded and the user has to act on it; shown so that it cannot be missed.</summary>
        void Warning(string message);
    }


    /// <summary>What the user confirmed, or what a plugin asked to load.</summary>
    internal class TimeLoadInput
    {
        public string IssueKey { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public TimeSpan TimeElapsed { get; set; }
        public string Comment { get; set; }
        public EstimateUpdateMethods EstimateUpdateMethod { get; set; }
        public string EstimateUpdateValue { get; set; }
        public TimeLoadSource Source { get; set; } = TimeLoadSource.User;

        /// <summary>
        /// What the timer holds right now, when the load comes from a timer. It
        /// can be more than <see cref="TimeElapsed"/> if the timer ran while a
        /// handler waited; it is what a notice should count the remainder from.
        /// Left null, the confirmed total stands in for it.
        /// </summary>
        public Func<TimeSpan> CurrentElapsed { get; set; }
    }


    /// <summary>
    /// How a load ended. The timer is reset exactly when <see cref="Outcome"/>
    /// is <see cref="TimeLoadOutcome.Succeeded"/>; after a partial load it is
    /// reduced by <see cref="TimeLoaded"/> (see <see cref="TimerAfterLoad"/>).
    /// </summary>
    internal class TimeLoadPipelineResult
    {
        public TimeLoadOutcome Outcome { get; set; }

        /// <summary>The plugin that handled the load, or null when the host did.</summary>
        public string HandledBy { get; set; }

        public int WritesMade { get; set; }

        /// <summary>
        /// The time that is in Jira because of this load: the confirmed total
        /// when it succeeded, otherwise the time the host measured through the
        /// handler's Jira API - never what the handler says it loaded.
        /// </summary>
        public TimeSpan TimeLoaded { get; set; }

        public string Reason { get; set; } = "";
    }


    /// <summary>
    /// Time loading as a pipeline: replacement handlers, the standard load,
    /// observers.
    ///
    /// Each handler is asked in turn and answers declined, handled, cancelled
    /// or failed. What the host does with the answer depends on how many
    /// writes the handler made through its counting Jira API, because that is
    /// what decides whether the standard load can still run without loading
    /// the same time twice. See the time-load-pipeline spec for the table.
    ///
    /// Handlers and observers run on the caller's thread: they may open
    /// windows and they may wait for a person, so they are never timed out.
    /// </summary>
    internal class TimeLoadPipeline
    {
        // Set while a handler runs, so that a load it starts through the
        // plugin loader does not consult handlers again and cannot loop.
        private static readonly AsyncLocal<bool> insideHandler = new AsyncLocal<bool>();

        private readonly IWorklogPoster original;
        private readonly TimeLoadRegistry registry;
        private readonly IJiraApi jira;
        private readonly Action<string, Exception> log;

        public TimeLoadPipeline(IWorklogPoster original, TimeLoadRegistry registry, IJiraApi jira, Action<string, Exception> log)
        {
            this.original = original ?? throw new ArgumentNullException(nameof(original));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.jira = jira ?? throw new ArgumentNullException(nameof(jira));
            this.log = log ?? ((message, ex) => { });
        }


        /// <summary>Where notices go. Left null, they are only logged.</summary>
        public ITimeLoadNotifier Notifier { get; set; }


        /// <summary>
        /// Runs a load. <paramref name="setBusy"/> is raised with true and
        /// false around the standard load only, never while a handler waits.
        /// </summary>
        public async Task<TimeLoadPipelineResult> LoadAsync(TimeLoadInput input, Action<bool> setBusy = null)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            TimeLoadPipelineResult result = await RunAsync(input, setBusy);
            NotifyObservers(input, result);
            return result;
        }


        #region stages
        private async Task<TimeLoadPipelineResult> RunAsync(TimeLoadInput input, Action<bool> setBusy)
        {
            if (!insideHandler.Value)
            {
                foreach (RegisteredHandler registered in registry.Handlers)
                {
                    if (IsOwnLoad(input.Source, registered.PluginId))
                        continue;

                    var api = new CountingJiraApi(jira);
                    InsteadOfResult answer = await AskAsync(registered, input, api);
                    int writes = api.WritesMade;
                    TimeSpan measured = api.TimeLoaded;

                    switch (answer.Kind)
                    {
                        case InsteadOfKind.Declined:
                            if (writes == 0)
                                continue;
                            return FailedWithWrites(input, registered.PluginId, writes, measured, "it declined the load after writing");

                        case InsteadOfKind.Handled:
                            return Handled(input, registered.PluginId, writes, measured, answer.TimeLoaded);

                        case InsteadOfKind.Cancelled:
                            if (writes == 0)
                                return Cancelled(registered.PluginId);
                            return FailedWithWrites(input, registered.PluginId, writes, measured, "it was cancelled after writing");

                        default:
                            if (writes == 0)
                            {
                                Notify(input, false, $"Plugin {registered.PluginId} failed on {input.IssueKey}{Because(answer.Reason)}; the standard load was used.");
                                return await RunOriginalAsync(input, setBusy);
                            }
                            return FailedWithWrites(input, registered.PluginId, writes, measured, answer.Reason);
                    }
                }
            }

            return await RunOriginalAsync(input, setBusy);
        }


        /// <summary>
        /// Calls a handler, turning anything that goes wrong - a throw before
        /// its first await, no task, no result, an exception later - into a
        /// failed answer instead of an exception.
        /// </summary>
        private async Task<InsteadOfResult> AskAsync(RegisteredHandler registered, TimeLoadInput input, IJiraApi api)
        {
            var request = new TimeLoadRequest(
                input.IssueKey,
                input.StartTime,
                input.TimeElapsed,
                input.Comment,
                EstimateMapping.ToPlugin(input.EstimateUpdateMethod),
                input.EstimateUpdateValue,
                input.Source,
                api);

            bool previous = insideHandler.Value;
            insideHandler.Value = true;

            try
            {
                Task<InsteadOfResult> task = registered.Handler(request);
                if (task == null)
                    return InsteadOfResult.Failed("the handler returned no task");

                InsteadOfResult answer = await task;
                return answer ?? InsteadOfResult.Failed("the handler returned no result");
            }
            catch (Exception ex)
            {
                log($"Plugin {registered.PluginId} time-load handler threw: {ex.Message}", ex);
                return InsteadOfResult.Failed(ex.Message);
            }
            finally
            {
                insideHandler.Value = previous;
            }
        }


        private async Task<TimeLoadPipelineResult> RunOriginalAsync(TimeLoadInput input, Action<bool> setBusy)
        {
            PostWorklogResult posted;

            setBusy?.Invoke(true);
            try
            {
                posted = await original.PostWorklogAsync(
                    input.IssueKey,
                    input.StartTime,
                    input.TimeElapsed,
                    input.Comment,
                    input.EstimateUpdateMethod,
                    input.EstimateUpdateValue);
            }
            finally
            {
                setBusy?.Invoke(false);
            }

            if (posted.Success)
                return new TimeLoadPipelineResult { Outcome = TimeLoadOutcome.Succeeded, TimeLoaded = input.TimeElapsed };

            return new TimeLoadPipelineResult { Outcome = TimeLoadOutcome.Failed, Reason = "the time could not be posted" };
        }


        private TimeLoadPipelineResult Handled(TimeLoadInput input, string pluginId, int writes, TimeSpan measured, TimeSpan declared)
        {
            if (declared == input.TimeElapsed)
            {
                return new TimeLoadPipelineResult
                {
                    Outcome = TimeLoadOutcome.Succeeded,
                    HandledBy = pluginId,
                    WritesMade = writes,
                    TimeLoaded = declared
                };
            }

            string message = $"Plugin {pluginId} says it loaded {Minutes(declared)} on {input.IssueKey}, but {Minutes(input.TimeElapsed)} was confirmed. {Minutes(measured)} reached Jira. {TimerNote(input, measured)} Check the worklogs in Jira before retrying.";
            Notify(input, true, message);

            return new TimeLoadPipelineResult
            {
                Outcome = TimeLoadOutcome.Failed,
                HandledBy = pluginId,
                WritesMade = writes,
                TimeLoaded = measured,
                Reason = $"loaded {Minutes(declared)} of {Minutes(input.TimeElapsed)}"
            };
        }


        private static TimeLoadPipelineResult Cancelled(string pluginId)
        {
            return new TimeLoadPipelineResult { Outcome = TimeLoadOutcome.Cancelled, HandledBy = pluginId, Reason = "cancelled by the user" };
        }


        private TimeLoadPipelineResult FailedWithWrites(TimeLoadInput input, string pluginId, int writes, TimeSpan measured, string reason)
        {
            string message = $"Plugin {pluginId} failed on {input.IssueKey} after {writes} write(s) to Jira, {Minutes(measured)} loaded{Because(reason)}. {TimerNote(input, measured)} Check Jira before retrying.";
            Notify(input, true, message);

            return new TimeLoadPipelineResult
            {
                Outcome = TimeLoadOutcome.Failed,
                HandledBy = pluginId,
                WritesMade = writes,
                TimeLoaded = measured,
                Reason = reason ?? "failed after writing"
            };
        }
        #endregion


        #region helpers
        private static bool IsOwnLoad(TimeLoadSource source, string pluginId)
        {
            return !source.IsUser && string.Equals(source.PluginId, pluginId, StringComparison.OrdinalIgnoreCase);
        }


        private static string Because(string reason)
        {
            return string.IsNullOrEmpty(reason) ? "" : " (" + reason + ")";
        }


        /// <summary>Tells what the timer keeps: what it holds now, less what reached Jira, the same figure <see cref="TimerAfterLoad"/> leaves in it.</summary>
        private static string TimerNote(TimeLoadInput input, TimeSpan measured)
        {
            TimeSpan current = input.CurrentElapsed != null ? input.CurrentElapsed() : input.TimeElapsed;
            return $"The timer was not reset: it keeps the {Minutes(TimerAfterLoad.Remaining(current, measured))} that are left.";
        }


        private static string Minutes(TimeSpan span)
        {
            return span.TotalMinutes.ToString("0.##") + " min";
        }


        /// <summary>Logs the message, and shows it only for loads the user started.</summary>
        private void Notify(TimeLoadInput input, bool serious, string message)
        {
            log(message, null);

            if (!input.Source.IsUser)
                return;

            if (serious)
                Notifier?.Warning(message);
            else
                Notifier?.Info(message);
        }


        private void NotifyObservers(TimeLoadInput input, TimeLoadPipelineResult result)
        {
            if (registry.Observers.Count == 0)
                return;

            var args = new TimeLoadedEventArgs(
                input.IssueKey,
                input.StartTime,
                input.TimeElapsed,
                result.TimeLoaded,
                input.Source,
                result.HandledBy,
                result.Outcome,
                result.Reason,
                result.WritesMade);

            foreach (EventHandler<TimeLoadedEventArgs> observer in registry.Observers)
            {
                try
                {
                    observer(this, args);
                }
                catch (Exception ex)
                {
                    log($"A time-load observer threw: {ex.Message}", ex);
                }
            }
        }
        #endregion
    }
}
