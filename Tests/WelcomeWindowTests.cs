using Kodlon.AssetRouter.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Tests
{
    public class WelcomeWindowTests
    {
        [Test]
        public void WelcomeWindow_CanBeInstantiated()
        {
            var win = ScriptableObject.CreateInstance<WelcomeWindow>();
            Assert.IsNotNull(win);
            Object.DestroyImmediate(win);
        }

        [Test]
        public void WelcomeWindow_InheritsEditorWindow()
        {
            Assert.IsTrue(typeof(WelcomeWindow).IsSubclassOf(typeof(EditorWindow)));
        }
    }
}