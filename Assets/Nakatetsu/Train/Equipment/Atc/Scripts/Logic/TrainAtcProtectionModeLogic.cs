using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcProtectionModeLogic
    {
        internal static void UpdateProtectionMode(TrainAtcContext context)
        {
            var protection = context.State.protectionMode;
            var validation = context.State.validation;
            bool hasNewOpeningOperation = ConsumeDoorOpeningOperation(context);

            if (validation.result == TrainAtcValidationResult.Retain)
            {
                // 保持するパターンと同じ方式を使う。方式の写しは他のStateへ置かない。
                return;
            }

            protection.isProtectionModeKnown = false;
            if (validation.result != TrainAtcValidationResult.Adopt)
            {
                return;
            }

            var mode = validation.routeInfomation.overrunProtectionMode;
            if ((mode != OverrunProtectionMode.Normal &&
                 mode != OverrunProtectionMode.Restricted && mode != OverrunProtectionMode.None) ||
                !TryGetStopDirection(context, out var stopDirection))
            {
                return;
            }

            string stopAtcEdgeId = validation.routeInfomation.stopAtcEdgeId;
            if (protection.hasHeldOrp &&
                (protection.heldStopAtcEdgeId != stopAtcEdgeId ||
                 protection.heldStopTravelDirection != stopDirection))
            {
                protection.hasHeldOrp = false;
            }

            if (mode == OverrunProtectionMode.None && protection.hasHeldOrp)
            {
                mode = protection.heldProtectionMode;
            }
            else if (mode != OverrunProtectionMode.None && !hasNewOpeningOperation)
            {
                // 明示された防護方式で更新し、Noneになった後も同じ着点・方向の間だけ使う。
                protection.hasHeldOrp = true;
                protection.heldProtectionMode = mode;
                protection.heldStopAtcEdgeId = stopAtcEdgeId;
                protection.heldStopTravelDirection = stopDirection;
            }

            protection.overrunProtectionMode = mode;
            protection.isProtectionModeKnown = true;
        }

        private static bool ConsumeDoorOpeningOperation(TrainAtcContext context)
        {
            var input = context.Input;
            var protection = context.State.protectionMode;
            if (!input.hasDoorOpeningOperation ||
                input.doorOpeningOperationRevision == protection.doorOpeningOperationRevision)
            {
                return false;
            }

            // キー切・無信号・入力不正中の操作も確認し、受信復帰後に古い保持を戻さない。
            protection.doorOpeningOperationRevision = input.doorOpeningOperationRevision;
            protection.hasHeldOrp = false;
            return true;
        }

        private static bool TryGetStopDirection(
            TrainAtcContext context, out TrackAtcTravelDirection direction)
        {
            var route = context.State.validation.routeInfomation;
            var operation = context.State.operation;
            var path = route.atcEdgePath;
            direction = operation.currentTravelDirection;
            if (path == null || path.Count == 0 || path[0] != operation.currentPosition.atcEdgeId ||
                route.stopAtcEdgeId != path[path.Count - 1] ||
                (direction != TrackAtcTravelDirection.AtoB && direction != TrackAtcTravelDirection.BtoA))
            {
                return false;
            }

            // 受電器の現在方向ではなく、接続をたどって求めた停止限界Edgeの方向で比較する。
            string entryNodeId = null;
            var usedEdgeIds = new HashSet<string>();
            for (int i = 0; i < path.Count; i++)
            {
                string edgeId = path[i];
                if (string.IsNullOrEmpty(edgeId) || !usedEdgeIds.Add(edgeId) ||
                    !context.atcEdgesById.TryGetValue(edgeId, out var edge) || edge == null)
                {
                    return false;
                }
                if (i > 0)
                {
                    bool entersAtA = edge.atcNodeAId == entryNodeId;
                    bool entersAtB = edge.atcNodeBId == entryNodeId;
                    if (string.IsNullOrEmpty(entryNodeId) || entersAtA == entersAtB)
                    {
                        return false;
                    }
                    direction = entersAtA ? TrackAtcTravelDirection.AtoB : TrackAtcTravelDirection.BtoA;
                }

                entryNodeId = direction == TrackAtcTravelDirection.AtoB ? edge.atcNodeBId : edge.atcNodeAId;
            }
            return true;
        }
    }
}
