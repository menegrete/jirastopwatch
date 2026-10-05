using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SplitTime
{
    /// <summary>
    /// Remembers, in the plugin's data directory, what a split already wrote.
    ///
    /// When a split fails halfway the host keeps the timer and the user posts
    /// again: the same issue, start and elapsed time. The hash of those three
    /// is how the plugin recognizes "the same split" and carries on with the
    /// shares that are missing, instead of writing the others twice.
    /// </summary>
    public sealed class SplitLedger
    {
        private readonly string file;

        public SplitLedger(string directory)
        {
            file = Path.Combine(directory, "splits.json");
        }


        public static string IdFor(string issueKey, DateTimeOffset start, int totalMinutes)
        {
            byte[] bytes = Encoding.UTF8.GetBytes($"{issueKey}|{start.ToUniversalTime():O}|{totalMinutes}");
            return Convert.ToHexString(SHA256.HashData(bytes));
        }


        public SplitPlan Find(string id)
        {
            Dictionary<string, SplitPlan> all = Read();
            return all.TryGetValue(id, out SplitPlan plan) ? plan : null;
        }


        public void Save(string id, SplitPlan plan)
        {
            Dictionary<string, SplitPlan> all = Read();
            all[id] = plan;
            Write(all);
        }


        public void Remove(string id)
        {
            Dictionary<string, SplitPlan> all = Read();
            if (all.Remove(id))
                Write(all);
        }


        private Dictionary<string, SplitPlan> Read()
        {
            try
            {
                if (File.Exists(file))
                    return JsonSerializer.Deserialize<Dictionary<string, SplitPlan>>(File.ReadAllText(file)) ?? new Dictionary<string, SplitPlan>();
            }
            catch (JsonException)
            {
                // A damaged ledger is the same as an empty one: the worst
                // case is a split that is asked for again.
            }

            return new Dictionary<string, SplitPlan>();
        }


        private void Write(Dictionary<string, SplitPlan> all)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, JsonSerializer.Serialize(all));
        }
    }
}
