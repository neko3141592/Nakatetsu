using System.Reflection;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Safety.Eb;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Integration;
using Nakatetsu.Train.Equipment.Tims.Safety.Eb;
using Nakatetsu.Train.Equipment.Tims.Operation;
using NUnit.Framework;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Tests
{
    public sealed class EbDeviceTimsInputAdapterTests
    {
        [TestCase(ActivatedCabPosition.Front, false)]
        [TestCase(ActivatedCabPosition.Rear, true)]
        [TestCase(ActivatedCabPosition.None, false)]
        public void ReadsLocalMasterControllerAndMasterBusCabSelection(
            ActivatedCabPosition activeCab, bool expectedActive)
        {
            var trainObject = new GameObject("Train");
            var consistDefinition = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            var car0 = ScriptableObject.CreateInstance<CarDefinitionAsset>();
            var car1 = ScriptableObject.CreateInstance<CarDefinitionAsset>();
            try
            {
                consistDefinition.cars.Add(car0);
                consistDefinition.cars.Add(car1);
                TrainRoot trainRoot = trainObject.AddComponent<TrainRoot>();
                trainRoot.Configure(consistDefinition);

                var timsObject = new GameObject("Tims");
                timsObject.transform.SetParent(trainObject.transform);
                TimsCommunicationController communicationController =
                    timsObject.AddComponent<TimsCommunicationController>();
                // EditModeではUnityのAwakeが自動実行されないため、端末を初期化する。
                typeof(TimsCommunicationController)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(communicationController, null);

                CreateMasterController(trainObject.transform, 0, 1);
                CreateMasterController(trainObject.transform, 1, 3);

                var ebObject = new GameObject("EbDevice");
                ebObject.transform.SetParent(trainObject.transform);
                TrainEquipmentAssignment assignment =
                    ebObject.AddComponent<TrainEquipmentAssignment>();
                assignment.AssignCarIndex(1);
                EbDevice ebDevice = ebObject.AddComponent<EbDevice>();
                EbDeviceTimsInputAdapter adapter =
                    ebObject.AddComponent<EbDeviceTimsInputAdapter>();
                adapter.Configure(communicationController);
                ebDevice.Configure(adapter, 60f);

                communicationController.CollectInputSources();
                communicationController.MasterBus.SetInt(
                    TimsDirectionController.ActivatedCabPositionKey, (int)activeCab);
                ebDevice.CollectInput();

                Assert.That(ebDevice.Context.Input.hasMasterControllerState, Is.True);
                Assert.That(ebDevice.Context.Input.masterController.powerPosition, Is.EqualTo(3));
                Assert.That(ebDevice.Context.Input.masterController.isActiveCab, Is.EqualTo(expectedActive));
            }
            finally
            {
                Object.DestroyImmediate(trainObject);
                Object.DestroyImmediate(consistDefinition);
                Object.DestroyImmediate(car0);
                Object.DestroyImmediate(car1);
            }
        }

        private static void CreateMasterController(
            Transform parent,
            int carIndex,
            int powerPosition)
        {
            // 指定車両のLocalBus送信元になるマスコンを生成する。
            var masterObject = new GameObject($"MasterController_{carIndex}");
            masterObject.transform.SetParent(parent);
            TrainEquipmentAssignment assignment =
                masterObject.AddComponent<TrainEquipmentAssignment>();
            assignment.AssignCarIndex(carIndex);
            MasterController masterController = masterObject.AddComponent<MasterController>();
            masterController.ConfigureLimits(4, 7, 8);
            masterController.SetPowerPosition(powerPosition);
            masterObject.AddComponent<MasterControllerTimsBusSource>();
        }
    }
}
