using System;
using System.Collections.Generic;
using Kodlon.AssetRouter.Actions;
using UnityEditor.Presets;

namespace Kodlon.AssetRouter.Data
{
    [Serializable]
    public class ImportRule : BaseImportRule
    {
        public Preset preset;

        public List<AssetImportActionAsset> postImportActions = new();
    }
}