using UnityEngine;

namespace Kodlon.AssetRouter
{
    public interface IAssetRouterDataSetup
    {
        void SetupAssetRouter(Object importedAsset, string assetPath);
    }
}
