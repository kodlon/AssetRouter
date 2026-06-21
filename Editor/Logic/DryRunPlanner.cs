using System;
using System.Collections.Generic;
using System.IO;
using Kodlon.AssetRouter.Data;
using UnityEditor;

namespace Kodlon.AssetRouter.Logic
{
    internal static class DryRunPlanner
    {
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
                    if (i % 100 == 0)
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

                    var rule = RuleValidator.FindMatchingRule(db.rules, path);

                    if (rule == null)
                    {
                        result.Add(new DryRunEntry(path, null, null, false));
                        continue;
                    }

                    var targetFolder  = PathUtility.NormalizeAssetPath(rule.targetFolder) + "/";
                    var currentFolder = PathUtility.NormalizeAssetPath(Path.GetDirectoryName(path) ?? "") + "/";
                    var alreadyInPlace = string.Equals(currentFolder, targetFolder, StringComparison.OrdinalIgnoreCase);
                    var targetPath = alreadyInPlace ? null : targetFolder + Path.GetFileName(path);

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
