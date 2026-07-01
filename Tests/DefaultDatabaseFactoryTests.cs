using NUnit.Framework;
using System.Linq;
using Kodlon.AssetRouter.Data;

namespace Kodlon.AssetRouter.Tests
{
    public class DefaultDatabaseFactoryTests
    {
        [Test]
        public void CreateDefaultRules_ContainsCharacterTexturesRule()
        {
            var rules = DefaultDatabaseFactory.CreateDefaultRules();
            var rule = rules.OfType<ImportRule>().FirstOrDefault(r => r.ruleName == "Character Textures");
            Assert.IsNotNull(rule, "Character Textures rule must exist");
            Assert.AreEqual(PatternMode.Glob, rule.patternMode);
            Assert.AreEqual("T_Char_*_*", rule.pattern);
            Assert.AreEqual("Assets/Art/Characters/{1}/", rule.targetFolder);
        }

        [Test]
        public void CreateDefaultRules_ContainsLocationTexturesRule()
        {
            var rules = DefaultDatabaseFactory.CreateDefaultRules();
            var rule = rules.OfType<ImportRule>().FirstOrDefault(r => r.ruleName == "Location Textures");
            Assert.IsNotNull(rule, "Location Textures rule must exist");
            Assert.AreEqual(PatternMode.Regex, rule.patternMode);
            Assert.AreEqual(@"^T_Loc_(?<loc>\w+)_.*", rule.pattern);
            Assert.AreEqual("Assets/Art/Locations/{loc}/", rule.targetFolder);
        }

        [Test]
        public void CreateDefaultRules_SpecificRulesBeforeGenericTRule()
        {
            var rules = DefaultDatabaseFactory.CreateDefaultRules();
            var charIdx    = rules.FindIndex(r => r.ruleName == "Character Textures");
            var locIdx     = rules.FindIndex(r => r.ruleName == "Location Textures");
            var generalIdx = rules.FindIndex(r => r.ruleName == "General Textures");

            Assert.Less(charIdx,    generalIdx, "Character Textures must come before General Textures");
            Assert.Less(locIdx,     generalIdx, "Location Textures must come before General Textures");
        }
    }
}
