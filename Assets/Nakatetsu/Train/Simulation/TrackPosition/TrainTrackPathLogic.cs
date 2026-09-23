using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Edge;

namespace Nakatetsu.Train.Simulation.TrackPosition
{
    public delegate bool TrainTrackConnectionResolver(string nodeId, string incomingEdgeId, out string nextEdgeId);

    internal static class TrainTrackPathLogic
    {
        internal static void EnsureReferencePath(TrainTrackPositionContext context, TrackEdgeDefinition edge)
        {
            var workspace = context.Workspace;
            if (workspace.ReferenceIndex >= 0 && workspace.ReferenceIndex < workspace.Path.Count)
            {
                var current = workspace.Path[workspace.ReferenceIndex];
                if (current.EdgeId == edge.edgeId && current.FrontFacesAtoB == context.State.frontFacesAtoB &&
                    current.LengthM == edge.LengthM)
                {
                    return;
                }
            }

            workspace.ResetPath();
            workspace.Path.Add(new TrainTrackPathEdge(edge.edgeId, context.State.frontFacesAtoB, edge.LengthM));
        }

        // 編成下の経路を保持し、通過済みのEdgeを捨て、新たに必要なEdgeだけ追加する。
        internal static void PrepareSamplingPath(TrainTrackPositionContext context, TrackGraphContext graph,
            TrainTrackConnectionResolver resolveNextEdge)
        {
            if (!graph.TryGetEdge(context.State.currentEdgeId, out var edge))
            {
                return;
            }

            EnsureReferencePath(context, edge);
            var workspace = context.Workspace;

            // 基準点を0として、保持経路の後端と前端の位置を求める。
            double start = context.State.frontFacesAtoB
                ? -context.State.distanceOnEdgeM : (double)context.State.distanceOnEdgeM - edge.LengthM;
            for (int i = 0; i < workspace.ReferenceIndex; i++)
            {
                start -= workspace.Path[i].LengthM;
            }

            double end = start;
            foreach (var entry in workspace.Path)
            {
                end += entry.LengthM;
            }

            // 編成全体が通り過ぎたEdgeを経路から外す。
            while (workspace.ReferenceIndex > 0 &&
                start + workspace.Path[0].LengthM <= context.Settings.RearExtentM)
            {
                start += workspace.Path[0].LengthM;
                workspace.Path.RemoveAt(0);
                workspace.ReferenceIndex--;
            }

            while (workspace.ReferenceIndex < workspace.Path.Count - 1 &&
                end - workspace.Path[^1].LengthM >= context.Settings.FrontExtentM)
            {
                end -= workspace.Path[^1].LengthM;
                workspace.Path.RemoveAt(workspace.Path.Count - 1);
            }

            // 車体位置の取得に必要な範囲だけ経路を延ばす。
            for (int i = 0; start > context.Settings.RearExtentM && i < TrainTrackPositionLogic.guard; i++)
            {
                if (!TryExtendPath(workspace, false, graph, resolveNextEdge))
                {
                    break;
                }

                start -= workspace.Path[0].LengthM;
            }

            for (int i = 0; end < context.Settings.FrontExtentM && i < TrainTrackPositionLogic.guard; i++)
            {
                if (!TryExtendPath(workspace, true, graph, resolveNextEdge))
                {
                    break;
                }

                end += workspace.Path[^1].LengthM;
            }
        }

        internal static bool TryExtendPath(TrainTrackPositionWorkspace workspace, bool forward,
            TrackGraphContext graph, TrainTrackConnectionResolver resolveNextEdge)
        {
            var from = forward ? workspace.Path[^1] : workspace.Path[0];
            if (!graph.TryGetEdge(from.EdgeId, out var edge))
            {
                return false;
            }

            string nodeId = forward == from.FrontFacesAtoB ? edge.nodeBId : edge.nodeAId;
            if (!resolveNextEdge(nodeId, edge.edgeId, out var nextId) || nextId == edge.edgeId ||
                !graph.TryGetEdge(nextId, out var next))
            {
                return false;
            }

            bool entersAtA = next.nodeAId == nodeId;
            bool entersAtB = next.nodeBId == nodeId;
            if (entersAtA == entersAtB || !IsFinite(next.LengthM) || next.LengthM <= 0f)
            {
                return false;
            }

            // 経路は常に編成後方から前方の順で保持する。
            var entry = new TrainTrackPathEdge(nextId, forward ? entersAtA : entersAtB, next.LengthM);
            if (forward)
            {
                workspace.Path.Add(entry);
            }
            else
            {
                workspace.Path.Insert(0, entry);
                workspace.ReferenceIndex++;
            }

            return true;
        }

        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
