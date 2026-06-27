using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Derives a bounding rectangle from the sprite's opaque pixels and applies it as the physics shape
    /// via <c>Sprite.OverridePhysicsShape</c>. Requires Read/Write enabled on the texture.
    /// </summary>
    [CreateAssetMenu(menuName = "Asset Router/Actions/Generate Sprite Physics Shape", fileName = "GenerateSpritePhysicsShapeAction")]
    public sealed class GenerateSpritePhysicsShapeAction : AssetImportActionAsset
    {
        /// <summary>
        /// Pixels with alpha below this value are treated as transparent and excluded from the bounding box.
        /// Range: 0 to 1.
        /// </summary>
        [Range(0f, 1f)]
        [Tooltip("Alpha below this value is treated as transparent.")]
        public float alphaThreshold = 0.1f;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => importedAsset is Texture2D t && t.isReadable
               && AssetImporter.GetAtPath(ctx.AssetPath) is TextureImporter ti
               && ti.textureType == TextureImporterType.Sprite;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            if (importedAsset is not Texture2D texture)
                return;

            if (!texture.isReadable)
            {
                ctx.Logger.LogWarning("AssetRouter", $"[AssetRouter] SpritePhysicsShape: Read/Write must be enabled on {ctx.AssetPath}");
                return;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ctx.AssetPath);
            if (sprite == null)
                return;

            var pixels = texture.GetPixels();
            var w      = texture.width;
            var h      = texture.height;

            var left   = FindLeft(pixels, w, h, alphaThreshold);
            var right  = FindRight(pixels, w, h, alphaThreshold);
            var bottom = FindBottom(pixels, w, h, alphaThreshold);
            var top    = FindTop(pixels, w, h, alphaThreshold);

            if (left > right || bottom > top)
                return;

            var ppu    = sprite.pixelsPerUnit;
            var pivot  = sprite.pivot;
            var outline = new Vector2[]
            {
                new Vector2((left - pivot.x) / ppu,          (bottom - pivot.y) / ppu),
                new Vector2((right + 1 - pivot.x) / ppu,     (bottom - pivot.y) / ppu),
                new Vector2((right + 1 - pivot.x) / ppu,     (top + 1 - pivot.y) / ppu),
                new Vector2((left - pivot.x) / ppu,          (top + 1 - pivot.y) / ppu),
            };

            sprite.OverridePhysicsShape(new List<Vector2[]> { outline });
            EditorUtility.SetDirty(sprite);
            AssetDatabase.SaveAssets();

            ctx.Logger.Log($"[AssetRouter] SpritePhysicsShape → {ctx.AssetPath}");
        }

        private static int FindLeft(Color[] p, int w, int h, float t)
        {
            for (var x = 0; x < w; x++)
                for (var y = 0; y < h; y++)
                    if (p[y * w + x].a >= t) return x;
            return 0;
        }

        private static int FindRight(Color[] p, int w, int h, float t)
        {
            for (var x = w - 1; x >= 0; x--)
                for (var y = 0; y < h; y++)
                    if (p[y * w + x].a >= t) return x;
            return w - 1;
        }

        private static int FindBottom(Color[] p, int w, int h, float t)
        {
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    if (p[y * w + x].a >= t) return y;
            return 0;
        }

        private static int FindTop(Color[] p, int w, int h, float t)
        {
            for (var y = h - 1; y >= 0; y--)
                for (var x = 0; x < w; x++)
                    if (p[y * w + x].a >= t) return y;
            return h - 1;
        }
    }
}
