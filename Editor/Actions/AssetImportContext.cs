using Kodlon.AssetRouter.Data;
using Kodlon.AssetRouter.Logic;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>Read-only context passed to every action in the post-import pipeline.</summary>
    public readonly struct AssetImportContext
    {
        /// <summary>Unity asset path of the imported file with forward slashes (e.g. <c>Assets/Art/Textures/T_Rock_D.png</c>).</summary>
        public readonly string AssetPath;

        /// <summary>The settings database that owned the matched rule. Useful for looking up shared configuration.</summary>
        public readonly ImporterSettingsDatabase Database;

        /// <summary>Logger to use for action output. Defaults to <c>Debug.unityLogger</c> when not supplied.</summary>
        public readonly ILogger Logger;

        /// <summary>The rule that matched this asset and triggered the action.</summary>
        public readonly BaseImportRule Rule;

        /// <summary>
        /// Optional sink that records action side effects (created assets and
        /// folders) so Undo can reverse them. Null in tests and hand-built contexts.
        /// </summary>
        public readonly IArtifactSink Sink;

        /// <summary>Creates a context without an artifact sink. Convenience overload for tests and direct callers.</summary>
        public AssetImportContext(string assetPath, BaseImportRule rule, ImporterSettingsDatabase database, ILogger logger = null)
            : this(assetPath, rule, database, logger,
                null) { }

        /// <summary>Creates a context with an artifact sink so created assets and folders can be undone.</summary>
        public AssetImportContext(
            string assetPath,
            BaseImportRule rule,
            ImporterSettingsDatabase database,
            ILogger logger,
            IArtifactSink sink)
        {
            AssetPath = assetPath;
            Rule = rule;
            Database = database;
            Logger = logger ?? Debug.unityLogger;
            Sink = sink;
        }
    }
}