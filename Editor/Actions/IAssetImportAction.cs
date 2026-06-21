using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    public interface IAssetImportAction
    {
        bool CanRunOn(Object importedAsset, AssetImportContext ctx);
        void Execute(Object importedAsset, AssetImportContext ctx);
    }
}
