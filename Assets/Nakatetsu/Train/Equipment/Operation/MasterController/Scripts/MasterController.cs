using System;
using UnityEngine;
using Nakatetsu.Train.Equipment.Shared;

namespace Nakatetsu.Train.Equipment.Operation
{
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class MasterController : MonoBehaviour
    {
        [Header("Notch limits")]
        [SerializeField, Min(1)] private int maxPowerPosition = 4;
        [SerializeField, Min(1)] private int maxServiceBrakePosition = 7;
        [SerializeField, Min(2)] private int emergencyBrakePosition = 8;

        [Header("Initial state")]
        [SerializeField] private ReverserPosition initialReverserPosition = ReverserPosition.Neutral;
        [SerializeField] private bool inputEnabledOnAwake = true;

        private readonly MasterControllerContext context = new();
        private TrainEquipmentAssignment equipmentAssignment;

        public event Action StateChanged;

        public MasterControllerState State => context.State;
        public int PowerPosition => context.State.powerPosition;
        public int BrakePosition => context.State.brakePosition;
        public int ServiceBrakePosition => Mathf.Min(BrakePosition, MaxServiceBrakePosition);
        public ReverserPosition ReverserPosition => context.State.reverserPosition;
        public bool IsNeutral => PowerPosition == 0 && BrakePosition == 0;
        public bool IsEmergencyBrake => BrakePosition == EmergencyBrakePosition;
        public bool IsInputEnabled => context.State.isInputEnabled;
        public int MaxPowerPosition => context.Settings.maxPowerPosition;
        public int MaxServiceBrakePosition => context.Settings.maxServiceBrakePosition;
        public int EmergencyBrakePosition => context.Settings.emergencyBrakePosition;
        public int AssignedCarIndex => ResolveEquipmentAssignment()
            ? equipmentAssignment.AssignedCarIndex
            : -1;

        private void Awake()
        {
            ResolveEquipmentAssignment();
            ApplySerializedSettings();
            MasterControllerLogic.Initialize(context, initialReverserPosition, inputEnabledOnAwake);
        }

        public void ConfigureLimits(
            int maxPower,
            int maxServiceBrake,
            int emergencyBrake)
        {
            ChangeState(() => MasterControllerLogic.ConfigureLimits(
                context,
                maxPower,
                maxServiceBrake,
                emergencyBrake));
        }

        public void SetInputEnabled(bool isEnabled)
        {
            if (context.State.isInputEnabled == isEnabled)
            {
                return;
            }

            context.State.isInputEnabled = isEnabled;
            StateChanged?.Invoke();
        }

        public void SetPowerPosition(int position)
        {
            ChangeState(() => MasterControllerLogic.SetPowerPosition(context, position));
        }

        public void SetBrakePosition(int position)
        {
            ChangeState(() => MasterControllerLogic.SetBrakePosition(context, position));
        }

        public void SetServiceBrakePosition(int position)
        {
            ChangeState(() => MasterControllerLogic.SetServiceBrakePosition(context, position));
        }

        public void SetEmergencyBrake()
        {
            ChangeState(() => MasterControllerLogic.SetEmergencyBrake(context));
        }

        public void SetNeutral()
        {
            ChangeState(() => MasterControllerLogic.SetNeutral(context));
        }

        public bool SetReverserPosition(ReverserPosition position)
        {
            bool changed = false;
            bool accepted = false;
            ChangeState(() =>
            {
                accepted = MasterControllerLogic.TrySetReverserPosition(context, position);
                changed = accepted;
            }, () => changed);
            return accepted;
        }

        public void MoveOneStepTowardBrake()
        {
            ChangeState(() => MasterControllerLogic.MoveOneStepTowardBrake(context));
        }

        public void MoveOneStepTowardPower()
        {
            ChangeState(() => MasterControllerLogic.MoveOneStepTowardPower(context));
        }

        public void StepTowardNeutral()
        {
            ChangeState(() => MasterControllerLogic.StepTowardNeutral(context));
        }

        public void StepTowardServiceMaxBrake()
        {
            ChangeState(() => MasterControllerLogic.StepTowardServiceMaxBrake(context));
        }

        private void ApplySerializedSettings()
        {
            maxPowerPosition = Mathf.Max(1, maxPowerPosition);
            maxServiceBrakePosition = Mathf.Max(1, maxServiceBrakePosition);
            emergencyBrakePosition = Mathf.Max(maxServiceBrakePosition + 1, emergencyBrakePosition);
            MasterControllerLogic.ConfigureLimits(
                context,
                maxPowerPosition,
                maxServiceBrakePosition,
                emergencyBrakePosition);
        }

        private void ChangeState(Action change, Func<bool> shouldNotify = null)
        {
            int previousPower = PowerPosition;
            int previousBrake = BrakePosition;
            ReverserPosition previousReverser = ReverserPosition;
            bool previousInputEnabled = IsInputEnabled;

            change();

            bool changed = previousPower != PowerPosition ||
                previousBrake != BrakePosition ||
                previousReverser != ReverserPosition ||
                previousInputEnabled != IsInputEnabled;
            if (changed && (shouldNotify == null || shouldNotify()))
            {
                StateChanged?.Invoke();
            }
        }

        private void OnValidate()
        {
            maxPowerPosition = Mathf.Max(1, maxPowerPosition);
            maxServiceBrakePosition = Mathf.Max(1, maxServiceBrakePosition);
            emergencyBrakePosition = Mathf.Max(maxServiceBrakePosition + 1, emergencyBrakePosition);
            if (Application.isPlaying)
            {
                ApplySerializedSettings();
            }

            ResolveEquipmentAssignment();
        }

        private bool ResolveEquipmentAssignment()
        {
            if (equipmentAssignment == null)
            {
                equipmentAssignment = GetComponent<TrainEquipmentAssignment>();
            }

            return equipmentAssignment != null;
        }
    }
}
