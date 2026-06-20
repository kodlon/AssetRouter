using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace Kodlon.AssetRouter.Data
{
    /// <summary>
    /// Single source of truth for the default contents of an <see cref="ImporterSettingsDatabase"/>.
    /// Both <see cref="ImporterSettingsDatabase.Reset"/> and <see cref="Logic.AssetRouterInitializer"/>
    /// delegate here so the defaults are never duplicated.
    /// </summary>
    internal static class DefaultDatabaseFactory
    {
        /// <summary>Fills <paramref name="db"/> with the standard defaults.</summary>
        public static void PopulateDefaults(ImporterSettingsDatabase db)
        {
            db.enableAutoImport = true;
            db.showPopupForUnknownFiles = true;
            db.monitoredExtensions = CreateMonitoredExtensions();
            db.ignoredFolders = CreateIgnoredFolders();
            db.rules = CreateDefaultRules();
        }

        public static List<string> CreateMonitoredExtensions() => new()
        {
            ".fbx", ".obj", ".dae", ".3ds",
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tiff", ".exr", ".hdr",
            ".wav", ".mp3", ".ogg", ".aif", ".aiff"
        };

        public static List<string> CreateIgnoredFolders() => new()
        {
            "Assets/Editor/",
            "Assets/Plugins/",
            "Assets/StreamingAssets/",
            "Assets/AssetRouter/",
            "Packages/"
        };

        public static List<BaseImportRule> CreateDefaultRules() => new()
        {
            new ImportRule
            {
                ruleName = "UI Textures",
                prefix = "UI_",
                targetFolder = "Assets/Art/UI/",
                preset = LoadPreset("TextureImporter_UI")
            },
            new ImportRule
            {
                ruleName = "General Textures",
                prefix = "T_",
                targetFolder = "Assets/Art/Textures/",
                preset = LoadPreset("TextureImporter")
            },
            new ImportRule
            {
                ruleName = "Sound Effects",
                prefix = "SFX_",
                targetFolder = "Assets/Audio/SFX/",
                preset = LoadPreset("AudioImporter")
            },
            new ImportRule
            {
                ruleName = "Music",
                prefix = "Mus_",
                targetFolder = "Assets/Audio/Music/",
                preset = LoadPreset("AudioImporter_Music")
            }
        };

        private static Preset LoadPreset(string presetName)
        {
            var guids = AssetDatabase.FindAssets($"{presetName} t:Preset");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (Path.GetFileNameWithoutExtension(path) == presetName)
                    return AssetDatabase.LoadAssetAtPath<Preset>(path);
            }

            Debug.LogWarning($"[AssetRouter] Preset '{presetName}' not found. Assign it manually in the database.");

            return null;
        }
    }
}
