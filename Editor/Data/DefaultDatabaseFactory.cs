using System.Collections.Generic;
using System.IO;
using Kodlon.AssetRouter.Actions;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace Kodlon.AssetRouter.Data
{
    internal static class DefaultDatabaseFactory
    {
        public static List<BaseImportRule> CreateDefaultRules()
        {
            // baseMaterial left null → resolves to the active pipeline's default at import time.
            var materialAction = ScriptableObject.CreateInstance<CreateMaterialFromTextureAction>();
            materialAction.name = "Create Material From Texture";

            return new List<BaseImportRule>
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
                    ruleName = "Character Textures",
                    patternMode = PatternMode.Glob,
                    pattern = "T_Char_*_*",
                    targetFolder = "Assets/Art/Characters/{1}/",
                    preset = LoadPreset("TextureImporter")
                },
                new ImportRule
                {
                    ruleName = "Location Textures",
                    patternMode = PatternMode.Regex,
                    pattern = @"^T_Loc_(?<loc>\w+)_.*",
                    targetFolder = "Assets/Art/Locations/{loc}/",
                    preset = LoadPreset("TextureImporter")
                },
                new ImportRule
                {
                    ruleName = "General Textures",
                    patternMode = PatternMode.Glob,
                    pattern = "T_*",
                    targetFolder = "Assets/Art/Textures/",
                    preset = LoadPreset("TextureImporter"),
                    postImportActions = new List<AssetImportActionAsset>
                    {
                        materialAction
                    }
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
        }

        public static List<string> CreateIgnoredFolders() =>
            new()
            {
                "Assets/Editor/",
                "Assets/Plugins/",
                "Assets/StreamingAssets/",
                "Assets/AssetRouter/",
                "Packages/"
            };

        public static List<string> CreateMonitoredExtensions() =>
            new()
            {
                ".fbx",
                ".obj",
                ".png",
                ".jpg",
                ".jpeg",
                ".tga",
                ".psd",
                ".tiff",
                ".exr",
                ".hdr",
                ".wav",
                ".mp3",
                ".ogg",
                ".aif",
                ".aiff"
            };

        /// <summary>
        /// Registers every action instance in db.rules as a sub-asset of
        /// <paramref name="db" />.
        /// Must be called AFTER <c>AssetDatabase.CreateAsset(db, path)</c> because
        /// <c>AddObjectToAsset</c> requires the host asset to already exist on disk.
        /// </summary>
        public static void EmbedSubAssets(ImporterSettingsDatabase db)
        {
            if (!EditorUtility.IsPersistent(db))
                return;

            foreach (var rule in db.rules)
            {
                if (rule is not ImportRule importRule || importRule.postImportActions == null)
                    continue;

                foreach (var action in importRule.postImportActions)
                {
                    if (action == null || AssetDatabase.IsSubAsset(action))
                        continue;

                    AssetDatabase.AddObjectToAsset(action, db);
                }
            }

            EditorUtility.SetDirty(db);
        }

        public static void PopulateDefaults(ImporterSettingsDatabase db)
        {
            db.schemaVersion = ImporterSettingsDatabase.LatestSchemaVersion;
            db.enableAutoImport = true;
            db.showPopupForUnknownFiles = true;
            db.monitoredExtensions = CreateMonitoredExtensions();
            db.ignoredFolders = CreateIgnoredFolders();
            db.rules = CreateDefaultRules();
        }

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