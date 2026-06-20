using System.Collections.Generic;
using NUnit.Framework;
using Kodlon.AssetRouter.Data;
using Kodlon.AssetRouter.Logic;

namespace Kodlon.AssetRouter.Tests
{
    public class ConflictDetectorTests
    {
        [Test]
        public void Detect_NoDuplicates_ReturnsEmpty()
        {
            var rules = new List<BaseImportRule>
            {
                MakeRule("T_*"),
                MakeRule("UI_*")
            };

            var conflicts = ConflictDetector.Detect(rules);

            Assert.IsEmpty(conflicts);
        }

        [Test]
        public void Detect_IdenticalPatterns_ReportsDuplicate()
        {
            var rules = new List<BaseImportRule>
            {
                MakeRule("T_*"),
                MakeRule("T_*")
            };

            var conflicts = ConflictDetector.Detect(rules);

            Assert.AreEqual(1, conflicts.Count);
            Assert.AreEqual(ConflictType.Duplicate, conflicts[0].Type);
            Assert.AreEqual(0, conflicts[0].IndexA);
            Assert.AreEqual(1, conflicts[0].IndexB);
        }

        [Test]
        public void Detect_DuplicateCaseInsensitive()
        {
            var rules = new List<BaseImportRule>
            {
                MakeRule("T_*"),
                MakeRule("t_*")
            };

            var conflicts = ConflictDetector.Detect(rules);

            Assert.AreEqual(1, conflicts.Count);
            Assert.AreEqual(ConflictType.Duplicate, conflicts[0].Type);
        }

        [Test]
        public void Detect_OverlappingPatterns_ReportsOverlap()
        {
            // Both match "T_Rock_D.png" from the sample set.
            var rules = new List<BaseImportRule>
            {
                MakeRule("T_*"),
                MakeRule("T_*_D.png")
            };

            var conflicts = ConflictDetector.Detect(rules);

            Assert.AreEqual(1, conflicts.Count);
            Assert.AreEqual(ConflictType.Overlap, conflicts[0].Type);
        }

        [Test]
        public void Detect_DisabledRule_IsIgnored()
        {
            var rules = new List<BaseImportRule>
            {
                MakeRule("T_*"),
                MakeRule("T_*", enabled: false)
            };

            Assert.IsEmpty(ConflictDetector.Detect(rules));
        }

        [Test]
        public void Detect_EmptyPattern_IsIgnored()
        {
            var rules = new List<BaseImportRule>
            {
                MakeRule("T_*"),
                MakeRule("")
            };

            Assert.IsEmpty(ConflictDetector.Detect(rules));
        }

        [Test]
        public void Detect_SingleRule_ReturnsEmpty()
        {
            Assert.IsEmpty(ConflictDetector.Detect(new List<BaseImportRule> { MakeRule("T_*") }));
        }

        [Test]
        public void Detect_NullList_ReturnsEmpty()
        {
            Assert.IsEmpty(ConflictDetector.Detect(null));
        }

        private static ImportRule MakeRule(string pattern, bool enabled = true) =>
            new() { pattern = pattern, patternMode = PatternMode.Glob, isEnabled = enabled };
    }
}
