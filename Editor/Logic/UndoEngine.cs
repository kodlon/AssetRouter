using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class UndoEngine
    {
        public static UndoResult Revert(OperationSession session)
        {
            if (session?.entries == null || session.entries.Count == 0)
                return new UndoResult(0, 0);

            var reverted = 0;
            var failed   = 0;
            var entries  = session.entries;
            var total    = entries.Count;
            var cancelled = false;

            for (var i = entries.Count - 1; i >= 0; i--)
                PathUtility.EnsureFolderExists(PathUtility.NormalizeAssetPath(Path.GetDirectoryName(entries[i].from) ?? ""));

            AssetDatabase.StartAssetEditing();
            try
            {
                for (var i = entries.Count - 1; i >= 0; i--)
                {
                    var progress = (float)(total - i) / total;

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Asset Router — Undoing",
                            entries[i].to,
                            progress))
                    {
                        cancelled = true;
                        failed += i + 1;
                        break;
                    }

                    var entry = entries[i];

                    if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(entry.to)))
                    {
                        Debug.LogWarning($"[AssetRouter] Undo: asset no longer at \"{entry.to}\" — skipping.");
                        failed++;
                        continue;
                    }

                    var error = AssetDatabase.MoveAsset(entry.to, entry.from);

                    if (string.IsNullOrEmpty(error))
                        reverted++;
                    else
                    {
                        Debug.LogWarning($"[AssetRouter] Undo failed: {entry.to} -> {entry.from}: {error}");
                        failed++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            var summary = cancelled
                ? $"Undo cancelled. Reverted: {reverted}, Skipped/Failed: {failed}"
                : $"Undo complete. Reverted: {reverted}, Failed: {failed}";

            Debug.Log($"[AssetRouter] {summary}");

            if (failed > 0)
                EditorUtility.DisplayDialog("Asset Router — Undo Summary", summary, "OK");

            return new UndoResult(reverted, failed);
        }
    }
}
