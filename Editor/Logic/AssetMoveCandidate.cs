using System.Text.RegularExpressions;
using Kodlon.AssetRouter.Data;

namespace Kodlon.AssetRouter.Logic
{
    internal readonly struct AssetMoveCandidate
    {
        public readonly string Path;
        public readonly BaseImportRule Rule;
        public readonly Match Match;

        public AssetMoveCandidate(string path, BaseImportRule rule, Match match)
        {
            Path = path;
            Rule = rule;
            Match = match;
        }
    }
}
