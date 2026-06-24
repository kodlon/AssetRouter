using System.IO;
using Kodlon.AssetRouter;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    [CreateAssetMenu(menuName = "Asset Router/Actions/Create ScriptableObject From Template", fileName = "CreateScriptableObjectFromTemplateAction")]
    public sealed class CreateScriptableObjectFromTemplateAction : AssetImportActionAsset
    {
        public ScriptableObject template;
        [Tooltip("Output folder. Empty = rule's target folder.")]
        public string outputFolder = "";
        [Tooltip("{assetName} is replaced with the imported file name (no extension).")]
        public string namePattern = "{assetName}_Data";
        public bool overwriteExisting = false;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => template != null;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            var folder   = PathUtility.NormalizeAssetPath(string.IsNullOrEmpty(outputFolder) ? ctx.Rule.targetFolder : outputFolder);
            var baseName = Path.GetFileNameWithoutExtension(ctx.AssetPath);
            var soName   = namePattern.Replace("{assetName}", baseName);
            var soPath   = folder + "/" + soName + ".asset";

            if (!overwriteExisting && AssetDatabase.LoadAssetAtPath<ScriptableObject>(soPath) != null)
                return;

            var instance = Instantiate(template);
            instance.name = soName;
            (instance as IAssetRouterDataSetup)?.SetupAssetRouter(importedAsset, ctx.AssetPath);

            PathUtility.EnsureFolderExists(folder);
            AssetDatabase.CreateAsset(instance, soPath);

            ctx.Logger.Log($"[AssetRouter] CreateScriptableObject → {soPath}");
        }
    }
}
