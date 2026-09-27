using System.Collections.Generic;
using System.Reflection;
using Nakatetsu.Train;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Safety.Eb;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Operation;
using Nakatetsu.Train.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Nakatetsu.Application.Player.Tests
{
    public sealed class EbResetInputTests
    {
        private readonly List<Object> owned = new();
        private readonly InputTestFixture inputFixture = new();
        private TrainRoot train;
        private TrainIntegrationController integration;
        private TimsDirectionController direction;
        private EbDevice front;
        private EbDevice rear;
        private Keyboard keyboard;
        private InputActionAsset actions;

        [SetUp]
        public void SetUp()
        {
            inputFixture.Setup();
            train = CreateTrain("Train");
            direction = Child(train.transform, "TIMS").AddComponent<TimsDirectionController>();
            direction.Output.activatedCabPosition = ActivatedCabPosition.Front;
            integration = Child(train.transform, "Integration").AddComponent<TrainIntegrationController>();
            // EditModeではUnityのライフサイクルが進まないため明示的に初期化する。
            typeof(TrainIntegrationController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(integration, null);
            front = CreateDevice(train, 0);
            rear = CreateDevice(train, 1);
        }

        [TearDown]
        public void TearDown()
        {
            if (actions != null) actions.Disable();
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            for (int i = owned.Count - 1; i >= 0; i--)
                Object.DestroyImmediate(owned[i]);
            owned.Clear();
            inputFixture.TearDown();
        }

        [TestCase(ActivatedCabPosition.Front)]
        [TestCase(ActivatedCabPosition.Rear)]
        public void ResetReachesOnlyActiveCab(ActivatedCabPosition cab)
        {
            direction.Output.activatedCabPosition = cab;
            Assert.That(integration.Input.ResetEb(), Is.True);
            front.CollectInput();
            rear.CollectInput();
            Assert.That(front.Context.Input.resetRequested, Is.EqualTo(cab == ActivatedCabPosition.Front));
            Assert.That(rear.Context.Input.resetRequested, Is.EqualTo(cab == ActivatedCabPosition.Rear));
        }

        [TestCase("noCab")]
        [TestCase("missing")]
        [TestCase("disabledDevice")]
        [TestCase("disabledInput")]
        [TestCase("disabledDirection")]
        [TestCase("duplicate")]
        public void UnavailableOrAmbiguousTargetRejectsReset(string condition)
        {
            switch (condition)
            {
                case "noCab": direction.Output.activatedCabPosition = ActivatedCabPosition.None; break;
                case "missing": front.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(-1); break;
                case "disabledDevice": front.enabled = false; break;
                case "disabledInput": integration.Input.enabled = false; break;
                case "disabledDirection": direction.enabled = false; break;
                case "duplicate": CreateDevice(train, 0); break;
            }
            Assert.That(integration.Input.ResetEb(), Is.False);
            front.CollectInput();
            rear.CollectInput();
            Assert.That(front.Context.Input.resetRequested, Is.False);
            Assert.That(rear.Context.Input.resetRequested, Is.False);
        }

        [Test]
        public void NestedOtherTrainIsNotAResetTarget()
        {
            TrainRoot other = CreateTrain("Other train");
            other.transform.SetParent(train.transform);
            EbDevice foreign = CreateDevice(other, 0);
            front.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(-1);
            Assert.That(integration.Input.ResetEb(), Is.False);
            foreign.CollectInput();
            Assert.That(foreign.Context.Input.resetRequested, Is.False);
        }

        [Test]
        public void SpaceResetsOncePerPressAndUsesCurrentFocus()
        {
            var inputObject = new GameObject("Keyboard");
            owned.Add(inputObject);
            var receiver = inputObject.AddComponent<TrainKeyboardInputController>();
            receiver.GetComponent<PlayerInput>().enabled = false;
            receiver.SetTarget(integration);
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Nakatetsu/Application/Player/Data/TrainInputActions.inputactions");
            Assert.That(asset, Is.Not.Null);
            actions = Object.Instantiate(asset);
            owned.Add(actions);
            keyboard = InputSystem.AddDevice<Keyboard>();
            actions.devices = new InputDevice[] { keyboard };
            InputAction reset = actions.FindAction("Driving/EbReset", true);
            reset.started += receiver.OnEbReset;
            reset.performed += receiver.OnEbReset;
            reset.canceled += receiver.OnEbReset;
            reset.Enable();

            SetSpace(true);
            front.CollectInput();
            Assert.That(front.Context.Input.resetRequested, Is.True);
            SetSpace(true);
            front.CollectInput();
            Assert.That(front.Context.Input.resetRequested, Is.False, "Holding must not repeat");
            SetSpace(false);
            front.CollectInput();
            Assert.That(front.Context.Input.resetRequested, Is.False, "Releasing must not reset");

            receiver.SetTarget(null);
            SetSpace(true);
            front.CollectInput();
            Assert.That(front.Context.Input.resetRequested, Is.False, "No focus must not target old train");
            SetSpace(false);
            receiver.SetTarget(integration);
            direction.Output.activatedCabPosition = ActivatedCabPosition.Rear;
            SetSpace(true);
            front.CollectInput();
            rear.CollectInput();
            Assert.That(front.Context.Input.resetRequested, Is.False);
            Assert.That(rear.Context.Input.resetRequested, Is.True);
        }

        private void SetSpace(bool down)
        {
            InputSystem.QueueStateEvent(keyboard, down ? new KeyboardState(Key.Space) : new KeyboardState());
            InputSystem.Update();
        }

        private TrainRoot CreateTrain(string name)
        {
            var definition = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            owned.Add(definition);
            definition.cars.Add(null);
            definition.cars.Add(null);
            var go = new GameObject(name);
            owned.Add(go);
            var root = go.AddComponent<TrainRoot>();
            root.Configure(definition);
            return root;
        }

        private static EbDevice CreateDevice(TrainRoot root, int carIndex)
        {
            var device = Child(root.transform, "EB").AddComponent<EbDevice>();
            device.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(carIndex);
            return device;
        }

        private static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            return go;
        }
    }
}
