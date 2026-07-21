using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Kodlon.AssetRouter.Actions;
using Kodlon.AssetRouter.Data;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kodlon.AssetRouter.Logic
{
    internal static class JsonImporter
    {
        private static readonly BindingFlags SerializedFieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

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
                    if (rToken is not JObject rObj)
                        continue;

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

        public static void ImportFromFile(ImporterSettingsDatabase db, string path)
        {
            Import(db, File.ReadAllText(path));
        }

        public static bool TryImportRuleFromJson(string json, out ImportRule rule, out string error)
        {
            rule = null;
            error = null;

            JObject rObj;

            try
            {
                rObj = JObject.Parse(json);
            }
            catch (Exception e)
            {
                error = e.Message;

                return false;
            }

            if (rObj["$assetRouterRule"] == null)
            {
                error = "Clipboard does not contain a valid Asset Router rule.\nUse 'Copy Rule to Clipboard' on a rule first.";

                return false;
            }

            rule = new ImportRule
            {
                ruleName = rObj["ruleName"]?.Value<string>() ?? "Imported Rule",
                isEnabled = rObj["isEnabled"]?.Value<bool>() ?? true,
                pattern = rObj["pattern"]?.Value<string>() ?? "",
                matchAgainstFullPath = rObj["matchAgainstFullPath"]?.Value<bool>() ?? false,
                scopeFolder = rObj["scopeFolder"]?.Value<string>() ?? "",
                targetFolder = rObj["targetFolder"]?.Value<string>() ?? "Assets/"
            };

            var modeStr = rObj["patternMode"]?.Value<string>();

            if (!string.IsNullOrEmpty(modeStr)
                && Enum.TryParse<PatternMode>(modeStr, true, out var mode)
                && Enum.IsDefined(typeof(PatternMode), mode))
                rule.patternMode = mode;

            if (rObj["preset"] is JObject presetRefObj)
            {
                var resolved = ResolveAssetRef(presetRefObj, typeof(Preset)) as Preset;

                if (resolved != null)
                    rule.preset = resolved;
                else
                    Debug.LogWarning($"[AssetRouter] Rule \"{rule.ruleName}\": preset could not be resolved — re-link manually.");
            }

            rule.postImportActions = ParseTemplateActions(rObj["postImportActions"] as JArray);

            return true;
        }

        private static bool IsSerializedObjectRef(FieldInfo f)
        {
            if (f.IsStatic)
                return false;

            if (!typeof(Object).IsAssignableFrom(f.FieldType))
                return false;

            if (f.GetCustomAttribute<NonSerializedAttribute>() != null)
                return false;

            return f.IsPublic || f.GetCustomAttribute<SerializeField>() != null;
        }

        private static ImportRule ParseRule(JObject rObj)
        {
            var rule = new ImportRule
            {
                ruleName = rObj["ruleName"]?.Value<string>() ?? "",
                isEnabled = rObj["isEnabled"]?.Value<bool>() ?? true,
                pattern = rObj["pattern"]?.Value<string>() ?? "",
                matchAgainstFullPath = rObj["matchAgainstFullPath"]?.Value<bool>() ?? false,
                scopeFolder = rObj["scopeFolder"]?.Value<string>() ?? "",
                targetFolder = rObj["targetFolder"]?.Value<string>() ?? ""
            };

            var patternModeStr = rObj["patternMode"]?.Value<string>();

            if (!string.IsNullOrEmpty(patternModeStr))
            {
                if (Enum.TryParse<PatternMode>(patternModeStr, true, out var mode) && Enum.IsDefined(typeof(PatternMode), mode))
                    rule.patternMode = mode;
                else
                    Debug.LogWarning($"[AssetRouter] Rule \"{rule.ruleName}\": unknown patternMode \"{patternModeStr}\" — defaulting to {rule.patternMode}.");
            }

            // Schema v1 exports predate "pattern" and used these three fields instead. RuleMigrator only
            // has something to migrate if they're actually read here — without this, MigrateImportedRules
            // is a no-op for every v1 JSON, since ImportRule always starts with these fields empty.
            rule._legacyPrefix = rObj["prefix"]?.Value<string>() ?? "";
            rule._legacySuffix = rObj["suffix"]?.Value<string>() ?? "";
            rule._legacyExtensionFilter = rObj["extensionFilter"]?.Value<string>() ?? "";

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
                    if (aToken is not JObject aObj)
                        continue;

                    var guid = aObj["guid"]?.Value<string>();

                    if (string.IsNullOrEmpty(guid))
                        continue;

                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);

                    if (string.IsNullOrEmpty(assetPath))
                        continue;

                    if (!long.TryParse(aObj["fileId"]?.Value<string>(), out var fileId))
                        continue;

                    foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                    {
                        if (sub is not AssetImportActionAsset action)
                            continue;

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

        private static List<AssetImportActionAsset> ParseTemplateActions(JArray actionsArr)
        {
            var result = new List<AssetImportActionAsset>();

            if (actionsArr == null)
                return result;

            var allTypes = TypeCache.GetTypesDerivedFrom<AssetImportActionAsset>();
            var totalUnresolved = 0;

            foreach (var aToken in actionsArr)
            {
                if (aToken is not JObject aObj)
                    continue;

                var typeName = aObj["$type"]?.Value<string>();

                if (string.IsNullOrEmpty(typeName))
                    continue;

                Type actionType = null;

                foreach (var t in allTypes)
                    if (t.FullName == typeName)
                    {
                        actionType = t;

                        break;
                    }

                if (actionType == null)
                {
                    Debug.LogWarning($"[AssetRouter] Unknown action type '{typeName}' — skipped.");

                    continue;
                }

                var action = ScriptableObject.CreateInstance(actionType) as AssetImportActionAsset;

                if (action == null)
                    continue;

                action.name = aObj["name"]?.Value<string>() ?? ObjectNames.NicifyVariableName(actionType.Name);

                if (aObj["fields"] is JObject fieldsObj)
                {
                    // Strip object-ref fields before JsonUtility — cross-project instanceIDs are meaningless.
                    var valueFields = (JObject)fieldsObj.DeepClone();

                    foreach (var field in actionType.GetFields(SerializedFieldFlags))
                    {
                        if (IsSerializedObjectRef(field) && valueFields[field.Name] != null)
                            valueFields[field.Name] = JValue.CreateNull();
                    }

                    try
                    {
                        JsonUtility.FromJsonOverwrite(valueFields.ToString(), action);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[AssetRouter] Could not restore fields for '{actionType.Name}': {e.Message}");
                    }

                    totalUnresolved += RestoreActionObjectRefs(action, actionType, fieldsObj);
                }

                result.Add(action);
            }

            if (totalUnresolved > 0)
                Debug.LogWarning(
                    $"[AssetRouter] {totalUnresolved} object reference(s) in pasted actions could not be resolved — re-link manually in the Inspector.");

            return result;
        }

        private static Object ResolveAssetRef(JObject refObj, Type targetType)
        {
            var guid = refObj["guid"]?.Value<string>();

            if (!string.IsNullOrEmpty(guid))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrEmpty(path))
                {
                    var obj = AssetDatabase.LoadAssetAtPath(path, targetType);

                    if (obj != null)
                        return obj;
                }
            }

            var pathFallback = refObj["path"]?.Value<string>();

            if (!string.IsNullOrEmpty(pathFallback))
                return AssetDatabase.LoadAssetAtPath(pathFallback, targetType);

            return null;
        }

        private static int RestoreActionObjectRefs(AssetImportActionAsset action, Type actionType, JObject fieldsObj)
        {
            var unresolved = 0;

            foreach (var field in actionType.GetFields(SerializedFieldFlags))
            {
                if (!IsSerializedObjectRef(field))
                    continue;

                var token = fieldsObj[field.Name];

                if (token == null || token.Type == JTokenType.Null)
                    continue;

                if (token is not JObject refObj)
                {
                    unresolved++;

                    continue;
                }

                var resolved = ResolveAssetRef(refObj, field.FieldType);

                if (resolved != null)
                    field.SetValue(action, resolved);
                else
                    unresolved++;
            }

            return unresolved;
        }
    }
}