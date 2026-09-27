using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Nakatetsu.Application.Player;
using Nakatetsu.Application.Audio;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Safety.Eb;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Operation;
using Nakatetsu.Train.Integration;
using Nakatetsu.Train.Presentation.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nakatetsu.Train.Presentation.Audio.Tests
{
    public sealed class CabAudioTests
    {
        private readonly List<Object> owned = new();
        private readonly Cab[] cabs = new Cab[4];
        private readonly TrainIntegrationController[] trains = new TrainIntegrationController[2];
        private readonly TimsDirectionController[] directions = new TimsDirectionController[2];
        private TrainFocusController focus;
        private ApplicationAudioController applicationAudio;
        private AudioClip clip;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            clip = AudioClip.Create("EB test tone", 48000, 1, 48000, false);
            owned.Add(clip);
            for (int t = 0; t < 2; t++)
            {
                var definition = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
                definition.cars.Add(null);
                definition.cars.Add(null);
                owned.Add(definition);
                var rootObject = new GameObject($"Train {t}");
                owned.Add(rootObject);
                var root = rootObject.AddComponent<TrainRoot>();
                root.Configure(definition);
                trains[t] = Child(root.transform, "Integration").AddComponent<TrainIntegrationController>();
                trains[t].GetComponent<TrainCabInitializer>().enabled = false;
                directions[t] = Child(root.transform, "TIMS").AddComponent<TimsDirectionController>();
                directions[t].Output.activatedCabPosition = ActivatedCabPosition.Front;
                cabs[t * 2] = CreateCab(root, 0);
                cabs[t * 2 + 1] = CreateCab(root, 1);
            }
            var focusObject = new GameObject("Focus");
            owned.Add(focusObject);
            focusObject.SetActive(false);
            focus = focusObject.AddComponent<TrainFocusController>();
            SetField(focus, "focusedTrain", trains[0]);
            focusObject.SetActive(true);
            var audioObject = new GameObject("Application Audio");
            owned.Add(audioObject);
            applicationAudio = audioObject.AddComponent<ApplicationAudioController>();
            SetField(applicationAudio, "focusController", focus);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = owned.Count - 1; i >= 0; i--) Object.Destroy(owned[i]);
            owned.Clear();
            yield return null;
            Assert.That(CabAudioController.ActiveControllers.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator OnlyFocusedTrainAndCabCanPlayEvenWhenAllDevicesRequestBuzzer()
        {
            foreach (Cab cab in cabs) cab.Tick(60f);
            yield return null;
            AssertPlaying(0);
            SetField(focus, "focusedTrain", trains[1]);
            applicationAudio.RefreshCabAudio();
            AssertPlaying(2);
            directions[1].Output.activatedCabPosition = ActivatedCabPosition.Rear;
            applicationAudio.RefreshCabAudio();
            AssertPlaying(3);
            focus.enabled = false;
            yield return null;
            AssertPlaying(-1);
            focus.enabled = true;
            yield return null;
            AssertPlaying(3);
            SetField(focus, "focusedTrain", null);
            applicationAudio.RefreshCabAudio();
            AssertPlaying(-1);
        }

        [UnityTest]
        public IEnumerator DisablingApplicationAudioOrRemovingFocusReferenceSilencesAllCabs()
        {
            foreach (Cab cab in cabs) cab.Tick(60f);
            yield return null;
            AssertPlaying(0);
            applicationAudio.enabled = false;
            AssertPlaying(-1);
            SetField(focus, "focusedTrain", trains[1]);
            applicationAudio.enabled = true;
            yield return null;
            AssertPlaying(2);
            SetField(applicationAudio, "focusController", null);
            yield return null;
            AssertPlaying(-1);
        }

        [UnityTest]
        public IEnumerator WarningResetStopsPlaybackAndNextWarningRestartsIt()
        {
            Cab cab = cabs[0];
            cab.Tick(60f);
            yield return null;
            Assert.That(cab.Audio.IsBuzzerPlaying, Is.True);
            cab.Device.RequestReset();
            cab.Tick(1f);
            yield return null;
            Assert.That(cab.Audio.IsBuzzerPlaying, Is.False);
            cab.Tick(60f);
            yield return null;
            Assert.That(cab.Audio.IsBuzzerPlaying, Is.True);
            cab.Device.enabled = false;
            yield return null;
            Assert.That(cab.Audio.IsBuzzerPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator EmergencyKeepsBuzzerPlayingUntilRelease()
        {
            Cab cab = cabs[0];
            cab.Tick(60f);
            yield return null;
            Assert.That(cab.Audio.IsBuzzerPlaying, Is.True);
            Assert.That(cab.Device.IsEmergencyBrakeRequested, Is.False);
            cab.Tick(5f);
            cab.Device.RequestReset();
            cab.Tick(1f);
            yield return null;
            Assert.That(cab.Audio.IsBuzzerPlaying, Is.True);
            Assert.That(cab.Device.IsEmergencyBrakeRequested, Is.True);
            cab.Input.SpeedMps = 0f;
            cab.Tick(1f);
            yield return null;
            Assert.That(cab.Audio.IsBuzzerPlaying, Is.False);
            Assert.That(cab.Device.IsEmergencyBrakeRequested, Is.False);
        }

        [UnityTest]
        public IEnumerator MissingOrAmbiguousDeviceDoesNotBindOtherCarOrTrain()
        {
            Cab cab = cabs[0];
            cab.Device.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(-1);
            Assert.That(cab.Audio.BindEbFromAssignment(), Is.False);
            Assert.That(cab.Audio.EbDevice, Is.Null);
            cab.Device.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(0);
            var duplicate = Child(cab.Audio.Owner.transform, "Duplicate EB").AddComponent<EbDevice>();
            duplicate.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(0);
            Assert.That(cab.Audio.BindEbFromAssignment(), Is.False);
            foreach (Cab item in cabs) item.Tick(60f);
            yield return null;
            AssertPlaying(-1);
        }

        [UnityTest]
        public IEnumerator DisablingPresentationStopsLoopAndRemovesRegistration()
        {
            cabs[0].Tick(60f);
            yield return null;
            Assert.That(cabs[0].Audio.IsBuzzerPlaying, Is.True);
            cabs[0].Audio.enabled = false;
            Assert.That(cabs[0].Audio.IsBuzzerPlaying, Is.False);
            Assert.That(CabAudioController.ActiveControllers, Has.No.Member(cabs[0].Audio));
            cabs[0].Audio.enabled = true;
            yield return null;
            Assert.That(cabs[0].Audio.IsBuzzerPlaying, Is.True);
        }

        private Cab CreateCab(TrainRoot root, int carIndex)
        {
            var device = Child(root.transform, "EB").AddComponent<EbDevice>();
            device.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(carIndex);
            var input = new CabInput();
            device.Configure(input, 60f);
            var car = Child(root.transform, $"Car {carIndex}");
            car.AddComponent<TrainPresentationAssignment>().AssignCarIndex(carIndex);
            var audioObject = Child(car.transform, "CabAudio");
            audioObject.SetActive(false);
            var source = Child(audioObject.transform, "EbBuzzer").AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clip;
            var audio = audioObject.AddComponent<CabAudioController>();
            SetField(audio, "ebBuzzer", source);
            audioObject.SetActive(true);
            return new Cab { Device = device, Input = input, Audio = audio };
        }

        private void AssertPlaying(int index)
        {
            for (int i = 0; i < cabs.Length; i++)
                Assert.That(cabs[i].Audio.IsBuzzerPlaying, Is.EqualTo(i == index), $"Cab {i}");
        }

        private static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            return go;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private sealed class CabInput : IEbMasterControllerInputSource
        {
            public float SpeedMps = 10f;
            public bool TryReadMasterControllerInput(out EbMasterControllerInput input)
            {
                input = new EbMasterControllerInput { isActiveCab = true, isInputEnabled = true, speedMps = SpeedMps };
                return true;
            }
        }

        private sealed class Cab
        {
            public EbDevice Device;
            public CabInput Input;
            public CabAudioController Audio;
            public void Tick(float seconds)
            {
                Device.CollectInput();
                Device.Calculate(seconds);
                Device.ApplyOutput(seconds);
            }
        }
    }
}
