using Kodlon.AssetRouter.Data;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Appends the imported asset to an <see cref="AssetCatalog"/> ScriptableObject.
    /// Idempotent — skips if the asset is already in the catalog.
    /// </summary>
    [CreateAssetMenu(menuName = "Asset Router/Actions/Append to Catalog", fileName = "AppendToCatalogAction")]
    public sealed class AppendToCatalogAction : AssetImportActionAsset
    {
        [Tooltip("The catalog ScriptableObject to append to.")]
        public AssetCatalog catalog;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => catalog != null && importedAsset != null;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            if (catalog == null || importedAsset == null)
                return;

            if (catalog.entries.Contains(importedAsset))
                return;

            catalog.entries.Add(importedAsset);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);

            ctx.Logger.Log($"[AssetRouter] AppendToCatalog → {ctx.AssetPath} added to '{catalog.name}'");
        }
    }
}
