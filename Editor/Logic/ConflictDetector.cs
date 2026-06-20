using System;
using System.Collections.Generic;
using Kodlon.AssetRouter.Data;

namespace Kodlon.AssetRouter.Logic
{
    internal enum ConflictType
    {
        /// <summary>Two rules share the identical pattern, mode, and scope.</summary>
        Duplicate,

        /// <summary>Both rules match at least one of the same sample filenames.</summary>
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

    /// <summary>
    /// Detects duplicate and overlapping rules in an <see cref="ImporterSettingsDatabase"/>.
    /// Duplicate detection is exact; overlap detection is a pragmatic heuristic using a fixed
    /// set of representative asset paths.
    /// </summary>
    internal static class ConflictDetector
    {
        // Representative paths used for overlap detection.
        // Using full paths so both matchAgainstFullPath=false (filename only) and
        // matchAgainstFullPath=true (full path) are exercised correctly.
        private static readonly string[] SamplePaths =
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

                    if (HasSampleOverlap(rules[i], rules[j]))
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

        private static bool HasSampleOverlap(BaseImportRule a, BaseImportRule b)
        {
            for (var i = 0; i < SamplePaths.Length; i++)
            {
                if (PatternMatcher.Matches(a, SamplePaths[i]) && PatternMatcher.Matches(b, SamplePaths[i]))
                    return true;
            }

            return false;
        }
    }
}
