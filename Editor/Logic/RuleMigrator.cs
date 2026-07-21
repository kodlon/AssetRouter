using System.Collections.Generic;
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
                MigrateToV2(db.rules);
                migrated = true;
            }

            if (!migrated)
                return;

            db.schemaVersion = ImporterSettingsDatabase.LatestSchemaVersion;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AssetRouter][Migration] Database migrated to schema v{ImporterSettingsDatabase.LatestSchemaVersion}.");
        }

        /// <summary>
        /// Migrates a specific set of rules (e.g. freshly imported from JSON) using their
        /// own source schema
        /// version, independent of the target database's current
        /// <see cref="ImporterSettingsDatabase.schemaVersion" />.
        /// Safe to call even when the target database is already at the latest schema —
        /// migration is
        /// idempotent (guarded by whether each rule's <c>pattern</c> is already
        /// populated).
        /// </summary>
        public static void MigrateImportedRules(List<BaseImportRule> rules, int fromSchemaVersion)
        {
            if (fromSchemaVersion >= 2)
                return;

            MigrateToV2(rules);
        }

        private static void MigrateToV2(List<BaseImportRule> rules)
        {
            if (rules == null)
                return;

            var count = 0;

            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];

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

                if (!string.IsNullOrEmpty(prefix))
                    sb.Append(prefix);

                sb.Append('*');

                if (!string.IsNullOrEmpty(suffix))
                    sb.Append(suffix);

                if (!string.IsNullOrEmpty(ext))
                    sb.Append(ext);

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