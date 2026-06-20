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

            AssetDatabase.StartAssetEditing();
            try
            {
                // Reverse order so later moves don't conflict with earlier ones.
                for (var i = entries.Count - 1; i >= 0; i--)
                {
                    EditorUtility.DisplayProgressBar(
                        "Asset Router — Undoing",
                        entries[i].to,
                        (float)(total - i) / total);

                    var entry = entries[i];

                    if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(entry.to)))
                    {
                        Debug.LogWarning($"[AssetRouter] Undo: asset no longer at \"{entry.to}\" — skipping.");
                        failed++;
                        continue;
                    }

                    var fromFolder = PathUtility.NormalizeAssetPath(Path.GetDirectoryName(entry.from) ?? "");
                    EnsureFolderExists(fromFolder);

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

            Debug.Log($"[AssetRouter] Undo complete. Reverted: {reverted}, Failed: {failed}");
            return new UndoResult(reverted, failed);
        }

        private static void EnsureFolderExists(string folderPath)
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
