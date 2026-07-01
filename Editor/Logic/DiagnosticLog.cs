using System;
using System.Collections.Generic;
using UnityEditor;

namespace Kodlon.AssetRouter.Logic
{
    internal readonly struct DiagnosticEntry
    {
        public readonly string Timestamp;
        public readonly string AssetPath;
        public readonly string MatchedRule;
        public readonly string TargetPath;
        public readonly bool Moved;
        public readonly bool AlreadyInPlace;

        public DiagnosticEntry(string timestamp, string assetPath, string matchedRule, string targetPath, bool moved, bool alreadyInPlace)
        {
            Timestamp      = timestamp;
            AssetPath      = assetPath;
            MatchedRule    = matchedRule;
            TargetPath     = targetPath;
            Moved          = moved;
            AlreadyInPlace = alreadyInPlace;
        }
    }

    internal static class DiagnosticLog
    {
        private const int MaxEntries = 500;
        private static readonly List<DiagnosticEntry> _entries = new(MaxEntries);

        public static bool IsEnabled { get; set; }

        public static IReadOnlyList<DiagnosticEntry> Entries => _entries;

        [InitializeOnLoadMethod]
        private static void RegisterReloadHook()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Clear;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
        }

        public static void Add(string assetPath, string matchedRule, string targetPath, bool moved, bool alreadyInPlace)
        {
            if (_entries.Count >= MaxEntries)
                _entries.RemoveAt(0);

            _entries.Add(new DiagnosticEntry(
                DateTime.Now.ToString("HH:mm:ss.fff"),
                assetPath, matchedRule, targetPath, moved, alreadyInPlace));
        }

        public static void Clear() => _entries.Clear();
    }
}
