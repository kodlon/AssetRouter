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
                return new BatchResult(0, 0, 0, 0);

            var moved          = 0;
            var reimported     = 0;
            var skipped        = 0;
            var errored        = 0;
            var logEntries     = new List<OperationLogEntry>();
            var forceReimports = new List<string>();
            var total   = entries.Count;
            var current = 0;

            foreach (var entry in entries)
            {
                if (entry.Selected && entry.MatchedRule != null && !entry.AlreadyInPlace && entry.TargetPath != null)
                    PathUtility.EnsureFolderExists(PathUtility.NormalizeAssetPath(Path.GetDirectoryName(entry.TargetPath) ?? ""));
            }

            AssetDatabase.StartAssetEditing();
            var cancelled = false;
            try
            {
                foreach (var entry in entries)
                {
                    current++;

                    if (cancelled)
                    {
                        skipped++;
                        continue;
                    }

                    if (!entry.Selected || entry.MatchedRule == null)
                    {
                        skipped++;
                        continue;
                    }

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Asset Router — Applying",
                            entry.AssetPath,
                            (float)current / total))
                    {
                        cancelled = true;
                        skipped++;
                        continue;
                    }

                    if (entry.AlreadyInPlace)
                    {
                        if (forceReimportInPlace)
                            forceReimports.Add(entry.AssetPath);
                        else
                            skipped++;

                        continue;
                    }

                    var targetPath = entry.TargetPath;

                    if (string.IsNullOrEmpty(targetPath))
                    {
                        skipped++;
                        continue;
                    }

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
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            foreach (var path in forceReimports)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                reimported++;
            }

            if (logEntries.Count > 0)
                OperationLog.RecordBatch(logEntries, "BatchMover");

            var result = new BatchResult(moved, reimported, skipped, errored);
            Debug.Log($"[AssetRouter] Batch complete. {result}");
            return result;
        }
    }
}
