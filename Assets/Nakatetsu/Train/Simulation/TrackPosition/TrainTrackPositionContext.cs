using System;
using System.Collections.Generic;

namespace Nakatetsu.Train.Simulation.TrackPosition
{
    public sealed class TrainTrackPositionInput
    {
        public float signedDisplacementM;
    }

    public sealed class TrainTrackPositionState
    {
        public string currentEdgeId;
        public float distanceOnEdgeM;
        public bool frontFacesAtoB;
    }

    public sealed class TrainTrackPositionContext
    {
        public TrainTrackPositionInput Input { get; } = new();
        public TrainTrackPositionState State { get; } = new();
        public TrainTrackPositionSettings Settings { get; } = new();
        internal TrainTrackPositionWorkspace Workspace { get; } = new();
    }

    public sealed class TrainTrackPositionSettings
    {
        internal double[] CarCenterOffsetsM = Array.Empty<double>();
        internal double RearExtentM;
        internal double FrontExtentM;
        public int CarCount => CarCenterOffsetsM.Length;
    }

    internal readonly struct TrainTrackPathEdge
    {
        internal string EdgeId { get; }
        internal bool FrontFacesAtoB { get; }
        internal float LengthM { get; }

        internal TrainTrackPathEdge(string edgeId, bool frontFacesAtoB, float lengthM)
        {
            EdgeId = edgeId;
            FrontFacesAtoB = frontFacesAtoB;
            LengthM = lengthM;
        }
    }

    internal sealed class TrainTrackPositionWorkspace
    {
        // 編成の後方から前方へ並ぶEdgeの出現列。同じEdgeを再訪しても別要素として扱う。
        internal readonly List<TrainTrackPathEdge> Path = new();
        internal int ReferenceIndex;

        internal void ResetPath()
        {
            Path.Clear();
            ReferenceIndex = 0;
        }
    }
}
