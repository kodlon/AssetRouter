using System.Collections.Generic;
using Kodlon.AssetRouter.Data;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    [InitializeOnLoad]
    public static class AssetRouterInitializer
    {
        private const string AssetFolder = "Assets/AssetRouter";
        private const string AssetPath = "Assets/AssetRouter/ImporterSettingsDatabase.asset";

        static AssetRouterInitializer() => EditorApplication.delayCall += CreateDefaultDatabaseIfMissing;

        private static void CreateDefaultDatabaseIfMissing()
        {
            var existing = AssetDatabase.FindAssets("t:ImporterSettingsDatabase");

            if (existing.Length > 0)
                return;

            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets", "AssetRouter");

            var db = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();

            db.enableAutoImport = true;
            db.showPopupForUnknownFiles = true;

            db.monitoredExtensions = new List<string>
            {
                ".fbx",
                ".obj",
                ".png",
                ".jpg",
                ".jpeg",
                ".wav",
                ".mp3",
                ".ogg"
            };

            db.ignoredFolders = new List<string>
            {
                "Assets/Editor/",
                "Assets/Plugins/",
                "Assets/StreamingAssets/",
                "Assets/AssetRouter/",
                "Packages/"
            };

            var texDefault = LoadPreset("TextureImporter");
            var texUI = LoadPreset("TextureImporter_UI");
            var audioSFX = LoadPreset("AudioImporter");
            var audioMusic = LoadPreset("AudioImporter_Music");

            db.rules = new List<BaseImportRule>
            {
                new ImportRule
                {
                    ruleName = "UI Textures",
                    prefix = "UI_",
                    targetFolder = "Assets/Art/UI/",
                    preset = texUI
                },
                new ImportRule
                {
                    ruleName = "General Textures",
                    prefix = "T_",
                    targetFolder = "Assets/Art/Textures/",
                    preset = texDefault
                },
                new ImportRule
                {
                    ruleName = "Sound Effects",
                    prefix = "SFX_",
                    targetFolder = "Assets/Audio/SFX/",
                    preset = audioSFX
                },
                new ImportRule
                {
                    ruleName = "Music",
                    prefix = "Mus_",
                    targetFolder = "Assets/Audio/Music/",
                    preset = audioMusic
                }
            };

            AssetDatabase.CreateAsset(db, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            DatabaseLocator.InvalidateCache();

            Debug.Log($"[AssetRouter] Database created: {AssetPath}");
        }

        private static Preset LoadPreset(string presetName)
        {
            var guids = AssetDatabase.FindAssets($"{presetName} t:Preset");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (System.IO.Path.GetFileNameWithoutExtension(path) == presetName)
                    return AssetDatabase.LoadAssetAtPath<Preset>(path);
            }

            Debug.LogWarning($"[AssetRouter] Preset '{presetName}' not found. Assign it manually in the database.");

            return null;
        }
    }
}