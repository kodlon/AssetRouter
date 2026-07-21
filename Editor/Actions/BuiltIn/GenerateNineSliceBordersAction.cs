using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Scans the sprite texture for transparent border regions and writes the result
    /// to
    /// <c>TextureImporter.spriteBorder</c>. Requires Read/Write enabled on the
    /// texture.
    /// Only runs on Sprite-type textures.
    /// </summary>
    [CreateAssetMenu(menuName = "Asset Router/Actions/Generate Nine Slice Borders", fileName = "GenerateNineSliceBordersAction")]
    public sealed class GenerateNineSliceBordersAction : AssetImportActionAsset
    {
        /// <summary>
        /// Pixels with alpha below this value are treated as transparent.
        /// The action scans from each edge inward until it finds a pixel at or above this
        /// threshold.
        /// Range: 0 to 1.
        /// </summary>
        [Range(0f, 1f)]
        [Tooltip("Alpha below this value is treated as transparent.")]
        public float alphaThreshold = 0.1f;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx) =>
            importedAsset is Texture2D t && t.isReadable
                                         && AssetImporter.GetAtPath(ctx.AssetPath) is TextureImporter ti
                                         && ti.textureType == TextureImporterType.Sprite;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            if (importedAsset is not Texture2D texture)
                return;

            if (!texture.isReadable)
            {
                ctx.Logger.LogWarning("AssetRouter", $"[AssetRouter] NineSliceBorders: Read/Write must be enabled on {ctx.AssetPath}");

                return;
            }

            if (AssetImporter.GetAtPath(ctx.AssetPath) is not TextureImporter importer
                || importer.textureType != TextureImporterType.Sprite)
                return;

            var border = ComputeBorder(texture, alphaThreshold);

            if (importer.spriteBorder == border)
                return;

            importer.spriteBorder = border;
            AssetDatabase.ImportAsset(ctx.AssetPath, ImportAssetOptions.ForceUpdate);

            ctx.Logger.Log($"[AssetRouter] NineSliceBorders {border} → {ctx.AssetPath}");
        }

        internal static Vector4 ComputeBorder(Color[] pixels, int w, int h, float threshold)
        {
            var left = FindLeft(pixels, w, h, threshold);
            var right = FindRight(pixels, w, h, threshold);
            var bottom = FindBottom(pixels, w, h, threshold);
            var top = FindTop(pixels, w, h, threshold);

            return new Vector4(left, bottom, w - 1 - right, h - 1 - top);
        }

        private static Vector4 ComputeBorder(Texture2D texture, float threshold) =>
            ComputeBorder(texture.GetPixels(), texture.width, texture.height, threshold);

        private static int FindBottom(Color[] p, int w, int h, float t)
        {
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                    if (p[y * w + x].a >= t)
                        return y;
            }

            return 0;
        }

        private static int FindLeft(Color[] p, int w, int h, float t)
        {
            for (var x = 0; x < w; x++)
            {
                for (var y = 0; y < h; y++)
                    if (p[y * w + x].a >= t)
                        return x;
            }

            return 0;
        }

        private static int FindRight(Color[] p, int w, int h, float t)
        {
            for (var x = w - 1; x >= 0; x--)
            {
                for (var y = 0; y < h; y++)
                    if (p[y * w + x].a >= t)
                        return x;
            }

            return w - 1;
        }

        private static int FindTop(Color[] p, int w, int h, float t)
        {
            for (var y = h - 1; y >= 0; y--)
            {
                for (var x = 0; x < w; x++)
                    if (p[y * w + x].a >= t)
                        return y;
            }

            return h - 1;
        }
    }
}