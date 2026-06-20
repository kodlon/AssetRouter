using System;
using System.Collections.Generic;
using System.IO;
using Kodlon.AssetRouter.Data;

namespace Kodlon.AssetRouter.Logic
{
    internal static class RuleValidator
    {
        /// <summary>
        /// Returns the first enabled rule whose pattern matches <paramref name="assetPath"/>,
        /// or <c>null</c> if none match.
        /// </summary>
        public static BaseImportRule FindMatchingRule(List<BaseImportRule> rules, string assetPath)
        {
            if (rules == null || rules.Count == 0)
                return null;

            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];

                if (rule == null || !rule.isEnabled || string.IsNullOrEmpty(rule.pattern))
                    continue;

                if (PatternMatcher.Matches(rule, assetPath))
                    return rule;
            }

            return null;
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="assetPath"/> should be processed:
        /// its extension is monitored and its path is not under any ignored folder.
        /// </summary>
        public static bool ShouldProcess(ImporterSettingsDatabase db, string assetPath)
        {
            if (db == null || string.IsNullOrEmpty(assetPath))
                return false;

            var extension = Path.GetExtension(assetPath);

            if (string.IsNullOrEmpty(extension))
                return false;

            var monitored = false;

            for (var i = 0; i < db.monitoredExtensions.Count; i++)
            {
                if (db.monitoredExtensions[i].Equals(extension, StringComparison.OrdinalIgnoreCase))
                {
                    monitored = true;
                    break;
                }
            }

            if (!monitored)
                return false;

            for (var i = 0; i < db.ignoredFolders.Count; i++)
            {
                if (PathUtility.IsUnderFolder(assetPath, db.ignoredFolders[i]))
                    return false;
            }

            return true;
        }
    }
}
