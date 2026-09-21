using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("guideLineId")]
        public string trainGeometryId;
        [FormerlySerializedAs("startDistanceOnGuideLineM")]
        public float startDistanceOnTrainGeometryM;
        [FormerlySerializedAs("endDistanceOnGuideLineM")]
        public float endDistanceOnTrainGeometryM;

        public float LengthM => Math.Abs(endDistanceOnTrainGeometryM - startDistanceOnTrainGeometryM);
    }
}
