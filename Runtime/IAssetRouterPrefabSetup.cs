using UnityEngine;

namespace Kodlon.AssetRouter
{
    public interface IAssetRouterPrefabSetup
    {
        void SetupAssetRouter(Object importedAsset, string assetPath);
    }
}
