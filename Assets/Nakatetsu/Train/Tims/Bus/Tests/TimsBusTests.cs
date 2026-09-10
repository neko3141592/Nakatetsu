using System;
using System.Collections.Generic;
using NUnit.Framework;
using Nakatetsu.Train.Tims.Bus;
using Nakatetsu.Train.Tims.Communication;
using Nakatetsu.Train.Tims.Notch;
using Nakatetsu.Train.Tims.Brake;
using Nakatetsu.Train.Tims.Traction;

namespace Nakatetsu.Train.Tims.Tests
{
    public sealed class TimsBusTests
    {
        [Test]
        public void EqualKeysReadTheSameTagAndWrongTypesFail()
        {
            var bus = new TimsBusState();
            bus.SetInt(new TimsTagKey("Notch", "BrakeStep"), 5);
            Assert.That(bus.TryGetInt(new TimsTagKey("Notch", "BrakeStep"), out int step), Is.True);
            Assert.That(step, Is.EqualTo(5));
            Assert.That(bus.TryGetFloat(new TimsTagKey("Notch", "BrakeStep"), out float value), Is.False);
            Assert.That(value, Is.Zero);
            Assert.That(bus.TryGetInt(new TimsTagKey("Other", "BrakeStep"), out _), Is.False);
        }

        [Test]
        public void ArraysAreCopiedOnWriteAndRead()
        {
            var bus = new TimsBusState();
            var key = new TimsTagKey("Brake", "Forces");
            float[] source = { 10f, 20f };
            bus.SetFloatArray(key, source);
            source[0] = 999f;
            Assert.That(bus.TryGetFloatArray(key, out var read), Is.True);
            read[1] = 999f;
            bus.TryGetFloatArray(key, out var next);
            Assert.That(next, Is.EqualTo(new[] { 10f, 20f }));
        }

        [Test]
        public void SnapshotRestoresPreviousValues()
        {
            var bus = new TimsBusState();
            var key = new TimsTagKey("Device", "Value");
            bus.SetBool(key, true);
            var snapshot = bus.GetSnapshot();
            bus.SetBool(key, false);
            bus.ReplaceSnapshot(snapshot);
            Assert.That(bus.TryGetBool(key, out bool value) && value, Is.True);
            bus.ReplaceSnapshot(null);
            Assert.That(bus.Tags.Count, Is.Zero);
        }

        [Test]
        public void LogicAndContextsDoNotReferenceUnityOrTrainControllers()
        {
            foreach (var reference in typeof(TimsBrakeLogic).Assembly.GetReferencedAssemblies())
            {
                Assert.That(reference.Name.StartsWith("UnityEngine", StringComparison.Ordinal), Is.False);
                Assert.That(reference.Name, Is.Not.EqualTo("Nakatetsu.Train"));
            }
        }
    }
}
