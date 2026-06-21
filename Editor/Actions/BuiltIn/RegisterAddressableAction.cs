using UnityEditor;
using UnityEngine;

#if UNITY_ADDRESSABLES
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace Kodlon.AssetRouter.Actions
{
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

            if (group == null)
            {
                ctx.Logger.LogWarning("AssetRouter",
                    $"[AssetRouter] RegisterAddressable: no group named '{groupName}' found and DefaultGroup is null. " +
                    "Configure a Default Group in the Addressables window.");
                return;
            }

            settings.CreateOrMoveEntry(guid, group);

            EditorUtility.SetDirty(settings);

            ctx.Logger.Log($"[AssetRouter] RegisterAddressable → {ctx.AssetPath} in group '{group.Name}'");
#else
            ctx.Logger.LogWarning("AssetRouter", "[AssetRouter] RegisterAddressableAction: com.unity.addressables package is not installed.");
#endif
        }
    }
}
