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

        // Separate set: a trigger asset whose own pipeline run is in flight. An action that reimports its
        // own trigger asset (e.g. SetPivotAction's ForceUpdate) causes that path to come back through
        // OnPostprocessAllAssets while ActionPipeline.Execute is still running the rest of the rule's
        // actions for it. Without this guard, the rule matches again and the entire action list — not just
        // the reimporting action — runs a second time (double-fired events, double-counted stats, and
        // an outright infinite loop for any action without its own equality guard).
        private static readonly HashSet<string> _inFlight = new(StringComparer.OrdinalIgnoreCase);

        public static void BeginRun(string assetPath) => _inFlight.Add(PathUtility.NormalizeAssetPath(assetPath));

        public static void EndRun(string assetPath) => _inFlight.Remove(PathUtility.NormalizeAssetPath(assetPath));

        public static bool IsRunning(string assetPath) => _inFlight.Contains(PathUtility.NormalizeAssetPath(assetPath));
    }
}
