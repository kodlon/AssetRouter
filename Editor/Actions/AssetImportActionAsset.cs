using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    public abstract class AssetImportActionAsset : ScriptableObject, IAssetImportAction
    {
        public abstract bool CanRunOn(Object importedAsset, AssetImportContext ctx);
        public abstract void Execute(Object importedAsset, AssetImportContext ctx);
    }
}
