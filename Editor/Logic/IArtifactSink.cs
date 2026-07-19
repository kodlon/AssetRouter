using System.Collections.Generic;

namespace Kodlon.AssetRouter.Logic
{
    /// <summary>
    /// Receives assets and folders that an import action creates so Undo can reverse them.
    /// Actions guard with <c>ctx.Sink?.OnAssetCreated(path)</c> — the sink is null when the
    /// context is built by test code or direct callers.
    /// </summary>
    public interface IArtifactSink
    {
        void OnAssetCreated(string assetPath);
        void OnFoldersCreated(IReadOnlyList<string> folderPaths);
    }
}
