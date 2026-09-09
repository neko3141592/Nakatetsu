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
    public sealed class TimsCommunicationTests
    {
        private static TimsCommunicationContext Create(float interval = 0.05f)
        {
            var context = new TimsCommunicationContext();
            context.State.terminals.Add(new TimsCarTerminalState { carIndex = 0 });
            context.Input.sources.Add(new TimsTransmissionInput
                { sourceId = 7, carIndex = 0, transmissionIntervalSeconds = interval });
            return context;
        }

        [Test]
        public void FirstSendIsImmediateAndOnlySuccessfulSendAdvancesSchedule()
        {
            var context = Create();
            TimsCommunicationLogic.Calculate(context);
            Assert.That(context.Output.dueSourceIds, Is.EqualTo(new[] { 7 }));
            TimsCommunicationLogic.Calculate(context);
            Assert.That(context.Output.dueSourceIds.Count, Is.EqualTo(1));
            TimsCommunicationLogic.MarkTransmitted(context, 7);
            context.Input.timeSeconds = 0.049f;
            TimsCommunicationLogic.Calculate(context);
            Assert.That(context.Output.dueSourceIds.Count, Is.Zero);
            context.Input.timeSeconds = 0.05f;
            TimsCommunicationLogic.Calculate(context);
            Assert.That(context.Output.dueSourceIds, Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public void MinimumIntervalAndResetMatchLegacySchedule()
        {
            var context = Create(-1f);
            TimsCommunicationLogic.MarkTransmitted(context, 7);
            Assert.That(context.Workspace.nextTransmissionTimesSeconds[7], Is.EqualTo(0.001f));
            TimsCommunicationLogic.ResetSchedule(context);
            TimsCommunicationLogic.Calculate(context);
            Assert.That(context.Output.dueSourceIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void MissingTerminalIsSkippedAndDuplicateSourceIdsAreRejected()
        {
            var context = Create();
            context.State.terminals.Clear();
            TimsCommunicationLogic.Calculate(context);
            Assert.That(context.Output.dueSourceIds.Count, Is.Zero);
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
