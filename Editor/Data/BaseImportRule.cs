using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Serialization;

namespace Kodlon.AssetRouter.Data
{
    [Serializable]
    public abstract class BaseImportRule
    {
        public string ruleName = "New Rule";
        public bool isEnabled = true;

        [Space]
        public PatternMode patternMode = PatternMode.Glob;

        /// <summary>
        /// Glob (e.g. <c>T_*_D.png</c>) or Regex (e.g. <c>^T_.+_D\.png$</c>) pattern
        /// matched against the asset filename, or the full asset path when
        /// <see cref="matchAgainstFullPath"/> is <c>true</c>.
        /// </summary>
        public string pattern = "";

        public bool matchAgainstFullPath = false;

        [Space]
        public string targetFolder = "Assets/";

        // ── Legacy fields (schema v1) ─────────────────────────────────────────────
        // Kept solely so Unity can deserialise old .asset files. RuleMigrator reads
        // these and converts them into `pattern` when schemaVersion < 2.
        // FormerlySerializedAs maps the old field names from YAML/binary to these
        // internal fields so no data is silently dropped.

        [SerializeField, HideInInspector, FormerlySerializedAs("prefix")]
        internal string _legacyPrefix = "";

        [SerializeField, HideInInspector, FormerlySerializedAs("suffix")]
        internal string _legacySuffix = "";

        [SerializeField, HideInInspector, FormerlySerializedAs("extensionFilter")]
        internal string _legacyExtensionFilter = "";

        // ── Compiled-regex cache ──────────────────────────────────────────────────
        // Not serialised — rebuilt on demand by PatternMatcher. Stored here so each
        // rule owns its cache without an external dictionary.

        [NonSerialized] internal Regex _compiledPattern;
        [NonSerialized] internal string _compiledFor;
    }
}
