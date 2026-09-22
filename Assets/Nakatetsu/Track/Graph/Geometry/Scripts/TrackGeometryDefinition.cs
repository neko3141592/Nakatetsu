using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Nakatetsu.Track.Graph.Geometry
{
    [Serializable]
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.Graph.Geometry", sourceAssembly: "Nakatetsu.Track", sourceClassName: "TrainGeometryDefinition")]
    public sealed class TrackGeometryDefinition
    {
        public string displayName;
        [FormerlySerializedAs("guideLineId")]
        [FormerlySerializedAs("trainGeometryId")]
        public string trackGeometryId;
        [Min(0f)] public float lengthM;

        public Vector3 originPosition;
        public Quaternion originRotation = Quaternion.identity;

        public List<TrackGeometryHorizontalSegment> horizontalSegments = new();
        public List<TrackGeometryVerticalSegment> verticalSegments = new();
    }
}
