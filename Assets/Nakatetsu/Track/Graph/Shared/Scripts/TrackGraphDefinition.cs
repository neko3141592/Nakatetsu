using System;
using System.Collections.Generic;

namespace Nakatetsu.Track.Graph
{
    [Serializable]
    public sealed class TrackGraphDefinition
    {
        public string graphId;
        public List<TrackNodeDefinition> nodes = new();
        public List<TrackEdgeDefinition> edges = new();
    }

    [Serializable]
    public sealed class TrackNodeDefinition
    {
        public string nodeId;
        public List<string> connectedEdgeIds = new();
    }

    [Serializable]
    public sealed class TrackEdgeDefinition
    {
        public string edgeId;
        public string nodeAId;
        public string nodeBId;
        public string guideLineId;
        public float startDistanceOnGuideLineM;
        public float endDistanceOnGuideLineM;

        public float LengthM => Math.Abs(endDistanceOnGuideLineM - startDistanceOnGuideLineM);
    }
}
