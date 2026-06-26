using System.Text;
using Kodlon.AssetRouter.Data;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class RuleMigrator
    {
        public static void MigrateIfNeeded(ImporterSettingsDatabase db)
        {
            if (db.schemaVersion >= ImporterSettingsDatabase.LatestSchemaVersion)
                return;

            var migrated = false;

            if (db.schemaVersion < 2)
            {
                MigrateToV2(db);
                migrated = true;
            }

            if (!migrated)
                return;

            db.schemaVersion = ImporterSettingsDatabase.LatestSchemaVersion;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AssetRouter][Migration] Database migrated to schema v{ImporterSettingsDatabase.LatestSchemaVersion}.");
        }

        private static void MigrateToV2(ImporterSettingsDatabase db)
        {
            if (db.rules == null)
                return;

            var count = 0;

            for (var i = 0; i < db.rules.Count; i++)
            {
                var rule = db.rules[i];

                if (rule == null)
                    continue;

                if (!string.IsNullOrEmpty(rule.pattern))
                    continue;

                var prefix = rule._legacyPrefix ?? "";
                var suffix = rule._legacySuffix ?? "";
                var ext = rule._legacyExtensionFilter ?? "";

                if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(suffix) && string.IsNullOrEmpty(ext))
                    continue;

                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(prefix)) sb.Append(prefix);
                sb.Append('*');
                if (!string.IsNullOrEmpty(suffix)) sb.Append(suffix);
                if (!string.IsNullOrEmpty(ext)) sb.Append(ext);

                rule.pattern = sb.ToString();
                rule.patternMode = PatternMode.Glob;
                rule.matchAgainstFullPath = false;

                count++;
            }

            if (count > 0)
                Debug.Log($"[AssetRouter][Migration] {count} rule(s) migrated from prefix/suffix/extension to glob pattern.");
        }
    }
}
