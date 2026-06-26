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
        private static readonly HashSet<string> AssetsBeingMoved = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, BaseImportRule> _pendingActions =
            new(StringComparer.OrdinalIgnoreCase);

        [InitializeOnLoadMethod]
        private static void RegisterClearHooks()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ClearGuards;
            AssemblyReloadEvents.beforeAssemblyReload += ClearGuards;
        }

        [InitializeOnEnterPlayMode]
        private static void OnEnterPlayMode() => ClearGuards();

        private static void ClearGuards()
        {
            AssetsBeingMoved.Clear();
            _pendingActions.Clear();
        }

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

            foreach (var assetPath in importedAssets)
            {
                if (AssetsBeingMoved.Contains(assetPath))
                {
                    AssetsBeingMoved.Remove(assetPath);

                    if (_pendingActions.TryGetValue(assetPath, out var pendingRule))
                    {
                        _pendingActions.Remove(assetPath);
                        ActionPipeline.Execute(pendingRule, assetPath, db);
                    }

                    continue;
                }

                if (!RuleValidator.ShouldProcess(db, assetPath))
                    continue;

                var rule = RuleValidator.FindMatchingRule(db.rules, assetPath);

                if (rule == null)
                {
                    if (db.showPopupForUnknownFiles)
                        unknownAssets.Add(assetPath);

                    if (DiagnosticLog.IsEnabled)
                        DiagnosticLog.Add(assetPath, null, false, false);

                    continue;
                }

                rule._sessionMatchCount++;
                batchMatchedRules.Add(rule.ruleName);

                var currentFolder  = PathUtility.NormalizeAssetPath(Path.GetDirectoryName(assetPath) ?? "") + "/";
                var targetFolder   = PathUtility.NormalizeAssetPath(rule.targetFolder) + "/";
                var alreadyInPlace = string.Equals(currentFolder, targetFolder, StringComparison.OrdinalIgnoreCase);

                if (DiagnosticLog.IsEnabled)
                    DiagnosticLog.Add(assetPath, rule.ruleName, !alreadyInPlace, alreadyInPlace);

                if (alreadyInPlace)
                {
                    ActionPipeline.Execute(rule, assetPath, db);
                    continue;
                }

                toMove.Add(new AssetMoveCandidate(assetPath, rule));
            }

            if (toMove.Count > 0)
                ExecuteMovesBatched(toMove, db);

            if (batchMatchedRules.Count > 0)
                RuleStatsStore.IncrementBatch(batchMatchedRules);

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

            if (AssetsBeingMoved.Contains(assetPath))
                return;

            if (!RuleValidator.ShouldProcess(db, assetPath))
                return;

            var rule = RuleValidator.FindMatchingRule(db.rules, assetPath);
            ApplyPreset(rule);
        }

        private static void ExecuteMovesBatched(List<AssetMoveCandidate> candidates, ImporterSettingsDatabase db)
        {
            foreach (var candidate in candidates)
                PathUtility.EnsureFolderExists(PathUtility.NormalizeAssetPath(candidate.Rule.targetFolder));

            var logEntries = new List<OperationLogEntry>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var candidate in candidates)
                {
                    if (MoveToTargetFolder(candidate.Path, candidate.Rule, out var targetPath))
                        logEntries.Add(new OperationLogEntry(candidate.Path, targetPath, candidate.Rule.ruleName));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            foreach (var entry in logEntries)
                AssetsBeingMoved.Remove(entry.from);

            if (logEntries.Count > 0)
                OperationLog.RecordBatch(logEntries, "AutoImport");
        }

        private static bool MoveToTargetFolder(string assetPath, BaseImportRule rule, out string targetPath)
        {
            var targetFolder = PathUtility.NormalizeAssetPath(rule.targetFolder) + "/";
            targetPath = targetFolder + Path.GetFileName(assetPath);

            AssetsBeingMoved.Add(assetPath);
            AssetsBeingMoved.Add(targetPath);
            _pendingActions[targetPath] = rule;

            var error = AssetDatabase.MoveAsset(assetPath, targetPath);

            if (!string.IsNullOrEmpty(error))
            {
                AssetsBeingMoved.Remove(assetPath);
                AssetsBeingMoved.Remove(targetPath);
                _pendingActions.Remove(targetPath);
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

            var importAll = EditorUtility.DisplayDialog(
                $"Asset Router — {existing.Count} Unknown File(s)",
                $"The following files match no import rule:\n{fileList}\n\nWhat would you like to do?",
                "Import all as-is",
                "Delete all");

            if (importAll)
            {
                foreach (var path in existing)
                    Debug.LogWarning($"[AssetRouter] \"{Path.GetFileName(path)}\" imported without a matching rule.");
            }
            else
            {
                foreach (var path in existing)
                {
                    if (AssetDatabase.DeleteAsset(path))
                        Debug.Log($"[AssetRouter] \"{Path.GetFileName(path)}\" deleted.");
                    else
                        Debug.LogWarning($"[AssetRouter] Failed to delete \"{path}\".");
                }
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
