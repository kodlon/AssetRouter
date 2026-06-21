using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace Kodlon.AssetRouter.Data
{
    internal static class DefaultDatabaseFactory
    {
        public static void PopulateDefaults(ImporterSettingsDatabase db)
        {
            db.schemaVersion = ImporterSettingsDatabase.LatestSchemaVersion;
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
                patternMode = PatternMode.Glob,
                pattern = "UI_*",
                targetFolder = "Assets/Art/UI/",
                preset = LoadPreset("TextureImporter_UI")
            },
            new ImportRule
            {
                ruleName = "General Textures",
                patternMode = PatternMode.Glob,
                pattern = "T_*",
                targetFolder = "Assets/Art/Textures/",
                preset = LoadPreset("TextureImporter")
            },
            new ImportRule
            {
                ruleName = "Sound Effects",
                patternMode = PatternMode.Glob,
                pattern = "SFX_*",
                targetFolder = "Assets/Audio/SFX/",
                preset = LoadPreset("AudioImporter")
            },
            new ImportRule
            {
                ruleName = "Music",
                patternMode = PatternMode.Glob,
                pattern = "Mus_*",
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
