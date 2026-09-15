using System.Reflection;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Safety.Eb;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Integration;
using Nakatetsu.Train.Equipment.Tims.Safety.Eb;
using Nakatetsu.Train.Equipment.Tims.Operation;
using Nakatetsu.Train.Equipment.Tims.Speed;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Tests
{
    public sealed class EbDeviceTimsBusSourceTests
    {
        private const string PrefabPath =
            "Assets/Nakatetsu/Train/Equipment/Safety/Eb/Prefabs/EbDevice.prefab";

        private GameObject trainObject;
        private ConsistDefinitionAsset consistDefinition;
        private CarDefinitionAsset car0;
        private CarDefinitionAsset car1;
        private TimsCommunicationController communication;
        private readonly EbDevice[] devices = new EbDevice[2];
        private readonly MasterController[] masters = new MasterController[2];

        [SetUp]
        public void SetUp()
        {
            trainObject = new GameObject("Train");
            consistDefinition = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            car0 = ScriptableObject.CreateInstance<CarDefinitionAsset>();
            car1 = ScriptableObject.CreateInstance<CarDefinitionAsset>();
            consistDefinition.cars.Add(car0);
            consistDefinition.cars.Add(car1);
            TrainRoot trainRoot = trainObject.AddComponent<TrainRoot>();
            trainRoot.Configure(consistDefinition);

            var timsObject = new GameObject("Tims");
            timsObject.transform.SetParent(trainObject.transform);
            communication = timsObject.AddComponent<TimsCommunicationController>();
            // EditModeではUnityのAwakeが自動実行されないため、端末を初期化する。
            InitializeCommunication(communication);
            SetActiveCab(ActivatedCabPosition.Front);
            // 既存の無操作監視テストは走行中の測定値を供給する。
            communication.GetLocalBus(0).SetFloat(SpeedSensorTimsBusSource.MeasuredSpeedMpsKey, 10f);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            for (int i = 0; i < devices.Length; i++)
            {
                var masterObject = new GameObject($"MasterController_{i}");
                masterObject.transform.SetParent(trainObject.transform);
                masterObject.AddComponent<TrainEquipmentAssignment>().AssignCarIndex(i);
                masters[i] = masterObject.AddComponent<MasterController>();
                masters[i].ConfigureLimits(4, 7, 8);
                masters[i].SetInputEnabled(true);
                masterObject.AddComponent<MasterControllerTimsBusSource>();

                GameObject instance = Object.Instantiate(prefab, trainObject.transform);
                instance.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(i);
                devices[i] = instance.GetComponent<EbDevice>();
                Assert.That(instance.GetComponent<EbDeviceTimsBusSource>(), Is.Not.Null);
            }
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(trainObject);
            Object.DestroyImmediate(consistDefinition);
            Object.DestroyImmediate(car0);
            Object.DestroyImmediate(car1);
        }

        [Test]
        public void CollectsPreviousStepOutputIntoEachCarsLocalBus()
        {
            devices[1].Configure(devices[1].GetComponent<EbDeviceTimsInputAdapter>(), 120f);
            StepDevices(60f);

            // 最初の収集では、まだ一度も計算していない出力を送らない。
            AssertNoStatus(communication.GetLocalBus(0));
            AssertNoStatus(communication.GetLocalBus(1));

            communication.CollectInputSources();
            AssertStatus(communication.GetLocalBus(0), true, 60f, 0f);
            AssertStatus(communication.GetLocalBus(1), false, 0f, 120f);
            AssertNoStatus(communication.MasterBus);

            // 収集だけではEBタイマーを進めない。
            communication.CollectInputSources();
            Assert.That(devices[0].Output.inactivitySeconds, Is.EqualTo(60f));
            Assert.That(devices[1].Output.inactivitySeconds, Is.Zero);
        }

        [Test]
        public void OperationUpdatesOnlyTheOperatedCarsPublishedState()
        {
            StepDevices(60f);
            communication.CollectInputSources();
            masters[0].SetPowerPosition(1);
            StepDevices(1f);

            // 計算直後は前ステップ値、次の収集でリセット結果へ更新する。
            AssertStatus(communication.GetLocalBus(0), true, 60f, 0f);
            communication.CollectInputSources();
            AssertStatus(communication.GetLocalBus(0), false, 0f, 60f);
            AssertStatus(communication.GetLocalBus(1), false, 0f, 60f);
        }

        [Test]
        public void DisabledMasterControllerPublishesExistingResetBehavior()
        {
            StepDevices(60f);
            masters[0].SetInputEnabled(false);
            StepDevices(1f);
            communication.CollectInputSources();

            AssertStatus(communication.GetLocalBus(0), false, 0f, 60f);
            AssertStatus(communication.GetLocalBus(1), false, 0f, 60f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnavailableComponentRemovesOnlyEbTags(bool disableSource)
        {
            StepDevices(60f);
            communication.CollectInputSources();
            if (disableSource)
            {
                devices[0].GetComponent<EbDeviceTimsBusSource>().enabled = false;
            }
            else
            {
                devices[0].enabled = false;
            }

            communication.CollectInputSources();

            TimsBusState localBus = communication.GetLocalBus(0);
            AssertNoStatus(localBus);
            Assert.That(localBus.TryGetInt(MasterControllerTimsBusSource.PowerPositionKey, out _), Is.True);
            AssertStatus(communication.GetLocalBus(1), false, 0f, 60f);
        }

        [Test]
        public void UnassignedSourceAndNullBusDoNotPublish()
        {
            StepDevices(60f);
            devices[0].GetComponent<TrainEquipmentAssignment>().AssignCarIndex(-1);
            EbDeviceTimsBusSource source = devices[0].GetComponent<EbDeviceTimsBusSource>();
            var bus = new TimsBusState();

            Assert.That(source.AssignedCarIndex, Is.EqualTo(-1));
            Assert.DoesNotThrow(() => source.WriteTimsBus(null));
            source.WriteTimsBus(bus);
            AssertNoStatus(bus);
        }

        [Test]
        public void OtherTrainCannotCollectThisTrainsEbStatus()
        {
            StepDevices(60f);
            var otherTrainObject = new GameObject("OtherTrain");
            try
            {
                otherTrainObject.AddComponent<TrainRoot>().Configure(consistDefinition);
                var otherTimsObject = new GameObject("Tims");
                otherTimsObject.transform.SetParent(otherTrainObject.transform);
                var otherCommunication = otherTimsObject.AddComponent<TimsCommunicationController>();
                InitializeCommunication(otherCommunication);

                otherCommunication.CollectInputSources();
                AssertNoStatus(otherCommunication.GetLocalBus(0));
                AssertNoStatus(otherCommunication.GetLocalBus(1));
                communication.CollectInputSources();
                AssertStatus(communication.GetLocalBus(0), true, 60f, 0f);
            }
            finally
            {
                Object.DestroyImmediate(otherTrainObject);
            }
        }

        [TestCase("Tc1")]
        [TestCase("Tc2")]
        public void CabDefinitionIncludesStatusSource(string carName)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CarDefinitionAsset>(
                $"Assets/Nakatetsu/Train/Consist/Definitions/Data/{carName}.asset");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.additionalEquipmentPrefabs, Does.Contain(prefab));
            Assert.That(prefab.GetComponents<EbDeviceTimsBusSource>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void CabSwitchResetsFormerCabAndStartsNewCabsTimer()
        {
            StepDevices(60f);
            communication.CollectInputSources();
            AssertStatus(communication.GetLocalBus(0), true, 60f, 0f);

            SetActiveCab(ActivatedCabPosition.Rear);
            StepDevices(1f);
            communication.CollectInputSources();
            AssertStatus(communication.GetLocalBus(0), false, 0f, 60f);
            AssertStatus(communication.GetLocalBus(1), false, 1f, 59f);

            StepDevices(59f);
            communication.CollectInputSources();
            AssertStatus(communication.GetLocalBus(0), false, 0f, 60f);
            AssertStatus(communication.GetLocalBus(1), true, 60f, 0f);

            SetActiveCab(ActivatedCabPosition.Front);
            StepDevices(1f);
            communication.CollectInputSources();
            AssertStatus(communication.GetLocalBus(0), false, 1f, 59f);
            AssertStatus(communication.GetLocalBus(1), false, 0f, 60f);
        }

        [TestCase("missing")]
        [TestCase("wrongType")]
        [TestCase("invalid")]
        [TestCase("none")]
        public void UnavailableCabSelectionStopsMonitoringAndClearsRequest(string state)
        {
            StepDevices(60f);
            communication.CollectInputSources();
            switch (state)
            {
                case "missing":
                    communication.MasterBus.Remove(TimsDirectionController.ActivatedCabPositionKey);
                    break;
                case "wrongType":
                    communication.MasterBus.SetString(TimsDirectionController.ActivatedCabPositionKey, "Front");
                    break;
                case "invalid":
                    communication.MasterBus.SetInt(TimsDirectionController.ActivatedCabPositionKey, 99);
                    break;
                case "none":
                    SetActiveCab(ActivatedCabPosition.None);
                    break;
            }

            StepDevices(120f);
            communication.CollectInputSources();
            AssertStatus(communication.GetLocalBus(0), false, 0f, 60f);
            AssertStatus(communication.GetLocalBus(1), false, 0f, 60f);
        }

        [TestCase(0f, false)]
        [TestCase(4.999f, false)]
        [TestCase(5f, true)]
        public void MasterBusSpeedControlsMonitoring(float speedKmh, bool expectedEmergency)
        {
            communication.GetLocalBus(0).SetFloat(
                SpeedSensorTimsBusSource.MeasuredSpeedMpsKey, speedKmh / 3.6f);
            StepDevices(60f);
            communication.CollectInputSources();
            AssertStatus(communication.GetLocalBus(0), expectedEmergency,
                expectedEmergency ? 60f : 0f, expectedEmergency ? 0f : 60f);
        }

        [Test]
        public void MissingSpeedClearsPreviousRequest()
        {
            StepDevices(60f);
            Assert.That(devices[0].Output.isEmergencyBrakeRequested, Is.True);
            communication.GetLocalBus(0).Remove(SpeedSensorTimsBusSource.MeasuredSpeedMpsKey);
            StepDevices(1f);
            communication.CollectInputSources();
            Assert.That(devices[0].Context.Input.hasMasterControllerState, Is.False);
            AssertStatus(communication.GetLocalBus(0), false, 0f, 60f);
        }

        private void SetActiveCab(ActivatedCabPosition position)
        {
            communication.MasterBus.SetInt(
                TimsDirectionController.ActivatedCabPositionKey, (int)position);
        }

        private static void InitializeCommunication(TimsCommunicationController controller)
        {
            typeof(TimsCommunicationController)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
        }

        private void StepDevices(float deltaTimeSeconds)
        {
            // TrainSimulationControllerと同じ収集→全入力→全計算→全反映の順序。
            communication.CollectInputSources();
            foreach (EbDevice device in devices)
            {
                device.CollectInput();
            }
            foreach (EbDevice device in devices)
            {
                device.Calculate(deltaTimeSeconds);
            }
            foreach (EbDevice device in devices)
            {
                device.ApplyOutput(deltaTimeSeconds);
            }
        }

        private static void AssertNoStatus(TimsBusState bus)
        {
            Assert.That(bus, Is.Not.Null);
            Assert.That(bus.TryGetBool(EbDeviceTimsBusSource.IsEmergencyBrakeRequestedKey, out _), Is.False);
            Assert.That(bus.TryGetFloat(EbDeviceTimsBusSource.InactivitySecondsKey, out _), Is.False);
            Assert.That(bus.TryGetFloat(EbDeviceTimsBusSource.RemainingSecondsKey, out _), Is.False);
        }

        private static void AssertStatus(TimsBusState bus, bool emergency, float inactivity, float remaining)
        {
            Assert.That(bus.TryGetBool(EbDeviceTimsBusSource.IsEmergencyBrakeRequestedKey, out bool actualEmergency), Is.True);
            Assert.That(actualEmergency, Is.EqualTo(emergency));
            Assert.That(bus.TryGetFloat(EbDeviceTimsBusSource.InactivitySecondsKey, out float actualInactivity), Is.True);
            Assert.That(actualInactivity, Is.EqualTo(inactivity));
            Assert.That(bus.TryGetFloat(EbDeviceTimsBusSource.RemainingSecondsKey, out float actualRemaining), Is.True);
            Assert.That(actualRemaining, Is.EqualTo(remaining));
        }
    }
}
