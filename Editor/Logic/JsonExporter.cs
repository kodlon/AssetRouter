using System.IO;
using Kodlon.AssetRouter.Actions;
using Kodlon.AssetRouter.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class JsonExporter
    {
        public static void ExportToFile(ImporterSettingsDatabase db, string path)
        {
            var json = Export(db);
            var tmp  = path + ".tmp";

            try
            {
                File.WriteAllText(tmp, json);

                if (File.Exists(path))
                    File.Delete(path);

                File.Move(tmp, path);
            }
            finally
            {
                if (File.Exists(tmp))
                    File.Delete(tmp);
            }
        }

        public static string Export(ImporterSettingsDatabase db)
        {
            var root = new JObject
            {
                ["$schema"]              = 1,
                ["schemaVersion"]        = db.schemaVersion,
                ["enableAutoImport"]     = db.enableAutoImport,
                ["showPopupForUnknownFiles"] = db.showPopupForUnknownFiles
            };

            var extensions = new JArray();
            foreach (var ext in db.monitoredExtensions) extensions.Add(ext);
            root["monitoredExtensions"] = extensions;

            var ignored = new JArray();
            foreach (var f in db.ignoredFolders) ignored.Add(f);
            root["ignoredFolders"] = ignored;

            var rulesArr = new JArray();

            foreach (var rule in db.rules)
            {
                if (rule is not ImportRule importRule)
                    continue;

                var rObj = new JObject
                {
                    ["$type"]               = rule.GetType().Name,
                    ["ruleName"]            = importRule.ruleName,
                    ["isEnabled"]           = importRule.isEnabled,
                    ["pattern"]             = importRule.pattern,
                    ["patternMode"]         = importRule.patternMode.ToString(),
                    ["matchAgainstFullPath"] = importRule.matchAgainstFullPath,
                    ["targetFolder"]        = importRule.targetFolder
                };

                rObj["preset"] = GuidRef(importRule.preset);

                var actionsArr = new JArray();
                if (importRule.postImportActions != null)
                    foreach (var action in importRule.postImportActions)
                        actionsArr.Add(SubAssetRef(action));

                rObj["postImportActions"] = actionsArr;
                rulesArr.Add(rObj);
            }

            root["rules"] = rulesArr;
            return root.ToString(Formatting.Indented);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static JToken GuidRef(UnityEngine.Object obj)
        {
            if (obj == null) return JValue.CreateNull();
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
            return string.IsNullOrEmpty(guid) ? JValue.CreateNull() : new JValue(guid);
        }

        private static JToken SubAssetRef(AssetImportActionAsset action)
        {
            if (action == null) return JValue.CreateNull();

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(action, out var guid, out long fileId);

            if (string.IsNullOrEmpty(guid)) return JValue.CreateNull();

            return new JObject { ["guid"] = guid, ["fileId"] = fileId.ToString() };
        }
    }
}
