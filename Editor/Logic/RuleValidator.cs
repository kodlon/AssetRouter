using System;
using System.Collections.Generic;
using System.IO;
using Kodlon.AssetRouter.Data;

namespace Kodlon.AssetRouter.Logic
{
    internal static class RuleValidator
    {
        public static BaseImportRule FindMatchingRule(List<BaseImportRule> rules, string assetPath)
        {
            if (rules == null || rules.Count == 0)
                return null;

            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            var extension = Path.GetExtension(assetPath);

            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];

                if (rule == null || !rule.isEnabled)
                    continue;

                if (string.IsNullOrEmpty(rule.prefix)
                    && string.IsNullOrEmpty(rule.suffix)
                    && string.IsNullOrEmpty(rule.extensionFilter))
                    continue;

                var prefixOk = string.IsNullOrEmpty(rule.prefix)
                               || fileName.StartsWith(rule.prefix, StringComparison.OrdinalIgnoreCase);

                var suffixOk = string.IsNullOrEmpty(rule.suffix)
                               || fileName.EndsWith(rule.suffix, StringComparison.OrdinalIgnoreCase);

                var extOk = string.IsNullOrEmpty(rule.extensionFilter)
                            || rule.extensionFilter.Equals(extension, StringComparison.OrdinalIgnoreCase);

                if (prefixOk && suffixOk && extOk)
                    return rule;
            }

            return null;
        }

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
