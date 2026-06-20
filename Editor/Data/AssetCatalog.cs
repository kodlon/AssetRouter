using System.Collections.Generic;
using UnityEngine;

namespace Kodlon.AssetRouter.Data
{
    /// <summary>
    /// A simple ScriptableObject list of asset references.
    /// Used by <c>AppendToCatalogAction</c> to collect imported assets into a single SO.
    /// </summary>
    [CreateAssetMenu(fileName = "AssetCatalog", menuName = "Asset Router/Asset Catalog")]
    public class AssetCatalog : ScriptableObject
    {
        public List<Object> entries = new();
    }
}
