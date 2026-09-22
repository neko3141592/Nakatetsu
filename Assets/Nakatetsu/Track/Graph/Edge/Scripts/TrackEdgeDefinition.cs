using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

namespace Nakatetsu.Track.Graph.Edge
{
    [Serializable]
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.Graph", sourceAssembly: "Nakatetsu.Track.Graph", sourceClassName: "TrackEdgeDefinition")]
    public sealed class TrackEdgeDefinition
    {
        public string edgeId;

        public string nodeAId;
        public string nodeBId;

        [FormerlySerializedAs("guideLineId")]
        [FormerlySerializedAs("trainGeometryId")]
        [FormerlySerializedAs("trackGeometryId")]
        public string geometryId;

        [FormerlySerializedAs("startDistanceOnGuideLineM")]
        [FormerlySerializedAs("startDistanceOnTrainGeometryM")]
        [FormerlySerializedAs("startDistanceOnTrackGeometryM")]
        public float startDistanceOnGeometryM;
        [FormerlySerializedAs("endDistanceOnGuideLineM")]
        [FormerlySerializedAs("endDistanceOnTrainGeometryM")]
        [FormerlySerializedAs("endDistanceOnTrackGeometryM")]
        public float endDistanceOnGeometryM;

        // Shape-specific settings are saved in Geometry coordinates, independently of Edge orientation.
        [SerializeReference] public List<TrackEdgeOffsetSegment> offsetSegments = new();

        // Generated samples ordered from Node A to Node B. Do not modify during simulation.
        [HideInInspector] public List<TrackEdgeDistanceSample> distanceMap = new();
    }

    [Serializable]
    public struct TrackEdgeDistanceSample
    {
        public float distanceOnGeometryM;
        public float distanceOnEdgeM;
    }
}
