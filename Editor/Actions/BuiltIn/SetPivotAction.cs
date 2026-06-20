using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Sets the sprite pivot on a Sprite-type texture and reimports.
    /// Idempotent — skips reimport if the pivot is already at the target value.
    /// </summary>
    [CreateAssetMenu(menuName = "Asset Router/Actions/Set Pivot", fileName = "SetPivotAction")]
    public sealed class SetPivotAction : AssetImportActionAsset
    {
        public Vector2 pivot = new Vector2(0.5f, 0.5f);

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => AssetImporter.GetAtPath(ctx.AssetPath) is TextureImporter ti
               && ti.textureType == TextureImporterType.Sprite;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            if (AssetImporter.GetAtPath(ctx.AssetPath) is not TextureImporter importer)
                return;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            if (settings.spritePivot == pivot)
                return;

            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            AssetDatabase.ImportAsset(ctx.AssetPath, ImportAssetOptions.ForceUpdate);

            ctx.Logger.Log($"[AssetRouter] SetPivot ({pivot}) → {ctx.AssetPath}");
        }
    }
}
