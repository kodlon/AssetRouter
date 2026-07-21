using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class OperationLog
    {
        private const int MaxSessions = 500;

        /// <summary>
        /// Test-only override so tests don't read/write the real project's
        /// operation log.
        /// </summary>
        internal static string OverrideLogPathForTests;

        private static string LogPath =>
            OverrideLogPathForTests ?? Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
                "Library", "AssetRouter", "log.json");

        public static void Clear() => WriteLogFile(new OperationLogFile());

        public static List<OperationSession> ReadAll()
        {
            var sessions = ReadLogFile().sessions ?? new List<OperationSession>();

            // v=1 sessions have no createdAssets/createdFolders — JsonUtility leaves them null.
            foreach (var s in sessions)
            {
                s.createdAssets ??= new List<string>();
                s.createdFolders ??= new List<string>();
            }

            return sessions;
        }

        public static void RecordBatch(
            List<OperationLogEntry> entries,
            string source = "AutoImport",
            IEnumerable<string> createdAssets = null,
            IEnumerable<string> createdFolders = null)
        {
            if (entries == null || entries.Count == 0)
                return;

            var log = ReadLogFile();

            log.sessions.Add(new OperationSession
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                source = source,
                entries = new List<OperationLogEntry>(entries),
                createdAssets = createdAssets == null ? new List<string>() : new List<string>(createdAssets),
                createdFolders = createdFolders == null ? new List<string>() : new List<string>(createdFolders)
            });

            if (log.sessions.Count > MaxSessions)
                log.sessions.RemoveRange(0, log.sessions.Count - MaxSessions);

            WriteLogFile(log);
        }

        private static OperationLogFile ReadLogFile()
        {
            var path = LogPath;

            if (!File.Exists(path))
                return new OperationLogFile();

            try
            {
                var json = File.ReadAllText(path);

                return JsonUtility.FromJson<OperationLogFile>(json) ?? new OperationLogFile();
            }
            catch (Exception e)
            {
                var corruptPath = path + ".corrupt";
                var corruptSaved = false;

                try
                {
                    File.Copy(path, corruptPath, true);
                    corruptSaved = true;
                }
                catch (Exception)
                {
                    /* best-effort */
                }

                var corruptNote = corruptSaved
                    ? $"Corrupt copy saved to: {corruptPath}"
                    : "Could not save corrupt copy (disk full or permissions).";

                Debug.LogWarning("[AssetRouter] Operation log was corrupted and has been reset. " +
                                 $"{corruptNote}\n{e.Message}");

                return new OperationLogFile();
            }
        }

        private static void WriteLogFile(OperationLogFile log)
        {
            var path = LogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";

            try
            {
                File.WriteAllText(tmp, JsonUtility.ToJson(log, true));

                if (File.Exists(path))
                    File.Replace(tmp, path, path + ".bak");
                else
                    File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AssetRouter] Failed to write operation log: {e.Message}");

                if (File.Exists(tmp))
                    try
                    {
                        File.Delete(tmp);
                    }
                    catch (Exception)
                    {
                        /* best-effort */
                    }
            }
        }
    }
}