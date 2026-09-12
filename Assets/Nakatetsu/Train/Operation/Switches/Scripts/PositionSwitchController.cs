using System;
using UnityEngine;

namespace Nakatetsu.Train.Operation.Switches
{
    [DisallowMultipleComponent]
    public sealed class PositionSwitchController : MonoBehaviour
    {
        [Header("Position range")]
        [SerializeField] private int minimumPosition;
        [SerializeField] private int maximumPosition = 1;

        [Header("Initial state")]
        [SerializeField] private int initialPosition;

        [Header("Spring return")]
        [SerializeField] private bool hasSpringReturn;
        [SerializeField] private int springReturnPosition;

        private readonly PositionSwitchContext context = new PositionSwitchContext();

        public event Action<int> PositionChanged;

        public PositionSwitchState State => context.State;
        public int Position => context.State.position;
        public int MinimumPosition => context.Settings.minimumPosition;
        public int MaximumPosition => context.Settings.maximumPosition;
        public bool HasSpringReturn => context.Settings.hasSpringReturn;
        public int SpringReturnPosition => context.Settings.springReturnPosition;

        private void Awake()
        {
            ApplySerializedSettings();
            PositionSwitchLogic.Initialize(context, initialPosition);
        }

        public void Configure(
            int firstPosition,
            int lastPosition,
            int initial,
            bool springReturn = false,
            int returnPosition = 0)
        {
            minimumPosition = Math.Min(firstPosition, lastPosition);
            maximumPosition = Math.Max(firstPosition, lastPosition);
            initialPosition = initial;
            hasSpringReturn = springReturn;
            springReturnPosition = returnPosition;

            int previousPosition = Position;
            ApplySerializedSettings();
            PositionSwitchLogic.Initialize(context, initialPosition);
            NotifyIfChanged(previousPosition);
        }

        public bool SetPosition(int position)
        {
            bool changed = PositionSwitchLogic.SetPosition(context, position);
            if (changed)
            {
                PositionChanged?.Invoke(Position);
            }
            return changed;
        }

        public bool MoveOneStepTowardMaximum() => MoveOneStep(1);

        public bool MoveOneStepTowardMinimum() => MoveOneStep(-1);

        public bool Release()
        {
            bool changed = PositionSwitchLogic.Release(context);
            if (changed)
            {
                PositionChanged?.Invoke(Position);
            }
            return changed;
        }

        private bool MoveOneStep(int direction)
        {
            bool changed = PositionSwitchLogic.MoveOneStep(context, direction);
            if (changed)
            {
                PositionChanged?.Invoke(Position);
            }
            return changed;
        }

        private void ApplySerializedSettings()
        {
            PositionSwitchLogic.Configure(
                context,
                minimumPosition,
                maximumPosition,
                hasSpringReturn,
                springReturnPosition);

            minimumPosition = context.Settings.minimumPosition;
            maximumPosition = context.Settings.maximumPosition;
            springReturnPosition = context.Settings.springReturnPosition;
            initialPosition = Mathf.Clamp(initialPosition, minimumPosition, maximumPosition);
        }

        private void NotifyIfChanged(int previousPosition)
        {
            if (previousPosition != Position)
            {
                PositionChanged?.Invoke(Position);
            }
        }

        private void OnValidate()
        {
            int firstPosition = minimumPosition;
            minimumPosition = Math.Min(firstPosition, maximumPosition);
            maximumPosition = Math.Max(firstPosition, maximumPosition);
            initialPosition = Mathf.Clamp(initialPosition, minimumPosition, maximumPosition);
            springReturnPosition = Mathf.Clamp(
                springReturnPosition,
                minimumPosition,
                maximumPosition);

            if (Application.isPlaying)
            {
                int previousPosition = Position;
                ApplySerializedSettings();
                NotifyIfChanged(previousPosition);
            }
        }
    }
}
