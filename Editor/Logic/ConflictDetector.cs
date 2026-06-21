using System;
using System.Collections.Generic;
using Kodlon.AssetRouter.Data;
using UnityEditor;

namespace Kodlon.AssetRouter.Logic
{
    internal enum ConflictType
    {
        Duplicate,
        Overlap
    }

    internal readonly struct RuleConflict
    {
        public readonly int IndexA;
        public readonly int IndexB;
        public readonly ConflictType Type;

        public RuleConflict(int a, int b, ConflictType type)
        {
            IndexA = a;
            IndexB = b;
            Type = type;
        }
    }

    internal static class ConflictDetector
    {
        private static readonly string[] FixedSamplePaths =
        {
            "Assets/T_Rock_D.png",   "Assets/T_Rock_N.png",   "Assets/T_Wall.png",
            "Assets/UI_Button.png",  "Assets/UI_Icon.png",    "Assets/UI_Background.png",
            "Assets/SFX_Click.wav",  "Assets/SFX_Jump.wav",
            "Assets/Mus_Theme.mp3",  "Assets/Mus_Loop.ogg",
            "Assets/Env_Rock.fbx",   "Assets/Char_Hero.fbx",
            "Assets/Sprite_Player.png", "Assets/Atlas_UI.png",
        };

        public static List<RuleConflict> Detect(List<BaseImportRule> rules)
        {
            var conflicts = new List<RuleConflict>();

            if (rules == null || rules.Count < 2)
                return conflicts;

            var samplePaths = BuildSamplePaths();

            for (var i = 0; i < rules.Count; i++)
            {
                if (!IsActive(rules[i])) continue;

                for (var j = i + 1; j < rules.Count; j++)
                {
                    if (!IsActive(rules[j])) continue;

                    if (IsStrictDuplicate(rules[i], rules[j]))
                    {
                        conflicts.Add(new RuleConflict(i, j, ConflictType.Duplicate));
                        continue;
                    }

                    if (HasSampleOverlap(rules[i], rules[j], samplePaths))
                        conflicts.Add(new RuleConflict(i, j, ConflictType.Overlap));
                }
            }

            return conflicts;
        }

        private static bool IsActive(BaseImportRule rule)
            => rule != null && rule.isEnabled && !string.IsNullOrEmpty(rule.pattern);

        private static bool IsStrictDuplicate(BaseImportRule a, BaseImportRule b)
            => a.patternMode == b.patternMode
               && a.matchAgainstFullPath == b.matchAgainstFullPath
               && string.Equals(a.pattern, b.pattern, StringComparison.OrdinalIgnoreCase);

        private static bool HasSampleOverlap(BaseImportRule a, BaseImportRule b, string[] samplePaths)
        {
            for (var i = 0; i < samplePaths.Length; i++)
            {
                if (PatternMatcher.Matches(a, samplePaths[i]) && PatternMatcher.Matches(b, samplePaths[i]))
                    return true;
            }

            return false;
        }

        private static string[] BuildSamplePaths()
        {
            var paths = new HashSet<string>(FixedSamplePaths, StringComparer.OrdinalIgnoreCase);

            var guids = AssetDatabase.FindAssets("", new[] { "Assets" });
            var limit = Math.Min(guids.Length, 100);

            for (var i = 0; i < limit; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!AssetDatabase.IsValidFolder(path))
                    paths.Add(path);
            }

            var result = new string[paths.Count];
            paths.CopyTo(result);
            return result;
        }
    }
}
