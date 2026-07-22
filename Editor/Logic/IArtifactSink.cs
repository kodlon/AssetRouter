using System.Collections.Generic;

namespace Kodlon.AssetRouter.Logic
{
    /// <summary>
    /// Receives assets and folders that an import action creates so Undo can reverse
    /// them.
    /// Actions guard with <c>ctx.Sink?.OnAssetCreated(path)</c> — the sink is null
    /// when the
    /// context is built by test code or direct callers.
    /// </summary>
    public interface IArtifactSink
    {
        /// <summary>
        /// Reports that an action created a new asset at the given path. Recorded in the
        /// operation log so Undo can delete it later.
        /// </summary>
        /// <param name="assetPath">Unity asset path with forward slashes.</param>
        void OnAssetCreated(string assetPath);

        /// <summary>
        /// Reports that an action created one or more new folders. Folders are undone
        /// only when they end up empty after any created assets in them are removed.
        /// </summary>
        /// <param name="folderPaths">Unity asset paths of the folders with forward slashes.</param>
        void OnFoldersCreated(IReadOnlyList<string> folderPaths);
    }
}