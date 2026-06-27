using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Contract for post-import actions attached to a rule.
    /// </summary>
    /// <remarks>
    /// Extend <see cref="AssetImportActionAsset"/> rather than implementing this interface directly.
    /// The action pipeline calls <see cref="CanRunOn"/> first; <see cref="Execute"/> runs only when it returns true.
    /// Exceptions thrown in <see cref="Execute"/> are caught per-action and logged; the pipeline continues.
    /// </remarks>
    public interface IAssetImportAction
    {
        /// <summary>
        /// Returns true when this action can run on the given asset. Return false to skip.
        /// </summary>
        /// <param name="importedAsset">The imported Unity asset object.</param>
        /// <param name="ctx">Context with the asset path, matched rule, database, and logger.</param>
        bool CanRunOn(Object importedAsset, AssetImportContext ctx);

        /// <summary>
        /// Performs the action. Called only when <see cref="CanRunOn"/> returned true.
        /// </summary>
        /// <param name="importedAsset">The imported Unity asset object.</param>
        /// <param name="ctx">Context with the asset path, matched rule, database, and logger.</param>
        void Execute(Object importedAsset, AssetImportContext ctx);
    }
}
