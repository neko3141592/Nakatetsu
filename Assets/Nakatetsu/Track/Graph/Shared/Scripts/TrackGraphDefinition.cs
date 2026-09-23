using System;
using System.Collections.Generic;
using Nakatetsu.Track.Graph.Connection;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Graph.Geometry;
using Nakatetsu.Track.Graph.Node;

namespace Nakatetsu.Track.Graph
{
    [Serializable]
    public sealed class TrackGraphDefinition
    {
        public string graphId;
        public List<TrackGeometryDefinition> geometries = new();
        public List<TrackNodeDefinition> nodes = new();
        public List<TrackEdgeDefinition> edges = new();
        public List<TrackConnectionDefinition> connections = new();
    }
}
