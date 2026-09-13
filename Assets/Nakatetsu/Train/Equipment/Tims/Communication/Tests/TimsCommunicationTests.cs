using System;
using System.Collections.Generic;
using NUnit.Framework;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Notch;
using Nakatetsu.Train.Equipment.Tims.Brake;
using Nakatetsu.Train.Equipment.Tims.Traction;

namespace Nakatetsu.Train.Equipment.Tims.Tests
{
    public sealed class TimsCommunicationTests
    {
        private static TimsCommunicationContext Create()
        {
            var context = new TimsCommunicationContext();
            context.State.terminals.Add(new TimsCarTerminalState { carIndex = 0 });
            context.Input.sources.Add(new TimsTransmissionInput
                { sourceId = 7, carIndex = 0 });
            return context;
        }

        [Test]
        public void AvailableSourcesAreReturnedOnEveryCalculation()
        {
            var context = Create();
            TimsCommunicationLogic.Calculate(context);
            Assert.That(context.Output.availableSourceIds, Is.EqualTo(new[] { 7 }));
            TimsCommunicationLogic.Calculate(context);
            Assert.That(context.Output.availableSourceIds, Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public void MissingTerminalIsSkipped()
        {
            var context = Create();
            context.State.terminals.Clear();
            TimsCommunicationLogic.Calculate(context);
            Assert.That(context.Output.availableSourceIds, Is.Empty);
        }

        [Test]
        public void DuplicateSourceIdsAreRejected()
        {
            var context = Create();
            context.Input.sources.Add(context.Input.sources[0]);
            Assert.Throws<ArgumentException>(() => TimsCommunicationLogic.Calculate(context));
        }

        [Test]
        public void FloatCollectionRetainsMissingCarSlots()
        {
            var context = Create();
            var key = new TimsTagKey("Load", "Mass");
            context.State.terminals[0].localBus.SetFloat(key, 40000f);
            context.State.terminals.Add(null);
            var values = new List<float>();
            var founds = new List<bool>();
            TimsCommunicationLogic.CollectFloatFromCars(context, key, values, founds);
            Assert.That(values, Is.EqualTo(new[] { 40000f, 0f }));
            Assert.That(founds, Is.EqualTo(new[] { true, false }));
        }
    }
}
