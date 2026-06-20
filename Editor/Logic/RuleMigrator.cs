using System.Text;
using Kodlon.AssetRouter.Data;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    /// <summary>
    /// One-shot migration that upgrades an <see cref="ImporterSettingsDatabase"/> from an older
    /// schema version to the current one.
    /// </summary>
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

            db.schemaVersion = ImporterSettingsDatabase.LatestSchemaVersion;

            if (!migrated)
                return;

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AssetRouter][Migration] Database migrated to schema v{ImporterSettingsDatabase.LatestSchemaVersion}.");
        }

        // ── v1 → v2 ──────────────────────────────────────────────────────────────
        // Combines the old prefix / suffix / extensionFilter fields into a single
        // glob pattern: prefix + "*" + suffix + extensionFilter.
        // Example: prefix="T_", suffix="", extensionFilter=".png" → "T_*.png"

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

                // Skip rules that already have a pattern (created under the new schema).
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
