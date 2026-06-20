using System.Collections.Generic;
using NUnit.Framework;
using Kodlon.AssetRouter.Logic;

namespace Kodlon.AssetRouter.Tests
{
    public class OperationLogTests
    {
        [Test]
        public void RecordBatch_NullEntries_DoesNotAddSession()
        {
            var before = OperationLog.ReadAll().Count;
            OperationLog.RecordBatch(null, "Test");
            Assert.AreEqual(before, OperationLog.ReadAll().Count);
        }

        [Test]
        public void RecordBatch_EmptyList_DoesNotAddSession()
        {
            var before = OperationLog.ReadAll().Count;
            OperationLog.RecordBatch(new List<OperationLogEntry>(), "Test");
            Assert.AreEqual(before, OperationLog.ReadAll().Count);
        }

        [Test]
        public void RecordBatch_ThenReadAll_ContainsSession()
        {
            const string source = "OperationLogTests_RecordBatch";
            var entries = new List<OperationLogEntry>
            {
                new OperationLogEntry("Assets/a.png", "Assets/Target/a.png", "Textures")
            };

            OperationLog.RecordBatch(entries, source);

            var sessions = OperationLog.ReadAll();
            var last = sessions.FindLast(s => s.source == source);

            Assert.IsNotNull(last);
            Assert.AreEqual(1, last.entries.Count);
            Assert.AreEqual("Assets/a.png",        last.entries[0].from);
            Assert.AreEqual("Assets/Target/a.png", last.entries[0].to);
            Assert.AreEqual("Textures",            last.entries[0].ruleName);
        }

        [Test]
        public void ReadAll_ReturnsNonNullList()
        {
            var result = OperationLog.ReadAll();
            Assert.IsNotNull(result);
        }

        [Test]
        public void OperationLogEntry_ConstructorSetsFields()
        {
            var entry = new OperationLogEntry("Assets/from.png", "Assets/to.png", "MyRule");

            Assert.AreEqual("Assets/from.png", entry.from);
            Assert.AreEqual("Assets/to.png",   entry.to);
            Assert.AreEqual("MyRule",          entry.ruleName);
        }
    }
}
