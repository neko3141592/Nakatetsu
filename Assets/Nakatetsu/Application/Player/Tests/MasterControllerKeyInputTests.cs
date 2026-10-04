using System.Collections.Generic;
using System.Reflection;
using Nakatetsu.Train;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Operation;
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
    public sealed class MasterControllerKeyInputTests
    {
        private readonly List<Object> owned = new();
        private readonly InputTestFixture inputFixture = new();
        private TrainRoot train;
        private TrainIntegrationController integration;
        private TimsDirectionController direction;
        private MasterController front;
        private MasterController rear;
        private Keyboard keyboard;
        private InputActionAsset actions;

        [SetUp]
        public void SetUp()
        {
            inputFixture.Setup();
            var definition = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            owned.Add(definition);
            definition.cars.Add(null);
            definition.cars.Add(null);
            var trainObject = new GameObject("Train");
            owned.Add(trainObject);
            train = trainObject.AddComponent<TrainRoot>();
            train.Configure(definition);
            direction = Child(train.transform, "Tims").AddComponent<TimsDirectionController>();
            direction.Output.activatedCabPosition = ActivatedCabPosition.Front;
            front = CreateMaster(0);
            rear = CreateMaster(1);
            integration = Child(train.transform, "Integration").AddComponent<TrainIntegrationController>();
            // EditModeではUnityのライフサイクルが進まないため明示的に初期化する。
            typeof(TrainIntegrationController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(integration, null);
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
        public void KeyCanBeRemovedAndReinsertedOnlyAtCurrentCab(ActivatedCabPosition cab)
        {
            direction.Output.activatedCabPosition = cab;
            MasterController selected = cab == ActivatedCabPosition.Front ? front : rear;
            MasterController other = cab == ActivatedCabPosition.Front ? rear : front;
            Assert.That(integration.Input.ToggleMasterControllerKey(), Is.True);
            Assert.That(selected.IsKeyInserted, Is.True);
            Assert.That(other.IsKeyInserted, Is.False);
            Assert.That(integration.Input.ToggleMasterControllerKey(), Is.True);
            Assert.That(selected.IsKeyInserted, Is.False);
            Assert.That(integration.Input.ToggleMasterControllerKey(), Is.True);
            Assert.That(selected.IsKeyInserted, Is.True);
            Assert.That(other.IsKeyInserted, Is.False);
        }

        [TestCase("forward")]
        [TestCase("power")]
        [TestCase("noCab")]
        [TestCase("missing")]
        [TestCase("disabledMaster")]
        [TestCase("disabledInput")]
        [TestCase("inputNotAllowed")]
        [TestCase("disabledDirection")]
        [TestCase("duplicate")]
        public void LockedOrUnavailableTargetRejectsKeyOperation(string condition)
        {
            switch (condition)
            {
                case "forward":
                case "power":
                    Assert.That(front.SetKeyInserted(true), Is.True);
                    Assert.That(front.SetReverserPosition(ReverserPosition.Forward), Is.True);
                    if (condition == "power") front.SetPowerPosition(2);
                    break;
                case "noCab": direction.Output.activatedCabPosition = ActivatedCabPosition.None; break;
                case "missing": front.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(-1); break;
                case "disabledMaster": front.enabled = false; break;
                case "disabledInput": integration.Input.enabled = false; break;
                case "inputNotAllowed": front.SetInputEnabled(false); break;
                case "disabledDirection": direction.enabled = false; break;
                case "duplicate": CreateMaster(0); break;
            }

            bool wasInserted = front.IsKeyInserted;
            Assert.That(integration.Input.ToggleMasterControllerKey(), Is.False);
            Assert.That(front.IsKeyInserted, Is.EqualTo(wasInserted));
            Assert.That(rear.IsKeyInserted, Is.False);
        }

        [Test]
        public void KOperatesOncePerPressAndUsesCurrentTarget()
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
            InputAction toggle = actions.FindAction("Driving/ToggleMasterControllerKey", true);
            toggle.started += receiver.OnToggleMasterControllerKey;
            toggle.performed += receiver.OnToggleMasterControllerKey;
            toggle.canceled += receiver.OnToggleMasterControllerKey;
            toggle.Enable();

            SetK(true);
            Assert.That(front.IsKeyInserted, Is.True);
            SetK(true);
            Assert.That(front.IsKeyInserted, Is.True, "Holding must not repeat");
            SetK(false);
            Assert.That(front.IsKeyInserted, Is.True, "Releasing must not remove the key");
            SetK(true);
            Assert.That(front.IsKeyInserted, Is.False);
            SetK(false);
            SetK(true);
            Assert.That(front.IsKeyInserted, Is.True, "A removed key must be insertable again");
            SetK(false);
            receiver.SetTarget(null);
            SetK(true);
            Assert.That(front.IsKeyInserted, Is.True, "No focus must not target the old train");
            SetK(false);
            receiver.SetTarget(integration);
            direction.Output.activatedCabPosition = ActivatedCabPosition.Rear;
            SetK(true);
            Assert.That(front.IsKeyInserted, Is.True);
            Assert.That(rear.IsKeyInserted, Is.True);
        }

        private void SetK(bool down)
        {
            InputSystem.QueueStateEvent(keyboard, down ? new KeyboardState(Key.K) : new KeyboardState());
            InputSystem.Update();
        }

        private MasterController CreateMaster(int carIndex)
        {
            var master = Child(train.transform, "MasterController").AddComponent<MasterController>();
            master.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(carIndex);
            master.ConfigureLimits(4, 7, 8);
            return master;
        }

        private static GameObject Child(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent);
            return child;
        }
    }
}
