using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace Kodlon.AssetRouter.Data
{
    [CreateAssetMenu(fileName = "ImporterSettingsDatabase", menuName = "Asset Router/Settings Database")]
    public class ImporterSettingsDatabase : ScriptableObject
    {
        public bool enableAutoImport = true;
        public bool showPopupForUnknownFiles = true;

        [Space]
        public List<string> monitoredExtensions = new();

        public List<string> ignoredFolders = new();

        [Space]
        [SerializeReference]
        public List<BaseImportRule> rules = new();

        private void Reset()
        {
            monitoredExtensions = new List<string>
            {
                ".fbx",
                ".obj",
                ".dae",
                ".3ds",
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

            ignoredFolders = new List<string>
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

            rules = new List<BaseImportRule>
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