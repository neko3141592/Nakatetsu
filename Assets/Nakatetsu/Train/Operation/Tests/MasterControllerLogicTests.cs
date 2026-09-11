using NUnit.Framework;
using UnityEngine;
using Nakatetsu.Train.Brake.ControlDevice;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment;
using Nakatetsu.Train.Operation;

namespace Nakatetsu.Train.Tests
{
    public sealed class MasterControllerLogicTests
    {
        private static MasterControllerContext Create()
        {
            var context = new MasterControllerContext();
            MasterControllerLogic.ConfigureLimits(context, 4, 7, 8);
            MasterControllerLogic.Initialize(context, ReverserPosition.Neutral, true);
            return context;
        }

        [Test]
        public void PowerAndBrakeAreMutuallyExclusive()
        {
            MasterControllerContext context = Create();

            MasterControllerLogic.SetPowerPosition(context, 3);
            Assert.That(context.State.powerPosition, Is.EqualTo(3));
            Assert.That(context.State.brakePosition, Is.Zero);

            MasterControllerLogic.SetBrakePosition(context, 2);
            Assert.That(context.State.powerPosition, Is.Zero);
            Assert.That(context.State.brakePosition, Is.EqualTo(2));
        }

        [Test]
        public void CombinedHandleMovesThroughNeutral()
        {
            MasterControllerContext context = Create();
            MasterControllerLogic.SetPowerPosition(context, 2);

            MasterControllerLogic.MoveOneStepTowardBrake(context);
            MasterControllerLogic.MoveOneStepTowardBrake(context);
            Assert.That(context.State.powerPosition, Is.Zero);
            Assert.That(context.State.brakePosition, Is.Zero);

            MasterControllerLogic.MoveOneStepTowardBrake(context);
            Assert.That(context.State.brakePosition, Is.EqualTo(1));

            MasterControllerLogic.MoveOneStepTowardPower(context);
            Assert.That(context.State.brakePosition, Is.Zero);
        }

        [Test]
        public void ServiceStepDoesNotEnterEmergencyButDirectBrakeStepCan()
        {
            MasterControllerContext context = Create();

            for (int i = 0; i < 20; i++)
            {
                MasterControllerLogic.StepTowardServiceMaxBrake(context);
            }

            Assert.That(context.State.brakePosition, Is.EqualTo(7));
            MasterControllerLogic.MoveOneStepTowardBrake(context);
            Assert.That(context.State.brakePosition, Is.EqualTo(8));
        }

        [Test]
        public void ReverserCannotChangeWhilePowerIsApplied()
        {
            MasterControllerContext context = Create();
            MasterControllerLogic.SetPowerPosition(context, 1);

            bool accepted = MasterControllerLogic.TrySetReverserPosition(
                context,
                ReverserPosition.Forward);

            Assert.That(accepted, Is.False);
            Assert.That(context.State.reverserPosition, Is.EqualTo(ReverserPosition.Neutral));
        }

        [Test]
        public void EmergencyBrakeClearsPowerAndUsesConfiguredPosition()
        {
            MasterControllerContext context = Create();
            MasterControllerLogic.SetPowerPosition(context, 4);

            MasterControllerLogic.SetEmergencyBrake(context);

            Assert.That(context.State.powerPosition, Is.Zero);
            Assert.That(context.State.brakePosition, Is.EqualTo(8));
        }
    }

    public sealed class TrainEquipmentBuilderTests
    {
        [Test]
        public void BuildsEquipmentUnderCarsAndAssignsZeroBasedCarIndexes()
        {
            GameObject equipmentsObject = null;
            GameObject masterControllerPrefab = null;
            GameObject brakePrefab = null;
            ConsistDefinitionAsset consistDefinition = null;
            CarDefinitionAsset cabDefinition = null;
            CarDefinitionAsset trailerDefinition = null;

            try
            {
                equipmentsObject = new GameObject("Equipments");
                Transform common = new GameObject("Common").transform;
                common.SetParent(equipmentsObject.transform, false);
                Transform tims = new GameObject("TIMS").transform;
                tims.SetParent(common, false);

                masterControllerPrefab = new GameObject("MasterController");
                masterControllerPrefab.AddComponent<MasterController>();
                brakePrefab = new GameObject("BrakeEquipment");
                brakePrefab.AddComponent<BrakeControlDevice>();

                cabDefinition = ScriptableObject.CreateInstance<CarDefinitionAsset>();
                cabDefinition.masterControllerPrefab = masterControllerPrefab;
                trailerDefinition = ScriptableObject.CreateInstance<CarDefinitionAsset>();

                consistDefinition = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
                consistDefinition.cars.Add(cabDefinition);
                consistDefinition.cars.Add(trailerDefinition);

                TrainEquipmentBuilder builder =
                    equipmentsObject.AddComponent<TrainEquipmentBuilder>();
                builder.Configure(consistDefinition, defaultBrakeEquipmentPrefab: brakePrefab);

                Assert.That(builder.Build(), Is.True);
                Assert.That(equipmentsObject.transform.Find("Common/TIMS"), Is.Not.Null);
                Assert.That(equipmentsObject.transform.Find("Cars/Car_1/MasterController"), Is.Not.Null);
                Assert.That(equipmentsObject.transform.Find("Cars/Car_2/BrakeEquipment"), Is.Not.Null);

                TrainEquipmentAssignment[] assignments =
                    equipmentsObject.GetComponentsInChildren<TrainEquipmentAssignment>(true);
                Assert.That(assignments.Length, Is.EqualTo(3));
                Assert.That(
                    equipmentsObject.transform
                        .Find("Cars/Car_1/MasterController")
                        .GetComponent<TrainEquipmentAssignment>()
                        .AssignedCarIndex,
                    Is.Zero);
                Assert.That(
                    equipmentsObject.transform
                        .Find("Cars/Car_2/BrakeEquipment")
                        .GetComponent<TrainEquipmentAssignment>()
                        .AssignedCarIndex,
                    Is.EqualTo(1));

                Assert.That(builder.Build(), Is.True);
                assignments = equipmentsObject.GetComponentsInChildren<TrainEquipmentAssignment>(true);
                Assert.That(assignments.Length, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(equipmentsObject);
                Object.DestroyImmediate(masterControllerPrefab);
                Object.DestroyImmediate(brakePrefab);
                Object.DestroyImmediate(consistDefinition);
                Object.DestroyImmediate(cabDefinition);
                Object.DestroyImmediate(trailerDefinition);
            }
        }
    }
}
