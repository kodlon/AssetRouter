using NUnit.Framework;
using System.Text.RegularExpressions;
using Kodlon.AssetRouter.Logic;

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
        public void Resolve_EmptyCapture_GroupNotParticipated_KeepsTokenLiterally()
        {
            // Optional group that did not participate — group exists but Success = false.
            // .NET quirk: named groups are numbered AFTER unnamed groups, so in
            // ^(?<x>foo)?(Hello)$ the (Hello) group is {1} and (?<x>foo)? is {2}/{x}.
            var m = Regex.Match("Hello", @"^(?<x>foo)?(Hello)$");
            var result = TargetResolver.Resolve("Assets/{x}/{1}/", m);
            Assert.AreEqual("Assets/{x}/Hello/", result);
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
    }
}
