using Kodlon.AssetRouter.Data;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    [InitializeOnLoad]
    internal static class AssetRouterInitializer
    {
        private const string AssetFolder = "Assets/AssetRouter";
        private const string AssetPath = "Assets/AssetRouter/ImporterSettingsDatabase.asset";

        static AssetRouterInitializer()
        {
            EditorApplication.delayCall += Initialize;
            // Also run the migrator when the project changes (e.g. a new DB is created
            // while Domain Reload is disabled and the static constructor doesn't re-fire).
            EditorApplication.projectChanged += RunMigratorIfNeeded;
        }

        private static void Initialize()
        {
            CreateDefaultDatabaseIfMissing();

            var db = DatabaseLocator.FindDatabase();

            if (db != null)
                RuleMigrator.MigrateIfNeeded(db);
        }

        private static void RunMigratorIfNeeded()
        {
            DatabaseLocator.InvalidateCache();
            var db = DatabaseLocator.FindDatabase();

            if (db != null)
                RuleMigrator.MigrateIfNeeded(db);
        }

        private static void CreateDefaultDatabaseIfMissing()
        {
            if (AssetDatabase.FindAssets("t:ImporterSettingsDatabase").Length > 0)
                return;

            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets", "AssetRouter");

            var db = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            DefaultDatabaseFactory.PopulateDefaults(db);

            AssetDatabase.CreateAsset(db, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            DatabaseLocator.InvalidateCache();

            Debug.Log($"[AssetRouter] Database created: {AssetPath}");
        }
    }
}
