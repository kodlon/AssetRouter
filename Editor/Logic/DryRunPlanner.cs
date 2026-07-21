using System;
using System.Collections.Generic;
using System.IO;
using Kodlon.AssetRouter.Data;
using UnityEditor;

namespace Kodlon.AssetRouter.Logic
{
    internal static class DryRunPlanner
    {
        private const int ProgressBarUpdateEveryNAssets = 100;

        public static List<DryRunEntry> Scan(ImporterSettingsDatabase db)
        {
            var result = new List<DryRunEntry>();

            if (db == null)
                return result;

            var guids = AssetDatabase.FindAssets("", new[] { "Assets" });
            var total = guids.Length;

            try
            {
                for (var i = 0; i < total; i++)
                {
                    if (i % ProgressBarUpdateEveryNAssets == 0)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar(
                                "Asset Router — Scanning",
                                $"{i} / {total} assets",
                                (float)i / Math.Max(total, 1)))
                            break;
                    }

                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);

                    if (AssetDatabase.IsValidFolder(path))
                        continue;

                    if (!RuleValidator.ShouldProcess(db, path))
                        continue;

                    var ruleMatch = RuleValidator.FindMatchingRule(db.rules, path);

                    if (ruleMatch == null)
                    {
                        result.Add(new DryRunEntry(path, null, null, false));
                        continue;
                    }

                    var rule           = ruleMatch.Value.Rule;
                    var resolvedFolder = TargetResolver.Resolve(rule.targetFolder, ruleMatch.Value.Match);
                    var targetFolder   = PathUtility.NormalizeAssetPath(resolvedFolder) + "/";
                    var currentFolder  = PathUtility.NormalizeAssetPath(Path.GetDirectoryName(path) ?? "") + "/";
                    var alreadyInPlace = string.Equals(currentFolder, targetFolder, StringComparison.OrdinalIgnoreCase);
                    var targetPath     = alreadyInPlace ? null : targetFolder + Path.GetFileName(path);

                    result.Add(new DryRunEntry(path, rule, targetPath, alreadyInPlace));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return result;
        }
    }
}
