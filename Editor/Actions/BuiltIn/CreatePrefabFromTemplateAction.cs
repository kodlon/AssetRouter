using System.IO;
using Kodlon.AssetRouter;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    [CreateAssetMenu(menuName = "Asset Router/Actions/Create Prefab From Template", fileName = "CreatePrefabFromTemplateAction")]
    public sealed class CreatePrefabFromTemplateAction : AssetImportActionAsset
    {
        public GameObject templatePrefab;
        [Tooltip("Output folder. Empty = rule's target folder.")]
        public string outputFolder = "";
        [Tooltip("{assetName} is replaced with the imported file name (no extension).")]
        public string namePattern = "{assetName}_Prefab";
        public bool overwriteExisting = false;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => templatePrefab != null;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            var folder      = PathUtility.NormalizeAssetPath(string.IsNullOrEmpty(outputFolder) ? ctx.Rule.targetFolder : outputFolder);
            var baseName    = Path.GetFileNameWithoutExtension(ctx.AssetPath);
            var prefabName  = namePattern.Replace("{assetName}", baseName);
            var prefabPath  = folder + "/" + prefabName + ".prefab";

            if (!overwriteExisting && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(templatePrefab);

            try
            {
                instance.GetComponentInChildren<IAssetRouterPrefabSetup>()
                    ?.SetupAssetRouter(importedAsset, ctx.AssetPath);

                PathUtility.EnsureFolderExists(folder);
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            ctx.Logger.Log($"[AssetRouter] CreatePrefab → {prefabPath}");
        }
    }
}
