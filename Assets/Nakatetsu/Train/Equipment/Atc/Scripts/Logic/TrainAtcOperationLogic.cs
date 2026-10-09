using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Equipment.Operation;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcOperationLogic
    {
        internal static void UpdateOperation(TrainAtcContext context)
        {
            var operation = context.State.operation;

            // 電源と速度照査の有効状態は別に扱う。前回の選択結果は残さない。
            operation.isAtcPowerOn = true;
            operation.hasCabState = false;
            operation.cab = default;
            operation.isAtcEnabled = false;
            ClearReceiverSelection(operation);

            if (!TryCaptureCabState(context.Input, out var cab))
            {
                return;
            }

            operation.hasCabState = true;
            operation.cab = cab;
            operation.isAtcEnabled = cab.isKeyInserted &&
                (cab.reverserPosition == ReverserPosition.Forward ||
                 cab.reverserPosition == ReverserPosition.Reverse);

            if (!operation.isAtcEnabled)
            {
                return;
            }

            SelectReceiver(context);
        }

        private static bool TryCaptureCabState(TrainAtcInput input, out TrainAtcCabInput cab)
        {
            cab = default;
            if (!input.hasCabState || input.cab.carIndex < 0 ||
                (input.cab.isFrontCab && input.cab.carIndex != 0) ||
                input.cab.reverserPosition < ReverserPosition.Reverse ||
                input.cab.reverserPosition > ReverserPosition.Forward)
            {
                return false;
            }

            if (input.hasCarMasses && input.cars.Count > 0)
            {
                // 有効運転台は編成の両端。後運転台も車両数と対応していることを確認する。
                int expectedCarIndex = 0;
                if (!input.cab.isFrontCab)
                {
                    expectedCarIndex = input.cars.Count - 1;
                }
                if (input.cab.carIndex != expectedCarIndex)
                {
                    return false;
                }
            }

            cab = input.cab;
            return true;
        }

        private static void SelectReceiver(TrainAtcContext context)
        {
            var operation = context.State.operation;

            // 旧ATCと同じく、有効運転台側の受電器を使用する。
            if (operation.cab.isFrontCab)
            {
                operation.selectedReceiver = TrainAtcReceiverSide.Front;
                operation.currentTelegram = context.Input.frontTelegram;
            }
            else
            {
                operation.selectedReceiver = TrainAtcReceiverSide.Rear;
                operation.currentTelegram = context.Input.rearTelegram;
            }
        }

        internal static void UpdateCurrentPosition(TrainAtcContext context)
        {
            var operation = context.State.operation;
            var position = context.State.position;
            operation.hasCurrentPosition = false;
            operation.currentPosition = null;
            operation.currentTravelDirection = TrackAtcTravelDirection.Unspecified;

            if (!operation.isAtcEnabled || !position.isPositionInitialized)
            {
                return;
            }

            // 位置更新後の選択端だけを確認し、非使用側の不明を照査へ持ち込まない。
            TrainAtcPosition receiverPosition;
            bool isPositionKnown;
            if (operation.selectedReceiver == TrainAtcReceiverSide.Front)
            {
                receiverPosition = position.frontPosition;
                isPositionKnown = position.isFrontPositionKnown;
            }
            else if (operation.selectedReceiver == TrainAtcReceiverSide.Rear)
            {
                receiverPosition = position.rearPosition;
                isPositionKnown = position.isRearPositionKnown;
            }
            else
            {
                return;
            }

            if (!isPositionKnown || receiverPosition == null)
            {
                return;
            }

            // PositionStateと同じオブジェクトを変更してしまわないよう、値をコピーする。
            operation.currentPosition = new TrainAtcPosition
            {
                atcEdgeId = receiverPosition.atcEdgeId,
                distanceOnAtcEdgeM = receiverPosition.distanceOnAtcEdgeM,
                frontFacesAtoB = receiverPosition.frontFacesAtoB
            };
            operation.hasCurrentPosition = true;

            bool travelsTowardFront = operation.cab.isFrontCab;
            if (operation.cab.reverserPosition == ReverserPosition.Reverse)
            {
                travelsTowardFront = !travelsTowardFront;
            }

            bool travelsAtoB = travelsTowardFront == receiverPosition.frontFacesAtoB;
            if (travelsAtoB)
            {
                operation.currentTravelDirection = TrackAtcTravelDirection.AtoB;
            }
            else
            {
                operation.currentTravelDirection = TrackAtcTravelDirection.BtoA;
            }
        }

        private static void ClearReceiverSelection(TrainAtcOperationState operation)
        {
            operation.selectedReceiver = TrainAtcReceiverSide.None;
            operation.hasCurrentPosition = false;
            operation.currentPosition = null;
            operation.currentTravelDirection = TrackAtcTravelDirection.Unspecified;
            operation.currentTelegram = null;
        }
    }
}
