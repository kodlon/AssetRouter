using System;
using System.Collections.Generic;
using Kodlon.AssetRouter.Actions;
using UnityEditor.Presets;

namespace Kodlon.AssetRouter.Data
{
    /// <summary>
    /// Standard import rule. Applies a Unity Preset on import and runs a list of post-import actions after the asset is moved.
    /// </summary>
    [Serializable]
    public class ImportRule : BaseImportRule
    {
        /// <summary>
        /// Unity Preset applied to the asset importer in <c>OnPreprocessAsset</c>.
        /// Must match the importer type (e.g. a TextureImporter preset on a texture file).
        /// </summary>
        public Preset preset;

        /// <summary>
        /// Ordered list of actions that run after the asset is moved to <see cref="BaseImportRule.targetFolder"/>.
        /// Actions execute in order; a failure in one action does not stop the rest.
        /// </summary>
        public List<AssetImportActionAsset> postImportActions = new();
    }
}