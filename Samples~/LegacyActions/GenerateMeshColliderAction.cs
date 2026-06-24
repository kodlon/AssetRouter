using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    [CreateAssetMenu(menuName = "Asset Router/Actions/Generate Mesh Collider", fileName = "GenerateMeshColliderAction")]
    public sealed class GenerateMeshColliderAction : AssetImportActionAsset
    {
        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => AssetImporter.GetAtPath(ctx.AssetPath) is ModelImporter;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            if (AssetImporter.GetAtPath(ctx.AssetPath) is not ModelImporter importer)
                return;

            if (importer.addCollider)
                return;

            importer.addCollider = true;
            AssetDatabase.ImportAsset(ctx.AssetPath, ImportAssetOptions.ForceUpdate);

            ctx.Logger.Log($"[AssetRouter] GenerateMeshCollider → {ctx.AssetPath}");
        }
    }
}
