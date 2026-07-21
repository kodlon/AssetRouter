using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Kodlon.AssetRouter.Actions;
using Kodlon.AssetRouter.Data;
using Kodlon.AssetRouter.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Defined at namespace level (not nested) so Unity can create ScriptableObject instances.

namespace Kodlon.AssetRouter.Tests
{
    internal sealed class CountingAction : AssetImportActionAsset
    {
        public int CanRunOnCount;
        public int ExecuteCount;
        public bool ShouldRun = true;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
        {
            CanRunOnCount++;

            return ShouldRun;
        }

        public override void Execute(Object importedAsset, AssetImportContext ctx) => ExecuteCount++;
    }

    internal sealed class ThrowingAction : AssetImportActionAsset
    {
        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx) => true;

        public override void Execute(Object importedAsset, AssetImportContext ctx) => throw new InvalidOperationException("test exception from ThrowingAction");
    }

    public class ActionPipelineTests
    {
        private CountingAction _counter;
        private ImporterSettingsDatabase _db;
        private ThrowingAction _thrower;

        [SetUp]
        public void SetUp()
        {
            _db = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            _counter = ScriptableObject.CreateInstance<CountingAction>();
            _thrower = ScriptableObject.CreateInstance<ThrowingAction>();
        }

        [Test]
        public void ErrorInOneAction_DoesNotBlockNext()
        {
            var rule = MakeRule(_thrower, _counter);

            // LogAssert.Expect prevents the test runner from failing on the expected exception log.
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));

            Assert.DoesNotThrow(() => ActionPipeline.Execute(rule, null, "Assets/fake.png", _db));
            Assert.AreEqual(1, _counter.ExecuteCount);
        }

        [Test]
        public void Execute_CallsExecute_WhenCanRunOnReturnsTrue()
        {
            _counter.ShouldRun = true;
            var rule = MakeRule(_counter);

            ActionPipeline.Execute(rule, null, "Assets/fake.png", _db);

            Assert.AreEqual(1, _counter.CanRunOnCount);
            Assert.AreEqual(1, _counter.ExecuteCount);
        }

        [Test]
        public void Execute_NoOp_WhenActionsListIsEmpty()
        {
            var rule = new ImportRule(); // no actions

            Assert.DoesNotThrow(() => ActionPipeline.Execute(rule, null, "Assets/fake.png", _db));
        }

        [Test]
        public void Execute_NoOp_WhenRuleIsNotImportRule()
        {
            var baseRule = new DummyBaseRule();
            Assert.DoesNotThrow(() => ActionPipeline.Execute(baseRule, null, "Assets/fake.png", _db));
        }

        [Test]
        public void Execute_SkipsExecute_WhenCanRunOnReturnsFalse()
        {
            _counter.ShouldRun = false;
            var rule = MakeRule(_counter);

            ActionPipeline.Execute(rule, null, "Assets/fake.png", _db);

            Assert.AreEqual(1, _counter.CanRunOnCount);
            Assert.AreEqual(0, _counter.ExecuteCount);
        }

        [Test]
        public void NullAction_IsSkipped()
        {
            var rule = MakeRule(null, _counter);

            Assert.DoesNotThrow(() => ActionPipeline.Execute(rule, null, "Assets/fake.png", _db));
            Assert.AreEqual(1, _counter.ExecuteCount);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_db);
            Object.DestroyImmediate(_counter);
            Object.DestroyImmediate(_thrower);
        }

        private static ImportRule MakeRule(params AssetImportActionAsset[] actions)
        {
            var rule = new ImportRule();
            rule.postImportActions = new List<AssetImportActionAsset>(actions);

            return rule;
        }
    }

    [Serializable]
    internal sealed class DummyBaseRule : BaseImportRule { }
}