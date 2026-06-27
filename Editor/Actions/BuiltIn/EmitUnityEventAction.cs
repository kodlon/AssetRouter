using UnityEngine;
using UnityEngine.Events;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>Serializable <c>UnityEvent&lt;Object&gt;</c> used by <see cref="EmitUnityEventAction"/>.</summary>
    [System.Serializable]
    public sealed class ImportedAssetEvent : UnityEvent<Object> { }

    /// <summary>
    /// Fires a serialized <c>UnityEvent&lt;Object&gt;</c> when an asset is imported.
    /// Lets non-programmers wire up callbacks entirely in the Inspector without writing code.
    /// Skipped automatically when no persistent listeners are configured.
    /// </summary>
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
