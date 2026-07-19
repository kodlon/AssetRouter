using System;
using System.Collections.Generic;
using Kodlon.AssetRouter.Actions;
using Kodlon.AssetRouter.Data;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kodlon.AssetRouter.Logic
{
    internal static class ActionPipeline
    {
        public static void Execute(BaseImportRule rule, string assetPath, ImporterSettingsDatabase db, IArtifactSink sink = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            Execute(rule, asset, assetPath, db, sink);
        }

        public static void Execute(BaseImportRule rule, Object asset, string assetPath, ImporterSettingsDatabase db, IArtifactSink sink = null)
        {
            if (rule is not ImportRule importRule)
                return;

            var actions = importRule.postImportActions;

            if (actions == null || actions.Count == 0)
                return;

            var ctx = new AssetImportContext(assetPath, rule, db, logger: null, sink: sink);

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
