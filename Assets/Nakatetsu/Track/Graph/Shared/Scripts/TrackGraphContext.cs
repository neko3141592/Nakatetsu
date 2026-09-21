using System.Collections.Generic;

namespace Nakatetsu.Track.Graph
{
    /// <summary>Runtime lookup tables compiled from immutable graph authoring data.</summary>
    public sealed class TrackGraphContext
    {
        private readonly Dictionary<string, TrackNodeDefinition> nodesById = new();
        private readonly Dictionary<string, TrackEdgeDefinition> edgesById = new();

        public bool TryGetNode(string id, out TrackNodeDefinition node) => nodesById.TryGetValue(id, out node);
        public bool TryGetEdge(string id, out TrackEdgeDefinition edge) => edgesById.TryGetValue(id, out edge);

        internal void Rebuild(TrackGraphDefinition definition)
        {
            nodesById.Clear();
            edgesById.Clear();
            if (definition == null) return;

            if (definition.nodes != null)
                foreach (var node in definition.nodes)
                    if (node != null && !string.IsNullOrEmpty(node.nodeId)) nodesById[node.nodeId] = node;
            if (definition.edges != null)
                foreach (var edge in definition.edges)
                    if (edge != null && !string.IsNullOrEmpty(edge.edgeId)) edgesById[edge.edgeId] = edge;
        }
    }
}
