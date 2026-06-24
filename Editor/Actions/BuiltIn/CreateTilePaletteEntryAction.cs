using System.IO;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Kodlon.AssetRouter.Actions
{
    [CreateAssetMenu(menuName = "Asset Router/Actions/Create Tile Palette Entry", fileName = "CreateTilePaletteEntryAction")]
    public sealed class CreateTilePaletteEntryAction : AssetImportActionAsset
    {
        [Tooltip("Output folder. Empty = rule's target folder.")]
        public string outputFolder = "";
        [Tooltip("{assetName} is replaced with the imported file name (no extension).")]
        public string namePattern = "{assetName}_Tile";
        public bool overwriteExisting = false;
        public Color tileColor = Color.white;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => AssetImporter.GetAtPath(ctx.AssetPath) is TextureImporter ti
               && ti.textureType == TextureImporterType.Sprite;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ctx.AssetPath);
            if (sprite == null)
            {
                ctx.Logger.LogWarning("AssetRouter", $"[AssetRouter] CreateTile: no Sprite sub-asset at {ctx.AssetPath}");
                return;
            }

            var folder   = PathUtility.NormalizeAssetPath(string.IsNullOrEmpty(outputFolder) ? ctx.Rule.targetFolder : outputFolder);
            var baseName = Path.GetFileNameWithoutExtension(ctx.AssetPath);
            var tileName = namePattern.Replace("{assetName}", baseName);
            var tilePath = folder + "/" + tileName + ".asset";

            if (!overwriteExisting && AssetDatabase.LoadAssetAtPath<Tile>(tilePath) != null)
                return;

            var tile = CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color  = tileColor;
            tile.name   = tileName;

            PathUtility.EnsureFolderExists(folder);
            AssetDatabase.CreateAsset(tile, tilePath);

            ctx.Logger.Log($"[AssetRouter] CreateTile → {tilePath}");
        }
    }
}
