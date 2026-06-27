using UnityEngine;

namespace Kodlon.AssetRouter
{
    /// <summary>
    /// Implement on a MonoBehaviour to receive a callback from <c>CreatePrefabFromTemplateAction</c>
    /// after the prefab instance is created and before it is saved as an asset.
    /// </summary>
    /// <remarks>
    /// This interface lives in the Runtime assembly so game-code MonoBehaviours can implement it
    /// without taking an Editor assembly dependency.
    /// </remarks>
    public interface IAssetRouterPrefabSetup
    {
        /// <summary>
        /// Called on each component that implements this interface after the prefab is instantiated.
        /// Use it to populate default values based on the imported asset.
        /// </summary>
        /// <param name="importedAsset">The imported asset that triggered the action (e.g. a Texture2D or Mesh).</param>
        /// <param name="assetPath">Unity asset path of the imported file with forward slashes.</param>
        void SetupAssetRouter(Object importedAsset, string assetPath);
    }
}
