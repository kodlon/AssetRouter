using System.Collections.Generic;
using Kodlon.AssetRouter.Data;
using Kodlon.AssetRouter.Logic;
using NUnit.Framework;
using UnityEngine;

namespace Kodlon.AssetRouter.Tests
{
    public class RuleMigratorTests
    {
        private ImporterSettingsDatabase _db;

        [SetUp]
        public void SetUp()
        {
            _db = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            _db.schemaVersion = 0;
            _db.rules = new List<BaseImportRule>();
        }

        [Test]
        public void MigrateIfNeeded_AlreadyCurrent_DoesNothing()
        {
            _db.schemaVersion = ImporterSettingsDatabase.LatestSchemaVersion;

            var rule = new ImportRule
            {
                _legacyPrefix = "T_"
            };

            _db.rules.Add(rule);

            RuleMigrator.MigrateIfNeeded(_db);

            Assert.IsEmpty(rule.pattern);
        }

        [Test]
        public void MigrateIfNeeded_BumpsSchemaVersion()
        {
            _db.rules.Add(new ImportRule
            {
                _legacyPrefix = "T_"
            });

            RuleMigrator.MigrateIfNeeded(_db);

            Assert.AreEqual(ImporterSettingsDatabase.LatestSchemaVersion, _db.schemaVersion);
        }

        [Test]
        public void MigrateIfNeeded_EmptyLegacyFields_RulePatternStaysEmpty()
        {
            var rule = new ImportRule();
            _db.rules.Add(rule);

            RuleMigrator.MigrateIfNeeded(_db);

            Assert.IsEmpty(rule.pattern);
        }

        [Test]
        public void MigrateIfNeeded_PrefixAndExtension_CombinedCorrectly()
        {
            var rule = new ImportRule
            {
                _legacyPrefix = "T_",
                _legacyExtensionFilter = ".png"
            };

            _db.rules.Add(rule);

            RuleMigrator.MigrateIfNeeded(_db);

            Assert.AreEqual("T_*.png", rule.pattern);
        }

        [Test]
        public void MigrateIfNeeded_PrefixOnly_BecomesGlobWithStar()
        {
            var rule = new ImportRule
            {
                _legacyPrefix = "T_"
            };

            _db.rules.Add(rule);

            RuleMigrator.MigrateIfNeeded(_db);

            Assert.AreEqual("T_*", rule.pattern);
            Assert.AreEqual(PatternMode.Glob, rule.patternMode);
        }

        [Test]
        public void MigrateIfNeeded_PrefixSuffixExtension_AllCombined()
        {
            var rule = new ImportRule
            {
                _legacyPrefix = "T_",
                _legacySuffix = "_D",
                _legacyExtensionFilter = ".png"
            };

            _db.rules.Add(rule);

            RuleMigrator.MigrateIfNeeded(_db);

            Assert.AreEqual("T_*_D.png", rule.pattern);
        }

        [Test]
        public void MigrateIfNeeded_RuleAlreadyHasPattern_IsSkipped()
        {
            var rule = new ImportRule
            {
                _legacyPrefix = "T_",
                pattern = "UI_*"
            };

            _db.rules.Add(rule);

            RuleMigrator.MigrateIfNeeded(_db);

            Assert.AreEqual("UI_*", rule.pattern);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_db);
        }
    }
}