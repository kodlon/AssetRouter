using Kodlon.AssetRouter.Data;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    public static class DatabaseLocator
    {
        private static bool AlreadySearched;
        private static ImporterSettingsDatabase Cached;

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