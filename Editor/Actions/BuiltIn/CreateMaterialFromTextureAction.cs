using System.IO;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    [CreateAssetMenu(menuName = "Asset Router/Actions/Create Material From Texture", fileName = "CreateMaterialFromTextureAction")]
    public sealed class CreateMaterialFromTextureAction : AssetImportActionAsset
    {
        public Material baseMaterial;
        [Tooltip("Shader property to assign the texture to.")]
        public string textureProperty = "_MainTex";
        [Tooltip("Output folder. Empty = rule's target folder.")]
        public string outputFolder = "";
        [Tooltip("{assetName} is replaced with the texture file name (no extension).")]
        public string namePattern = "{assetName}_Mat";
        public bool overwriteExisting = false;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => importedAsset is Texture2D && baseMaterial != null;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            if (importedAsset is not Texture2D texture)
                return;

            var folder   = PathUtility.NormalizeAssetPath(string.IsNullOrEmpty(outputFolder) ? (ctx.Rule?.targetFolder ?? string.Empty) : outputFolder);
            var baseName = Path.GetFileNameWithoutExtension(ctx.AssetPath);
            var matName  = namePattern.Replace("{assetName}", baseName);
            var matPath  = folder + "/" + matName + ".mat";

            if (!overwriteExisting && AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                return;

            var mat = new Material(baseMaterial);
            mat.SetTexture(textureProperty, texture);

            PathUtility.EnsureFolderExists(folder);
            AssetDatabase.CreateAsset(mat, matPath);

            ctx.Logger.Log($"[AssetRouter] CreateMaterial → {matPath}");
        }
    }
}
