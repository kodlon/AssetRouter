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
    public class AssetCatalog : ScriptableObject
    {
        /// <summary>
        /// Assets registered in this catalog. <c>AppendToCatalogAction</c> adds to this list; duplicates are skipped.
        /// Note: <c>List.Contains</c> is O(N). On catalogs with 10 000+ entries, each import adds measurable overhead.
        /// </summary>
        public List<Object> entries = new();
    }
}
