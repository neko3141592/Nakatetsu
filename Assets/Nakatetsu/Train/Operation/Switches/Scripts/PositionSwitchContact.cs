using System;
using UnityEngine;

namespace Nakatetsu.Train.Operation.Switches
{
    [DisallowMultipleComponent]
    public sealed class PositionSwitchContact : MonoBehaviour, ISwitchContact
    {
        [SerializeField] private PositionSwitchController positionSwitch;
        [SerializeField] private int[] closedPositions = Array.Empty<int>();
        [SerializeField] private bool invertOutput;

        public PositionSwitchController PositionSwitch => ResolvePositionSwitch()
            ? positionSwitch
            : null;
        public bool IsClosed => PositionSwitch != null && IsClosedAtPosition(
            PositionSwitch.Position,
            closedPositions,
            invertOutput);

        private void Awake()
        {
            ResolvePositionSwitch();
        }

        public void Configure(
            PositionSwitchController source,
            int[] positions,
            bool invert = false)
        {
            positionSwitch = source;
            closedPositions = positions ?? Array.Empty<int>();
            invertOutput = invert;
        }

        public static bool IsClosedAtPosition(
            int position,
            int[] positions,
            bool invert = false)
        {
            bool isListed = positions != null && Array.IndexOf(positions, position) >= 0;
            return invert ? !isListed : isListed;
        }

        private bool ResolvePositionSwitch()
        {
            if (positionSwitch == null)
            {
                positionSwitch = GetComponentInParent<PositionSwitchController>(true);
            }
            return positionSwitch != null;
        }

        private void OnValidate()
        {
            ResolvePositionSwitch();
        }
    }
}
