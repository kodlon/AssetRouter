using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class UndoEngine
    {
        public const string UndoSessionSource = "Undo";
        public const string RecycleFolder     = "Assets/_AssetRouterRecycle";

        public static UndoResult Revert(OperationSession session)
        {
            if (session?.entries == null || session.entries.Count == 0)
                return new UndoResult(0, 0);

            var entries         = session.entries;
            var revertedEntries = new List<OperationLogEntry>(entries.Count);
            var restored        = 0;
            var recycled        = 0;
            var failed          = 0;
            var cancelled       = false;
            var recycleReady    = false;

            // Parent folders must exist before StartAssetEditing — creation inside a batch breaks MoveAsset.
            foreach (var e in entries)
            {
                if (!IsProjectRoot(e.from))
                    PathUtility.EnsureFolderExists(PathUtility.NormalizeAssetPath(Path.GetDirectoryName(e.from) ?? string.Empty));
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                for (var i = entries.Count - 1; i >= 0; i--)
                {
                    var entry = entries[i];

                    if (EditorUtility.DisplayCancelableProgressBar("Asset Router — Undoing", entry.to, (float)(entries.Count - i) / entries.Count))
                    {
                        cancelled = true;
                        failed += i + 1;
                        break;
                    }

                    if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(entry.to)))
                    {
                        Debug.LogWarning($"[AssetRouter] Undo: asset no longer at \"{entry.to}\" — skipping.");
                        failed++;
                        continue;
                    }

                    // Root-origin drops go to the recycle folder to avoid re-cluttering the drop zone.
                    // Everything else restores in place.
                    var toRecycle = IsProjectRoot(entry.from);
                    if (toRecycle && !recycleReady)
                    {
                        PathUtility.EnsureFolderExists(RecycleFolder);
                        recycleReady = true;
                    }

                    var destination = toRecycle ? ResolveRecyclePath(entry.to) : entry.from;
                    var error       = AssetDatabase.MoveAsset(entry.to, destination);

                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogWarning($"[AssetRouter] Undo failed: {entry.to} -> {destination}: {error}");
                        failed++;
                        continue;
                    }

                    if (toRecycle) recycled++; else restored++;
                    revertedEntries.Add(new OperationLogEntry(entry.to, destination, entry.ruleName));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            // Splitting cleanup from the move batch avoids Unity refresh-order flakes seen when
            // move and delete share the same StartAssetEditing scope.
            var (deletedAssets, deletedFolders, failedCleanup) = CleanupSideEffects(session);

            if (revertedEntries.Count > 0)
                OperationLog.RecordBatch(revertedEntries, UndoSessionSource);

            var head = cancelled ? "Undo cancelled." : "Undo complete.";
            var summary = $"{head} Restored: {restored}, Recycled: {recycled}, Failed: {failed}";
            if (deletedAssets > 0 || deletedFolders > 0 || failedCleanup > 0)
                summary += $" · Cleanup: {deletedAssets} asset(s), {deletedFolders} folder(s)"
                         + (failedCleanup > 0 ? $", {failedCleanup} left behind" : string.Empty);

            Debug.Log($"[AssetRouter] {summary}");

            if (failed > 0 || failedCleanup > 0)
                EditorUtility.DisplayDialog("Asset Router — Undo Summary", summary, "OK");

            return new UndoResult(restored + recycled, failed);
        }

        private static bool IsProjectRoot(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            var parent = Path.GetDirectoryName(PathUtility.NormalizeAssetPath(assetPath))?.Replace('\\', '/');
            return string.Equals(parent, "Assets", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveRecyclePath(string sourcePath)
        {
            var fileName  = Path.GetFileName(sourcePath);
            var baseName  = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);

            var candidate = $"{RecycleFolder}/{fileName}";
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(candidate)))
                return candidate;

            for (var i = 1; i < 1000; i++)
            {
                candidate = $"{RecycleFolder}/{baseName}_{i}{extension}";
                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(candidate)))
                    return candidate;
            }

            return $"{RecycleFolder}/{baseName}_{DateTime.UtcNow.Ticks}{extension}";
        }

        private static (int assets, int folders, int failed) CleanupSideEffects(OperationSession session)
        {
            var assets  = 0;
            var folders = 0;
            var failed  = 0;

            if (session.createdAssets != null)
            {
                foreach (var path in session.createdAssets)
                {
                    if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                        continue;

                    if (AssetDatabase.DeleteAsset(path)) assets++;
                    else { Debug.LogWarning($"[AssetRouter] Undo cleanup: failed to delete \"{path}\"."); failed++; }
                }
            }

            if (session.createdFolders != null && session.createdFolders.Count > 0)
            {
                // Deepest first so nested folders empty out before their parents are checked.
                var sorted = new List<string>(session.createdFolders);
                sorted.Sort((a, b) => Depth(b).CompareTo(Depth(a)));

                foreach (var folder in sorted)
                {
                    if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                        continue;

                    var contents = AssetDatabase.FindAssets(string.Empty, new[] { folder });
                    if (contents != null && contents.Length > 0)
                        continue;

                    if (AssetDatabase.DeleteAsset(folder)) folders++;
                    else { Debug.LogWarning($"[AssetRouter] Undo cleanup: failed to delete empty folder \"{folder}\"."); failed++; }
                }
            }

            return (assets, folders, failed);
        }

        private static int Depth(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            var count = 1;
            for (var i = 0; i < path.Length; i++)
                if (path[i] == '/') count++;
            return count;
        }
    }
}
