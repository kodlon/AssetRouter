using UnityEditor;
using UnityEngine;

#if UNITY_ADDRESSABLES
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Registers the imported asset in an Addressables group.
    /// Only compiled and active when <c>com.unity.addressables &gt;= 1.19.0</c> is installed.
    /// </summary>
    [CreateAssetMenu(menuName = "Asset Router/Actions/Register Addressable", fileName = "RegisterAddressableAction")]
    public sealed class RegisterAddressableAction : AssetImportActionAsset
    {
        [Tooltip("Name of the Addressables group to add the asset to. Uses the Default group if empty or not found.")]
        public string groupName = "";

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
        {
#if UNITY_ADDRESSABLES
            return AddressableAssetSettingsDefaultObject.Settings != null;
#else
            return false;
#endif
        }

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
#if UNITY_ADDRESSABLES
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;

            var guid = AssetDatabase.AssetPathToGUID(ctx.AssetPath);
            if (string.IsNullOrEmpty(guid)) return;

            var group = (!string.IsNullOrEmpty(groupName) ? settings.FindGroup(groupName) : null)
                        ?? settings.DefaultGroup;

            settings.CreateOrMoveEntry(guid, group);
            AssetDatabase.SaveAssets();

            ctx.Logger.Log($"[AssetRouter] RegisterAddressable → {ctx.AssetPath} in group '{group.Name}'");
#else
            ctx.Logger.LogWarning("AssetRouter", "[AssetRouter] RegisterAddressableAction: com.unity.addressables package is not installed.");
#endif
        }
    }
}
