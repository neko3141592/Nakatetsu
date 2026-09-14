using Nakatetsu.Train.Simulation.Load;
using NUnit.Framework;

namespace Nakatetsu.Train.Tests
{
    public sealed class TrainLoadTests
    {
        [Test]
        public void CalculatesActualMassFromVehiclePassengersAndCargo()
        {
            var context = new TrainLoadContext();
            context.Settings.emptyMassKg = 29000f;
            context.Settings.passengerCapacity = 150;
            context.Settings.averagePassengerMassKg = 55f;
            context.Input.passengerCount = 100;
            context.Input.cargoMassKg = 500f;

            TrainLoadLogic.Calculate(context);
            TrainLoadLogic.ApplyOutput(context);

            Assert.That(context.State.isInitialized, Is.True);
            Assert.That(context.State.actualPassengerCount, Is.EqualTo(100));
            Assert.That(context.State.actualPassengerMassKg, Is.EqualTo(5500f));
            Assert.That(context.State.actualCargoMassKg, Is.EqualTo(500f));
            Assert.That(context.State.actualTotalMassKg, Is.EqualTo(35000f));
            Assert.That(context.State.actualSupportedMassKg, Is.EqualTo(35000f));
        }

        [Test]
        public void ClampsPassengerCountAndNegativeMassInputs()
        {
            var context = new TrainLoadContext();
            context.Settings.emptyMassKg = -100f;
            context.Settings.passengerCapacity = 2;
            context.Settings.averagePassengerMassKg = 50f;
            context.Input.passengerCount = 3;
            context.Input.cargoMassKg = -10f;

            TrainLoadLogic.Calculate(context);
            TrainLoadLogic.ApplyOutput(context);

            Assert.That(context.State.actualPassengerCount, Is.EqualTo(2));
            Assert.That(context.State.actualPassengerMassKg, Is.EqualTo(100f));
            Assert.That(context.State.actualCargoMassKg, Is.Zero);
            Assert.That(context.State.actualTotalMassKg, Is.EqualTo(100f));
        }
    }
}
