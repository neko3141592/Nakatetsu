using Nakatetsu.Train.Integration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nakatetsu.Application.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class TrainKeyboardInputController : MonoBehaviour
    {
        [SerializeField] private TrainIntegrationController target;

        public void SetTarget(TrainIntegrationController integration)
        {
            target = integration;
        }

        public void OnNotchTowardBrake(InputAction.CallbackContext context)
        {
            if (CanOperate(context))
            {
                target.Input.MoveNotchTowardBrake();
            }
        }

        public void OnNotchTowardPower(InputAction.CallbackContext context)
        {
            if (CanOperate(context))
            {
                target.Input.MoveNotchTowardPower();
            }
        }

        public void OnNotchNeutral(InputAction.CallbackContext context)
        {
            if (CanOperate(context))
            {
                target.Input.SetNeutral();
            }
        }

        public void OnReverserForward(InputAction.CallbackContext context)
        {
            if (CanOperate(context))
            {
                target.Input.MoveReverserForward();
            }
        }

        public void OnReverserBackward(InputAction.CallbackContext context)
        {
            if (CanOperate(context))
            {
                target.Input.MoveReverserBackward();
            }
        }

        public void OnEbReset(InputAction.CallbackContext context)
        {
            if (CanOperate(context))
            {
                target.Input.ResetEb();
            }
        }

        private bool CanOperate(InputAction.CallbackContext context)
        {
            return isActiveAndEnabled && context.performed &&
                target != null && target.isActiveAndEnabled && target.Input != null;
        }
    }
}
