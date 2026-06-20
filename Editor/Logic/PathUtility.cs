using System;
using System.IO;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    /// <summary>
    /// Centralised path helpers. All methods that interact with AssetDatabase use forward
    /// slashes; methods returning absolute paths use the OS separator via Path.Combine.
    /// </summary>
    internal static class PathUtility
    {
        /// <summary>Replaces back-slashes with forward slashes and strips any trailing slash.</summary>
        public static string NormalizeAssetPath(string path)
            => path?.Replace('\\', '/').TrimEnd('/') ?? string.Empty;

        /// <summary>
        /// Converts an AssetDatabase-relative path (e.g. "Assets/Textures/T_Rock.png")
        /// to an absolute file-system path using the project root, not Application.dataPath,
        /// to avoid stripping "Assets" from paths that contain the word elsewhere.
        /// </summary>
        public static string ToAbsolute(string assetPath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="assetPath"/> is directly inside
        /// <paramref name="folder"/> or any sub-folder of it (case-insensitive).
        /// Both arguments are normalised before comparison, so mixed separators are safe.
        /// </summary>
        public static bool IsUnderFolder(string assetPath, string folder)
        {
            var normalizedPath = NormalizeAssetPath(assetPath);
            var normalizedFolder = NormalizeAssetPath(folder).TrimEnd('/') + "/";
            return normalizedPath.StartsWith(normalizedFolder, StringComparison.OrdinalIgnoreCase);
        }
    }
}
