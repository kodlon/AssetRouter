using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class TargetResolver
    {
        /// <summary>
        /// Resolves {n} (positional) and {name} (named) capture group tokens in <paramref name="template"/>.
        /// <c>{{</c> and <c>}}</c> are escape sequences for literal braces.
        /// Returns <paramref name="template"/> unchanged when no <c>{</c> characters are present or
        /// <paramref name="match"/> is null / unsuccessful.
        /// </summary>
        public static string Resolve(string template, Match match)
        {
            if (string.IsNullOrEmpty(template) || !template.Contains('{'))
                return template;

            if (match == null || !match.Success)
                return template;

            var sb = new StringBuilder(template.Length + 32);
            var i = 0;

            while (i < template.Length)
            {
                var c = template[i];

                if (c == '{')
                {
                    if (i + 1 < template.Length && template[i + 1] == '{')
                    {
                        sb.Append('{');
                        i += 2;
                        continue;
                    }

                    var end = template.IndexOf('}', i + 1);
                    if (end < 0)
                    {
                        sb.Append(c);
                        i++;
                        continue;
                    }

                    var token = template.Substring(i + 1, end - i - 1);
                    string value;

                    if (int.TryParse(token, out var idx))
                    {
                        if (idx >= 0 && idx < match.Groups.Count && match.Groups[idx].Success)
                        {
                            value = Sanitize(match.Groups[idx].Value, token);
                        }
                        else
                        {
                            Debug.LogWarning($"[AssetRouter] TargetResolver: token '{{{token}}}' — group index {idx} not found in pattern captures. Token kept literally.");
                            value = null;
                        }
                    }
                    else
                    {
                        var group = match.Groups[token];
                        if (group != null && group.Success)
                        {
                            value = Sanitize(group.Value, token);
                        }
                        else
                        {
                            Debug.LogWarning($"[AssetRouter] TargetResolver: token '{{{token}}}' — named group '{token}' not found in pattern captures. Token kept literally.");
                            value = null;
                        }
                    }

                    if (value != null)
                        sb.Append(value);
                    else
                        sb.Append('{').Append(token).Append('}');

                    i = end + 1;
                }
                else if (c == '}' && i + 1 < template.Length && template[i + 1] == '}')
                {
                    sb.Append('}');
                    i += 2;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            var raw = sb.ToString();
            if (raw.IndexOf("//", StringComparison.Ordinal) < 0)
                return raw;
            while (raw.Contains("//"))
                raw = raw.Replace("//", "/");
            return raw;
        }

        internal static bool TryValidate(string template, Match match, out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(template) || !template.Contains('{'))
                return true;

            if (match == null || !match.Success)
                return true;

            var i = 0;
            while (i < template.Length)
            {
                var c = template[i];

                if (c == '{')
                {
                    if (i + 1 < template.Length && template[i + 1] == '{')
                    {
                        i += 2;
                        continue;
                    }

                    var end = template.IndexOf('}', i + 1);
                    if (end < 0)
                    {
                        i++;
                        continue;
                    }

                    var token = template.Substring(i + 1, end - i - 1);

                    if (int.TryParse(token, out var idx))
                    {
                        if (idx < 0 || idx >= match.Groups.Count || !match.Groups[idx].Success)
                        {
                            error = $"token '{{{token}}}' not found in pattern captures";
                            return false;
                        }
                    }
                    else
                    {
                        var group = match.Groups[token];
                        if (group == null || !group.Success)
                        {
                            error = $"token '{{{token}}}' not found in pattern captures";
                            return false;
                        }
                    }

                    i = end + 1;
                }
                else if (c == '}' && i + 1 < template.Length && template[i + 1] == '}')
                {
                    i += 2;
                }
                else
                {
                    i++;
                }
            }

            return true;
        }

        private static string Sanitize(string value, string token)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            value = value.Replace('\\', '/');

            var parts = value.Split('/');
            var sb = new StringBuilder(value.Length);

            foreach (var part in parts)
            {
                if (part == ".." || part == ".")
                {
                    Debug.LogWarning($"[AssetRouter] TargetResolver: captured value for token '{{{token}}}' contains path traversal — token kept literally.");
                    return null;
                }

                if (sb.Length > 0) sb.Append('/');
                sb.Append(part);
            }

            return sb.ToString().TrimStart('/');
        }
    }
}
