using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Edge;

namespace Nakatetsu.Train.Simulation.TrackPosition
{
    public delegate bool TrainTrackConnectionResolver(string nodeId, string incomingEdgeId, out string nextEdgeId);

    // 経路は保存せず、基準位置から現在のGraph接続をたどる。
    internal static class TrainTrackPathLogic
    {
        internal static bool TryLocateOffset(TrainTrackPositionContext context, TrackGraphContext graph,
            TrainTrackConnectionResolver resolveNextEdge, double offsetFromFirstCarCenterM,
            out TrackEdgeDefinition edge, out float distanceOnEdgeM, out bool frontFacesAtoB)
        {
            edge = null;
            distanceOnEdgeM = default;
            frontFacesAtoB = default;
            if (graph == null || resolveNextEdge == null || double.IsNaN(offsetFromFirstCarCenterM)
                || double.IsInfinity(offsetFromFirstCarCenterM)
                || !graph.TryGetEdge(context.State.currentEdgeId, out edge)
                || !IsValidEdge(edge) || !IsFinite(context.State.distanceOnEdgeM)
                || context.State.distanceOnEdgeM < 0f || context.State.distanceOnEdgeM > edge.LengthM)
            {
                return false;
            }

            frontFacesAtoB = context.State.frontFacesAtoB;
            double distance = context.State.distanceOnEdgeM
                + (frontFacesAtoB ? offsetFromFirstCarCenterM : -offsetFromFirstCarCenterM);
            for (int transitions = 0; transitions <= TrainTrackPositionLogic.guard; transitions++)
            {
                if (distance >= 0d && distance <= edge.LengthM)
                {
                    distanceOnEdgeM = (float)distance;
                    return true;
                }

                if (transitions == TrainTrackPositionLogic.guard)
                {
                    return false;
                }

                bool exitsAtB = distance > edge.LengthM;
                bool towardFront = exitsAtB == frontFacesAtoB;
                double overflow = exitsAtB ? distance - edge.LengthM : -distance;
                if (!TryGetAdjacent(graph, resolveNextEdge, edge, frontFacesAtoB, towardFront,
                    out var next, out bool nextFacesAtoB, out float entryDistanceM))
                {
                    return false;
                }

                edge = next;
                frontFacesAtoB = nextFacesAtoB;
                distance = entryDistanceM == 0f ? overflow : edge.LengthM - overflow;
            }

            return false;
        }

        internal static bool TryGetAdjacent(TrackGraphContext graph,
            TrainTrackConnectionResolver resolveNextEdge, TrackEdgeDefinition from,
            bool frontFacesAtoB, bool towardFront, out TrackEdgeDefinition next,
            out bool nextFacesAtoB, out float entryDistanceM)
        {
            next = null;
            nextFacesAtoB = default;
            entryDistanceM = default;
            if (graph == null || resolveNextEdge == null || !IsValidEdge(from))
            {
                return false;
            }

            string nodeId = towardFront == frontFacesAtoB ? from.nodeBId : from.nodeAId;
            if (!resolveNextEdge(nodeId, from.edgeId, out string nextId)
                || nextId == from.edgeId || !graph.TryGetEdge(nextId, out next) || !IsValidEdge(next))
            {
                return false;
            }

            bool entersAtA = next.nodeAId == nodeId;
            bool entersAtB = next.nodeBId == nodeId;
            if (entersAtA == entersAtB)
            {
                return false;
            }

            nextFacesAtoB = towardFront ? entersAtA : entersAtB;
            entryDistanceM = entersAtA ? 0f : next.LengthM;
            return true;
        }

        internal static bool IsValidEdge(TrackEdgeDefinition edge) =>
            edge != null && IsFinite(edge.LengthM) && edge.LengthM > 0f;

        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
