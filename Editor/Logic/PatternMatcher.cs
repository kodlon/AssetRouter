using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Kodlon.AssetRouter.Data;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class PatternMatcher
    {
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(50);

        /// <summary>Returns the regex Match for capture group access, or null on no match / error.</summary>
        public static Match Match(BaseImportRule rule, string assetPath)
        {
            if (string.IsNullOrEmpty(rule?.pattern))
                return null;

            var target = rule.matchAgainstFullPath
                ? PathUtility.NormalizeAssetPath(assetPath)
                : Path.GetFileName(assetPath);

            var regex = GetOrCompile(rule);

            if (regex == null)
                return null;

            try
            {
                var m = regex.Match(target);
                return m.Success ? m : null;
            }
            catch (RegexMatchTimeoutException)
            {
                Debug.LogWarning($"[AssetRouter] Pattern '{rule.pattern}' timed out matching '{target}'. Simplify the pattern.");
                return null;
            }
        }

        public static bool Matches(BaseImportRule rule, string assetPath)
            => Match(rule, assetPath) != null;

        public static bool TryGetRegexError(string pattern, out string error)
        {
            try
            {
                _ = new Regex(pattern, RegexOptions.None, MatchTimeout);
                error = null;
                return false;
            }
            catch (ArgumentException e)
            {
                error = e.Message;
                return true;
            }
        }

        private static Regex GetOrCompile(BaseImportRule rule)
        {
            if (rule._compiledFor == rule.pattern && rule._compiledForMode == rule.patternMode)
                return rule._compiledPattern;

            var regexSource = rule.patternMode == PatternMode.Glob
                ? GlobToRegex(rule.pattern)
                : rule.pattern;

            try
            {
                rule._compiledPattern = new Regex(
                    regexSource,
                    RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    MatchTimeout);
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning($"[AssetRouter] Invalid pattern '{rule.pattern}': {e.Message}");
                rule._compiledPattern = null;
            }

            rule._compiledFor = rule.pattern;
            rule._compiledForMode = rule.patternMode;
            return rule._compiledPattern;
        }

        internal static string GlobToRegex(string glob)
        {
            var sb = new StringBuilder("^");
            var i = 0;

            while (i < glob.Length)
            {
                if (glob[i] == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
                {
                    i += 2;

                    // "**/" must expand over whole path segments only — a bare (.*) before the "/" literal
                    // would let the match start mid-segment (e.g. "Assets/**/T_*.png" wrongly matching
                    // "Assets/SubT_Rock.png"). A trailing "**" with nothing after it just captures the rest.
                    if (i < glob.Length && glob[i] == '/')
                    {
                        sb.Append("(?:(.*)/)?");
                        i++;
                    }
                    else
                    {
                        sb.Append("(.*)");
                    }
                }
                else if (glob[i] == '*')
                {
                    sb.Append("([^/]*)");
                    i++;
                }
                else if (glob[i] == '?')
                {
                    sb.Append("[^/]");
                    i++;
                }
                else
                {
                    sb.Append(Regex.Escape(glob[i].ToString()));
                    i++;
                }
            }

            sb.Append("$");
            return sb.ToString();
        }
    }
}
