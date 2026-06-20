using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class BatchMover
    {
        public static BatchResult Move(List<DryRunEntry> entries, bool forceReimportInPlace = false)
        {
            if (entries == null || entries.Count == 0)
                return new BatchResult(0, 0, 0);

            var moved   = 0;
            var skipped = 0;
            var errored = 0;
            var logEntries      = new List<OperationLogEntry>();
            var forceReimports  = new List<string>();
            var total   = entries.Count;
            var current = 0;
            var cancelled = false;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var entry in entries)
                {
                    if (cancelled)
                    {
                        skipped++;
                        continue;
                    }

                    if (!entry.Selected || entry.MatchedRule == null)
                    {
                        skipped++;
                        current++;
                        continue;
                    }

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Asset Router — Applying",
                            entry.AssetPath,
                            (float)current / total))
                    {
                        cancelled = true;
                        skipped++;
                        current++;
                        continue;
                    }

                    if (entry.AlreadyInPlace)
                    {
                        if (forceReimportInPlace)
                            forceReimports.Add(entry.AssetPath);
                        else
                            skipped++;

                        current++;
                        continue;
                    }

                    var targetFolder = PathUtility.NormalizeAssetPath(entry.MatchedRule.targetFolder);
                    EnsureFolderExists(targetFolder);
                    var targetPath = targetFolder + "/" + Path.GetFileName(entry.AssetPath);

                    var error = AssetDatabase.MoveAsset(entry.AssetPath, targetPath);

                    if (string.IsNullOrEmpty(error))
                    {
                        logEntries.Add(new OperationLogEntry(entry.AssetPath, targetPath, entry.MatchedRule.ruleName));
                        moved++;
                    }
                    else
                    {
                        Debug.LogWarning($"[AssetRouter] Failed to move {entry.AssetPath} -> {targetPath}: {error}");
                        errored++;
                    }

                    current++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            // Force-reimport in-place assets after the batch (outside StartAssetEditing).
            foreach (var path in forceReimports)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                moved++;
            }

            if (logEntries.Count > 0)
                OperationLog.RecordBatch(logEntries, "BatchMover");

            Debug.Log($"[AssetRouter] Batch complete. {new BatchResult(moved, skipped, errored)}");
            return new BatchResult(moved, skipped, errored);
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parts   = folderPath.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;

                var next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
