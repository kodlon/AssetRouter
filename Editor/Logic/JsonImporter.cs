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
                db.monitoredExtensions.Clear();
                foreach (var ext in extensions)
                    db.monitoredExtensions.Add(ext.Value<string>());
            }

            if (root["ignoredFolders"] is JArray ignored)
            {
                db.ignoredFolders.Clear();
                foreach (var folder in ignored)
                    db.ignoredFolders.Add(folder.Value<string>());
            }

            if (root["rules"] is JArray rulesArr)
            {
                db.rules.Clear();
                foreach (var rToken in rulesArr)
                {
                    if (rToken is not JObject rObj) continue;
                    var rule = ParseRule(rObj);
                    if (rule != null)
                        db.rules.Add(rule);
                }
            }

            EditorUtility.SetDirty(db);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static ImportRule ParseRule(JObject rObj)
        {
            var rule = new ImportRule
            {
                ruleName             = rObj["ruleName"]?.Value<string>()             ?? "",
                isEnabled            = rObj["isEnabled"]?.Value<bool>()              ?? true,
                pattern              = rObj["pattern"]?.Value<string>()              ?? "",
                matchAgainstFullPath = rObj["matchAgainstFullPath"]?.Value<bool>()   ?? false,
                targetFolder         = rObj["targetFolder"]?.Value<string>()         ?? ""
            };

            if (Enum.TryParse<PatternMode>(rObj["patternMode"]?.Value<string>(), out var mode))
                rule.patternMode = mode;

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
