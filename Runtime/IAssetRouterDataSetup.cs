using UnityEngine;

namespace Kodlon.AssetRouter
{
    /// <summary>
    /// Implement on a ScriptableObject to receive a callback from <c>CreateScriptableObjectFromTemplateAction</c>
    /// after the SO is cloned from the template and before it is saved as an asset.
    /// </summary>
    /// <remarks>
    /// This interface lives in the Runtime assembly so game-code ScriptableObjects can implement it
    /// without taking an Editor assembly dependency.
    /// </remarks>
    public interface IAssetRouterDataSetup
    {
        /// <summary>
        /// Called on the cloned ScriptableObject instance before it is saved.
        /// Use it to populate fields based on the imported asset.
        /// </summary>
        /// <param name="importedAsset">The imported asset that triggered the action.</param>
        /// <param name="assetPath">Unity asset path of the imported file with forward slashes.</param>
        void SetupAssetRouter(Object importedAsset, string assetPath);
    }
}
