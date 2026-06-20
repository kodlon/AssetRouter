using Kodlon.AssetRouter.Data;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Contextual information passed to each <see cref="IAssetImportAction"/> during execution.
    /// </summary>
    public readonly struct AssetImportContext
    {
        /// <summary>AssetDatabase-relative path to the asset (post-move location, if the asset was moved).</summary>
        public readonly string AssetPath;

        /// <summary>The rule that matched this asset.</summary>
        public readonly BaseImportRule Rule;

        /// <summary>The active settings database.</summary>
        public readonly ImporterSettingsDatabase Database;

        /// <summary>Logger for action diagnostics. Defaults to <see cref="Debug.unityLogger"/>.</summary>
        public readonly ILogger Logger;

        public AssetImportContext(string assetPath, BaseImportRule rule, ImporterSettingsDatabase database, ILogger logger = null)
        {
            AssetPath = assetPath;
            Rule = rule;
            Database = database;
            Logger = logger ?? Debug.unityLogger;
        }
    }
}
