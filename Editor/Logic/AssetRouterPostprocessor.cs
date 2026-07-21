using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Kodlon.AssetRouter.Data;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal sealed class AssetRouterPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var db = DatabaseLocator.FindDatabase(true);

            if (db == null || !db.enableAutoImport)
                return;

            var toMove = new List<AssetMoveCandidate>();
            var unknownAssets = new List<string>();
            var batchMatchedRules = new List<string>();
            var pendingRuns = new List<(BaseImportRule Rule, string Path)>();

            // Shared across the whole batch — every action's side effects land in one place per session.
            var artifactCollector = new ArtifactCollector();

            foreach (var assetPath in importedAssets)
            {
                // Assets sitting in the recycle folder are the outcome of a previous Undo — routing
                // them back would immediately undo the undo and create fresh side effects.
                if (assetPath.StartsWith(UndoEngine.RecycleFolder + "/", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip assets the pipeline itself just created (e.g. a generated prefab/material) —
                // otherwise a wildcard rule matching an action's own output would route it again, forever.
                if (PipelineOutputGuard.WasCreatedByPipeline(assetPath))
                    continue;

                // Skip assets whose pipeline run is already in flight — an action that reimports its own
                // trigger asset (e.g. SetPivotAction) would otherwise cause the whole rule to run again.
                if (PipelineOutputGuard.IsRunning(assetPath))
                    continue;

                if (!RuleValidator.ShouldProcess(db, assetPath))
                    continue;

                var ruleMatch = RuleValidator.FindMatchingRule(db.rules, assetPath);

                if (ruleMatch == null)
                {
                    if (db.showPopupForUnknownFiles)
                        unknownAssets.Add(assetPath);

                    continue;
                }

                var rule = ruleMatch.Value.Rule;
                rule._sessionMatchCount++;
                batchMatchedRules.Add(rule.ruleName);

                var resolvedTarget = TargetResolver.Resolve(rule.targetFolder, ruleMatch.Value.Match);
                var currentFolder = PathUtility.NormalizeAssetPath(Path.GetDirectoryName(assetPath) ?? "") + "/";
                var targetFolder = PathUtility.NormalizeAssetPath(resolvedTarget) + "/";
                var alreadyInPlace = string.Equals(currentFolder, targetFolder, StringComparison.OrdinalIgnoreCase);

                if (alreadyInPlace)
                {
                    pendingRuns.Add((rule, assetPath));

                    continue;
                }

                toMove.Add(new AssetMoveCandidate(assetPath, rule, ruleMatch.Value.Match));
            }

            var moveLogEntries = new List<OperationLogEntry>();

            if (toMove.Count > 0)
                pendingRuns.AddRange(ExecuteMovesBatched(toMove, artifactCollector, moveLogEntries));

            if (batchMatchedRules.Count > 0)
                RuleStatsStore.IncrementBatch(batchMatchedRules);

            if (pendingRuns.Count > 0)
            {
                // Mark in-flight synchronously (not inside the delayed closure) so a reimport triggered by
                // one of the rule's own actions is caught even if it happens before delayCall fires.
                foreach (var (_, path) in pendingRuns)
                    PipelineOutputGuard.BeginRun(path);

                // Actions that create assets (CreateAsset/SaveAsPrefabAsset/ImportAsset) must not run from
                // inside OnPostprocessAllAssets — Unity explicitly disallows nested import batches. Deferring
                // via delayCall also means a self-matching output asset gets its own top-level postprocess
                // call, where PipelineOutputGuard can catch and skip it.
                EditorApplication.delayCall += () =>
                {
                    foreach (var (rule, path) in pendingRuns)
                    {
                        try
                        {
                            ActionPipeline.Execute(rule, path, db, artifactCollector);
                        }
                        finally
                        {
                            PipelineOutputGuard.EndRun(path);
                        }
                    }

                    // Deferred so createdAssets captures artifacts built asynchronously by actions.
                    // In-place-only batches don't create a session (RecordBatch needs ≥1 entry).
                    if (moveLogEntries.Count > 0)
                        OperationLog.RecordBatch(moveLogEntries, "AutoImport", artifactCollector.Assets, artifactCollector.Folders);
                };
            }

            if (unknownAssets.Count > 0)
            {
                Debug.LogWarning($"[AssetRouter] {unknownAssets.Count} file(s) matched no rule " +
                                 "— dialog will appear shortly.");

                var captured = new List<string>(unknownAssets);
                EditorApplication.delayCall += () => HandleUnknownAssets(captured);
            }
        }

        private void OnPreprocessAsset()
        {
            var db = DatabaseLocator.FindDatabase();

            if (db == null || !db.enableAutoImport)
                return;

            if (!RuleValidator.ShouldProcess(db, assetPath))
                return;

            var ruleMatch = RuleValidator.FindMatchingRule(db.rules, assetPath);
            ApplyPreset(ruleMatch?.Rule);
        }

        private void ApplyPreset(BaseImportRule rule)
        {
            if (rule is not ImportRule importRule || importRule.preset == null)
                return;

            if (!importRule.preset.ApplyTo(assetImporter))
                Debug.LogWarning($"[AssetRouter] Preset type mismatch for {assetPath} ({importRule.ruleName})");
        }

        private static List<(BaseImportRule Rule, string Path)> ExecuteMovesBatched(
            List<AssetMoveCandidate> candidates,
            IArtifactSink artifactSink,
            List<OperationLogEntry> logEntriesOut)
        {
            // Folders must be created before StartAssetEditing — inside a batch CreateFolder is a no-op.
            foreach (var candidate in candidates)
            {
                var resolved = TargetResolver.Resolve(candidate.Rule.targetFolder, candidate.Match);
                artifactSink?.OnFoldersCreated(PathUtility.EnsureFolderExists(PathUtility.NormalizeAssetPath(resolved)));
            }

            var moved = new List<(BaseImportRule Rule, string Path)>();

            AssetDatabase.StartAssetEditing();

            try
            {
                foreach (var candidate in candidates)
                {
                    if (MoveToTargetFolder(candidate.Path, candidate.Rule, candidate.Match, out var targetPath))
                    {
                        logEntriesOut.Add(new OperationLogEntry(candidate.Path, targetPath, candidate.Rule.ruleName));
                        moved.Add((candidate.Rule, targetPath));
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (logEntriesOut.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append("[AssetRouter] Moved ").Append(logEntriesOut.Count).Append(" asset(s):");

                foreach (var e in logEntriesOut)
                    sb.Append('\n').Append("  ").Append(e.from).Append(" → ").Append(e.to).Append("  (").Append(e.ruleName).Append(')');

                Debug.Log(sb.ToString());
            }

            // Caller writes the session after actions finish (see delayCall in OnPostprocessAllAssets).
            return moved;
        }

        private static void HandleUnknownAssets(List<string> assetPaths)
        {
            var existing = assetPaths
                .Where(p => File.Exists(PathUtility.ToAbsolute(p)))
                .ToList();

            if (existing.Count == 0)
                return;

            // Never prompt in batch/CI mode. Unity forces the default button in headless runs, which
            // in DisplayDialogComplex terms is `0` (Import all as-is) — accidental "Delete all" is
            // out of reach here, but blocking the build waiting for a click still is not. Log and move on.
            if (Application.isBatchMode)
            {
                foreach (var path in existing)
                    Debug.LogWarning($"[AssetRouter] \"{Path.GetFileName(path)}\" imported without a matching rule (batch mode — dialog suppressed).");

                return;
            }

            var fileList = string.Join("\n", existing.Select(p => $"  • {Path.GetFileName(p)}"));

            // DisplayDialogComplex's cancel action is the "cancel" button — Esc/close is a safe no-op here,
            // unlike DisplayDialog where Esc silently picks the second button (which used to be "Delete all").
            var choice = EditorUtility.DisplayDialogComplex($"Asset Router — {existing.Count} Unknown File(s)",
                $"The following files match no import rule:\n{fileList}\n\nWhat would you like to do?",
                "Import all as-is",
                "Cancel",
                "Delete all…");

            switch (choice)
            {
                case 0: // Import all as-is
                    foreach (var path in existing)
                        Debug.LogWarning($"[AssetRouter] \"{Path.GetFileName(path)}\" imported without a matching rule.");

                break;

                case 2: // Delete all…
                    var confirmed = EditorUtility.DisplayDialog("Asset Router — Confirm Delete",
                        $"Permanently delete {existing.Count} file(s)?\n{fileList}\n\nThis cannot be undone.",
                        "Delete",
                        "Cancel");

                    if (!confirmed)
                        break;

                    foreach (var path in existing)
                    {
                        if (AssetDatabase.DeleteAsset(path))
                            Debug.Log($"[AssetRouter] \"{Path.GetFileName(path)}\" deleted.");
                        else
                            Debug.LogWarning($"[AssetRouter] Failed to delete \"{path}\".");
                    }

                break;
            }
        }

        private static bool MoveToTargetFolder(string assetPath, BaseImportRule rule, Match match, out string targetPath)
        {
            var resolvedFolder = TargetResolver.Resolve(rule.targetFolder, match);
            var targetFolder = PathUtility.NormalizeAssetPath(resolvedFolder) + "/";
            targetPath = targetFolder + Path.GetFileName(assetPath);

            var error = AssetDatabase.MoveAsset(assetPath, targetPath);

            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning($"[AssetRouter] Failed to move {assetPath} -> {targetPath}: {error}");

                return false;
            }

            return true;
        }
    }
}