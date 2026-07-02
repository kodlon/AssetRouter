using System.IO;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Creates a new Material from a base material, assigns the imported texture to a shader property, and saves the result.
    /// Runs only on Texture2D assets.
    /// </summary>
    [CreateAssetMenu(menuName = "Asset Router/Actions/Create Material From Texture", fileName = "CreateMaterialFromTextureAction")]
    public sealed class CreateMaterialFromTextureAction : AssetImportActionAsset
    {
        /// <summary>Material to use as the base. The new material copies its shader and all property values.</summary>
        public Material baseMaterial;

        /// <summary>
        /// Shader property name to assign the texture to. Examples: <c>_MainTex</c> (Standard), <c>_BaseMap</c> (URP Lit).
        /// If the property does not exist in the shader, the material is still created but the texture is not assigned.
        /// </summary>
        [Tooltip("Shader property to assign the texture to.")]
        public string textureProperty = "_MainTex";

        /// <summary>Folder where the output .mat is saved. Falls back to the imported asset's folder when empty.</summary>
        [Tooltip("Output folder. Empty = the imported asset's folder.")]
        public string outputFolder = "";

        /// <summary>File name for the output material without extension. <c>{assetName}</c> is replaced with the texture file name.</summary>
        [Tooltip("{assetName} is replaced with the texture file name (no extension).")]
        public string namePattern = "{assetName}_Mat";

        /// <summary>When false, the action is skipped if a material already exists at the output path.</summary>
        public bool overwriteExisting = false;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => importedAsset is Texture2D && baseMaterial != null;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            if (importedAsset is not Texture2D texture)
                return;

            var folder   = PathUtility.NormalizeAssetPath(string.IsNullOrEmpty(outputFolder) ? (Path.GetDirectoryName(ctx.AssetPath) ?? string.Empty) : outputFolder);
            var baseName = Path.GetFileNameWithoutExtension(ctx.AssetPath);
            var matName  = namePattern.Replace("{assetName}", baseName);
            var matPath  = folder + "/" + matName + ".mat";

            if (!overwriteExisting && AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                return;

            var mat = new Material(baseMaterial);
            mat.SetTexture(textureProperty, texture);

            PathUtility.EnsureFolderExists(folder);
            PipelineOutputGuard.MarkCreated(matPath);
            AssetDatabase.CreateAsset(mat, matPath);

            ctx.Logger.Log($"[AssetRouter] CreateMaterial → {matPath}");
        }
    }
}
