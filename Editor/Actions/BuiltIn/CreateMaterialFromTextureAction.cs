using System;
using System.IO;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Creates a new Material from a base material, assigns the imported texture to a
    /// shader property, and saves the result.
    /// Runs only on Texture2D assets. When <see cref="baseMaterial" /> is null, falls
    /// back to the active render pipeline's
    /// default material (URP/HDRP <c>RenderPipelineAsset.defaultMaterial</c>) or the
    /// Built-in <c>Default-Material</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Asset Router/Actions/Create Material From Texture", fileName = "CreateMaterialFromTextureAction")]
    public sealed class CreateMaterialFromTextureAction : AssetImportActionAsset
    {
        /// <summary>
        /// Material to use as the base. The new material copies its shader and all
        /// property values.
        /// When null, the action falls back to the active render pipeline's default
        /// material — useful for zero-config setups.
        /// </summary>
        public Material baseMaterial;

        /// <summary>
        /// Shader property to assign the texture to. Examples: <c>_MainTex</c> (Standard),
        /// <c>_BaseMap</c> (URP Lit).
        /// If the property does not exist in the shader, the action falls back to
        /// <c>Material.mainTexture</c>, which
        /// respects the shader's <c>[MainTexture]</c> attribute (URP Lit maps this to
        /// <c>_BaseMap</c>).
        /// </summary>
        [Tooltip("Shader property to assign the texture to. Falls back to Material.mainTexture if not present on the shader.")]
        public string textureProperty = "_MainTex";

        /// <summary>
        /// Output folder for the .mat. Absolute paths starting with <c>Assets/</c> are
        /// used as-is; a relative path
        /// (e.g. <c>Materials</c>) is resolved as a subfolder of the imported texture's
        /// folder. Empty falls back to
        /// the texture's folder directly.
        /// </summary>
        [Tooltip("Absolute (Assets/...) or relative (subfolder next to texture). Empty = texture's folder.")]
        public string outputFolder = "Materials";

        /// <summary>
        /// File name for the output material without extension.
        /// <c>{assetName}</c> is replaced with the texture file name.
        /// </summary>
        [Tooltip("{assetName} is replaced with the texture file name (no extension).")]
        public string namePattern = "{assetName}_Mat";

        /// <summary>
        /// When false, the action is skipped if a material already exists at the
        /// output path.
        /// </summary>
        public bool overwriteExisting;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx) => importedAsset is Texture2D && ResolveBaseMaterial() != null;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            if (importedAsset is not Texture2D texture)
                return;

            var source = ResolveBaseMaterial();

            if (source == null)
            {
                ctx.Logger.LogWarning("AssetRouter",
                    "[AssetRouter] CreateMaterial: baseMaterial is null and no render-pipeline default could be resolved.");

                return;
            }

            var folder = ResolveOutputFolder(ctx.AssetPath);
            var baseName = Path.GetFileNameWithoutExtension(ctx.AssetPath);
            var matName = namePattern.Replace("{assetName}", baseName);
            var matPath = folder + "/" + matName + ".mat";

            if (!overwriteExisting && AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                return;

            var mat = new Material(source);
            AssignTexture(mat, texture);

            ctx.Sink?.OnFoldersCreated(PathUtility.EnsureFolderExists(folder));
            PipelineOutputGuard.MarkCreated(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
            ctx.Sink?.OnAssetCreated(matPath);

            ctx.Logger.Log($"[AssetRouter] CreateMaterial → {matPath}");
        }

        private void AssignTexture(Material mat, Texture2D texture)
        {
            // Fall back to Material.mainTexture, which honors [MainTexture] — URP Lit routes it to _BaseMap.
            if (!string.IsNullOrEmpty(textureProperty) && mat.HasProperty(textureProperty))
                mat.SetTexture(textureProperty, texture);
            else
                mat.mainTexture = texture;
        }

        /// <summary>
        /// Explicit <see cref="baseMaterial" /> → active SRP <c>defaultMaterial</c>
        /// (URP/HDRP) → Built-in RP <c>Default-Material</c>.
        /// </summary>
        private Material ResolveBaseMaterial()
        {
            if (baseMaterial != null)
                return baseMaterial;

            var srp = GraphicsSettings.currentRenderPipeline;

            if (srp != null && srp.defaultMaterial != null)
                return srp.defaultMaterial;

            return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
        }

        private string ResolveOutputFolder(string assetPath)
        {
            var assetFolder = PathUtility.NormalizeAssetPath(Path.GetDirectoryName(assetPath) ?? string.Empty);

            if (string.IsNullOrEmpty(outputFolder))
                return assetFolder;

            var normalized = PathUtility.NormalizeAssetPath(outputFolder).TrimStart('/');

            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || normalized == "Assets")
                return normalized;

            return assetFolder + "/" + normalized;
        }
    }
}