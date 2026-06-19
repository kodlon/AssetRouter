using System;
using UnityEditor.Presets;

namespace Kodlon.AssetRouter.Data
{
    [Serializable]
    public class ImportRule : BaseImportRule
    {
        public Preset preset;
    }
}