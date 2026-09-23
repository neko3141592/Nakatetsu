using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Edge;

namespace Nakatetsu.Train.Simulation.TrackPosition
{
    public static class TrainTrackSamplingLogic
    {
        public static void ConfigureLayout(TrainTrackPositionContext context, IReadOnlyList<float> carLengthsM)
        {
            var offsets = new double[carLengthsM.Count];
            double totalLength = 0;

            for (int i = 0; i < carLengthsM.Count; i++)
            {
                offsets[i] = carLengthsM[0] * 0.5d - totalLength - carLengthsM[i] * 0.5d;
                totalLength += carLengthsM[i];
            }

            context.Settings.CarCenterOffsetsM = offsets;
            context.Settings.FrontExtentM = carLengthsM[0] * 0.5d;
            context.Settings.RearExtentM = context.Settings.FrontExtentM - totalLength;
            context.Workspace.ResetPath();
        }

        // 現在位置と保持経路を読むだけで、列車や転轍機の状態は変更しない。
        public static bool TryGetTrackSample(TrainTrackPositionContext context, TrackGraphContext graph,
            int carIndex, float offsetFromCarCenterM, out TrainTrackSample sample)
        {
            sample = default;
            if (carIndex < 0 || carIndex >= context.Settings.CarCount ||
                !TrainTrackPathLogic.IsFinite(offsetFromCarCenterM))
            {
                return false;
            }

            var workspace = context.Workspace;
            if (workspace.Path.Count == 0)
            {
                return false;
            }

            int index = workspace.ReferenceIndex;
            var entry = workspace.Path[index];
            var state = context.State;
            if (entry.EdgeId != state.currentEdgeId || entry.FrontFacesAtoB != state.frontFacesAtoB ||
                !(state.distanceOnEdgeM >= 0f && state.distanceOnEdgeM <= entry.LengthM))
            {
                return false;
            }

            // 基準車中心からの距離を、編成前方向に並べた経路上の距離に直す。
            double distance = entry.FrontFacesAtoB ? state.distanceOnEdgeM : (double)entry.LengthM - state.distanceOnEdgeM;
            distance += context.Settings.CarCenterOffsetsM[carIndex] + offsetFromCarCenterM;

            while (distance < 0d)
            {
                if (--index < 0)
                {
                    return false;
                }

                entry = workspace.Path[index];
                distance += entry.LengthM;
            }

            while (distance > entry.LengthM)
            {
                distance -= entry.LengthM;
                if (++index >= workspace.Path.Count)
                {
                    return false;
                }

                entry = workspace.Path[index];
            }

            float edgeDistance = (float)(entry.FrontFacesAtoB ? distance : entry.LengthM - distance);
            if (!TrackEdgeCalculator.TryEvaluate(graph, entry.EdgeId, edgeDistance, out var trackSample))
            {
                return false;
            }

            sample = new TrainTrackSample(carIndex, offsetFromCarCenterM, entry.EdgeId,
                entry.FrontFacesAtoB, trackSample);
            return true;
        }
    }
}
