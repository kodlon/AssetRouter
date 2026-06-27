using Kodlon.AssetRouter.Data;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Read-only context passed to every action in the post-import pipeline.
    /// </summary>
    public readonly struct AssetImportContext
    {
        /// <summary>Unity asset path with forward slashes, e.g. <c>Assets/Art/T_Rock_D.png</c>.</summary>
        public readonly string AssetPath;

        /// <summary>The rule that matched this asset and triggered the action chain.</summary>
        public readonly BaseImportRule Rule;

        /// <summary>The settings database that owns the matched rule.</summary>
        public readonly ImporterSettingsDatabase Database;

        /// <summary>Logger for action output. Defaults to <see cref="Debug.unityLogger"/> when not provided.</summary>
        public readonly ILogger Logger;

        /// <summary>
        /// Creates a new context. <paramref name="logger"/> defaults to <see cref="Debug.unityLogger"/> when null.
        /// </summary>
        public AssetImportContext(string assetPath, BaseImportRule rule, ImporterSettingsDatabase database, ILogger logger = null)
        {
            AssetPath = assetPath;
            Rule = rule;
            Database = database;
            Logger = logger ?? Debug.unityLogger;
        }
    }
}
