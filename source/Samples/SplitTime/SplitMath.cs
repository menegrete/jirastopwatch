using System;

namespace SplitTime
{
    /// <summary>
    /// How whole minutes are shared out. Jira takes worklogs in whole
    /// minutes, so the shares must add up to the total exactly: the remainder
    /// goes one minute at a time to the first shares, 7 minutes over 3 parts
    /// being 3, 2 and 2.
    /// </summary>
    public static class SplitMath
    {
        public static int[] Distribute(int totalMinutes, int parts)
        {
            if (parts <= 0)
                throw new ArgumentOutOfRangeException(nameof(parts));
            if (totalMinutes < 0)
                throw new ArgumentOutOfRangeException(nameof(totalMinutes));

            int each = totalMinutes / parts;
            int remainder = totalMinutes % parts;

            var shares = new int[parts];
            for (int i = 0; i < parts; i++)
                shares[i] = each + (i < remainder ? 1 : 0);

            return shares;
        }
    }
}
