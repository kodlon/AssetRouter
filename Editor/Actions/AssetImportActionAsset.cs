using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Base class for all post-import actions in Asset Router.
    /// </summary>
    /// <remarks>
    /// Extend this class, override <see cref="CanRunOn"/> and <see cref="Execute"/>, and add
    /// <c>[CreateAssetMenu]</c> so the action appears in the + dropdown in the rule detail panel.
    /// Actions are stored as sub-assets inside the settings database .asset file.
    /// Each action instance can be shared across multiple rules.
    /// </remarks>
    public abstract class AssetImportActionAsset : ScriptableObject, IAssetImportAction
    {
        /// <inheritdoc/>
        public abstract bool CanRunOn(Object importedAsset, AssetImportContext ctx);

        /// <inheritdoc/>
        public abstract void Execute(Object importedAsset, AssetImportContext ctx);
    }
}
