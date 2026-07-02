using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    [Serializable]
    internal sealed class RuleStatEntry
    {
        public string ruleName;
        public int count;
        public string since;
    }

    [Serializable]
    internal sealed class RuleStatsFile
    {
        public int v = 1;
        public List<RuleStatEntry> entries = new List<RuleStatEntry>();
    }

    internal static class RuleStatsStore
    {
        /// <summary>Test-only override so tests don't read/write the real project's stats file.</summary>
        internal static string OverrideStatsPathForTests;

        private static string StatsPath =>
            OverrideStatsPathForTests ?? Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
                "Library", "AssetRouter", "stats.json");

        public static void IncrementBatch(List<string> ruleNames)
        {
            if (ruleNames == null || ruleNames.Count == 0)
                return;

            var file = ReadFile();

            foreach (var name in ruleNames)
            {
                if (string.IsNullOrEmpty(name))
                    continue;

                RuleStatEntry entry = null;
                for (var i = 0; i < file.entries.Count; i++)
                {
                    if (string.Equals(file.entries[i].ruleName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        entry = file.entries[i];
                        break;
                    }
                }

                if (entry == null)
                {
                    entry = new RuleStatEntry
                    {
                        ruleName = name,
                        count    = 0,
                        since    = DateTime.UtcNow.ToString("o")
                    };
                    file.entries.Add(entry);
                }

                entry.count++;
            }

            WriteFile(file);
        }

        public static Dictionary<string, int> ReadAll()
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var file = ReadFile();

            foreach (var entry in file.entries)
            {
                if (!string.IsNullOrEmpty(entry.ruleName))
                    result[entry.ruleName] = entry.count;
            }

            return result;
        }

        public static void Clear()
        {
            WriteFile(new RuleStatsFile());
        }

        private static RuleStatsFile ReadFile()
        {
            var path = StatsPath;

            if (!File.Exists(path))
                return new RuleStatsFile();

            try
            {
                return JsonUtility.FromJson<RuleStatsFile>(File.ReadAllText(path)) ?? new RuleStatsFile();
            }
            catch
            {
                return new RuleStatsFile();
            }
        }

        private static void WriteFile(RuleStatsFile file)
        {
            var path = StatsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";

            try
            {
                File.WriteAllText(tmp, JsonUtility.ToJson(file, true));

                if (File.Exists(path))
                    File.Replace(tmp, path, path + ".bak");
                else
                    File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AssetRouter] Failed to write stats: {e.Message}");
                if (File.Exists(tmp))
                    try { File.Delete(tmp); } catch { }
            }
        }
    }
}
