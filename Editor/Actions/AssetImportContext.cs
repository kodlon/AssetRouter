using Kodlon.AssetRouter.Data;
using Kodlon.AssetRouter.Logic;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>Read-only context passed to every action in the post-import pipeline.</summary>
    public readonly struct AssetImportContext
    {
        public readonly string AssetPath;
        public readonly ImporterSettingsDatabase Database;
        public readonly ILogger Logger;
        public readonly BaseImportRule Rule;

        /// <summary>
        /// Optional sink that records action side effects (created assets and
        /// folders) so Undo can reverse them. Null in tests and hand-built contexts.
        /// </summary>
        public readonly IArtifactSink Sink;

        public AssetImportContext(string assetPath, BaseImportRule rule, ImporterSettingsDatabase database, ILogger logger = null)
            : this(assetPath, rule, database, logger,
                null) { }

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