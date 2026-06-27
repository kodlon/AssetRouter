using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Serialization;

namespace Kodlon.AssetRouter.Data
{
    /// <summary>
    /// Base class for all import rules. Extend this to create rule variants with additional fields.
    /// </summary>
    [Serializable]
    public abstract class BaseImportRule
    {
        /// <summary>Display name shown in the rules list and logs.</summary>
        public string ruleName = "New Rule";

        /// <summary>When false, the rule is skipped entirely during matching.</summary>
        public bool isEnabled = true;

        [Space]
        /// <summary>Determines whether <see cref="pattern"/> is treated as Glob or Regex.</summary>
        public PatternMode patternMode = PatternMode.Glob;

        /// <summary>
        /// Pattern used to match asset file names (or full paths when <see cref="matchAgainstFullPath"/> is true).
        /// Glob example: <c>T_*_D.png</c>. Regex example: <c>^T_.+_D\.png$</c>.
        /// </summary>
        public string pattern = "";

        /// <summary>
        /// When true, the pattern is matched against the full asset path (e.g. <c>Assets/Raw/T_Rock.png</c>).
        /// When false, only the file name is matched. Required for path-based patterns like <c>Assets/**</c>.
        /// </summary>
        public bool matchAgainstFullPath = false;

        [Space]
        /// <summary>
        /// Assets matched by this rule are moved to this folder. Must start with <c>Assets/</c>.
        /// </summary>
        public string targetFolder = "Assets/";

        /// <summary>
        /// When non-empty, the rule only applies to assets that are already inside this folder.
        /// Useful for routing the same file name differently depending on the source folder.
        /// </summary>
        public string scopeFolder = "";

        [SerializeField, HideInInspector, FormerlySerializedAs("prefix")]
        internal string _legacyPrefix = "";

        [SerializeField, HideInInspector, FormerlySerializedAs("suffix")]
        internal string _legacySuffix = "";

        [SerializeField, HideInInspector, FormerlySerializedAs("extensionFilter")]
        internal string _legacyExtensionFilter = "";

        [NonSerialized] internal Regex _compiledPattern;
        [NonSerialized] internal string _compiledFor;
        [NonSerialized] internal int _sessionMatchCount;
    }
}
