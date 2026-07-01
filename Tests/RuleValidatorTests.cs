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

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_db);
        }

        [Test]
        public void FindMatchingRule_DisabledRule_IsSkipped()
        {
            var disabled = new ImportRule { ruleName = "Off", pattern = "T_*", patternMode = PatternMode.Glob, isEnabled = false };
            var enabled  = new ImportRule { ruleName = "On",  pattern = "T_*", patternMode = PatternMode.Glob, isEnabled = true };

            var result = RuleValidator.FindMatchingRule(new List<BaseImportRule> { disabled, enabled }, "Assets/T_Rock.png");

            Assert.AreEqual(enabled, result?.Rule);
        }

        [Test]
        public void FindMatchingRule_FirstRuleWins()
        {
            var first  = new ImportRule { ruleName = "First",  pattern = "T_*", patternMode = PatternMode.Glob, isEnabled = true };
            var second = new ImportRule { ruleName = "Second", pattern = "T_*", patternMode = PatternMode.Glob, isEnabled = true };

            var result = RuleValidator.FindMatchingRule(new List<BaseImportRule> { first, second }, "Assets/T_Rock.png");

            Assert.AreEqual(first, result?.Rule);
        }

        [Test]
        public void FindMatchingRule_MatchesByGlobPrefix()
        {
            var rule = new ImportRule { ruleName = "Textures", pattern = "T_*", patternMode = PatternMode.Glob, isEnabled = true };

            Assert.AreEqual(rule, RuleValidator.FindMatchingRule(new List<BaseImportRule> { rule }, "Assets/T_Rock.png")?.Rule);
        }

        [Test]
        public void FindMatchingRule_PatternWithExtension_FiltersCorrectly()
        {
            var rule = new ImportRule { ruleName = "Env FBX", pattern = "Env_*.fbx", patternMode = PatternMode.Glob, isEnabled = true };
            var rules = new List<BaseImportRule> { rule };

            Assert.IsNull(RuleValidator.FindMatchingRule(rules, "Assets/Env_Rock.png"));
            Assert.AreEqual(rule, RuleValidator.FindMatchingRule(rules, "Assets/Env_Rock.fbx")?.Rule);
        }

        [Test]
        public void FindMatchingRule_EmptyPattern_IsSkipped()
        {
            var rule = new ImportRule { pattern = "", isEnabled = true };

            Assert.IsNull(RuleValidator.FindMatchingRule(new List<BaseImportRule> { rule }, "Assets/T_Rock.png"));
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

        [Test]
        public void ShouldProcess_MonitoredExtension_ReturnsTrue()
        {
            Assert.IsTrue(RuleValidator.ShouldProcess(_db, "Assets/T_Rock.png"));
        }

        [Test]
        public void ShouldProcess_NullDatabase_ReturnsFalse()
        {
            Assert.IsFalse(RuleValidator.ShouldProcess(null, "Assets/T_Rock.png"));
        }

        [Test]
        public void ShouldProcess_NullPath_ReturnsFalse()
        {
            Assert.IsFalse(RuleValidator.ShouldProcess(_db, null));
        }

        [Test]
        public void ShouldProcess_EmptyPath_ReturnsFalse()
        {
            Assert.IsFalse(RuleValidator.ShouldProcess(_db, ""));
        }

        [Test]
        public void ShouldProcess_NoExtension_ReturnsFalse()
        {
            Assert.IsFalse(RuleValidator.ShouldProcess(_db, "Assets/FileWithoutExtension"));
        }

        [Test]
        public void ShouldProcess_IgnoredFolderPrefixCollision_ReturnsTrue()
        {
            // "Assets/Plugins" is ignored, but "Assets/PluginsCustom" must NOT be.
            Assert.IsTrue(RuleValidator.ShouldProcess(_db, "Assets/PluginsCustom/T_Rock.png"));
        }

        [Test]
        public void FindMatchingRule_NullRuleList_ReturnsNull()
        {
            Assert.IsNull(RuleValidator.FindMatchingRule(null, "Assets/T_Rock.png"));
        }

        [Test]
        public void FindMatchingRule_EmptyRuleList_ReturnsNull()
        {
            Assert.IsNull(RuleValidator.FindMatchingRule(new List<BaseImportRule>(), "Assets/T_Rock.png"));
        }

        [Test]
        public void FindMatchingRule_NullRuleInList_IsSkipped()
        {
            var rule  = new ImportRule { ruleName = "Textures", pattern = "T_*", patternMode = PatternMode.Glob, isEnabled = true };
            var rules = new List<BaseImportRule> { null, rule };

            Assert.AreEqual(rule, RuleValidator.FindMatchingRule(rules, "Assets/T_Rock.png")?.Rule);
        }

        [Test]
        public void FindMatchingRule_ScopeFolder_AssetInScope_Matches()
        {
            var rule = new ImportRule
            {
                ruleName = "Scoped",
                pattern = "T_*",
                patternMode = PatternMode.Glob,
                isEnabled = true,
                scopeFolder = "Assets/Art/"
            };

            Assert.AreEqual(rule, RuleValidator.FindMatchingRule(new List<BaseImportRule> { rule }, "Assets/Art/T_Rock.png")?.Rule);
        }

        [Test]
        public void FindMatchingRule_ScopeFolder_AssetOutsideScope_DoesNotMatch()
        {
            var rule = new ImportRule
            {
                ruleName = "Scoped",
                pattern = "T_*",
                patternMode = PatternMode.Glob,
                isEnabled = true,
                scopeFolder = "Assets/Art/"
            };

            Assert.IsNull(RuleValidator.FindMatchingRule(new List<BaseImportRule> { rule }, "Assets/UI/T_Icon.png"));
        }

        [Test]
        public void FindMatchingRule_ScopeFolder_Empty_MatchesEverywhere()
        {
            var rule = new ImportRule
            {
                ruleName = "Unscoped",
                pattern = "T_*",
                patternMode = PatternMode.Glob,
                isEnabled = true,
                scopeFolder = ""
            };

            Assert.AreEqual(rule, RuleValidator.FindMatchingRule(new List<BaseImportRule> { rule }, "Assets/UI/T_Icon.png")?.Rule);
        }
    }
}
