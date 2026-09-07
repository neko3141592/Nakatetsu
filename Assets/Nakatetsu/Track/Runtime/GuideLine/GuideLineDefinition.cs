using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nakatetsu.Track.GuideLine
{
    [Serializable]
    public sealed class GuideLineDefinition
    {
        public string displayName;
        public string guideLineId;
        [Min(0f)] public float lengthM;
        [Min(0.001f)] public float gaugeM = 1.067f;

        public Vector3 originPosition;
        public Quaternion originRotation = Quaternion.identity;

        public List<TrackHorizontalSegment> horizontalSegments = new();
        public List<TrackVerticalSegment> verticalSegments = new();
        public List<TrackCantSegment> cantSegments = new();
    }
}
