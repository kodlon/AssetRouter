using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Base class for ScriptableObject-based import actions.
    /// Subclass this to create reusable, Inspector-configurable post-import actions
    /// that can be assigned to rules in the Asset Router settings window.
    /// </summary>
    public abstract class AssetImportActionAsset : ScriptableObject, IAssetImportAction
    {
        /// <inheritdoc/>
        public abstract bool CanRunOn(Object importedAsset, AssetImportContext ctx);

        /// <inheritdoc/>
        public abstract void Execute(Object importedAsset, AssetImportContext ctx);
    }
}
