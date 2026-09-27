using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using UnityEngine;

namespace Nakatetsu.Track.Simulation.Circuit
{
    [DisallowMultipleComponent]
    public sealed class TrackCircuitSimulationController : MonoBehaviour
    {
        [SerializeField] private TrackGraphController trackGraphController;
        private readonly TrackCircuitSimulationContext context = new();
        private readonly List<ITrackOccupancySource> sources = new();
        private readonly List<TrackOccupiedEdgeSpan> sourceEdges = new();
        private readonly List<TrackOccupiedEdgeSpan> allOccupiedEdges = new();
        public TrackCircuitSimulationContext Context => context;

        public void SetTrackGraphController(TrackGraphController controller)
        {
            trackGraphController = controller;
            TrackCircuitSimulationLogic.Calculate(context, controller != null ? controller.Context : null, null);
        }

        public bool RegisterSource(ITrackOccupancySource source)
        {
            if (source == null || source is Object unityObject && unityObject == null
                || sources.Contains(source))
            {
                return false;
            }

            sources.Add(source);
            TrackCircuitSimulationLogic.Calculate(context,
                trackGraphController != null ? trackGraphController.Context : null, null);
            return true;
        }

        // 登録された列車に直接問い合わせ、同じ時点の占有状態を確定する。
        public void RefreshOccupancy()
        {
            TrackGraphContext graph = trackGraphController != null ? trackGraphController.Context : null;
            bool complete = graph != null && graph.IsInitialized && sources.Count > 0;
            allOccupiedEdges.Clear();
            if (complete)
            {
                foreach (ITrackOccupancySource source in sources)
                {
                    if (source == null || source is Object unityObject && unityObject == null)
                    {
                        complete = false;
                        break;
                    }

                    sourceEdges.Clear();
                    if (!source.TryGetOccupiedEdges(sourceEdges))
                    {
                        complete = false;
                        break;
                    }

                    allOccupiedEdges.AddRange(sourceEdges);
                }
            }

            TrackCircuitSimulationLogic.Calculate(context, graph,
                complete ? allOccupiedEdges : null);
        }

        public bool IsOccupied(string circuitId) => context.State.IsOccupied(circuitId);
    }
}
