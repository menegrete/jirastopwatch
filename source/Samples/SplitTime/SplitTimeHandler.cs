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
    /// - writes only through <c>request.Jira</c>, so the host can measure what
    ///   reached Jira: a failure before writing makes the host fall back to the
    ///   standard load, one after writing leaves the host to take the loaded
    ///   time off the timer;
    /// - reports handled with the total it loaded, which the host checks.
    ///
    /// It keeps no memory of earlier attempts. After a partial failure the
    /// host leaves in the timer only what is not in Jira, so the user's retry
    /// is a new split of that remainder. A subtask created before the failure
    /// is one more subtask of the issue and is offered in the dialog.
    /// </summary>
    public sealed class SplitTimeHandler
    {
        private readonly ISplitPrompt prompt;
        private readonly Action<string> log;

        public SplitTimeHandler(ISplitPrompt prompt, Action<string> log)
        {
            this.prompt = prompt;
            this.log = log;
        }


        public async Task<InsteadOfResult> HandleAsync(TimeLoadRequest request)
        {
            // Loads started by other plugins, an importer for instance, are
            // not asked about.
            if (!request.Source.IsUser)
                return InsteadOfResult.Declined();

            int total = (int)Math.Round(request.TimeElapsed.TotalMinutes);

            IReadOnlyList<PluginSubtask> subtasks = await request.Jira.GetSubtasksAsync(request.IssueKey);
            if (subtasks == null)
                return InsteadOfResult.Failed("the subtasks could not be read");
            if (subtasks.Count == 0)
                return InsteadOfResult.Declined();

            SplitPlan plan = prompt.Ask(request.IssueKey, subtasks, total);
            if (plan == null)
                return InsteadOfResult.Cancelled();

            if (!plan.Conserves(total))
                return InsteadOfResult.Failed("the split does not add up to the total");

            return await ExecuteAsync(request, plan);
        }


        private async Task<InsteadOfResult> ExecuteAsync(TimeLoadRequest request, SplitPlan plan)
        {
            DateTimeOffset start = request.StartTime;

            foreach (SplitPart part in plan.Parts)
            {
                DateTimeOffset partStart = start;
                start = start.AddMinutes(part.Minutes);

                if (part.Minutes == 0)
                    continue;

                string target = part.ExistingKey;
                if (target == null)
                {
                    target = await request.Jira.CreateSubtaskAsync(request.IssueKey, part.NewSummary);
                    if (target == null)
                        return Stop(request, $"the subtask \"{part.NewSummary}\" could not be created");
                }

                bool posted = await request.Jira.AddWorklogAsync(target, partStart, TimeSpan.FromMinutes(part.Minutes), request.Comment, request.EstimateUpdate, request.EstimateValue);
                if (!posted)
                    return Stop(request, $"the worklog on {target} was not accepted");
            }

            return InsteadOfResult.Handled(request.TimeElapsed);
        }


        private InsteadOfResult Stop(TimeLoadRequest request, string reason)
        {
            log($"Split of {request.IssueKey} stopped: {reason}");
            return InsteadOfResult.Failed(reason);
        }
    }
}
