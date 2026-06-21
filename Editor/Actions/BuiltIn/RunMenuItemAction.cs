using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    [CreateAssetMenu(menuName = "Asset Router/Actions/Run Menu Item", fileName = "RunMenuItemAction")]
    public sealed class RunMenuItemAction : AssetImportActionAsset
    {
        [Tooltip("Full menu path, e.g. \"Assets/Refresh\" or \"Tools/My Custom Tool\".")]
        public string menuItem = "";

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => !string.IsNullOrEmpty(menuItem);

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            if (!EditorApplication.ExecuteMenuItem(menuItem))
                ctx.Logger.LogWarning("AssetRouter", $"[AssetRouter] RunMenuItem: '{menuItem}' not found or returned false.");
            else
                ctx.Logger.Log($"[AssetRouter] RunMenuItem: '{menuItem}' executed.");
        }
    }
}
