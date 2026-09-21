using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Nakatetsu.Track.TrainGeometry
{
    [Serializable]
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.GuideLine", sourceAssembly: "Nakatetsu.Track", sourceClassName: "GuideLineDefinition")]
    public sealed class TrainGeometryDefinition
    {
        public string displayName;
        [FormerlySerializedAs("guideLineId")]
        public string trainGeometryId;
        [Min(0f)] public float lengthM;

        public Vector3 originPosition;
        public Quaternion originRotation = Quaternion.identity;

        public List<TrainGeometryHorizontalSegment> horizontalSegments = new();
        public List<TrainGeometryVerticalSegment> verticalSegments = new();
    }
}
