using System.IO;
using Kodlon.AssetRouter;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Instantiates a template prefab, calls <see cref="IAssetRouterPrefabSetup.SetupAssetRouter"/> on all
    /// implementing components, then saves the result as a new prefab asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Asset Router/Actions/Create Prefab From Template", fileName = "CreatePrefabFromTemplateAction")]
    public sealed class CreatePrefabFromTemplateAction : AssetImportActionAsset
    {
        /// <summary>Prefab to use as the template. The instance is created, configured, and saved.</summary>
        public GameObject templatePrefab;

        /// <summary>Folder where the output prefab is saved. Falls back to the imported asset's folder when empty.</summary>
        [Tooltip("Output folder. Empty = the imported asset's folder.")]
        public string outputFolder = "";

        /// <summary>
        /// File name for the output prefab without extension. <c>{assetName}</c> is replaced with the imported file
        /// name (without extension). Example: <c>{assetName}_Prefab</c> on <c>Character.fbx</c> produces <c>Character_Prefab.prefab</c>.
        /// </summary>
        [Tooltip("{assetName} is replaced with the imported file name (no extension).")]
        public string namePattern = "{assetName}_Prefab";

        /// <summary>When false, the action is skipped if a prefab already exists at the output path.</summary>
        public bool overwriteExisting = false;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => templatePrefab != null;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            var folder      = PathUtility.NormalizeAssetPath(string.IsNullOrEmpty(outputFolder) ? (Path.GetDirectoryName(ctx.AssetPath) ?? string.Empty) : outputFolder);
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
