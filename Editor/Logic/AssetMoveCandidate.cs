using Kodlon.AssetRouter.Data;

namespace Kodlon.AssetRouter.Logic
{
    internal readonly struct AssetMoveCandidate
    {
        public readonly string Path;
        public readonly BaseImportRule Rule;

        public AssetMoveCandidate(string path, BaseImportRule rule)
        {
            Path = path;
            Rule = rule;
        }
    }
}