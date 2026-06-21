using Kodlon.AssetRouter.Data;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    public readonly struct AssetImportContext
    {
        public readonly string AssetPath;
        public readonly BaseImportRule Rule;
        public readonly ImporterSettingsDatabase Database;
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
