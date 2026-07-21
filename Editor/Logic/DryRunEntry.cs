using System.IO;
using Kodlon.AssetRouter.Data;

namespace Kodlon.AssetRouter.Logic
{
    internal sealed class DryRunEntry
    {
        public readonly bool AlreadyInPlace;
        public readonly string AssetPath;
        public readonly BaseImportRule MatchedRule;
        public readonly string TargetPath;
        public bool Selected;

        public string CurrentFolder => PathUtility.NormalizeAssetPath(Path.GetDirectoryName(AssetPath) ?? "");

        public string FileName => Path.GetFileName(AssetPath);

        public DryRunEntry(string assetPath, BaseImportRule rule, string targetPath, bool alreadyInPlace)
        {
            AssetPath = assetPath;
            MatchedRule = rule;
            TargetPath = targetPath;
            AlreadyInPlace = alreadyInPlace;
            Selected = rule != null && !alreadyInPlace;
        }
    }
}