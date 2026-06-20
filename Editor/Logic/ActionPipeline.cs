using System;
using System.Collections.Generic;
using Kodlon.AssetRouter.Actions;
using Kodlon.AssetRouter.Data;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kodlon.AssetRouter.Logic
{
    /// <summary>
    /// Executes the <see cref="AssetImportActionAsset"/> chain attached to an <see cref="ImportRule"/>.
    /// Errors in individual actions are caught and logged — one bad action does not block the rest.
    /// </summary>
    internal static class ActionPipeline
    {
        /// <summary>
        /// Loads the asset at <paramref name="assetPath"/> and runs all actions on the matching rule.
        /// No-op if the rule has no actions or is not an <see cref="ImportRule"/>.
        /// </summary>
        public static void Execute(BaseImportRule rule, string assetPath, ImporterSettingsDatabase db)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            Execute(rule, asset, assetPath, db);
        }

        /// <summary>
        /// Overload for unit tests — accepts a pre-loaded (or <c>null</c>) asset so
        /// <see cref="AssetDatabase"/> is not required in test contexts.
        /// </summary>
        public static void Execute(BaseImportRule rule, Object asset, string assetPath, ImporterSettingsDatabase db)
        {
            if (rule is not ImportRule importRule)
                return;

            var actions = importRule.postImportActions;

            if (actions == null || actions.Count == 0)
                return;

            var ctx = new AssetImportContext(assetPath, rule, db);

            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];

                if (action == null)
                    continue;

                try
                {
                    if (action.CanRunOn(asset, ctx))
                        action.Execute(asset, ctx);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
