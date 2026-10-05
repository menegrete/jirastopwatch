using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StopWatch.Plugin;

namespace SplitTime
{
    /// <summary>Asks the user how to share the time. Returns null when they cancel.</summary>
    public interface ISplitPrompt
    {
        SplitPlan Ask(string issueKey, IReadOnlyList<PluginSubtask> subtasks, int totalMinutes);
    }


    /// <summary>
    /// The replacement handler: shares the time of an issue among its
    /// subtasks. It shows what the whole contract allows:
    ///
    /// - declines what is not for it (loads from other plugins, issues
    ///   without subtasks);
    /// - cancels when the user closes its dialog;
    /// - writes only through <c>request.Jira</c>, so the host can count the
    ///   writes and tell a failure before writing (the host falls back to the
    ///   standard load) from one after (it does not, and the timer stays);
    /// - reports handled with the total it loaded, which the host checks.
    ///
    /// Safe to repeat: a split that failed halfway is resumed, not redone.
    /// </summary>
    public sealed class SplitTimeHandler
    {
        private readonly ISplitPrompt prompt;
        private readonly SplitLedger ledger;
        private readonly Action<string> log;
        private readonly Action<string> tellUser;

        public SplitTimeHandler(ISplitPrompt prompt, SplitLedger ledger, Action<string> log, Action<string> tellUser)
        {
            this.prompt = prompt;
            this.ledger = ledger;
            this.log = log;
            this.tellUser = tellUser;
        }


        public async Task<InsteadOfResult> HandleAsync(TimeLoadRequest request)
        {
            // Loads started by other plugins, an importer for instance, are
            // not asked about.
            if (!request.Source.IsUser)
                return InsteadOfResult.Declined();

            int total = (int)Math.Round(request.TimeElapsed.TotalMinutes);
            string id = SplitLedger.IdFor(request.IssueKey, request.StartTime, total);

            SplitPlan plan = ledger.Find(id);
            if (plan == null)
            {
                IReadOnlyList<PluginSubtask> subtasks = await request.Jira.GetSubtasksAsync(request.IssueKey);
                if (subtasks == null)
                    return InsteadOfResult.Failed("the subtasks could not be read");
                if (subtasks.Count == 0)
                    return InsteadOfResult.Declined();

                plan = prompt.Ask(request.IssueKey, subtasks, total);
                if (plan == null)
                    return InsteadOfResult.Cancelled();

                if (!plan.Conserves(total))
                    return InsteadOfResult.Failed("the split does not add up to the total");

                ledger.Save(id, plan);
            }
            else
            {
                log($"Resuming the split of {request.IssueKey}");
            }

            return await ExecuteAsync(request, id, plan);
        }


        private async Task<InsteadOfResult> ExecuteAsync(TimeLoadRequest request, string id, SplitPlan plan)
        {
            bool earlierProgress = plan.HasProgress;
            int writesThisRun = 0;
            DateTimeOffset start = request.StartTime;

            foreach (SplitPart part in plan.Parts)
            {
                DateTimeOffset partStart = start;
                start = start.AddMinutes(part.Minutes);

                if (part.Posted || part.Minutes == 0)
                    continue;

                string target = part.ExistingKey ?? part.CreatedKey;
                if (target == null)
                {
                    target = await request.Jira.CreateSubtaskAsync(request.IssueKey, part.NewSummary);
                    if (target == null)
                        return Stop(request, id, writesThisRun, earlierProgress, $"the subtask \"{part.NewSummary}\" could not be created");

                    part.CreatedKey = target;
                    writesThisRun++;
                    ledger.Save(id, plan);
                }

                bool posted = await request.Jira.AddWorklogAsync(target, partStart, TimeSpan.FromMinutes(part.Minutes), request.Comment, request.EstimateUpdate, request.EstimateValue);
                if (!posted)
                    return Stop(request, id, writesThisRun, earlierProgress, $"the worklog on {target} was not accepted");

                part.Posted = true;
                writesThisRun++;
                ledger.Save(id, plan);
            }

            ledger.Remove(id);
            return InsteadOfResult.Handled(request.TimeElapsed);
        }


        /// <summary>
        /// A share failed. What to answer depends on what the host will see:
        /// it counts only the writes made in this invocation.
        /// </summary>
        private InsteadOfResult Stop(TimeLoadRequest request, string id, int writesThisRun, bool earlierProgress, string reason)
        {
            log($"Split of {request.IssueKey} stopped: {reason}");

            if (writesThisRun > 0)
                return InsteadOfResult.Failed(reason);

            if (earlierProgress)
            {
                // An earlier attempt already wrote part of the split, but this
                // one wrote nothing, so the host would see no writes and fall
                // back to the standard load, which would load the whole time
                // again on top of it. Say so here and cancel instead.
                tellUser($"Part of the split of {request.IssueKey} was already loaded in Jira, and {reason}. Nothing else was loaded and the timer was kept; post again to retry.");
                return InsteadOfResult.Cancelled();
            }

            // Nothing was written at all: the host falls back to the standard
            // load, so the half-made plan has no reason to stay.
            ledger.Remove(id);
            return InsteadOfResult.Failed(reason);
        }
    }
}
