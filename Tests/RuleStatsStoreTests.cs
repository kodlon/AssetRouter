using System;
using System.Collections.Generic;
using System.IO;
using Kodlon.AssetRouter.Logic;
using NUnit.Framework;

namespace Kodlon.AssetRouter.Tests
{
    public class RuleStatsStoreTests
    {
        private string _tempPath;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), $"assetrouter-teststats-{Guid.NewGuid():N}.json");
            RuleStatsStore.OverrideStatsPathForTests = _tempPath;
            RuleStatsStore.Clear();
        }

        [Test]
        public void Clear_AfterIncrement_ReturnsEmptyDictionary()
        {
            RuleStatsStore.IncrementBatch(new List<string>
            {
                "Rule"
            });

            RuleStatsStore.Clear();

            Assert.AreEqual(0, RuleStatsStore.ReadAll().Count);
        }

        [Test]
        public void IncrementBatch_CalledTwice_CountIsTwo()
        {
            RuleStatsStore.IncrementBatch(new List<string>
            {
                "My Rule"
            });

            RuleStatsStore.IncrementBatch(new List<string>
            {
                "My Rule"
            });

            Assert.AreEqual(2, RuleStatsStore.ReadAll()["My Rule"]);
        }

        [Test]
        public void IncrementBatch_MultipleDuplicatesInOneBatch_AllCounted()
        {
            RuleStatsStore.IncrementBatch(new List<string>
            {
                "A",
                "B",
                "A"
            });

            var all = RuleStatsStore.ReadAll();
            Assert.AreEqual(2, all["A"]);
            Assert.AreEqual(1, all["B"]);
        }

        [Test]
        public void IncrementBatch_NewRule_CreatesEntryWithCountOne()
        {
            RuleStatsStore.IncrementBatch(new List<string>
            {
                "UI Textures"
            });

            var all = RuleStatsStore.ReadAll();

            Assert.IsTrue(all.ContainsKey("UI Textures"));
            Assert.AreEqual(1, all["UI Textures"]);
        }

        [TearDown]
        public void TearDown()
        {
            RuleStatsStore.OverrideStatsPathForTests = null;

            foreach (var path in new[]
            {
                _tempPath,
                _tempPath + ".bak",
                _tempPath + ".tmp"
            })
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}