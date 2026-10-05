using System.Collections.Generic;
using System.Linq;

namespace SplitTime
{
    /// <summary>One share of the time: an existing subtask, or a subtask still to be created.</summary>
    public sealed class SplitPart
    {
        /// <summary>The key of an existing subtask, or null for a new one.</summary>
        public string ExistingKey { get; set; }

        /// <summary>The summary of the subtask to create, when <see cref="ExistingKey"/> is null.</summary>
        public string NewSummary { get; set; }

        public int Minutes { get; set; }
    }


    /// <summary>How the time is shared.</summary>
    public sealed class SplitPlan
    {
        public List<SplitPart> Parts { get; set; } = new List<SplitPart>();

        public int TotalMinutes
        {
            get { return Parts.Sum(p => p.Minutes); }
        }

        /// <summary>Whether the shares add up to the total, with no negative share.</summary>
        public bool Conserves(int totalMinutes)
        {
            return Parts.Count > 0 && Parts.All(p => p.Minutes >= 0) && TotalMinutes == totalMinutes;
        }
    }
}
