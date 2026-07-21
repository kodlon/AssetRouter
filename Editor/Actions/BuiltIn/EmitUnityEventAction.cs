using System;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Serializable <c>UnityEvent&lt;Object&gt;</c> used by
    /// <see cref="EmitUnityEventAction" />.
    /// </summary>
    [Serializable]
    public sealed class ImportedAssetEvent : UnityEvent<Object> { }

    /// <summary>
    /// Fires a serialized <c>UnityEvent&lt;Object&gt;</c> when an asset is imported.
    /// Lets non-programmers wire up callbacks entirely in the Inspector without
    /// writing code.
    /// </summary>
    /// <remarks>
    /// This action is itself a ScriptableObject asset, so its persistent listeners can
    /// only target other
    /// assets (e.g. a ScriptableObject or a prefab's components) — a listener pointing
    /// at a scene object
    /// serializes to null and silently no-ops when invoked, since scene objects don't
    /// exist at asset-edit time.
    /// <see cref="CanRunOn" /> only counts <em>persistent</em> listeners added in the
    /// Inspector; listeners
    /// added at runtime via <c>UnityEvent.AddListener</c> do not count and will never
    /// cause this action to run.
    /// </remarks>
    [CreateAssetMenu(menuName = "Asset Router/Actions/Emit Unity Event", fileName = "EmitUnityEventAction")]
    public sealed class EmitUnityEventAction : AssetImportActionAsset
    {
        [SerializeField]
        private ImportedAssetEvent _onImport = new();

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx) => _onImport != null && _onImport.GetPersistentEventCount() > 0;

        public override void Execute(Object importedAsset, AssetImportContext ctx) => _onImport?.Invoke(importedAsset);
    }
}