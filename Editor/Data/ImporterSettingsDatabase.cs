using System.Collections.Generic;
using UnityEngine;

namespace Kodlon.AssetRouter.Data
{
    [CreateAssetMenu(fileName = "ImporterSettingsDatabase", menuName = "Asset Router/Settings Database")]
    public class ImporterSettingsDatabase : ScriptableObject
    {
        public const int LatestSchemaVersion = 2;

        public int schemaVersion = 0;

        public bool enableAutoImport = true;
        public bool showPopupForUnknownFiles = true;

        [Space]
        public List<string> monitoredExtensions = new();

        public List<string> ignoredFolders = new();

        [Space]
        [SerializeReference]
        public List<BaseImportRule> rules = new();

        private void Reset() => DefaultDatabaseFactory.PopulateDefaults(this);
    }
}
