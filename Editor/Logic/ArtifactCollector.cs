using System;
using System.Collections.Generic;

namespace Kodlon.AssetRouter.Logic
{
    // Case-insensitive dedup; insertion order preserved for readable session logs.
    // Not thread-safe — imports run on the main thread.
    internal sealed class ArtifactCollector : IArtifactSink
    {
        private readonly HashSet<string> _assetsSet  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _foldersSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string>    _assets     = new List<string>();
        private readonly List<string>    _folders    = new List<string>();

        public IReadOnlyList<string> Assets  => _assets;
        public IReadOnlyList<string> Folders => _folders;
        public bool HasAny => _assets.Count > 0 || _folders.Count > 0;

        public void OnAssetCreated(string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath) && _assetsSet.Add(assetPath))
                _assets.Add(assetPath);
        }

        public void OnFoldersCreated(IReadOnlyList<string> folderPaths)
        {
            if (folderPaths == null) return;

            for (var i = 0; i < folderPaths.Count; i++)
            {
                var folder = folderPaths[i];
                if (!string.IsNullOrEmpty(folder) && _foldersSet.Add(folder))
                    _folders.Add(folder);
            }
        }
    }
}
