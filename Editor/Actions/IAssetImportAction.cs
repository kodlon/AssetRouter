using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Contract for pluggable actions that run after an asset is imported and moved.
    /// Implement via <see cref="AssetImportActionAsset"/> for serializable, reusable, Inspector-configurable actions.
    /// </summary>
    public interface IAssetImportAction
    {
        /// <summary>Returns <c>true</c> when this action can process <paramref name="importedAsset"/>.</summary>
        bool CanRunOn(Object importedAsset, AssetImportContext ctx);

        /// <summary>Executes the action. Only called when <see cref="CanRunOn"/> returns <c>true</c>.</summary>
        void Execute(Object importedAsset, AssetImportContext ctx);
    }
}
