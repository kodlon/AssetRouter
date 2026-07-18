using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Kodlon.AssetRouter.Logic;

namespace Kodlon.AssetRouter.Tests
{
    public class OperationLogTests
    {
        private string _tempPath;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), $"assetrouter-testlog-{Guid.NewGuid():N}.json");
            OperationLog.OverrideLogPathForTests = _tempPath;
        }

        [TearDown]
        public void TearDown()
        {
            OperationLog.OverrideLogPathForTests = null;

            foreach (var path in new[] { _tempPath, _tempPath + ".bak", _tempPath + ".tmp" })
                if (File.Exists(path))
                    File.Delete(path);
        }

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

        [Test]
        public void UndoEngine_UndoSessionSource_IsStable()
        {
            // HistoryView.Draw filters the "Undo Selected Session" button on this exact string.
            // If the constant is renamed, the guard silently stops matching and Undo cascades
            // into a redo — this test freezes the wire value.
            Assert.AreEqual("Undo", UndoEngine.UndoSessionSource);
        }

        [Test]
        public void RecordBatch_WithUndoSource_RoundTripsThroughReadAll()
        {
            // Verifies that a session written with the Undo source tag survives the JSON round-trip
            // and is retrievable — HistoryView's filter depends on this tag being read back verbatim.
            var reverted = new List<OperationLogEntry>
            {
                new OperationLogEntry("Assets/Textures/x.png", "Assets/x.png", "Textures")
            };

            OperationLog.RecordBatch(reverted, UndoEngine.UndoSessionSource);

            var last = OperationLog.ReadAll().FindLast(s => s.source == UndoEngine.UndoSessionSource);

            Assert.IsNotNull(last, "Undo session was not recorded.");
            Assert.AreEqual(1, last.entries.Count);
            Assert.AreEqual("Assets/Textures/x.png", last.entries[0].from,
                "Undo entry should record the pre-undo location as `from` (post-routing target).");
            Assert.AreEqual("Assets/x.png", last.entries[0].to,
                "Undo entry should record the post-undo location as `to` (original spot).");
            Assert.AreEqual("Textures", last.entries[0].ruleName,
                "Original rule name should be preserved for context in the History view.");
        }
    }
}
