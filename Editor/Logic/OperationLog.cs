using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class OperationLog
    {
        private static string LogPath
        {
            get
            {
                var dir = Path.Combine(
                    Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
                    "Library", "AssetRouter");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "log.json");
            }
        }

        public static void RecordBatch(List<OperationLogEntry> entries, string source = "AutoImport")
        {
            if (entries == null || entries.Count == 0)
                return;

            var log = ReadLogFile();
            log.sessions.Add(new OperationSession
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                source    = source,
                entries   = new List<OperationLogEntry>(entries)
            });

            WriteLogFile(log);
        }

        public static List<OperationSession> ReadAll() => ReadLogFile().sessions ?? new List<OperationSession>();

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
            catch
            {
                return new OperationLogFile();
            }
        }

        private static void WriteLogFile(OperationLogFile log)
        {
            var path = LogPath;
            var tmp  = path + ".tmp";

            try
            {
                File.WriteAllText(tmp, JsonUtility.ToJson(log, true));

                if (File.Exists(path))
                    File.Delete(path);

                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AssetRouter] Failed to write operation log: {e.Message}");

                if (File.Exists(tmp))
                    File.Delete(tmp);
            }
        }
    }
}
