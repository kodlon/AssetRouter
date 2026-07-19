using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class PathUtility
    {
        private static readonly List<string> EmptyCreatedFolders = new List<string>(0);

        public static string NormalizeAssetPath(string path)
            => path?.Replace('\\', '/').TrimEnd('/') ?? string.Empty;

        public static string ToAbsolute(string assetPath)
        {
            if (assetPath == null) return string.Empty;
            var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        public static bool IsUnderFolder(string assetPath, string folder)
        {
            var normalizedPath   = NormalizeAssetPath(assetPath);
            var normalizedFolder = NormalizeAssetPath(folder).TrimEnd('/') + "/";
            return normalizedPath.StartsWith(normalizedFolder, StringComparison.OrdinalIgnoreCase);
        }

        // Must run before AssetDatabase.StartAssetEditing — CreateFolder inside a batch breaks MoveAsset.
        // Returns paths that were actually created (empty when the whole hierarchy existed) so
        // callers can hand them to IArtifactSink for undo cleanup.
        public static IReadOnlyList<string> EnsureFolderExists(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
                return EmptyCreatedFolders;

            var parts   = folderPath.Split('/');
            var current = parts[0];
            List<string> created = null;

            for (var i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;

                var next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    var guid = AssetDatabase.CreateFolder(current, parts[i]);
                    if (string.IsNullOrEmpty(guid))
                        Debug.LogWarning($"[AssetRouter] Failed to create folder \"{next}\".");
                    else
                        (created ??= new List<string>()).Add(next);
                }

                current = next;
            }

            return (IReadOnlyList<string>)created ?? EmptyCreatedFolders;
        }
    }
}
