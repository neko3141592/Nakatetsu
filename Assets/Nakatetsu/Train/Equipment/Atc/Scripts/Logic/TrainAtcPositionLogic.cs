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
            // 共通の線路区間では選択されたATC Edgeへ対応し直す。分岐後は物理区間が一致しない。
            RebindSelectedEdge(context, telegram, position);
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
            var route = telegram != null && telegram.isValid ? telegram.GetRoute(direction) : null;
            if (route != null && TryResolveNextOnPath(context.atcEdgesById, route.atcEdgePath,
                atcEdgeId, exitNodeId, out nextEdge))
            {
                // 同じ更新で次のEdgeも越える場合は、読み取った経路を引き継ぐ。
                atcEdgePath = route.atcEdgePath;
                return true;
            }

            // 受電器が先に隣の回路へ入った場合、その回路の選択済みEdgeを使う。
            if (TryResolveSelectedOrigin(context, telegram, atcEdgeId, exitNodeId,
                out nextEdge, out route))
            {
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

        private static void RebindSelectedEdge(
            TrainAtcContext context, TrackCircuitAtcTelegram telegram, TrainAtcPosition position)
        {
            var route = telegram != null && telegram.isValid ? telegram.routeAtoB ?? telegram.routeBtoA : null;
            if (route == null || route.atcEdgePath == null || route.atcEdgePath.Count == 0 ||
                route.atcEdgePath[0] == position.atcEdgeId ||
                !TryGetAtcEdge(context.atcEdgesById, position.atcEdgeId, out var currentEdge) ||
                !TryGetAtcEdge(context.atcEdgesById, route.atcEdgePath[0], out var selectedEdge) ||
                string.IsNullOrEmpty(currentEdge.trackCircuitId) ||
                selectedEdge.trackCircuitId != currentEdge.trackCircuitId || currentEdge.physicalSpans == null)
            {
                return;
            }

            float startOnAtcEdgeM = 0f;
            foreach (var span in currentEdge.physicalSpans)
            {
                if (!IsValidSpan(span))
                {
                    return;
                }
                float lengthM = Math.Abs(span.endDistanceOnEdgeM - span.startDistanceOnEdgeM);
                if (position.distanceOnAtcEdgeM >= startOnAtcEdgeM &&
                    position.distanceOnAtcEdgeM <= startOnAtcEdgeM + lengthM)
                {
                    bool spanFacesAtoB = span.endDistanceOnEdgeM > span.startDistanceOnEdgeM;
                    float distanceOnSpanM = position.distanceOnAtcEdgeM - startOnAtcEdgeM;
                    float physicalDistanceM = span.startDistanceOnEdgeM +
                        (spanFacesAtoB ? distanceOnSpanM : -distanceOnSpanM);
                    bool physicalFrontFacesAtoB = position.frontFacesAtoB == spanFacesAtoB;
                    if (TryMapPhysicalPosition(selectedEdge, span.trackEdgeId, physicalDistanceM,
                        physicalFrontFacesAtoB, out float mappedDistanceM, out bool mappedFrontFacesAtoB))
                    {
                        position.atcEdgeId = selectedEdge.atcEdgeId;
                        position.distanceOnAtcEdgeM = mappedDistanceM;
                        position.frontFacesAtoB = mappedFrontFacesAtoB;
                        return;
                    }
                }
                startOnAtcEdgeM += lengthM;
            }
        }

        private static bool TryMapPhysicalPosition(
            TrackAtcGraphEdge edge, string trackEdgeId, float distanceOnEdgeM,
            bool frontFacesPhysicalAtoB, out float mappedDistanceM, out bool mappedFrontFacesAtoB)
        {
            mappedDistanceM = 0f;
            mappedFrontFacesAtoB = false;
            if (edge.physicalSpans == null)
            {
                return false;
            }

            float startOnAtcEdgeM = 0f;
            bool found = false;
            foreach (var span in edge.physicalSpans)
            {
                if (!IsValidSpan(span))
                {
                    return false;
                }
                float lengthM = Math.Abs(span.endDistanceOnEdgeM - span.startDistanceOnEdgeM);
                if (span.trackEdgeId == trackEdgeId &&
                    distanceOnEdgeM >= Math.Min(span.startDistanceOnEdgeM, span.endDistanceOnEdgeM) &&
                    distanceOnEdgeM <= Math.Max(span.startDistanceOnEdgeM, span.endDistanceOnEdgeM))
                {
                    if (found)
                    {
                        return false;
                    }
                    bool spanFacesAtoB = span.endDistanceOnEdgeM > span.startDistanceOnEdgeM;
                    mappedDistanceM = startOnAtcEdgeM + Math.Abs(distanceOnEdgeM - span.startDistanceOnEdgeM);
                    mappedFrontFacesAtoB = frontFacesPhysicalAtoB == spanFacesAtoB;
                    found = true;
                }
                startOnAtcEdgeM += lengthM;
            }
            return found && mappedDistanceM <= edge.lengthM;
        }

        private static bool IsValidSpan(TrackAtcPhysicalSpan span)
        {
            return span != null && !string.IsNullOrEmpty(span.trackEdgeId) &&
                !float.IsNaN(span.startDistanceOnEdgeM) && !float.IsInfinity(span.startDistanceOnEdgeM) &&
                !float.IsNaN(span.endDistanceOnEdgeM) && !float.IsInfinity(span.endDistanceOnEdgeM) &&
                span.startDistanceOnEdgeM >= 0f && span.endDistanceOnEdgeM >= 0f &&
                span.startDistanceOnEdgeM != span.endDistanceOnEdgeM;
        }

        private static bool TryResolveSelectedOrigin(
            TrainAtcContext context, TrackCircuitAtcTelegram telegram, string currentEdgeId,
            string exitNodeId, out TrackAtcGraphEdge nextEdge, out TrackCircuitAtcRouteInfomation route)
        {
            nextEdge = null;
            route = null;
            if (telegram == null || !telegram.isValid ||
                !TryGetAtcEdge(context.atcEdgesById, currentEdgeId, out var currentEdge))
            {
                return false;
            }

            for (int i = 0; i < 2; i++)
            {
                var direction = i == 0 ? TrackAtcTravelDirection.AtoB : TrackAtcTravelDirection.BtoA;
                var candidateRoute = telegram.GetRoute(direction);
                if (candidateRoute == null || candidateRoute.atcEdgePath == null ||
                    candidateRoute.atcEdgePath.Count == 0 ||
                    !TryGetAtcEdge(context.atcEdgesById, candidateRoute.atcEdgePath[0], out var candidate) ||
                    candidate.atcEdgeId == currentEdgeId || candidate.trackCircuitId == currentEdge.trackCircuitId)
                {
                    continue;
                }
                string entryNodeId = direction == TrackAtcTravelDirection.AtoB
                    ? candidate.atcNodeAId : candidate.atcNodeBId;
                if (entryNodeId == exitNodeId)
                {
                    nextEdge = candidate;
                    route = candidateRoute;
                    return true;
                }
            }
            return false;
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
            context.atcEdgesById.TryGetValue(atcEdgeId, out var currentEdge);
            foreach (var edge in context.atcEdgesById.Values)
            {
                if (edge == null || edge.atcEdgeId == atcEdgeId)
                {
                    continue;
                }
                if (currentEdge != null && !string.IsNullOrEmpty(currentEdge.trackCircuitId) &&
                    edge.trackCircuitId == currentEdge.trackCircuitId)
                {
                    // 同じ回路の定位・反位は代替経路であり、相互に続くEdgeではない。
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
