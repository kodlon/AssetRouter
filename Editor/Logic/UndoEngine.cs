using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Logic
{
    internal static class UndoEngine
    {
        /// <summary>
        /// Session source tag used when an Undo run is written back to the operation log.
        /// HistoryView disables its "Undo Selected Session" button when a session carries this
        /// source, so an Undo cannot itself be un-done into an accidental redo cascade.
        /// </summary>
        public const string UndoSessionSource = "Undo";

        public static UndoResult Revert(OperationSession session)
        {
            if (session?.entries == null || session.entries.Count == 0)
                return new UndoResult(0, 0);

            var reverted = 0;
            var failed   = 0;
            var entries  = session.entries;
            var total    = entries.Count;
            var cancelled = false;
            // Records what physically happened during this undo: for each successfully moved-back
            // asset, `from` = pre-undo location (the routed target), `to` = post-undo location (the
            // original spot). Direction mirrors AutoImport/BatchMover sessions so HistoryView's
            // "from → to" columns read naturally.
            var revertedEntries = new List<OperationLogEntry>();

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
                    {
                        reverted++;
                        revertedEntries.Add(new OperationLogEntry(entry.to, entry.from, entry.ruleName));
                    }
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

            // Audit trail. Only write when something actually moved — a fully-failed or fully-cancelled
            // undo would otherwise pollute History with empty sessions.
            if (revertedEntries.Count > 0)
                OperationLog.RecordBatch(revertedEntries, UndoSessionSource);

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
