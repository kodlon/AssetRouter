using System.Collections.Generic;
using UnityEngine;

namespace Kodlon.AssetRouter.Data
{
    /// <summary>
    /// ScriptableObject that stores all Asset Router rules and general settings.
    /// Create one via <c>Create &gt; Asset Router &gt; Settings Database</c>; the plugin auto-creates a default at
    /// <c>Assets/AssetRouter/ImporterSettingsDatabase.asset</c> on first Editor load.
    /// </summary>
    [CreateAssetMenu(fileName = "ImporterSettingsDatabase", menuName = "Asset Router/Settings Database")]
    public class ImporterSettingsDatabase : ScriptableObject
    {
        /// <summary>Current schema version for migration purposes. Do not edit manually.</summary>
        public const int LatestSchemaVersion = 2;

        /// <summary>Schema version stored in this asset. Migrated automatically on load when below <see cref="LatestSchemaVersion"/>.</summary>
        public int schemaVersion = 0;

        /// <summary>When true, the postprocessor routes assets automatically on every import. Disable to use Dry Run only.</summary>
        public bool enableAutoImport = true;

        /// <summary>When true, a dialog appears for imported files that match no rule.</summary>
        public bool showPopupForUnknownFiles = true;

        [Space]
        /// <summary>
        /// File extensions that Asset Router monitors. Assets with other extensions are ignored entirely.
        /// Each entry must include the dot, e.g. <c>.png</c>.
        /// </summary>
        public List<string> monitoredExtensions = new();

        /// <summary>
        /// Asset paths that Asset Router never processes. Any asset inside a listed folder is skipped.
        /// Each entry must start with <c>Assets/</c>, e.g. <c>Assets/Plugins/</c>.
        /// </summary>
        public List<string> ignoredFolders = new();

        [Space]
        /// <summary>
        /// Ordered list of rules. First matching rule wins. Rules are evaluated top to bottom.
        /// </summary>
        [SerializeReference]
        public List<BaseImportRule> rules = new();

        private void Reset() => DefaultDatabaseFactory.PopulateDefaults(this);
    }
}
