using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Executes a Unity Editor menu item by its full path when an asset is imported.
    /// Provided as a legacy sample; prefer dedicated actions for production use.
    /// </summary>
    [CreateAssetMenu(menuName = "Asset Router/Actions/Run Menu Item", fileName = "RunMenuItemAction")]
    public sealed class RunMenuItemAction : AssetImportActionAsset
    {
        /// <summary>Full menu path to execute, e.g. <c>Assets/Refresh</c> or <c>Tools/My Custom Tool</c>.</summary>
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
