using System.IO;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Creates a <c>UnityEngine.Tilemaps.Tile</c> asset from the first Sprite
    /// sub-asset of the imported texture.
    /// Only runs on Sprite-type textures. Logs a warning and skips if no Sprite
    /// sub-asset is found.
    /// </summary>
    [CreateAssetMenu(menuName = "Asset Router/Actions/Create Tile Palette Entry", fileName = "CreateTilePaletteEntryAction")]
    public sealed class CreateTilePaletteEntryAction : AssetImportActionAsset
    {
        /// <summary>
        /// Folder where the output .asset tile is saved. Falls back to the
        /// imported asset's folder when empty.
        /// </summary>
        [Tooltip("Output folder. Empty = the imported asset's folder.")]
        public string outputFolder = "";

        /// <summary>
        /// File name for the output tile without extension. <c>{assetName}</c> is
        /// replaced with the imported file name.
        /// </summary>
        [Tooltip("{assetName} is replaced with the imported file name (no extension).")]
        public string namePattern = "{assetName}_Tile";

        /// <summary>
        /// When false, the action is skipped if a tile asset already exists at
        /// the output path.
        /// </summary>
        public bool overwriteExisting;

        /// <summary>Tint color applied to the tile. White means no tint.</summary>
        public Color tileColor = Color.white;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx) =>
            AssetImporter.GetAtPath(ctx.AssetPath) is TextureImporter ti
            && ti.textureType == TextureImporterType.Sprite;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ctx.AssetPath);

            if (sprite == null)
            {
                ctx.Logger.LogWarning("AssetRouter", $"[AssetRouter] CreateTile: no Sprite sub-asset at {ctx.AssetPath}");

                return;
            }

            var folder = PathUtility.NormalizeAssetPath(
                string.IsNullOrEmpty(outputFolder) ? Path.GetDirectoryName(ctx.AssetPath) ?? string.Empty : outputFolder);

            var baseName = Path.GetFileNameWithoutExtension(ctx.AssetPath);
            var tileName = namePattern.Replace("{assetName}", baseName);
            var tilePath = folder + "/" + tileName + ".asset";

            if (!overwriteExisting && AssetDatabase.LoadAssetAtPath<Tile>(tilePath) != null)
                return;

            var tile = CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = tileColor;
            tile.name = tileName;

            ctx.Sink?.OnFoldersCreated(PathUtility.EnsureFolderExists(folder));
            PipelineOutputGuard.MarkCreated(tilePath);
            AssetDatabase.CreateAsset(tile, tilePath);
            ctx.Sink?.OnAssetCreated(tilePath);

            ctx.Logger.Log($"[AssetRouter] CreateTile → {tilePath}");
        }
    }
}