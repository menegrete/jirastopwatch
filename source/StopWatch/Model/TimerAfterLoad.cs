using System;

namespace StopWatch
{
    /// <summary>
    /// What a timer holds after a load: nothing when it succeeded, and only
    /// what is not in Jira when it did not.
    ///
    /// After a partial load the timer is reduced, not reset, by the time the
    /// host measured as loaded. The next load - the user's retry, or the
    /// standard load a failing retry falls back to - then covers just the
    /// remainder, and no plugin has to recognize that it is seeing the same
    /// load again.
    ///
    /// The timer is reduced through <paramref name="setElapsed"/>, which keeps
    /// its recorded start time and whether it runs, and from the value it holds
    /// when the load ends, so time that accrued while a handler waited is not
    /// lost.
    /// </summary>
    internal static class TimerAfterLoad
    {
        /// <summary>What is left of <paramref name="current"/> once <paramref name="loaded"/> is in Jira; never below zero.</summary>
        public static TimeSpan Remaining(TimeSpan current, TimeSpan loaded)
        {
            TimeSpan left = current - loaded;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }


        public static void Apply(TimeLoadPipelineResult result, Func<TimeSpan> currentElapsed, Action<TimeSpan> setElapsed, Action reset)
        {
            if (result.Outcome == Plugin.TimeLoadOutcome.Succeeded)
            {
                reset();
                return;
            }

            if (result.TimeLoaded <= TimeSpan.Zero)
                return;

            setElapsed(Remaining(currentElapsed(), result.TimeLoaded));
        }
    }
}
