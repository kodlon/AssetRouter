using System.IO;
using Kodlon.AssetRouter;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Clones a template ScriptableObject via <c>Instantiate</c>, calls <see cref="IAssetRouterDataSetup.SetupAssetRouter"/>
    /// if the clone implements it, then saves the result as a new .asset file.
    /// </summary>
    [CreateAssetMenu(menuName = "Asset Router/Actions/Create ScriptableObject From Template", fileName = "CreateScriptableObjectFromTemplateAction")]
    public sealed class CreateScriptableObjectFromTemplateAction : AssetImportActionAsset
    {
        /// <summary>ScriptableObject to clone. All serialized fields are copied to the new instance.</summary>
        public ScriptableObject template;

        /// <summary>Folder where the output .asset is saved. Falls back to the imported asset's folder when empty.</summary>
        [Tooltip("Output folder. Empty = the imported asset's folder.")]
        public string outputFolder = "";

        /// <summary>
        /// File name for the output asset without extension. <c>{assetName}</c> is replaced with the imported file
        /// name (without extension).
        /// </summary>
        [Tooltip("{assetName} is replaced with the imported file name (no extension).")]
        public string namePattern = "{assetName}_Data";

        /// <summary>When false, the action is skipped if an asset already exists at the output path.</summary>
        public bool overwriteExisting = false;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => template != null;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            var folder   = PathUtility.NormalizeAssetPath(string.IsNullOrEmpty(outputFolder) ? (Path.GetDirectoryName(ctx.AssetPath) ?? string.Empty) : outputFolder);
            var baseName = Path.GetFileNameWithoutExtension(ctx.AssetPath);
            var soName   = namePattern.Replace("{assetName}", baseName);
            var soPath   = folder + "/" + soName + ".asset";

            if (!overwriteExisting && AssetDatabase.LoadAssetAtPath<ScriptableObject>(soPath) != null)
                return;

            var instance = Instantiate(template);
            instance.name = soName;
            (instance as IAssetRouterDataSetup)?.SetupAssetRouter(importedAsset, ctx.AssetPath);

            PathUtility.EnsureFolderExists(folder);
            PipelineOutputGuard.MarkCreated(soPath);
            AssetDatabase.CreateAsset(instance, soPath);

            ctx.Logger.Log($"[AssetRouter] CreateScriptableObject → {soPath}");
        }
    }
}
