using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class PathUtility
    {
        public static string NormalizeAssetPath(string path)
            => path?.Replace('\\', '/').TrimEnd('/') ?? string.Empty;

        public static string ToAbsolute(string assetPath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        public static bool IsUnderFolder(string assetPath, string folder)
        {
            var normalizedPath = NormalizeAssetPath(assetPath);
            var normalizedFolder = NormalizeAssetPath(folder).TrimEnd('/') + "/";
            return normalizedPath.StartsWith(normalizedFolder, StringComparison.OrdinalIgnoreCase);
        }

        // Folders must be created BEFORE StartAssetEditing — creation inside the block causes MoveAsset to fail.
        public static void EnsureFolderExists(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
                return;

            var parts   = folderPath.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;

                var next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
