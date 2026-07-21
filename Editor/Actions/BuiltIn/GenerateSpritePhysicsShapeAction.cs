using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#if UNITY_2D_SPRITE
using UnityEditor.U2D.Sprites;
#endif

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Derives a bounding rectangle from each sprite's opaque pixels and persists it
    /// as the physics shape
    /// via <c>ISpritePhysicsOutlineDataProvider</c>, so it survives reimport and
    /// machine-to-machine transfer.
    /// Requires Read/Write enabled on the texture and the 2D Sprite package (
    /// <c>com.unity.2d.sprite</c>).
    /// Supports both Single and Multiple sprite import modes.
    /// </summary>
    [CreateAssetMenu(menuName = "Asset Router/Actions/Generate Sprite Physics Shape", fileName = "GenerateSpritePhysicsShapeAction")]
    public sealed class GenerateSpritePhysicsShapeAction : AssetImportActionAsset
    {
        /// <summary>
        /// Pixels with alpha below this value are treated as transparent and excluded from
        /// the bounding box.
        /// Range: 0 to 1.
        /// </summary>
        [Range(0f, 1f)]
        [Tooltip("Alpha below this value is treated as transparent.")]
        public float alphaThreshold = 0.1f;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
        {
#if UNITY_2D_SPRITE
            return importedAsset is Texture2D t && t.isReadable
                                                && AssetImporter.GetAtPath(ctx.AssetPath) is TextureImporter ti
                                                && ti.textureType == TextureImporterType.Sprite;
#else
            return false;
#endif
        }

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
#if UNITY_2D_SPRITE
            if (importedAsset is not Texture2D texture)
                return;

            if (!texture.isReadable)
            {
                ctx.Logger.LogWarning("AssetRouter", $"[AssetRouter] SpritePhysicsShape: Read/Write must be enabled on {ctx.AssetPath}");

                return;
            }

            if (AssetImporter.GetAtPath(ctx.AssetPath) is not TextureImporter importer)
                return;

            var factories = new SpriteDataProviderFactories();
            factories.Init();

            var dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);

            if (dataProvider == null)
                return;

            dataProvider.InitSpriteEditorDataProvider();

            var outlineProvider = dataProvider.GetDataProvider<ISpritePhysicsOutlineDataProvider>();

            if (outlineProvider == null)
                return;

            var spriteRects = dataProvider.GetSpriteRects();

            if (spriteRects == null || spriteRects.Length == 0)
                return;

            var pixels = texture.GetPixels();
            var texWidth = texture.width;
            var texHeight = texture.height;
            var appliedAny = false;

            foreach (var spriteRect in spriteRects)
            {
                // Bounds are computed per sprite rect so Multiple-mode sheets get one shape per sprite
                // instead of a single box spanning the whole texture.
                var rect = ClampToTexture(spriteRect.rect, texWidth, texHeight);

                var left = FindLeft(pixels, texWidth, rect, alphaThreshold);
                var right = FindRight(pixels, texWidth, rect, alphaThreshold);
                var bottom = FindBottom(pixels, texWidth, rect, alphaThreshold);
                var top = FindTop(pixels, texWidth, rect, alphaThreshold);

                if (left > right || bottom > top)
                    continue;

                // ISpritePhysicsOutlineDataProvider outlines are in texture-pixel space relative to the
                // CENTER of the sprite rect — unlike the runtime Sprite.OverridePhysicsShape API (used by
                // the old, non-persisting version of this action), they are NOT divided by pixels-per-unit
                // and are NOT relative to the pivot.
                var centerX = rect.width / 2f;
                var centerY = rect.height / 2f;

                var outline = new[]
                {
                    new Vector2(left - rect.xMin - centerX, bottom - rect.yMin - centerY),
                    new Vector2(right + 1 - rect.xMin - centerX, bottom - rect.yMin - centerY),
                    new Vector2(right + 1 - rect.xMin - centerX, top + 1 - rect.yMin - centerY),
                    new Vector2(left - rect.xMin - centerX, top + 1 - rect.yMin - centerY)
                };

                // Without this guard, SaveAndReimport() below triggers another import pass, the rule
                // matches again (already in place), and Execute runs again — an infinite reimport loop.
                if (OutlineMatches(outlineProvider.GetOutlines(spriteRect.spriteID), outline))
                    continue;

                outlineProvider.SetOutlines(spriteRect.spriteID, new List<Vector2[]>
                {
                    outline
                });

                appliedAny = true;
            }

            if (!appliedAny)
                return;

            dataProvider.Apply();
            importer.SaveAndReimport();

            ctx.Logger.Log($"[AssetRouter] SpritePhysicsShape → {ctx.AssetPath}");
#else
            ctx.Logger.LogWarning("AssetRouter", "[AssetRouter] GenerateSpritePhysicsShapeAction: com.unity.2d.sprite package is not installed.");
#endif
        }

#if UNITY_2D_SPRITE
        private static bool OutlineMatches(List<Vector2[]> existing, Vector2[] outline)
        {
            if (existing == null || existing.Count != 1)
                return false;

            var current = existing[0];

            if (current == null || current.Length != outline.Length)
                return false;

            for (var i = 0; i < outline.Length; i++)
            {
                if (current[i] != outline[i]) // Vector2's == is an approximate comparison
                    return false;
            }

            return true;
        }

        private static RectInt ClampToTexture(Rect rect, int texWidth, int texHeight)
        {
            var xMin = Mathf.Clamp(Mathf.RoundToInt(rect.xMin), 0, texWidth);
            var yMin = Mathf.Clamp(Mathf.RoundToInt(rect.yMin), 0, texHeight);
            var xMax = Mathf.Clamp(Mathf.RoundToInt(rect.xMax), 0, texWidth);
            var yMax = Mathf.Clamp(Mathf.RoundToInt(rect.yMax), 0, texHeight);

            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static int FindLeft(Color[] p, int texWidth, RectInt r, float t)
        {
            for (var x = r.xMin; x < r.xMax; x++)
            {
                for (var y = r.yMin; y < r.yMax; y++)
                    if (p[y * texWidth + x].a >= t)
                        return x;
            }

            return r.xMin;
        }

        private static int FindRight(Color[] p, int texWidth, RectInt r, float t)
        {
            for (var x = r.xMax - 1; x >= r.xMin; x--)
            {
                for (var y = r.yMin; y < r.yMax; y++)
                    if (p[y * texWidth + x].a >= t)
                        return x;
            }

            return r.xMax - 1;
        }

        private static int FindBottom(Color[] p, int texWidth, RectInt r, float t)
        {
            for (var y = r.yMin; y < r.yMax; y++)
            {
                for (var x = r.xMin; x < r.xMax; x++)
                    if (p[y * texWidth + x].a >= t)
                        return y;
            }

            return r.yMin;
        }

        private static int FindTop(Color[] p, int texWidth, RectInt r, float t)
        {
            for (var y = r.yMax - 1; y >= r.yMin; y--)
            {
                for (var x = r.xMin; x < r.xMax; x++)
                    if (p[y * texWidth + x].a >= t)
                        return y;
            }

            return r.yMax - 1;
        }
#endif
    }
}