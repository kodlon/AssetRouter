using System;
using System.Collections.Generic;
using System.IO;
using Kodlon.AssetRouter.Actions;
using Kodlon.AssetRouter.Data;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class JsonImporter
    {
        public static void ImportFromFile(ImporterSettingsDatabase db, string path)
        {
            Import(db, File.ReadAllText(path));
        }

        public static void Import(ImporterSettingsDatabase db, string json)
        {
            var root = JObject.Parse(json);

            if (root["enableAutoImport"] is JValue enableAutoImport)
                db.enableAutoImport = enableAutoImport.Value<bool>();

            if (root["showPopupForUnknownFiles"] is JValue showPopup)
                db.showPopupForUnknownFiles = showPopup.Value<bool>();

            if (root["monitoredExtensions"] is JArray extensions)
            {
                db.monitoredExtensions ??= new List<string>();
                db.monitoredExtensions.Clear();
                foreach (var ext in extensions)
                {
                    var val = ext.Value<string>();
                    if (!string.IsNullOrEmpty(val))
                        db.monitoredExtensions.Add(val);
                }
            }

            if (root["ignoredFolders"] is JArray ignored)
            {
                db.ignoredFolders ??= new List<string>();
                db.ignoredFolders.Clear();
                foreach (var folder in ignored)
                {
                    var val = folder.Value<string>();
                    if (!string.IsNullOrEmpty(val))
                        db.ignoredFolders.Add(val);
                }
            }

            if (root["rules"] is JArray rulesArr)
            {
                db.rules ??= new List<BaseImportRule>();
                db.rules.Clear();
                foreach (var rToken in rulesArr)
                {
                    if (rToken is not JObject rObj) continue;
                    var rule = ParseRule(rObj);
                    if (rule != null)
                        db.rules.Add(rule);
                }

                // Migrate using the JSON's own schemaVersion, not the target database's — otherwise
                // importing an old-schema JSON into an already-migrated database silently skips migration.
                var importedSchemaVersion = root["schemaVersion"]?.Value<int?>() ?? db.schemaVersion;
                RuleMigrator.MigrateImportedRules(db.rules, importedSchemaVersion);
            }

            EditorUtility.SetDirty(db);

            RuleMigrator.MigrateIfNeeded(db);
        }

        private static ImportRule ParseRule(JObject rObj)
        {
            var rule = new ImportRule
            {
                ruleName             = rObj["ruleName"]?.Value<string>()             ?? "",
                isEnabled            = rObj["isEnabled"]?.Value<bool>()              ?? true,
                pattern              = rObj["pattern"]?.Value<string>()              ?? "",
                matchAgainstFullPath = rObj["matchAgainstFullPath"]?.Value<bool>()   ?? false,
                scopeFolder          = rObj["scopeFolder"]?.Value<string>()          ?? "",
                targetFolder         = rObj["targetFolder"]?.Value<string>()         ?? ""
            };

            var patternModeStr = rObj["patternMode"]?.Value<string>();
            if (!string.IsNullOrEmpty(patternModeStr))
            {
                if (Enum.TryParse<PatternMode>(patternModeStr, ignoreCase: true, out var mode) && Enum.IsDefined(typeof(PatternMode), mode))
                    rule.patternMode = mode;
                else
                    Debug.LogWarning($"[AssetRouter] Rule \"{rule.ruleName}\": unknown patternMode \"{patternModeStr}\" — defaulting to {rule.patternMode}.");
            }

            var presetGuid = rObj["preset"]?.Value<string>();
            if (!string.IsNullOrEmpty(presetGuid))
            {
                var presetPath = AssetDatabase.GUIDToAssetPath(presetGuid);
                if (!string.IsNullOrEmpty(presetPath))
                    rule.preset = AssetDatabase.LoadAssetAtPath<Preset>(presetPath);
            }

            rule.postImportActions = new List<AssetImportActionAsset>();

            if (rObj["postImportActions"] is JArray actionsArr)
            {
                foreach (var aToken in actionsArr)
                {
                    if (aToken is not JObject aObj) continue;

                    var guid = aObj["guid"]?.Value<string>();
                    if (string.IsNullOrEmpty(guid)) continue;

                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath)) continue;

                    if (!long.TryParse(aObj["fileId"]?.Value<string>(), out var fileId)) continue;

                    foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                    {
                        if (sub is not AssetImportActionAsset action) continue;
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sub, out _, out long subFileId);

                        if (subFileId == fileId)
                        {
                            rule.postImportActions.Add(action);
                            break;
                        }
                    }
                }
            }

            return rule;
        }
    }
}
