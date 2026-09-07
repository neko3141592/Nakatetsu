using System.Collections.Generic;
using UnityEngine;

namespace Nakatetsu.Track.GuideLine
{
    public sealed class GuideLineContext
    {
        public GuideLineWorkspace Workspace { get; } = new GuideLineWorkspace();
    }

    public sealed class GuideLineWorkspace
    {
        public readonly List<GuideLineHorizontalStart> horizontalStarts = new();
        public readonly List<GuideLineVerticalStart> verticalStarts = new();
    }

    public struct GuideLineHorizontalStart
    {
        public int segmentIndex;
        public float startDistanceM;
        public float endDistanceM;
        // Horizontal pose: height and pitch are evaluated separately.
        public Vector3 position;
        public Quaternion rotation;
    }

    public struct GuideLineVerticalStart
    {
        public int segmentIndex;
        public float startDistanceM;
        public float endDistanceM;
        // Height relative to the GuideLine origin.
        public float heightM;
    }
}
