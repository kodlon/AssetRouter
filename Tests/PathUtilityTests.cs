using System.IO;
using NUnit.Framework;
using Kodlon.AssetRouter.Logic;
using UnityEngine;

namespace Kodlon.AssetRouter.Tests
{
    public class PathUtilityTests
    {
        // ── NormalizeAssetPath ────────────────────────────────────────────────────

        [Test]
        public void NormalizeAssetPath_Null_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, PathUtility.NormalizeAssetPath(null));
        }

        [Test]
        public void NormalizeAssetPath_Empty_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, PathUtility.NormalizeAssetPath(""));
        }

        [Test]
        public void NormalizeAssetPath_BackslashSeparators_AreConvertedToForwardSlash()
        {
            Assert.AreEqual("Assets/Sub/file.png", PathUtility.NormalizeAssetPath(@"Assets\Sub\file.png"));
        }

        [Test]
        public void NormalizeAssetPath_TrailingSlash_IsRemoved()
        {
            Assert.AreEqual("Assets/Sub", PathUtility.NormalizeAssetPath("Assets/Sub/"));
        }

        [Test]
        public void NormalizeAssetPath_AlreadyNormal_IsUnchanged()
        {
            Assert.AreEqual("Assets/Sub/file.png", PathUtility.NormalizeAssetPath("Assets/Sub/file.png"));
        }

        // ── IsUnderFolder ─────────────────────────────────────────────────────────

        [Test]
        public void IsUnderFolder_DirectChild_ReturnsTrue()
        {
            Assert.IsTrue(PathUtility.IsUnderFolder("Assets/Plugins/x.png", "Assets/Plugins"));
        }

        [Test]
        public void IsUnderFolder_DeepChild_ReturnsTrue()
        {
            Assert.IsTrue(PathUtility.IsUnderFolder("Assets/Plugins/Deep/Nested/x.png", "Assets/Plugins"));
        }

        [Test]
        public void IsUnderFolder_FolderWithTrailingSlash_NormalisedCorrectly()
        {
            Assert.IsTrue(PathUtility.IsUnderFolder("Assets/Plugins/x.png", "Assets/Plugins/"));
        }

        [Test]
        public void IsUnderFolder_UnrelatedFolder_ReturnsFalse()
        {
            Assert.IsFalse(PathUtility.IsUnderFolder("Assets/Art/x.png", "Assets/Plugins"));
        }

        [Test]
        public void IsUnderFolder_PrefixCollision_ReturnsFalse()
        {
            // "Assets/FooBar/..." must NOT match the folder "Assets/Foo".
            // Without the trailing-slash guard in IsUnderFolder, StartsWith("Assets/Foo/")
            // would incorrectly return false but StartsWith("Assets/Foo") alone would match.
            Assert.IsFalse(PathUtility.IsUnderFolder("Assets/FooBar/x.png", "Assets/Foo"));
        }

        [Test]
        public void IsUnderFolder_PluginsCustom_NotUnderPlugins()
        {
            // Concrete regression: "Assets/Plugins" is an ignored folder; assets in
            // "Assets/PluginsCustom" must not be considered ignored.
            Assert.IsFalse(PathUtility.IsUnderFolder("Assets/PluginsCustom/x.png", "Assets/Plugins"));
        }

        [Test]
        public void IsUnderFolder_CaseInsensitive()
        {
            Assert.IsTrue(PathUtility.IsUnderFolder("Assets/PLUGINS/x.png", "Assets/Plugins"));
        }

        // ── ToAbsolute ────────────────────────────────────────────────────────────

        [Test]
        public void ToAbsolute_PathContainingWordAssetsTwice_IsNotCorrupted()
        {
            // Regression: the old implementation used Application.dataPath.Replace("Assets", "")
            // which stripped ALL occurrences of "Assets" from the path, including the one in the
            // asset path argument. Now uses Path.GetDirectoryName(Application.dataPath) instead.
            var result = PathUtility.ToAbsolute("Assets/Assets/file.png");

            var expected = Path.Combine(
                Path.GetDirectoryName(Application.dataPath)!,
                "Assets", "Assets", "file.png");

            Assert.AreEqual(expected, result);
        }
    }
}
