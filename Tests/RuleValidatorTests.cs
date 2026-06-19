using System.Collections.Generic;
using NUnit.Framework;
using Kodlon.AssetRouter.Data;
using Kodlon.AssetRouter.Logic;
using UnityEngine;

namespace Kodlon.AssetRouter.Tests
{
    public class RuleValidatorTests
    {
        private ImporterSettingsDatabase _db;

        [SetUp]
        public void SetUp()
        {
            _db = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            _db.enableAutoImport = true;

            _db.monitoredExtensions = new List<string>
            {
                ".png",
                ".fbx",
                ".wav"
            };

            _db.ignoredFolders = new List<string>
            {
                "Assets/Plugins/",
                "Packages/"
            };

            _db.rules = new List<BaseImportRule>();
        }

        [Test]
        public void FindMatchingRule_DisabledRule_IsSkipped()
        {
            var disabled = new ImportRule
            {
                ruleName = "Off",
                prefix = "T_",
                isEnabled = false
            };

            var enabled = new ImportRule
            {
                ruleName = "On",
                prefix = "T_",
                isEnabled = true
            };

            var rules = new List<BaseImportRule>
            {
                disabled,
                enabled
            };

            Assert.AreEqual(enabled, RuleValidator.FindMatchingRule(rules, "Assets/T_Rock.png"));
        }

        [Test]
        public void FindMatchingRule_FirstRuleWins()
        {
            var first = new ImportRule
            {
                ruleName = "First",
                prefix = "T_",
                isEnabled = true
            };

            var second = new ImportRule
            {
                ruleName = "Second",
                prefix = "T_",
                isEnabled = true
            };

            var rules = new List<BaseImportRule>
            {
                first,
                second
            };

            Assert.AreEqual(first, RuleValidator.FindMatchingRule(rules, "Assets/T_Rock.png"));
        }

        [Test]
        public void FindMatchingRule_MatchesByPrefix()
        {
            var rule = new ImportRule
            {
                ruleName = "Textures",
                prefix = "T_",
                isEnabled = true
            };

            var rules = new List<BaseImportRule>
            {
                rule
            };

            Assert.AreEqual(rule, RuleValidator.FindMatchingRule(rules, "Assets/T_Rock.png"));
        }

        [Test]
        public void FindMatchingRule_PrefixAndExtensionBothRequired()
        {
            var rule = new ImportRule
            {
                ruleName = "Env FBX",
                prefix = "Env_",
                extensionFilter = ".fbx",
                isEnabled = true
            };

            var rules = new List<BaseImportRule>
            {
                rule
            };

            Assert.IsNull(RuleValidator.FindMatchingRule(rules, "Assets/Env_Rock.png"));
            Assert.AreEqual(rule, RuleValidator.FindMatchingRule(rules, "Assets/Env_Rock.fbx"));
        }

        [Test]
        public void ShouldProcess_IgnoredFolder_ReturnsFalse()
        {
            Assert.IsFalse(RuleValidator.ShouldProcess(_db, "Assets/Plugins/T_Rock.png"));
        }

        [Test]
        public void ShouldProcess_UnknownExtension_ReturnsFalse()
        {
            Assert.IsFalse(RuleValidator.ShouldProcess(_db, "Assets/Script.cs"));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_db);
        }
    }
}