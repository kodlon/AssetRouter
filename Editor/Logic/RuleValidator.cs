using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Kodlon.AssetRouter.Data;

namespace Kodlon.AssetRouter.Logic
{
    internal readonly struct RuleMatch
    {
        public readonly BaseImportRule Rule;
        public readonly Match Match;

        public RuleMatch(BaseImportRule rule, Match match)
        {
            Rule = rule;
            Match = match;
        }
    }

    internal static class RuleValidator
    {
        public static RuleMatch? FindMatchingRule(List<BaseImportRule> rules, string assetPath)
        {
            if (rules == null || rules.Count == 0)
                return null;

            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];

                if (rule == null || !rule.isEnabled || string.IsNullOrEmpty(rule.pattern))
                    continue;

                if (!string.IsNullOrEmpty(rule.scopeFolder) &&
                    !PathUtility.IsUnderFolder(assetPath, rule.scopeFolder))
                    continue;

                var m = PatternMatcher.Match(rule, assetPath);
                if (m != null)
                    return new RuleMatch(rule, m);
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

            if (db.monitoredExtensions != null)
            {
                for (var i = 0; i < db.monitoredExtensions.Count; i++)
                {
                    if (db.monitoredExtensions[i] == null) continue;
                    if (db.monitoredExtensions[i].Equals(extension, StringComparison.OrdinalIgnoreCase))
                    {
                        monitored = true;
                        break;
                    }
                }
            }

            if (!monitored)
                return false;

            if (db.ignoredFolders != null)
            {
                for (var i = 0; i < db.ignoredFolders.Count; i++)
                {
                    if (PathUtility.IsUnderFolder(assetPath, db.ignoredFolders[i]))
                        return false;
                }
            }

            return true;
        }
    }
}
