using Kodlon.AssetRouter.Data;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class DatabaseLocator
    {
        private static bool AlreadySearched;
        private static bool AmbiguityWarned; // survives InvalidateCache — reset only on domain reload
        private static ImporterSettingsDatabase Cached;

        // Statics are not reset on reload when Domain Reload is disabled — invalidate explicitly.
        [InitializeOnLoadMethod]
        private static void RegisterReloadHook()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= InvalidateCache;
            AssemblyReloadEvents.beforeAssemblyReload += InvalidateCache;
        }

        public static ImporterSettingsDatabase FindDatabase(bool logIfAmbiguous = false)
        {
            if (Cached != null)
                return Cached;

            if (AlreadySearched)
                return null;

            AlreadySearched = true;

            var guids = AssetDatabase.FindAssets("t:ImporterSettingsDatabase");

            if (guids.Length == 0)
            {
                if (Application.isBatchMode)
                {
                    Debug.LogWarning("[AssetRouter] ImporterSettingsDatabase not found. " +
                                     "Create one via Assets > Create > Asset Router > Settings Database.");
                }

                return null;
            }

            // Refuse to pick one arbitrarily — silent selection caused hard-to-diagnose bugs.
            if (guids.Length > 1)
            {
                if (logIfAmbiguous && !AmbiguityWarned)
                {
                    AmbiguityWarned = true;

                    var paths = new string[guids.Length];
                    for (var i = 0; i < guids.Length; i++)
                        paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);

                    Debug.LogWarning("[AssetRouter] Multiple ImporterSettingsDatabase assets found — auto-import is disabled " +
                                     "until only one remains. Delete or rename extras:\n  " + string.Join("\n  ", paths));
                }

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
