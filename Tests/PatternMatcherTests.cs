using NUnit.Framework;
using Kodlon.AssetRouter.Data;
using Kodlon.AssetRouter.Logic;

namespace Kodlon.AssetRouter.Tests
{
    public class PatternMatcherTests
    {
        [Test]
        public void Glob_Star_MatchesAnyFilename()
        {
            var rule = MakeRule(PatternMode.Glob, "T_*");
            Assert.IsTrue(PatternMatcher.Matches(rule, "Assets/T_Rock.png"));
            Assert.IsTrue(PatternMatcher.Matches(rule, "Assets/T_Wall_D.fbx"));
            Assert.IsFalse(PatternMatcher.Matches(rule, "Assets/UI_Button.png"));
        }

        [Test]
        public void Glob_StarWithExtension_FiltersExtension()
        {
            var rule = MakeRule(PatternMode.Glob, "T_*.png");
            Assert.IsTrue(PatternMatcher.Matches(rule, "Assets/T_Rock.png"));
            Assert.IsFalse(PatternMatcher.Matches(rule, "Assets/T_Rock.fbx"));
        }

        [Test]
        public void Glob_Question_MatchesSingleCharacter()
        {
            var rule = MakeRule(PatternMode.Glob, "T_?.png");
            Assert.IsTrue(PatternMatcher.Matches(rule, "Assets/T_A.png"));
            Assert.IsFalse(PatternMatcher.Matches(rule, "Assets/T_AB.png"));
        }

        [Test]
        public void Glob_DoubleStar_MatchesAcrossSlashes()
        {
            var rule = MakeRule(PatternMode.Glob, "Assets/**/T_*.png", matchFullPath: true);
            Assert.IsTrue(PatternMatcher.Matches(rule, "Assets/Textures/T_Rock.png"));
            Assert.IsTrue(PatternMatcher.Matches(rule, "Assets/Art/Textures/Sub/T_Wall.png"));
            Assert.IsFalse(PatternMatcher.Matches(rule, "Assets/Textures/UI_Button.png"));
        }

        [Test]
        public void Glob_DoubleStar_Alone_MatchesDirectChildToo()
        {
            var rule = MakeRule(PatternMode.Glob, "Assets/**", matchFullPath: true);
            Assert.IsTrue(PatternMatcher.Matches(rule, "Assets/x.png"));
            Assert.IsTrue(PatternMatcher.Matches(rule, "Assets/Sub/deep.png"));
        }

        [Test]
        public void Glob_PrefixAndSuffix_BothRequired()
        {
            var rule = MakeRule(PatternMode.Glob, "T_*_D.png");
            Assert.IsTrue(PatternMatcher.Matches(rule, "Assets/T_Rock_D.png"));
            Assert.IsFalse(PatternMatcher.Matches(rule, "Assets/T_Rock_N.png"));
        }

        [Test]
        public void Glob_SpecialRegexChars_AreEscaped()
        {
            var rule = MakeRule(PatternMode.Glob, "file.png");
            Assert.IsTrue(PatternMatcher.Matches(rule, "Assets/file.png"));
            Assert.IsFalse(PatternMatcher.Matches(rule, "Assets/fileXpng"));
        }

        [Test]
        public void Glob_CaseInsensitive()
        {
            var rule = MakeRule(PatternMode.Glob, "t_*");
            Assert.IsTrue(PatternMatcher.Matches(rule, "Assets/T_Rock.png"));
        }

        [Test]
        public void Regex_ValidPattern_Matches()
        {
            var rule = MakeRule(PatternMode.Regex, @"^T_.+\.png$");
            Assert.IsTrue(PatternMatcher.Matches(rule, "Assets/T_Rock.png"));
            Assert.IsFalse(PatternMatcher.Matches(rule, "Assets/T_Rock.fbx"));
        }

        [Test]
        public void Regex_InvalidPattern_ReturnsFalse()
        {
            var rule = MakeRule(PatternMode.Regex, "[invalid(");
            Assert.IsFalse(PatternMatcher.Matches(rule, "Assets/T_Rock.png"));
        }

        [Test]
        public void TryGetRegexError_ValidPattern_ReturnsFalse()
        {
            Assert.IsFalse(PatternMatcher.TryGetRegexError(@"^T_.*\.png$", out _));
        }

        [Test]
        public void TryGetRegexError_InvalidPattern_ReturnsTrue()
        {
            Assert.IsTrue(PatternMatcher.TryGetRegexError("[broken(", out var error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void MatchFullPath_False_UsesFilenameOnly()
        {
            var rule = MakeRule(PatternMode.Glob, "T_*", matchFullPath: false);
            Assert.IsTrue(PatternMatcher.Matches(rule, "Assets/Sub/T_Rock.png"));
        }

        [Test]
        public void MatchFullPath_True_UsesFullPath()
        {
            var rule = MakeRule(PatternMode.Glob, "Assets/Sub/*", matchFullPath: true);
            Assert.IsTrue(PatternMatcher.Matches(rule, "Assets/Sub/T_Rock.png"));
            Assert.IsFalse(PatternMatcher.Matches(rule, "Assets/Other/T_Rock.png"));
        }

        [Test]
        public void GlobToRegex_Star_ProducesNonSlashWildcard()
        {
            var regex = PatternMatcher.GlobToRegex("T_*");
            Assert.AreEqual("^T_([^/]*)$", regex);
        }

        [Test]
        public void GlobToRegex_DoubleStar_ProducesAnyWildcard()
        {
            var regex = PatternMatcher.GlobToRegex("T_**");
            Assert.AreEqual("^T_(.*)$", regex);
        }

        [Test]
        public void Match_GlobWithStar_CapturesPositionalValue()
        {
            var rule = MakeRule(PatternMode.Glob, "T_Char_*_*");
            var m = PatternMatcher.Match(rule, "Assets/T_Char_Hero_Diffuse.png");
            Assert.IsNotNull(m);
            Assert.AreEqual("Hero", m.Groups[1].Value);
            Assert.AreEqual("Diffuse.png", m.Groups[2].Value);
        }

        [Test]
        public void Match_GlobWithDoubleStar_CapturesPath()
        {
            var rule = MakeRule(PatternMode.Glob, "Assets/**/T_*.png", matchFullPath: true);
            var m = PatternMatcher.Match(rule, "Assets/Art/Sub/T_Rock.png");
            Assert.IsNotNull(m);
            Assert.AreEqual("Art/Sub", m.Groups[1].Value);
            Assert.AreEqual("Rock", m.Groups[2].Value);
        }

        [Test]
        public void Match_RegexNamedGroup_CapturesByName()
        {
            var rule = MakeRule(PatternMode.Regex, @"^T_Loc_(?<loc>\w+)_.*");
            var m = PatternMatcher.Match(rule, "Assets/T_Loc_Forest_Rock.png");
            Assert.IsNotNull(m);
            Assert.AreEqual("Forest", m.Groups["loc"].Value);
        }

        [Test]
        public void Match_NoMatch_ReturnsNull()
        {
            var rule = MakeRule(PatternMode.Glob, "T_*");
            var m = PatternMatcher.Match(rule, "Assets/UI_Button.png");
            Assert.IsNull(m);
        }

        [Test]
        public void Matches_LegacyWrapper_StillReturnsBool()
        {
            var rule = MakeRule(PatternMode.Glob, "T_*");
            Assert.IsTrue(PatternMatcher.Matches(rule, "Assets/T_Rock.png"));
            Assert.IsFalse(PatternMatcher.Matches(rule, "Assets/UI_Rock.png"));
        }

        private static ImportRule MakeRule(PatternMode mode, string pattern, bool matchFullPath = false) =>
            new() { patternMode = mode, pattern = pattern, matchAgainstFullPath = matchFullPath, isEnabled = true };
    }
}
