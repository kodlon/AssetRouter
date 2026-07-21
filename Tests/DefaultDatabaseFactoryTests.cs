using System.Collections.Generic;
using System.Linq;
using Kodlon.AssetRouter.Actions;
using Kodlon.AssetRouter.Data;
using NUnit.Framework;
using UnityEngine;

namespace Kodlon.AssetRouter.Tests
{
    public class DefaultDatabaseFactoryTests
    {
        [Test]
        public void CreateDefaultRules_ContainsCharacterTexturesRule()
        {
            var rules = DefaultDatabaseFactory.CreateDefaultRules();

            try
            {
                var rule = rules.OfType<ImportRule>().FirstOrDefault(r => r.ruleName == "Character Textures");
                Assert.IsNotNull(rule, "Character Textures rule must exist");
                Assert.AreEqual(PatternMode.Glob, rule.patternMode);
                Assert.AreEqual("T_Char_*_*", rule.pattern);
                Assert.AreEqual("Assets/Art/Characters/{1}/", rule.targetFolder);
            }
            finally
            {
                DestroyActionInstances(rules);
            }
        }

        [Test]
        public void CreateDefaultRules_ContainsLocationTexturesRule()
        {
            var rules = DefaultDatabaseFactory.CreateDefaultRules();

            try
            {
                var rule = rules.OfType<ImportRule>().FirstOrDefault(r => r.ruleName == "Location Textures");
                Assert.IsNotNull(rule, "Location Textures rule must exist");
                Assert.AreEqual(PatternMode.Regex, rule.patternMode);
                Assert.AreEqual(@"^T_Loc_(?<loc>\w+)_.*", rule.pattern);
                Assert.AreEqual("Assets/Art/Locations/{loc}/", rule.targetFolder);
            }
            finally
            {
                DestroyActionInstances(rules);
            }
        }

        [Test]
        public void CreateDefaultRules_GeneralTexturesRule_HasCreateMaterialFromTextureAction()
        {
            var rules = DefaultDatabaseFactory.CreateDefaultRules();

            try
            {
                var rule = rules.OfType<ImportRule>().FirstOrDefault(r => r.ruleName == "General Textures");
                Assert.IsNotNull(rule, "General Textures rule must exist");
                Assert.IsNotNull(rule.postImportActions, "postImportActions must not be null");
                Assert.AreEqual(1, rule.postImportActions.Count, "General Textures must have exactly one action");
                Assert.IsNotNull(rule.postImportActions[0], "action must not be null");

                Assert.IsInstanceOf<CreateMaterialFromTextureAction>(rule.postImportActions[0],
                    "action must be CreateMaterialFromTextureAction");
            }
            finally
            {
                DestroyActionInstances(rules);
            }
        }

        [Test]
        public void CreateDefaultRules_SpecificRulesBeforeGenericTRule()
        {
            var rules = DefaultDatabaseFactory.CreateDefaultRules();
            var charIdx = rules.FindIndex(r => r.ruleName == "Character Textures");
            var locIdx = rules.FindIndex(r => r.ruleName == "Location Textures");
            var generalIdx = rules.FindIndex(r => r.ruleName == "General Textures");

            try
            {
                Assert.Less(charIdx, generalIdx, "Character Textures must come before General Textures");
                Assert.Less(locIdx, generalIdx, "Location Textures must come before General Textures");
            }
            finally
            {
                DestroyActionInstances(rules);
            }
        }

        [Test]
        public void CreateMonitoredExtensions_DoesNotContainLegacyFormats()
        {
            var extensions = DefaultDatabaseFactory.CreateMonitoredExtensions();

            CollectionAssert.DoesNotContain(extensions, ".3ds", "Legacy .3ds must not be monitored by default.");
            CollectionAssert.DoesNotContain(extensions, ".dae", "Legacy .dae (Collada) must not be monitored by default.");
            CollectionAssert.Contains(extensions, ".fbx", ".fbx is required for modern Unity model pipelines.");
            CollectionAssert.Contains(extensions, ".obj", ".obj is required for static-mesh workflows.");
        }

        private static void DestroyActionInstances(List<BaseImportRule> rules)
        {
            foreach (var rule in rules.OfType<ImportRule>())
            {
                foreach (var action in rule.postImportActions)
                    if (action != null)
                        Object.DestroyImmediate(action);
            }
        }
    }
}