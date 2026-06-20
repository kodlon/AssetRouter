using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Kodlon.AssetRouter.Data;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    /// <summary>
    /// Matches an asset path against a rule's glob or regex pattern.
    /// Compiled <see cref="Regex"/> instances are cached on each rule and invalidated
    /// automatically when the pattern string changes.
    /// </summary>
    internal static class PatternMatcher
    {
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// Returns <c>true</c> when <paramref name="assetPath"/> satisfies the rule's pattern.
        /// Matching target is the filename (default) or the full normalised path
        /// when <see cref="BaseImportRule.matchAgainstFullPath"/> is <c>true</c>.
        /// </summary>
        public static bool Matches(BaseImportRule rule, string assetPath)
        {
            if (string.IsNullOrEmpty(rule?.pattern))
                return false;

            var target = rule.matchAgainstFullPath
                ? PathUtility.NormalizeAssetPath(assetPath)
                : Path.GetFileName(assetPath);

            var regex = GetOrCompile(rule);

            if (regex == null)
                return false;

            try
            {
                return regex.IsMatch(target);
            }
            catch (RegexMatchTimeoutException)
            {
                Debug.LogWarning($"[AssetRouter] Pattern '{rule.pattern}' timed out matching '{target}'. Simplify the pattern.");
                return false;
            }
        }

        /// <summary>
        /// Validates a Regex-mode pattern. Returns <c>false</c> (and sets <paramref name="error"/>
        /// to <c>null</c>) when the pattern is valid. Returns <c>true</c> with the parse error
        /// message when invalid.
        /// </summary>
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

        // ── Internal helpers ──────────────────────────────────────────────────────

        private static Regex GetOrCompile(BaseImportRule rule)
        {
            if (rule._compiledPattern != null && rule._compiledFor == rule.pattern)
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
            return rule._compiledPattern;
        }

        /// <summary>
        /// Converts a glob pattern to an anchored regex string.
        /// <list type="bullet">
        ///   <item><c>**</c> → <c>.*</c> (any sequence including <c>/</c>)</item>
        ///   <item><c>*</c>  → <c>[^/]*</c></item>
        ///   <item><c>?</c>  → <c>[^/]</c></item>
        ///   <item>all other characters are escaped</item>
        /// </list>
        /// </summary>
        internal static string GlobToRegex(string glob)
        {
            var sb = new StringBuilder("^");
            var i = 0;

            while (i < glob.Length)
            {
                if (glob[i] == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
                {
                    sb.Append(".*");
                    i += 2;
                    if (i < glob.Length && glob[i] == '/') i++;
                }
                else if (glob[i] == '*')
                {
                    sb.Append("[^/]*");
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
