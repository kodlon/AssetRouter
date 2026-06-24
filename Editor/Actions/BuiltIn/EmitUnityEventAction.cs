using UnityEngine;
using UnityEngine.Events;

namespace Kodlon.AssetRouter.Actions
{
    [System.Serializable]
    public sealed class ImportedAssetEvent : UnityEvent<Object> { }

    [CreateAssetMenu(menuName = "Asset Router/Actions/Emit Unity Event", fileName = "EmitUnityEventAction")]
    public sealed class EmitUnityEventAction : AssetImportActionAsset
    {
        [SerializeField] private ImportedAssetEvent _onImport = new ImportedAssetEvent();

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => _onImport != null && _onImport.GetPersistentEventCount() > 0;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
            => _onImport?.Invoke(importedAsset);
    }
}
