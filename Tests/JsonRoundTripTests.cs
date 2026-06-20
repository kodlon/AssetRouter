using NUnit.Framework;
using Kodlon.AssetRouter.Data;
using Kodlon.AssetRouter.Logic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Kodlon.AssetRouter.Tests
{
    public class JsonRoundTripTests
    {
        private ImporterSettingsDatabase _db;

        [SetUp]
        public void SetUp()
        {
            _db = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            // CreateInstance calls Reset() which populates defaults — clear explicitly.
            _db.rules.Clear();
            _db.monitoredExtensions.Clear();
            _db.ignoredFolders.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_db);
        }

        [Test]
        public void Export_ProducesValidJson()
        {
            _db.rules.Add(new ImportRule { ruleName = "Textures", pattern = "T_*", targetFolder = "Assets/Art" });

            var json = JsonExporter.Export(_db);

            Assert.IsNotNull(json);
            Assert.IsNotEmpty(json);
            Assert.DoesNotThrow(() => JObject.Parse(json));
        }

        [Test]
        public void Export_ContainsExpectedTopLevelFields()
        {
            var json = JsonExporter.Export(_db);
            var root = JObject.Parse(json);

            Assert.IsNotNull(root["enableAutoImport"]);
            Assert.IsNotNull(root["showPopupForUnknownFiles"]);
            Assert.IsNotNull(root["rules"]);
            Assert.IsNotNull(root["monitoredExtensions"]);
            Assert.IsNotNull(root["ignoredFolders"]);
        }

        [Test]
        public void Import_RestoresGeneralSettings()
        {
            _db.enableAutoImport        = true;
            _db.showPopupForUnknownFiles = false;
            var json = JsonExporter.Export(_db);

            var db2 = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            db2.rules.Clear();
            JsonImporter.Import(db2, json);

            Assert.AreEqual(true,  db2.enableAutoImport);
            Assert.AreEqual(false, db2.showPopupForUnknownFiles);

            Object.DestroyImmediate(db2);
        }

        [Test]
        public void Import_RestoresMonitoredExtensions()
        {
            _db.monitoredExtensions.Add(".png");
            _db.monitoredExtensions.Add(".wav");
            var json = JsonExporter.Export(_db);

            var db2 = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            db2.rules.Clear();
            db2.monitoredExtensions.Clear();
            JsonImporter.Import(db2, json);

            Assert.AreEqual(2, db2.monitoredExtensions.Count);
            Assert.IsTrue(db2.monitoredExtensions.Contains(".png"));
            Assert.IsTrue(db2.monitoredExtensions.Contains(".wav"));

            Object.DestroyImmediate(db2);
        }

        [Test]
        public void RoundTrip_PreservesRuleFields()
        {
            var rule = new ImportRule
            {
                ruleName             = "Textures",
                isEnabled            = true,
                pattern              = "T_*",
                patternMode          = PatternMode.Glob,
                matchAgainstFullPath = false,
                targetFolder         = "Assets/Art/Textures"
            };
            _db.rules.Add(rule);

            var json = JsonExporter.Export(_db);

            var db2 = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            db2.rules.Clear();
            JsonImporter.Import(db2, json);

            Assert.AreEqual(1, db2.rules.Count);
            var restored = db2.rules[0] as ImportRule;
            Assert.IsNotNull(restored);
            Assert.AreEqual("Textures",          restored.ruleName);
            Assert.AreEqual("T_*",               restored.pattern);
            Assert.AreEqual(PatternMode.Glob,    restored.patternMode);
            Assert.AreEqual("Assets/Art/Textures", restored.targetFolder);
            Assert.IsTrue(restored.isEnabled);
            Assert.IsFalse(restored.matchAgainstFullPath);

            Object.DestroyImmediate(db2);
        }

        [Test]
        public void RoundTrip_EmptyRuleList_ProducesEmptyRuleList()
        {
            var json = JsonExporter.Export(_db);

            var db2 = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            db2.rules.Clear();
            JsonImporter.Import(db2, json);

            Assert.AreEqual(0, db2.rules.Count);

            Object.DestroyImmediate(db2);
        }

        [Test]
        public void RoundTrip_MultipleRules_PreservesOrder()
        {
            _db.rules.Add(new ImportRule { ruleName = "A", pattern = "A_*" });
            _db.rules.Add(new ImportRule { ruleName = "B", pattern = "B_*" });
            _db.rules.Add(new ImportRule { ruleName = "C", pattern = "C_*" });

            var json = JsonExporter.Export(_db);

            var db2 = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            db2.rules.Clear();
            JsonImporter.Import(db2, json);

            Assert.AreEqual(3, db2.rules.Count);
            Assert.AreEqual("A", (db2.rules[0] as ImportRule)?.ruleName);
            Assert.AreEqual("B", (db2.rules[1] as ImportRule)?.ruleName);
            Assert.AreEqual("C", (db2.rules[2] as ImportRule)?.ruleName);

            Object.DestroyImmediate(db2);
        }

        [Test]
        public void Export_RegexRule_PreservesPatternMode()
        {
            _db.rules.Add(new ImportRule
            {
                ruleName    = "RegexRule",
                pattern     = @"^T_.+\.png$",
                patternMode = PatternMode.Regex
            });

            var json = JsonExporter.Export(_db);
            var db2  = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            db2.rules.Clear();
            JsonImporter.Import(db2, json);

            var restored = db2.rules[0] as ImportRule;
            Assert.AreEqual(PatternMode.Regex, restored?.patternMode);
            Assert.AreEqual(@"^T_.+\.png$",   restored?.pattern);

            Object.DestroyImmediate(db2);
        }
    }
}
