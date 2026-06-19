using System;
using UnityEngine;

namespace Kodlon.AssetRouter.Data
{
    [Serializable]
    public abstract class BaseImportRule
    {
        public string ruleName = "New Rule";
        public bool isEnabled = true;

        [Space]
        public string prefix = "";

        public string suffix = "";
        public string extensionFilter = "";

        [Space]
        public string targetFolder = "Assets/";
    }
}