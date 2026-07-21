using Kodlon.AssetRouter.Logic;
using NUnit.Framework;

namespace Kodlon.AssetRouter.Tests
{
    /// <summary>
    /// Guards the artifact-tracking side of the Undo pipeline. If dedup regresses or
    /// ordering breaks,
    /// Undo either double-deletes (harmless but noisy) or leaves orphans (the exact
    /// bug this system
    /// exists to fix).
    /// </summary>
    public class ArtifactCollectorTests
    {
        [Test]
        public void HasAny_FlipsOnFirstEntry()
        {
            var c = new ArtifactCollector();
            Assert.IsFalse(c.HasAny);

            c.OnFoldersCreated(new[]
            {
                "Assets/X"
            });

            Assert.IsTrue(c.HasAny);
        }

        [Test]
        public void OnAssetCreated_CaseVariation_IsDedupedCaseInsensitively()
        {
            // Windows and macOS asset paths are case-insensitive; treating "Assets/Foo.mat" and
            // "assets/foo.mat" as different entries would double-record and break the session log
            // once Unity normalises the path on write.
            var c = new ArtifactCollector();

            c.OnAssetCreated("Assets/Foo.mat");
            c.OnAssetCreated("assets/foo.MAT");

            Assert.AreEqual(1, c.Assets.Count);
        }

        [Test]
        public void OnAssetCreated_DuplicatePath_IsDeduped()
        {
            var c = new ArtifactCollector();

            c.OnAssetCreated("Assets/Foo.mat");
            c.OnAssetCreated("Assets/Foo.mat");

            Assert.AreEqual(1, c.Assets.Count);
            Assert.AreEqual("Assets/Foo.mat", c.Assets[0]);
        }

        [Test]
        public void OnAssetCreated_NullOrEmpty_Ignored()
        {
            var c = new ArtifactCollector();

            c.OnAssetCreated(null);
            c.OnAssetCreated(string.Empty);

            Assert.AreEqual(0, c.Assets.Count);
            Assert.IsFalse(c.HasAny);
        }

        [Test]
        public void OnAssetCreated_PreservesInsertionOrder()
        {
            var c = new ArtifactCollector();

            c.OnAssetCreated("Assets/A.mat");
            c.OnAssetCreated("Assets/B.mat");
            c.OnAssetCreated("Assets/C.mat");

            CollectionAssert.AreEqual(new[]
            {
                "Assets/A.mat",
                "Assets/B.mat",
                "Assets/C.mat"
            }, c.Assets);
        }

        [Test]
        public void OnFoldersCreated_MultipleCalls_AreDeduped()
        {
            // Same routing folder is reported by every rule that shares it — collector must not
            // record it once per rule or Undo's cleanup pass wastes work checking already-gone paths.
            var c = new ArtifactCollector();

            c.OnFoldersCreated(new[]
            {
                "Assets/Art",
                "Assets/Art/Textures"
            });

            c.OnFoldersCreated(new[]
            {
                "Assets/Art",
                "Assets/Art/Audio"
            });

            Assert.AreEqual(3, c.Folders.Count);
            CollectionAssert.Contains(c.Folders, "Assets/Art");
            CollectionAssert.Contains(c.Folders, "Assets/Art/Textures");
            CollectionAssert.Contains(c.Folders, "Assets/Art/Audio");
        }

        [Test]
        public void OnFoldersCreated_NullList_Ignored()
        {
            var c = new ArtifactCollector();

            c.OnFoldersCreated(null);

            Assert.AreEqual(0, c.Folders.Count);
            Assert.IsFalse(c.HasAny);
        }
    }
}