using System;
using System.Collections.Generic;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcPositionLogic
    {
        internal static bool InitializePosition(
            TrainAtcContext context,
            TrainAtcPosition frontPosition,
            TrainAtcPosition rearPosition)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var state = context.State.position;
            if (state.isPositionInitialized ||
                !IsValidPosition(context.atcEdgesById, frontPosition) ||
                !IsValidPosition(context.atcEdgesById, rearPosition))
            {
                return false;
            }

            // 初期位置と編成の向きは一度だけ設定する。外から渡された位置とは別に保持する。
            state.frontPosition = CopyPosition(frontPosition);
            state.rearPosition = CopyPosition(rearPosition);
            state.isPositionInitialized = true;
            state.isPositionKnown = true;
            return true;
        }

        internal static bool CorrectPosition(
            TrainAtcContext context,
            TrainAtcPosition frontPosition,
            TrainAtcPosition rearPosition)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var state = context.State.position;
            if (!state.isPositionInitialized ||
                !IsValidPosition(context.atcEdgesById, frontPosition) ||
                !IsValidPosition(context.atcEdgesById, rearPosition))
            {
                return false;
            }

            // 位置不明からの復帰は、両端の位置を明示的に補正した場合だけ認める。
            state.frontPosition = CopyPosition(frontPosition);
            state.rearPosition = CopyPosition(rearPosition);
            state.isPositionKnown = true;
            return true;
        }

        internal static void UpdatePosition(TrainAtcContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var state = context.State.position;
            if (!state.isPositionInitialized || !state.isPositionKnown)
            {
                // 位置不明になった後は、TrackSampleから自動で取り直さない。
                state.isPositionKnown = false;
                return;
            }

            if (!TryGetMovementDistance(context.Input, out double signedDistanceM))
            {
                state.isPositionKnown = false;
                return;
            }

            // 運転台やキーの状態によらず、固定前側・固定後側の両端を更新する。
            var retainedPath = context.State.pattern.atcEdgePath;
            bool frontUpdated = TryAdvancePosition(context, state.frontPosition,
                context.Input.frontTelegram, retainedPath, signedDistanceM, out var frontPosition);
            bool rearUpdated = TryAdvancePosition(context, state.rearPosition,
                context.Input.rearTelegram, retainedPath, signedDistanceM, out var rearPosition);
            if (!frontUpdated || !rearUpdated)
            {
                // 片側だけ更新しない。最後に確認できた両端の位置は記録として残す。
                state.isPositionKnown = false;
                return;
            }

            state.frontPosition = frontPosition;
            state.rearPosition = rearPosition;
        }

        private static bool TryGetMovementDistance(TrainAtcInput input, out double signedDistanceM)
        {
            signedDistanceM = 0d;
            if (float.IsNaN(input.deltaTimeSeconds) || float.IsInfinity(input.deltaTimeSeconds) ||
                input.deltaTimeSeconds < 0f)
            {
                return false;
            }

            // 初期化直後など、経過時間がない場合は移動させない。
            if (input.deltaTimeSeconds == 0f)
            {
                return true;
            }

            if (!input.hasSpeedMeasurement || float.IsNaN(input.signedSpeedMps) ||
                float.IsInfinity(input.signedSpeedMps))
            {
                return false;
            }

            signedDistanceM = (double)input.signedSpeedMps * input.deltaTimeSeconds;
            return true;
        }

        private static bool TryAdvancePosition(
            TrainAtcContext context,
            TrainAtcPosition previousPosition,
            TrackCircuitAtcTelegram telegram,
            IReadOnlyList<string> retainedPath,
            double signedDistanceM,
            out TrainAtcPosition position)
        {
            position = null;
            if (!IsValidPosition(context.atcEdgesById, previousPosition))
            {
                return false;
            }

            position = CopyPosition(previousPosition);
            if (signedDistanceM == 0d)
            {
                return true;
            }

            bool movesToConsistFront = signedDistanceM > 0d;
            double remainingDistanceM = Math.Abs(signedDistanceM);
            IReadOnlyList<string> atcEdgePath = retainedPath;

            // 一度の更新で複数のEdgeを越える場合も順に解決する。異常な循環は打ち切る。
            for (int i = 0; i <= context.atcEdgesById.Count; i++)
            {
                if (!TryGetAtcEdge(context.atcEdgesById, position.atcEdgeId, out var edge))
                {
                    return false;
                }

                bool movesAtoB = position.frontFacesAtoB == movesToConsistFront;
                double distanceToEndM = position.distanceOnAtcEdgeM;
                if (movesAtoB)
                {
                    distanceToEndM = edge.lengthM - position.distanceOnAtcEdgeM;
                }

                if (remainingDistanceM < distanceToEndM)
                {
                    if (movesAtoB)
                    {
                        position.distanceOnAtcEdgeM = (float)(position.distanceOnAtcEdgeM + remainingDistanceM);
                    }
                    else
                    {
                        position.distanceOnAtcEdgeM = (float)(position.distanceOnAtcEdgeM - remainingDistanceM);
                    }
                    return true;
                }

                string exitNodeId = edge.atcNodeAId;
                var direction = TrackAtcTravelDirection.BtoA;
                if (movesAtoB)
                {
                    exitNodeId = edge.atcNodeBId;
                    direction = TrackAtcTravelDirection.AtoB;
                }

                if (!TryResolveNextAtcEdge(context, telegram, edge.atcEdgeId, direction,
                    exitNodeId, ref atcEdgePath, out var nextEdge))
                {
                    // 次のEdgeが不明でも、ちょうど終端までの移動なら現在位置は特定できる。
                    if (remainingDistanceM == distanceToEndM)
                    {
                        position.distanceOnAtcEdgeM = 0f;
                        if (movesAtoB)
                        {
                            position.distanceOnAtcEdgeM = edge.lengthM;
                        }
                        return true;
                    }
                    return false;
                }

                remainingDistanceM -= distanceToEndM;
                bool entersAtNodeA = nextEdge.atcNodeAId == exitNodeId;
                if (entersAtNodeA && nextEdge.atcNodeBId == exitNodeId)
                {
                    return false;
                }

                position.atcEdgeId = nextEdge.atcEdgeId;
                position.distanceOnAtcEdgeM = nextEdge.lengthM;
                if (entersAtNodeA)
                {
                    position.distanceOnAtcEdgeM = 0f;
                }
                position.frontFacesAtoB = entersAtNodeA == movesToConsistFront;

                if (remainingDistanceM == 0d)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveNextAtcEdge(
            TrainAtcContext context,
            TrackCircuitAtcTelegram telegram,
            string atcEdgeId,
            TrackAtcTravelDirection direction,
            string exitNodeId,
            ref IReadOnlyList<string> atcEdgePath,
            out TrackAtcGraphEdge nextEdge)
        {
            nextEdge = null;
            if (string.IsNullOrWhiteSpace(exitNodeId))
            {
                return false;
            }

            // ここでは分岐先の解決にだけ電文を使う。無信号の猶予・採用可否は工程4で判定する。
            if (telegram != null && telegram.isValid &&
                telegram.atcRouteInfomation.TryGetValue((atcEdgeId, direction), out var route) &&
                route != null && TryResolveNextOnPath(context.atcEdgesById, route.atcEdgePath,
                    atcEdgeId, exitNodeId, out nextEdge))
            {
                // 同じ更新で次のEdgeも越える場合は、読み取った経路を引き継ぐ。
                atcEdgePath = route.atcEdgePath;
                return true;
            }

            if (TryResolveNextOnPath(context.atcEdgesById, atcEdgePath,
                atcEdgeId, exitNodeId, out nextEdge))
            {
                return true;
            }

            return TryResolveNextOnGraph(context, atcEdgeId, exitNodeId, out nextEdge);
        }

        private static bool TryResolveNextOnPath(
            IReadOnlyDictionary<string, TrackAtcGraphEdge> atcEdgesById,
            IReadOnlyList<string> atcEdgePath,
            string atcEdgeId,
            string exitNodeId,
            out TrackAtcGraphEdge nextEdge)
        {
            nextEdge = null;
            if (atcEdgePath == null)
            {
                return false;
            }

            int currentIndex = -1;
            for (int i = 0; i < atcEdgePath.Count; i++)
            {
                if (atcEdgePath[i] != atcEdgeId)
                {
                    continue;
                }
                if (currentIndex >= 0)
                {
                    // 同じIDが経路内に複数ある場合は、現在位置を一意に決められない。
                    return false;
                }
                currentIndex = i;
            }

            if (currentIndex < 0)
            {
                return false;
            }

            // 電文の順序と逆向きに移動する場合も、接続する手前側のEdgeを選べるようにする。
            for (int i = currentIndex - 1; i <= currentIndex + 1; i += 2)
            {
                if (i < 0 || i >= atcEdgePath.Count)
                {
                    continue;
                }
                if (!TryGetAtcEdge(atcEdgesById, atcEdgePath[i], out var candidate))
                {
                    return false;
                }
                if (candidate.atcNodeAId != exitNodeId && candidate.atcNodeBId != exitNodeId)
                {
                    continue;
                }
                if (nextEdge != null)
                {
                    return false;
                }
                nextEdge = candidate;
            }

            return nextEdge != null;
        }

        private static bool TryResolveNextOnGraph(
            TrainAtcContext context,
            string atcEdgeId,
            string exitNodeId,
            out TrackAtcGraphEdge nextEdge)
        {
            nextEdge = null;
            int candidateCount = 0;
            foreach (var edge in context.atcEdgesById.Values)
            {
                if (edge == null || edge.atcEdgeId == atcEdgeId)
                {
                    continue;
                }
                if (edge.atcNodeAId != exitNodeId && edge.atcNodeBId != exitNodeId)
                {
                    continue;
                }
                candidateCount++;
                nextEdge = edge;
            }

            if (candidateCount == 1)
            {
                return TryGetAtcEdge(context.atcEdgesById, nextEdge.atcEdgeId, out nextEdge);
            }
            if (candidateCount == 0)
            {
                return false;
            }

            return TryResolveNextOnRoute(context, atcEdgeId, exitNodeId, out nextEdge);
        }

        private static bool TryResolveNextOnRoute(
            TrainAtcContext context,
            string atcEdgeId,
            string exitNodeId,
            out TrackAtcGraphEdge nextEdge)
        {
            nextEdge = null;
            if (context.Graph == null || context.Graph.routes == null)
            {
                return false;
            }

            // 開通状態によらず、定義済み進路の行き先が一つなら分岐を解決する。
            foreach (var route in context.Graph.routes)
            {
                if (route == null || !TryResolveNextOnPath(context.atcEdgesById,
                    route.atcEdgeIds, atcEdgeId, exitNodeId, out var candidate))
                {
                    continue;
                }
                if (nextEdge != null && nextEdge.atcEdgeId != candidate.atcEdgeId)
                {
                    return false;
                }
                nextEdge = candidate;
            }

            return nextEdge != null;
        }

        private static bool IsValidPosition(
            IReadOnlyDictionary<string, TrackAtcGraphEdge> atcEdgesById,
            TrainAtcPosition position)
        {
            if (position == null || float.IsNaN(position.distanceOnAtcEdgeM) ||
                float.IsInfinity(position.distanceOnAtcEdgeM))
            {
                return false;
            }

            return TryGetAtcEdge(atcEdgesById, position.atcEdgeId, out var edge) &&
                position.distanceOnAtcEdgeM >= 0f && position.distanceOnAtcEdgeM <= edge.lengthM;
        }

        private static bool TryGetAtcEdge(
            IReadOnlyDictionary<string, TrackAtcGraphEdge> atcEdgesById,
            string atcEdgeId,
            out TrackAtcGraphEdge edge)
        {
            edge = null;
            if (atcEdgesById == null || string.IsNullOrWhiteSpace(atcEdgeId) ||
                !atcEdgesById.TryGetValue(atcEdgeId, out edge))
            {
                return false;
            }

            return edge != null && edge.atcEdgeId == atcEdgeId &&
                !float.IsNaN(edge.lengthM) && !float.IsInfinity(edge.lengthM) && edge.lengthM > 0f;
        }

        private static TrainAtcPosition CopyPosition(TrainAtcPosition position)
        {
            return new TrainAtcPosition
            {
                atcEdgeId = position.atcEdgeId,
                distanceOnAtcEdgeM = position.distanceOnAtcEdgeM,
                frontFacesAtoB = position.frontFacesAtoB
            };
        }
    }
}
