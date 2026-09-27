using Nakatetsu.Track.Graph;

namespace Nakatetsu.Train.Simulation.TrackPosition
{
    public static class TrainTrackPositionLogic
    {
        public const int guard = 256;

        public static void Calculate(TrainTrackPositionContext context, TrackGraphContext graph,
            TrainTrackConnectionResolver resolveNextEdge)
        {
            if (context == null || graph == null
                || !graph.TryGetEdge(context.State.currentEdgeId, out var edge)
                || !TrainTrackPathLogic.IsValidEdge(edge)
                || !TrainTrackPathLogic.IsFinite(context.State.distanceOnEdgeM)
                || !TrainTrackPathLogic.IsFinite(context.Input.signedDisplacementM))
            {
                context?.Output.Invalidate();
                return;
            }

            double distance = context.State.distanceOnEdgeM
                + (context.State.frontFacesAtoB
                    ? (double)context.Input.signedDisplacementM
                    : -(double)context.Input.signedDisplacementM);
            if (distance < float.MinValue || distance > float.MaxValue)
            {
                context.Output.Invalidate();
                return;
            }

            context.State.distanceOnEdgeM = (float)distance;
            AdvanceEdgeIfNeeded(context, graph, resolveNextEdge);
            TrainTrackSamplingLogic.RefreshOutput(context, graph, resolveNextEdge);
        }

        // 現在の分岐接続を使って越境先へ進める。接続不能なら境界で止める。
        public static void AdvanceEdgeIfNeeded(TrainTrackPositionContext context, TrackGraphContext graph,
            TrainTrackConnectionResolver resolveNextEdge)
        {
            if (context == null || graph == null
                || !graph.TryGetEdge(context.State.currentEdgeId, out var edge)
                || !TrainTrackPathLogic.IsValidEdge(edge)
                || !TrainTrackPathLogic.IsFinite(context.State.distanceOnEdgeM))
            {
                context?.Output.Invalidate();
                return;
            }

            for (int transitions = 0; transitions <= guard; transitions++)
            {
                float distance = context.State.distanceOnEdgeM;
                if (distance >= 0f && distance <= edge.LengthM)
                {
                    return;
                }

                bool exitsAtB = distance > edge.LengthM;
                bool towardFront = exitsAtB == context.State.frontFacesAtoB;
                float overflow = exitsAtB ? distance - edge.LengthM : -distance;
                context.State.distanceOnEdgeM = exitsAtB ? edge.LengthM : 0f;
                if (transitions == guard
                    || !TrainTrackPathLogic.TryGetAdjacent(graph, resolveNextEdge, edge,
                        context.State.frontFacesAtoB, towardFront, out var next,
                        out bool nextFacesAtoB, out float entryDistanceM))
                {
                    return;
                }

                edge = next;
                context.State.currentEdgeId = next.edgeId;
                context.State.frontFacesAtoB = nextFacesAtoB;
                context.State.distanceOnEdgeM = entryDistanceM == 0f
                    ? overflow : edge.LengthM - overflow;
            }
        }
    }
}
