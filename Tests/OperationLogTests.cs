using System;
using System.Collections.Generic;
using System.IO;
using Kodlon.AssetRouter.Logic;
using NUnit.Framework;

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

        [Test]
        public void OperationLogEntry_ConstructorSetsFields()
        {
            var entry = new OperationLogEntry("Assets/from.png", "Assets/to.png", "MyRule");

            Assert.AreEqual("Assets/from.png", entry.from);
            Assert.AreEqual("Assets/to.png", entry.to);
            Assert.AreEqual("MyRule", entry.ruleName);
        }

        [Test]
        public void ReadAll_NormalisesNullSideEffectListsOnLegacySessions()
        {
            // Simulate a v=1 log file (predates the createdAssets/createdFolders fields) by writing
            // JSON that lacks them entirely. JsonUtility leaves the missing List<string> fields as
            // null; ReadAll must replace them with empty lists so callers (UndoEngine, HistoryView)
            // never see a null and never NRE.
            var legacyJson = "{\"v\":1,\"sessions\":[" +
                             "{\"timestamp\":\"2026-01-01T00:00:00Z\",\"source\":\"AutoImport\"," +
                             " \"entries\":[{\"from\":\"Assets/a.png\",\"to\":\"Assets/T/a.png\",\"ruleName\":\"R\"}]" +
                             "}]}";

            File.WriteAllText(_tempPath, legacyJson);

            var sessions = OperationLog.ReadAll();

            Assert.AreEqual(1, sessions.Count);
            Assert.IsNotNull(sessions[0].createdAssets, "createdAssets should be normalised to an empty list, not left null.");
            Assert.IsNotNull(sessions[0].createdFolders, "createdFolders should be normalised to an empty list, not left null.");
            Assert.AreEqual(0, sessions[0].createdAssets.Count);
            Assert.AreEqual(0, sessions[0].createdFolders.Count);
        }

        [Test]
        public void ReadAll_ReturnsNonNullList()
        {
            var result = OperationLog.ReadAll();
            Assert.IsNotNull(result);
        }

        [Test]
        public void RecordBatch_EmptyList_DoesNotAddSession()
        {
            var before = OperationLog.ReadAll().Count;
            OperationLog.RecordBatch(new List<OperationLogEntry>(), "Test");
            Assert.AreEqual(before, OperationLog.ReadAll().Count);
        }

        [Test]
        public void RecordBatch_NullEntries_DoesNotAddSession()
        {
            var before = OperationLog.ReadAll().Count;
            OperationLog.RecordBatch(null, "Test");
            Assert.AreEqual(before, OperationLog.ReadAll().Count);
        }

        [Test]
        public void RecordBatch_ThenReadAll_ContainsSession()
        {
            const string source = "OperationLogTests_RecordBatch";

            var entries = new List<OperationLogEntry>
            {
                new("Assets/a.png", "Assets/Target/a.png", "Textures")
            };

            OperationLog.RecordBatch(entries, source);

            var sessions = OperationLog.ReadAll();
            var last = sessions.FindLast(s => s.source == source);

            Assert.IsNotNull(last);
            Assert.AreEqual(1, last.entries.Count);
            Assert.AreEqual("Assets/a.png", last.entries[0].from);
            Assert.AreEqual("Assets/Target/a.png", last.entries[0].to);
            Assert.AreEqual("Textures", last.entries[0].ruleName);
        }

        [Test]
        public void RecordBatch_WithNullSideEffects_WritesEmptyLists()
        {
            // Overload accepts nulls for the two side-effect parameters. Log must still deserialize
            // cleanly with empty lists rather than nulls so downstream iteration is safe.
            const string source = "OperationLogTests_NullSideEffects";

            var entries = new List<OperationLogEntry>
            {
                new("Assets/a.png", "Assets/T/a.png", "R")
            };

            OperationLog.RecordBatch(entries, source);

            var last = OperationLog.ReadAll().FindLast(s => s.source == source);

            Assert.IsNotNull(last);
            Assert.IsNotNull(last.createdAssets);
            Assert.IsNotNull(last.createdFolders);
            Assert.AreEqual(0, last.createdAssets.Count);
            Assert.AreEqual(0, last.createdFolders.Count);
        }

        [Test]
        public void RecordBatch_WithSideEffects_RoundTripsCreatedAssetsAndFolders()
        {
            // Guards the schema-v2 addition: createdAssets and createdFolders must survive JSON
            // round-trip. UndoEngine.CleanupSideEffects reads these lists back to know what to
            // AssetDatabase.DeleteAsset — if serialization drops them, orphan artifacts return.
            const string source = "OperationLogTests_SideEffects";

            var entries = new List<OperationLogEntry>
            {
                new("Assets/T_Rock.png", "Assets/Art/Textures/T_Rock.png", "Textures")
            };

            var createdAssets = new[]
            {
                "Assets/Art/Textures/T_Rock_Mat.mat"
            };

            var createdFolders = new[]
            {
                "Assets/Art",
                "Assets/Art/Textures"
            };

            OperationLog.RecordBatch(entries, source, createdAssets, createdFolders);

            var last = OperationLog.ReadAll().FindLast(s => s.source == source);

            Assert.IsNotNull(last);
            CollectionAssert.AreEqual(createdAssets, last.createdAssets);
            CollectionAssert.AreEqual(createdFolders, last.createdFolders);
        }

        [Test]
        public void RecordBatch_WithUndoSource_RoundTripsThroughReadAll()
        {
            // Verifies that a session written with the Undo source tag survives the JSON round-trip
            // and is retrievable — HistoryView's filter depends on this tag being read back verbatim.
            var reverted = new List<OperationLogEntry>
            {
                new("Assets/Textures/x.png", "Assets/x.png", "Textures")
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

        [TearDown]
        public void TearDown()
        {
            OperationLog.OverrideLogPathForTests = null;

            foreach (var path in new[]
            {
                _tempPath,
                _tempPath + ".bak",
                _tempPath + ".tmp"
            })
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void UndoEngine_RecycleFolder_IsStable()
        {
            // The recycle folder path is a public contract with users' projects — renaming it
            // orphans everything they've previously undone. Freeze the value in tests.
            Assert.AreEqual("Assets/_AssetRouterRecycle", UndoEngine.RecycleFolder);
        }

        [Test]
        public void UndoEngine_UndoSessionSource_IsStable()
        {
            // The tag is written into on-disk log files. Renaming it would silently break
            // filtering / analytics on existing histories. Freeze the value.
            Assert.AreEqual("Undo", UndoEngine.UndoSessionSource);
        }
    }
}