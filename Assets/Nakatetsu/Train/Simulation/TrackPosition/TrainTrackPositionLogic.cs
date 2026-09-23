using System;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Edge;

namespace Nakatetsu.Train.Simulation.TrackPosition
{
    public static class TrainTrackPositionLogic
    {

        public const int guard = 256;

        public static void Calculate(TrainTrackPositionContext context, TrackGraphController graph)
        {
            if (context.State.frontFacesAtoB)
            {
                context.State.distanceOnEdgeM += context.Input.signedDisplacementM;
            } else
            {
                context.State.distanceOnEdgeM -= context.Input.signedDisplacementM;
            }

            AdvanceEdgeIfNeeded(context, graph);
        }

        public static void AdvanceEdgeIfNeeded(TrainTrackPositionContext context, TrackGraphController graph)
        {
            if (graph.Context.TryGetEdge(context.State.currentEdgeId, out TrackEdgeDefinition edge))
            {
                return;
            }

            for (int i = 0; i < guard; i++)
            {
                if (context.State.distanceOnEdgeM > edge.LengthM)
                {

                }
                else if (context.State.distanceOnEdgeM < 0f)
                {

                }
            }


        }
    }
}
