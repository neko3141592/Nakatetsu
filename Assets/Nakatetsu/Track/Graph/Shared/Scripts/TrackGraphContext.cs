using System.Collections.Generic;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Graph.Geometry;
using Nakatetsu.Track.Graph.Node;

namespace Nakatetsu.Track.Graph
{
    /// <summary>Runtime ID lookups. Referenced authoring definitions must not be modified during simulation.</summary>
    public sealed class TrackGraphContext
    {
        private readonly Dictionary<string, TrackNodeDefinition> nodesById = new();
        private readonly Dictionary<string, TrackEdgeDefinition> edgesById = new();
        private readonly Dictionary<string, TrackGeometryDefinition> geometriesById = new();

        public bool TryGetNode(string id, out TrackNodeDefinition node)
        {
            node = null;
            return !string.IsNullOrWhiteSpace(id) && nodesById.TryGetValue(id, out node);
        }

        public bool TryGetEdge(string id, out TrackEdgeDefinition edge)
        {
            edge = null;
            return !string.IsNullOrWhiteSpace(id) && edgesById.TryGetValue(id, out edge);
        }

        public bool TryGetGeometry(string id, out TrackGeometryDefinition geometry)
        {
            geometry = null;
            return !string.IsNullOrWhiteSpace(id) && geometriesById.TryGetValue(id, out geometry);
        }

        internal void Rebuild(TrackGraphDefinition definition)
        {
            nodesById.Clear();
            edgesById.Clear();
            geometriesById.Clear();
            if (definition == null) return;

            if (definition.nodes != null)
                foreach (var node in definition.nodes)
                    if (node != null && !string.IsNullOrEmpty(node.nodeId)) nodesById[node.nodeId] = node;
            if (definition.edges != null)
                foreach (var edge in definition.edges)
                    if (edge != null && !string.IsNullOrEmpty(edge.edgeId)) edgesById[edge.edgeId] = edge;
            if (definition.geometries != null)
                foreach (var geometry in definition.geometries)
                    if (geometry != null && !string.IsNullOrEmpty(geometry.trackGeometryId))
                        geometriesById[geometry.trackGeometryId] = geometry;
        }
    }
}
