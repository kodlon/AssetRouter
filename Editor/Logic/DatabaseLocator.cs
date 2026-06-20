using Kodlon.AssetRouter.Data;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class DatabaseLocator
    {
        private static bool AlreadySearched;
        private static ImporterSettingsDatabase Cached;

        // Invalidate the cache before assembly reload so a fresh search runs after recompile.
        // Covers the case where domain reload is disabled and statics are not reset automatically.
        [InitializeOnLoadMethod]
        private static void RegisterReloadHook()
        {
            AssemblyReloadEvents.beforeAssemblyReload += InvalidateCache;
        }

        public static ImporterSettingsDatabase FindDatabase()
        {
            if (Cached != null)
                return Cached;

            if (AlreadySearched)
                return null;

            AlreadySearched = true;

            var guids = AssetDatabase.FindAssets("t:ImporterSettingsDatabase");

            if (guids.Length == 0)
            {
                Debug.LogWarning("[AssetRouter] ImporterSettingsDatabase not found. " +
                                 "Create one via Assets > Create > Asset Router > Settings Database.");

                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            Cached = AssetDatabase.LoadAssetAtPath<ImporterSettingsDatabase>(path);

            return Cached;
        }

        public static void InvalidateCache()
        {
            Cached = null;
            AlreadySearched = false;
        }
    }
}
