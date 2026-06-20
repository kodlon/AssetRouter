using NUnit.Framework;
using Kodlon.AssetRouter.Data;
using Kodlon.AssetRouter.Logic;
using UnityEngine;

namespace Kodlon.AssetRouter.Tests
{
    public class DryRunPlannerTests
    {
        [Test]
        public void Scan_NullDatabase_ReturnsEmptyList()
        {
            var result = DryRunPlanner.Scan(null);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Scan_DatabaseWithNoRules_ReturnsNoMatchedEntries()
        {
            var db = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            db.enableAutoImport = true;
            db.monitoredExtensions.Add(".png");

            var result = DryRunPlanner.Scan(db);

            foreach (var entry in result)
                Assert.IsNull(entry.MatchedRule);

            Object.DestroyImmediate(db);
        }

        [Test]
        public void DryRunEntry_NullRule_NotSelectedByDefault()
        {
            var entry = new DryRunEntry("Assets/Foo.png", null, null, false);

            Assert.IsNull(entry.MatchedRule);
            Assert.IsFalse(entry.Selected);
        }

        [Test]
        public void DryRunEntry_WithRule_NotInPlace_SelectedByDefault()
        {
            var rule  = new ImportRule { ruleName = "Test" };
            var entry = new DryRunEntry("Assets/Foo.png", rule, "Assets/Target/Foo.png", false);

            Assert.IsTrue(entry.Selected);
        }

        [Test]
        public void DryRunEntry_AlreadyInPlace_NotSelectedByDefault()
        {
            var rule  = new ImportRule { ruleName = "Test" };
            var entry = new DryRunEntry("Assets/Target/Foo.png", rule, null, true);

            Assert.IsTrue(entry.AlreadyInPlace);
            Assert.IsFalse(entry.Selected);
        }

        [Test]
        public void DryRunEntry_FileName_ExtractedFromPath()
        {
            var entry = new DryRunEntry("Assets/Art/Textures/T_Rock.png", null, null, false);

            Assert.AreEqual("T_Rock.png", entry.FileName);
        }

        [Test]
        public void DryRunEntry_CurrentFolder_NormalisedFromPath()
        {
            var entry = new DryRunEntry("Assets/Art/T_Rock.png", null, null, false);

            Assert.AreEqual("Assets/Art", entry.CurrentFolder);
        }
    }
}
