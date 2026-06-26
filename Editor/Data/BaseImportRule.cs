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

        public string pattern = "";

        public bool matchAgainstFullPath = false;

        [Space]
        public string targetFolder = "Assets/";

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
