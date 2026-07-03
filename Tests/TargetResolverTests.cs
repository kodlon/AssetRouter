using NUnit.Framework;
using System.Text.RegularExpressions;
using Kodlon.AssetRouter.Logic;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kodlon.AssetRouter.Tests
{
    public class TargetResolverTests
    {
        private static Match MakeMatch(string pattern, string input)
        {
            var m = Regex.Match(input, pattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return m.Success ? m : null;
        }

        [Test]
        public void Resolve_NoTokens_ReturnsLiteral()
        {
            var m = MakeMatch(@"^T_(.+)$", "T_Rock.png");
            Assert.AreEqual("Assets/Art/Textures/", TargetResolver.Resolve("Assets/Art/Textures/", m));
        }

        [Test]
        public void Resolve_PositionalToken_SubstitutesCapture()
        {
            var m = MakeMatch(@"^T_Char_([^/]*)_([^/]*)$", "T_Char_Hero_Diffuse.png");
            var result = TargetResolver.Resolve("Assets/Art/Characters/{1}/", m);
            Assert.AreEqual("Assets/Art/Characters/Hero/", result);
        }

        [Test]
        public void Resolve_NamedToken_SubstitutesCapture()
        {
            var m = MakeMatch(@"^T_Loc_(?<loc>\w+)_.*", "T_Loc_Forest_Rock.png");
            var result = TargetResolver.Resolve("Assets/Art/Locations/{loc}/", m);
            Assert.AreEqual("Assets/Art/Locations/Forest/", result);
        }

        [Test]
        public void Resolve_MissingToken_KeepsTokenLiterally()
        {
            var m = MakeMatch(@"^T_(.+)$", "T_Rock.png");
            // {2} doesn't exist — only groups 0 and 1
            var result = TargetResolver.Resolve("Assets/Art/{2}/", m);
            Assert.AreEqual("Assets/Art/{2}/", result);
        }

        [Test]
        public void Resolve_EscapedDoubleBraces_RendersLiterally()
        {
            var m = MakeMatch(@"^T_(.+)$", "T_Rock.png");
            var result = TargetResolver.Resolve("Assets/{{literal}}/", m);
            Assert.AreEqual("Assets/{literal}/", result);
        }

        [Test]
        public void Resolve_EscapedClosingBrace_RendersLiterally()
        {
            var m = MakeMatch(@"^T_(.+)$", "T_Rock.png");
            var result = TargetResolver.Resolve("Assets/folder}}/", m);
            Assert.AreEqual("Assets/folder}/", result);
        }

        [Test]
        public void Resolve_GroupExistsButDidNotParticipate_ResolvesToEmpty()
        {
            // Optional group that did not participate — group exists in the pattern but Success = false.
            // This must resolve to "" (cleaned up by the // collapse), not a literal "{token}" — the
            // latter would create a real folder named e.g. "{1}" for patterns like "Assets/**/T_*.png"
            // matched against a file directly in Assets/ (no subfolder, so "**/" never participates).
            // .NET quirk: named groups are numbered AFTER unnamed groups, so in
            // ^(?<x>foo)?(Hello)$ the (Hello) group is {1} and (?<x>foo)? is {2}/{x}.
            var m = Regex.Match("Hello", @"^(?<x>foo)?(Hello)$");
            var result = TargetResolver.Resolve("Assets/{x}/{1}/", m);
            Assert.AreEqual("Assets/Hello/", result);
        }

        [Test]
        public void Resolve_UnknownGroupIndex_KeepsTokenLiterally()
        {
            var m = MakeMatch(@"^T_(.+)$", "T_Rock.png");
            // {5} genuinely doesn't exist in the pattern — only groups 0 and 1.
            var result = TargetResolver.Resolve("Assets/Art/{5}/", m);
            Assert.AreEqual("Assets/Art/{5}/", result);
        }

        [Test]
        public void Resolve_UnknownNamedGroup_KeepsTokenLiterally()
        {
            var m = MakeMatch(@"^T_Loc_(?<loc>\w+)_.*", "T_Loc_Forest_Rock.png");
            var result = TargetResolver.Resolve("Assets/{doesNotExist}/", m);
            Assert.AreEqual("Assets/{doesNotExist}/", result);
        }

        [Test]
        public void Resolve_TokenContainsDotDot_Sanitized()
        {
            // Group 1 captures "../evil" — path traversal must be rejected; token kept literally.
            var m = Regex.Match("../evil", @"^(.+)$");
            var result = TargetResolver.Resolve("Assets/{1}/", m);
            Assert.AreEqual("Assets/{1}/", result);
        }

        [Test]
        public void Resolve_TokenContainsBackslash_NormalisedToForwardSlash()
        {
            // Backslash in captured value is converted to forward slash and allowed through.
            var m = Regex.Match(@"Hero\World", @"^(.+)$");
            var result = TargetResolver.Resolve("Assets/{1}/", m);
            Assert.AreEqual("Assets/Hero/World/", result);
        }

        [Test]
        public void Resolve_MultipleTokensInTemplate_AllSubstituted()
        {
            var m = MakeMatch(@"^T_([^_]+)_([^_]+)_(.+)$", "T_Char_Hero_Diffuse.png");
            var result = TargetResolver.Resolve("Assets/{1}/{2}/{3}/", m);
            Assert.AreEqual("Assets/Char/Hero/Diffuse.png/", result);
        }

        [Test]
        public void Resolve_NullMatch_ReturnsTemplateLiterally()
        {
            var result = TargetResolver.Resolve("Assets/Art/{1}/", null);
            Assert.AreEqual("Assets/Art/{1}/", result);
        }

        [Test]
        public void Resolve_ZeroToken_ReturnsWholeMatch()
        {
            var m = Regex.Match("T_Rock.png", @"^T_(.+)$");
            Assert.AreEqual("Assets/T_Rock.png/", TargetResolver.Resolve("Assets/{0}/", m));
        }

        [Test]
        public void Resolve_EmptyCapture_DoubleSlashCollapsed()
        {
            var m = Regex.Match("T__file", @"^T_(.*)_file$");
            var result = TargetResolver.Resolve("Assets/{1}/Sub/", m);
            Assert.AreEqual("Assets/Sub/", result);
        }

        [Test]
        public void Resolve_CaptureWithTrailingSpace_Trimmed()
        {
            // Windows forbids a trailing space in a folder/file name — AssetDatabase.CreateFolder's
            // behavior on one is undefined.
            var m = Regex.Match("T_Rock _D.png", @"^T_(.*)_D\.png$");
            var result = TargetResolver.Resolve("Assets/{1}/", m);
            Assert.AreEqual("Assets/Rock/", result);
        }

        [Test]
        public void Resolve_CaptureWithTrailingDot_Trimmed()
        {
            // Windows forbids a trailing dot in a folder/file name.
            var m = Regex.Match("T_Rock._D.png", @"^T_(.*)_D\.png$");
            var result = TargetResolver.Resolve("Assets/{1}/", m);
            Assert.AreEqual("Assets/Rock/", result);
        }

        [Test]
        public void Resolve_CaptureWithLeadingDot_Trimmed()
        {
            // Unity ignores folders starting with "." outright — MoveAsset would just fail later with a
            // handled warning, so there's no reason to ever keep the leading dot.
            var m = Regex.Match("T_.hidden_D.png", @"^T_(.*)_D\.png$");
            var result = TargetResolver.Resolve("Assets/{1}/", m);
            Assert.AreEqual("Assets/hidden/", result);
        }

        [Test]
        public void Resolve_CaptureWithInvalidWindowsChars_Stripped()
        {
            // <>:"|?* are forbidden in a Windows file/folder name — a capture built from a filename that's
            // only valid on macOS/Linux must not silently produce a folder a Windows teammate can't check out.
            var m = Regex.Match("Rock:Boss?", @"^(.+)$");
            var result = TargetResolver.Resolve("Assets/{1}/", m);
            Assert.AreEqual("Assets/RockBoss/", result);
        }

        [Test]
        public void Resolve_CaptureBecomesEmptyAfterCleaning_KeepsTokenLiterally()
        {
            LogAssert.Expect(LogType.Warning, new Regex("became empty after removing characters"));

            var m = Regex.Match("???", @"^(.+)$");
            var result = TargetResolver.Resolve("Assets/{1}/", m);
            Assert.AreEqual("Assets/{1}/", result);
        }
    }
}
