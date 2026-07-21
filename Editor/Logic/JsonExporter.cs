using System;
using System.IO;
using System.Reflection;
using Kodlon.AssetRouter.Actions;
using Kodlon.AssetRouter.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kodlon.AssetRouter.Logic
{
    internal static class JsonExporter
    {
        private static readonly BindingFlags SerializedFieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static string Export(ImporterSettingsDatabase db)
        {
            var root = new JObject
            {
                ["$schema"] = 1,
                ["schemaVersion"] = db.schemaVersion,
                ["enableAutoImport"] = db.enableAutoImport,
                ["showPopupForUnknownFiles"] = db.showPopupForUnknownFiles
            };

            var extensions = new JArray();

            foreach (var ext in db.monitoredExtensions)
                extensions.Add(ext);

            root["monitoredExtensions"] = extensions;

            var ignored = new JArray();

            foreach (var f in db.ignoredFolders)
                ignored.Add(f);

            root["ignoredFolders"] = ignored;

            var rulesArr = new JArray();

            foreach (var rule in db.rules)
            {
                if (rule is not ImportRule importRule)
                {
                    Debug.LogWarning(
                        $"[AssetRouter] Rule \"{rule?.ruleName}\" ({rule?.GetType().Name}) is not an ImportRule and will be skipped during JSON export.");

                    continue;
                }

                var rObj = new JObject
                {
                    ["$type"] = rule.GetType().Name,
                    ["ruleName"] = importRule.ruleName,
                    ["isEnabled"] = importRule.isEnabled,
                    ["pattern"] = importRule.pattern,
                    ["patternMode"] = importRule.patternMode.ToString(),
                    ["matchAgainstFullPath"] = importRule.matchAgainstFullPath,
                    ["scopeFolder"] = importRule.scopeFolder,
                    ["targetFolder"] = importRule.targetFolder
                };

                rObj["preset"] = GuidRef(importRule.preset);

                var actionsArr = new JArray();

                if (importRule.postImportActions != null)
                {
                    foreach (var action in importRule.postImportActions)
                        actionsArr.Add(SubAssetRef(action));
                }

                rObj["postImportActions"] = actionsArr;
                rulesArr.Add(rObj);
            }

            root["rules"] = rulesArr;

            return root.ToString(Formatting.Indented);
        }

        public static string ExportRule(ImportRule rule)
        {
            var rObj = new JObject
            {
                ["$assetRouterRule"] = 1,
                ["$type"] = rule.GetType().Name,
                ["ruleName"] = rule.ruleName,
                ["isEnabled"] = rule.isEnabled,
                ["pattern"] = rule.pattern,
                ["patternMode"] = rule.patternMode.ToString(),
                ["matchAgainstFullPath"] = rule.matchAgainstFullPath,
                ["scopeFolder"] = rule.scopeFolder,
                ["targetFolder"] = rule.targetFolder,
                ["preset"] = GuidAndPathRef(rule.preset)
            };

            var actionsArr = new JArray();

            if (rule.postImportActions != null)
            {
                foreach (var action in rule.postImportActions)
                {
                    if (action == null)
                        continue;

                    actionsArr.Add(new JObject
                    {
                        ["$type"] = action.GetType().FullName,
                        ["name"] = action.name,
                        ["fields"] = SerializeActionFields(action)
                    });
                }
            }

            rObj["postImportActions"] = actionsArr;

            return rObj.ToString(Formatting.Indented);
        }

        public static void ExportToFile(ImporterSettingsDatabase db, string path)
        {
            var json = Export(db);
            var tmp = path + ".tmp";

            try
            {
                File.WriteAllText(tmp, json);

                if (File.Exists(path))
                    File.Replace(tmp, path, path + ".bak");
                else
                    File.Move(tmp, path);
            }
            finally
            {
                if (File.Exists(tmp))
                    try
                    {
                        File.Delete(tmp);
                    }
                    catch (Exception)
                    {
                        /* best-effort */
                    }
            }
        }

        private static JToken GuidAndPathRef(Object obj)
        {
            if (obj == null)
                return JValue.CreateNull();

            var path = AssetDatabase.GetAssetPath(obj);
            var guid = AssetDatabase.AssetPathToGUID(path);

            if (string.IsNullOrEmpty(guid))
                return JValue.CreateNull();

            return new JObject
            {
                ["guid"] = guid,
                ["path"] = path
            };
        }

        private static JToken GuidRef(Object obj)
        {
            if (obj == null)
                return JValue.CreateNull();

            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));

            return string.IsNullOrEmpty(guid) ? JValue.CreateNull() : new JValue(guid);
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

        private static JToken SerializeActionFields(AssetImportActionAsset action)
        {
            var jObj = JObject.Parse(JsonUtility.ToJson(action));

            foreach (var field in action.GetType().GetFields(SerializedFieldFlags))
            {
                if (!IsSerializedObjectRef(field))
                    continue;

                jObj[field.Name] = GuidAndPathRef(field.GetValue(action) as Object);
            }

            return jObj;
        }

        private static JToken SubAssetRef(AssetImportActionAsset action)
        {
            if (action == null)
                return JValue.CreateNull();

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(action, out var guid, out long fileId);

            if (string.IsNullOrEmpty(guid))
                return JValue.CreateNull();

            return new JObject
            {
                ["guid"] = guid,
                ["fileId"] = fileId.ToString()
            };
        }
    }
}