using UnityEngine;
using System;

namespace Nakatetsu.Train.Operation.CabActivationSwitch
{
    public enum CabActivationPosition
    {
        Rear = -1,
        Off = 0,
        Front = 1
    }

    [DisallowMultipleComponent]
    public sealed class CabActivationSwitchController : MonoBehaviour
    {
        [SerializeField]
        private CabActivationPosition initialPosition =
            CabActivationPosition.Off;

        public CabActivationPosition Position { get; private set; }

        public event Action<CabActivationPosition> PositionChanged;

        private void Awake()
        {
            Position = initialPosition;
        }

        public void SetPosition(CabActivationPosition position)
        {
            if (Position == position)
            {
                return;
            }

            Position = position;
            PositionChanged?.Invoke(position);
        }

        public void MoveTowardFront()
        {
            SetPosition(Position switch
            {
                CabActivationPosition.Rear => CabActivationPosition.Off,
                CabActivationPosition.Off => CabActivationPosition.Front,
                _ => CabActivationPosition.Front
            });
        }

        public void MoveTowardRear()
        {
            SetPosition(Position switch
            {
                CabActivationPosition.Front => CabActivationPosition.Off,
                CabActivationPosition.Off => CabActivationPosition.Rear,
                _ => CabActivationPosition.Rear
            });
        }
    }
}