using UnityEditor;

namespace Kodlon.AssetRouter.Logic
{
    [InitializeOnLoad]
    internal static class AssetRouterInitializer
    {
        static AssetRouterInitializer()
        {
            EditorApplication.delayCall -= Initialize;
            EditorApplication.delayCall += Initialize;

            // Also run the migrator when the project changes (e.g. a new DB is created
            // while Domain Reload is disabled and the static constructor doesn't re-fire).
            EditorApplication.projectChanged -= RunMigratorIfNeeded;
            EditorApplication.projectChanged += RunMigratorIfNeeded;
        }

        private static void Initialize()
        {
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
    }
}