using System;
using System.Collections.Generic;
using System.IO;
using Kodlon.AssetRouter.Data;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    public class AssetRouterPostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> AssetsBeingMoved = new(StringComparer.OrdinalIgnoreCase);

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

                var currentFolder = (Path.GetDirectoryName(assetPath) ?? "")
                    .Replace('\\', '/')
                    .TrimEnd('/') + "/";

                var targetFolder = rule.targetFolder.TrimEnd('/') + "/";

                if (string.Equals(currentFolder, targetFolder, StringComparison.OrdinalIgnoreCase))
                    continue;

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
            var importRule = rule as ImportRule;

            if (importRule?.preset == null)
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
            if (!File.Exists(Path.Combine(Application.dataPath.Replace("Assets", ""), assetPath)))
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
            var targetFolder = rule.targetFolder.TrimEnd('/') + "/";
            var targetPath = targetFolder + Path.GetFileName(assetPath);

            EnsureFolderExists(targetFolder.TrimEnd('/'));

            AssetsBeingMoved.Add(targetPath);

            var error = AssetDatabase.MoveAsset(assetPath, targetPath);

            if (!string.IsNullOrEmpty(error))
            {
                AssetsBeingMoved.Remove(targetPath);
                Debug.LogWarning($"[AssetRouter] Failed to move {assetPath} -> {targetPath}: {error}");
            }
            else
                Debug.Log($"[AssetRouter] Moved: {assetPath} -> {targetPath} ({rule.ruleName})");
        }
    }
}