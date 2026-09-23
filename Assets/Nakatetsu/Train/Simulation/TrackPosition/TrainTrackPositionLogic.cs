using Nakatetsu.Track.Graph;

namespace Nakatetsu.Train.Simulation.TrackPosition
{
    public static class TrainTrackPositionLogic
    {
        public const int guard = 256;

        public static void Calculate(TrainTrackPositionContext context, TrackGraphContext graph,
            TrainTrackConnectionResolver resolveNextEdge)
        {
            if (!graph.TryGetEdge(context.State.currentEdgeId, out var edge) ||
                !TrainTrackPathLogic.IsFinite(edge.LengthM) || edge.LengthM <= 0f)
            {
                return;
            }

            TrainTrackPathLogic.PrepareSamplingPath(context, graph, resolveNextEdge);

            float distance = context.State.distanceOnEdgeM +
                (context.State.frontFacesAtoB ? context.Input.signedDisplacementM : -context.Input.signedDisplacementM);
            if (!TrainTrackPathLogic.IsFinite(distance))
            {
                return;
            }

            context.State.distanceOnEdgeM = distance;
            AdvanceEdgeIfNeeded(context, graph, resolveNextEdge);
            TrainTrackPathLogic.PrepareSamplingPath(context, graph, resolveNextEdge);
        }

        // 境界を越えた距離を次Edgeへ繰り越す。進めない場合は境界で止める。
        public static void AdvanceEdgeIfNeeded(TrainTrackPositionContext context, TrackGraphContext graph,
            TrainTrackConnectionResolver resolveNextEdge)
        {
            if (!graph.TryGetEdge(context.State.currentEdgeId, out var edge) ||
                !TrainTrackPathLogic.IsFinite(edge.LengthM) || edge.LengthM <= 0f)
            {
                return;
            }

            TrainTrackPathLogic.EnsureReferencePath(context, edge);
            var workspace = context.Workspace;

            for (int transitions = 0; ; transitions++)
            {
                float distance = context.State.distanceOnEdgeM;
                if (distance >= 0f && distance <= edge.LengthM)
                {
                    return;
                }

                bool exitsAtB = distance > edge.LengthM;
                bool forward = exitsAtB == context.State.frontFacesAtoB;

                // 境界を越えた分を、接続先Edgeの距離に引き継ぐ。
                float overflow = exitsAtB ? distance - edge.LengthM : -distance;

                context.State.distanceOnEdgeM = exitsAtB ? edge.LengthM : 0f;
                if (transitions >= guard)
                {
                    return;
                }

                int nextIndex = workspace.ReferenceIndex + (forward ? 1 : -1);
                if (nextIndex < 0 || nextIndex >= workspace.Path.Count)
                {
                    if (!TrainTrackPathLogic.TryExtendPath(workspace, forward, graph, resolveNextEdge))
                    {
                        return;
                    }

                    nextIndex = workspace.ReferenceIndex + (forward ? 1 : -1);
                }

                var next = workspace.Path[nextIndex];
                if (!graph.TryGetEdge(next.EdgeId, out edge))
                {
                    return;
                }

                // 編成前方向を保ったまま、接続先のNode A起点の距離に変換する。
                context.State.currentEdgeId = next.EdgeId;
                context.State.frontFacesAtoB = next.FrontFacesAtoB;
                context.State.distanceOnEdgeM = forward == next.FrontFacesAtoB ? overflow : next.LengthM - overflow;
                workspace.ReferenceIndex = nextIndex;
            }
        }
    }
}
