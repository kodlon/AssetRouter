using System.Collections.Generic;
using UnityEngine;

namespace Kodlon.AssetRouter.Data
{
    [CreateAssetMenu(fileName = "AssetCatalog", menuName = "Asset Router/Asset Catalog")]
    public class AssetCatalog : ScriptableObject
    {
        public List<Object> entries = new();
    }
}
