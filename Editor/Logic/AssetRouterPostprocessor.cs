using System;
using System.Collections.Generic;
using System.IO;
using Kodlon.AssetRouter.Data;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal sealed class AssetRouterPostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> AssetsBeingMoved = new(StringComparer.OrdinalIgnoreCase);

        // Stores the matched rule for an asset that was moved, keyed by its target path.
        // ActionPipeline runs when the reimport at the target path arrives.
        private static readonly Dictionary<string, BaseImportRule> _pendingActions =
            new(StringComparer.OrdinalIgnoreCase);

        // Clear both guards on assembly reload and play-mode entry so stale entries
        // cannot block imports when domain reload is disabled.
        [InitializeOnLoadMethod]
        private static void RegisterClearHooks()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                AssetsBeingMoved.Clear();
                _pendingActions.Clear();
            };
        }

        [InitializeOnEnterPlayMode]
        private static void OnEnterPlayMode()
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

            var toMove = new List<AssetMoveCandidate>();
            var unknownAssets = new List<string>();

            foreach (var assetPath in importedAssets)
            {
                if (AssetsBeingMoved.Contains(assetPath))
                {
                    AssetsBeingMoved.Remove(assetPath);

                    // Asset was just moved and reimported at its target path — run pending actions.
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

                    continue;
                }

                var currentFolder = PathUtility.NormalizeAssetPath(Path.GetDirectoryName(assetPath) ?? "") + "/";
                var targetFolder = PathUtility.NormalizeAssetPath(rule.targetFolder) + "/";

                if (string.Equals(currentFolder, targetFolder, StringComparison.OrdinalIgnoreCase))
                {
                    // Already in the correct folder — run actions immediately.
                    ActionPipeline.Execute(rule, assetPath, db);
                    continue;
                }

                toMove.Add(new AssetMoveCandidate(assetPath, rule));
            }

            foreach (var candidate in toMove)
                MoveToTargetFolder(candidate.Path, candidate.Rule);

            foreach (var unknownPath in unknownAssets)
            {
                var captured = unknownPath;
                EditorApplication.delayCall += () => HandleUnknownAsset(captured);
            }
        }

        private void OnPreprocessAsset()
        {
            var db = DatabaseLocator.FindDatabase();

            if (db == null || !db.enableAutoImport)
                return;

            if (!RuleValidator.ShouldProcess(db, assetPath))
                return;

            var rule = RuleValidator.FindMatchingRule(db.rules, assetPath);
            ApplyPreset(rule);
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

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parts = folderPath.Split('/');
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

        private static void HandleUnknownAsset(string assetPath)
        {
            // PathUtility.ToAbsolute uses Path.GetDirectoryName(Application.dataPath) as the
            // project root — avoids the "Replace("Assets", "")" bug that would corrupt any
            // path containing the word "Assets" more than once.
            if (!File.Exists(PathUtility.ToAbsolute(assetPath)))
                return;

            var fileName = Path.GetFileName(assetPath);

            var importAsIs = EditorUtility.DisplayDialog("Asset Router — Unknown File",
                $"File \"{fileName}\" does not match any rule.\n\nPath: {assetPath}\n\nWhat to do?",
                "Import as-is",
                "Delete file");

            if (importAsIs)
                Debug.LogWarning($"[AssetRouter] \"{fileName}\" imported without a matching rule. Check the config.");
            else
            {
                if (AssetDatabase.DeleteAsset(assetPath))
                    Debug.Log($"[AssetRouter] \"{fileName}\" deleted. Rename it and try again.");
                else
                    Debug.LogWarning($"[AssetRouter] Failed to delete \"{assetPath}\".");
            }
        }

        private static void MoveToTargetFolder(string assetPath, BaseImportRule rule)
        {
            var targetFolder = PathUtility.NormalizeAssetPath(rule.targetFolder) + "/";
            var targetPath = targetFolder + Path.GetFileName(assetPath);

            EnsureFolderExists(targetFolder.TrimEnd('/'));

            AssetsBeingMoved.Add(targetPath);
            _pendingActions[targetPath] = rule;

            var error = AssetDatabase.MoveAsset(assetPath, targetPath);

            if (!string.IsNullOrEmpty(error))
            {
                AssetsBeingMoved.Remove(targetPath);
                _pendingActions.Remove(targetPath);
                Debug.LogWarning($"[AssetRouter] Failed to move {assetPath} -> {targetPath}: {error}");
            }
            else
                Debug.Log($"[AssetRouter] Moved: {assetPath} -> {targetPath} ({rule.ruleName})");
        }
    }
}
