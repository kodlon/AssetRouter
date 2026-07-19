using System.Collections.Generic;
using System.IO;
using Kodlon.AssetRouter.Data;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class BatchMover
    {
        public static BatchResult Move(List<DryRunEntry> entries, ImporterSettingsDatabase db, bool forceReimportInPlace = false)
        {
            if (entries == null || entries.Count == 0)
                return new BatchResult(0, 0, 0, 0);

            var moved          = 0;
            var reimported     = 0;
            var skipped        = 0;
            var errored        = 0;
            var logEntries     = new List<OperationLogEntry>();
            var forceReimports = new List<DryRunEntry>();
            // Everything that ends up here gets its preset (re)applied and its rule's post-import actions
            // run — mirroring what the live auto-import pipeline does. Without this, "Apply Selected"/
            // "Force Re-import In-Place" would silently skip both, unlike routing a freshly-dropped file.
            var toProcess = new List<(BaseImportRule Rule, string Path, bool ForceReimport)>();
            var artifactCollector = new ArtifactCollector();
            var total   = entries.Count;
            var current = 0;

            foreach (var entry in entries)
            {
                if (entry.Selected && entry.MatchedRule != null && !entry.AlreadyInPlace && entry.TargetPath != null)
                {
                    var created = PathUtility.EnsureFolderExists(PathUtility.NormalizeAssetPath(Path.GetDirectoryName(entry.TargetPath) ?? ""));
                    artifactCollector.OnFoldersCreated(created);
                }
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
                            forceReimports.Add(entry);
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
                        toProcess.Add((entry.MatchedRule, targetPath, false));
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

            foreach (var entry in forceReimports)
            {
                reimported++;
                toProcess.Add((entry.MatchedRule, entry.AssetPath, true));
            }

            foreach (var (rule, path, forceReimport) in toProcess)
            {
                PipelineOutputGuard.BeginRun(path);
                try
                {
                    ApplyPresetAndReimport(rule, path, forceReimport);
                    ActionPipeline.Execute(rule, path, db, artifactCollector);
                }
                finally
                {
                    PipelineOutputGuard.EndRun(path);
                }
            }

            // Record after actions — synchronous flow means the collector has everything by now.
            if (logEntries.Count > 0)
                OperationLog.RecordBatch(logEntries, "BatchMover", artifactCollector.Assets, artifactCollector.Folders);

            var result = new BatchResult(moved, reimported, skipped, errored);
            Debug.Log($"[AssetRouter] Batch complete. {result}");
            return result;
        }

        private static void ApplyPresetAndReimport(BaseImportRule rule, string assetPath, bool forceReimport)
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
                return;

            var presetApplied = false;

            if (rule is ImportRule importRule && importRule.preset != null)
            {
                if (importRule.preset.ApplyTo(importer))
                    presetApplied = true;
                else
                    Debug.LogWarning($"[AssetRouter] BatchMover: preset type mismatch for {assetPath} ({importRule.ruleName})");
            }

            // Only force a reimport when something actually needs to be persisted, or the user explicitly
            // asked for one via "Force Re-import In-Place" — a plain move with no preset needs neither.
            if (presetApplied || forceReimport)
                importer.SaveAndReimport();
        }
    }
}
