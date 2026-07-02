using System;
using System.Collections.Generic;

namespace Kodlon.AssetRouter.Logic
{
    /// <summary>
    /// Marks asset paths that were just created by an <c>AssetImportActionAsset</c> (e.g. a generated prefab
    /// or material) so the postprocessor can skip re-routing them when they come back through
    /// <c>OnPostprocessAllAssets</c>. Without this, a wildcard rule that also matches an action's own output
    /// extension would route the generated asset again, indefinitely.
    /// </summary>
    internal static class PipelineOutputGuard
    {
        private static readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);

        public static void MarkCreated(string assetPath) => _paths.Add(PathUtility.NormalizeAssetPath(assetPath));

        /// <summary>Returns true (and consumes the mark) if this path was just created by the pipeline.</summary>
        public static bool WasCreatedByPipeline(string assetPath) =>
            _paths.Remove(PathUtility.NormalizeAssetPath(assetPath));
    }
}
