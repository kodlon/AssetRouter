using System.Collections.Generic;
using UnityEngine;

namespace Kodlon.AssetRouter.Data
{
    /// <summary>
    /// ScriptableObject that holds a list of asset references. Used with <c>AppendToCatalogAction</c>
    /// to collect imported assets into a single registry accessible at runtime.
    /// Create one via <c>Create &gt; Asset Router &gt; Asset Catalog</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "AssetCatalog", menuName = "Asset Router/Asset Catalog")]
    public class AssetCatalog : ScriptableObject, ISerializationCallbackReceiver
    {
        /// <summary>Assets registered in this catalog. Kept public for Inspector edits and read-only iteration.</summary>
        public List<Object> entries = new();

        [System.NonSerialized] private HashSet<Object> _lookup;

        public void OnBeforeSerialize() { /* list is the source of truth */ }

        public void OnAfterDeserialize()
        {
            _lookup = new HashSet<Object>(entries);
        }

        /// <summary>Adds an entry if it is not already present. Returns true when the entry was appended.</summary>
        public bool Add(Object asset)
        {
            if (asset == null) return false;

            EnsureLookup();

            if (!_lookup.Add(asset)) return false;
            entries.Add(asset);
            return true;
        }

        public bool Contains(Object asset)
        {
            if (asset == null) return false;
            EnsureLookup();
            return _lookup.Contains(asset);
        }

        private void EnsureLookup()
        {
            // Deserialization only fires when the asset is loaded from disk. Runtime-created instances
            // (e.g. ScriptableObject.CreateInstance in tests) never see OnAfterDeserialize.
            if (_lookup == null)
                _lookup = new HashSet<Object>(entries);
        }
    }
}
