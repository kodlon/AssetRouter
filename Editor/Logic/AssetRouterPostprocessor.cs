using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            var db = DatabaseLocator.FindDatabase();

            if (db == null || !db.enableAutoImport)
                return;

            var toMove            = new List<AssetMoveCandidate>();
            var unknownAssets     = new List<string>();
            var batchMatchedRules = new List<string>();
            var pendingRuns       = new List<(BaseImportRule Rule, string Path)>();

            foreach (var assetPath in importedAssets)
            {
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
                var currentFolder  = PathUtility.NormalizeAssetPath(Path.GetDirectoryName(assetPath) ?? "") + "/";
                var targetFolder   = PathUtility.NormalizeAssetPath(resolvedTarget) + "/";
                var alreadyInPlace = string.Equals(currentFolder, targetFolder, StringComparison.OrdinalIgnoreCase);

                if (alreadyInPlace)
                {
                    pendingRuns.Add((rule, assetPath));
                    continue;
                }

                toMove.Add(new AssetMoveCandidate(assetPath, rule, ruleMatch.Value.Match));
            }

            if (toMove.Count > 0)
                pendingRuns.AddRange(ExecuteMovesBatched(toMove));

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
                            ActionPipeline.Execute(rule, path, db);
                        }
                        finally
                        {
                            PipelineOutputGuard.EndRun(path);
                        }
                    }
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

        private static List<(BaseImportRule Rule, string Path)> ExecuteMovesBatched(List<AssetMoveCandidate> candidates)
        {
            foreach (var candidate in candidates)
            {
                var resolved = TargetResolver.Resolve(candidate.Rule.targetFolder, candidate.Match);
                PathUtility.EnsureFolderExists(PathUtility.NormalizeAssetPath(resolved));
            }

            var logEntries = new List<OperationLogEntry>();
            // AssetDatabase.MoveAsset does not trigger a reimport, so the moved asset never comes back
            // through OnPostprocessAllAssets' importedAssets — the caller runs the pipeline for it instead.
            var moved = new List<(BaseImportRule Rule, string Path)>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var candidate in candidates)
                {
                    if (MoveToTargetFolder(candidate.Path, candidate.Rule, candidate.Match, out var targetPath))
                    {
                        logEntries.Add(new OperationLogEntry(candidate.Path, targetPath, candidate.Rule.ruleName));
                        moved.Add((candidate.Rule, targetPath));
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (logEntries.Count > 0)
                OperationLog.RecordBatch(logEntries, "AutoImport");

            return moved;
        }

        private static bool MoveToTargetFolder(string assetPath, BaseImportRule rule, System.Text.RegularExpressions.Match match, out string targetPath)
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

            Debug.Log($"[AssetRouter] Moved: {assetPath} -> {targetPath} ({rule.ruleName})");
            return true;
        }

        private static void HandleUnknownAssets(List<string> assetPaths)
        {
            var existing = assetPaths
                .Where(p => File.Exists(PathUtility.ToAbsolute(p)))
                .ToList();

            if (existing.Count == 0)
                return;

            var fileList = string.Join("\n", existing.Select(p => $"  • {Path.GetFileName(p)}"));

            // DisplayDialogComplex's cancel action is the "cancel" button — Esc/close is a safe no-op here,
            // unlike DisplayDialog where Esc silently picks the second button (which used to be "Delete all").
            var choice = EditorUtility.DisplayDialogComplex(
                $"Asset Router — {existing.Count} Unknown File(s)",
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
                    var confirmed = EditorUtility.DisplayDialog(
                        "Asset Router — Confirm Delete",
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

                default: // Cancel (1) or Esc — leave the files as imported, untouched
                    break;
            }
        }

        private void ApplyPreset(BaseImportRule rule)
        {
            if (rule is not ImportRule importRule || importRule.preset == null)
                return;

            if (importRule.preset.ApplyTo(assetImporter))
                Debug.Log($"[AssetRouter] Preset applied ({importRule.ruleName}) -> {assetPath}");
            else
                Debug.LogWarning($"[AssetRouter] Preset type mismatch for {assetPath} ({importRule.ruleName})");
        }
    }
}
