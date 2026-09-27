using System;
using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;

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

    // 0号車中心を基準にした、編成の固定前方向への距離。
    public readonly struct TrainTrackCarOffsets
    {
        public double CenterM { get; }
        public double FrontBogieM { get; }
        public double RearBogieM { get; }

        public TrainTrackCarOffsets(double centerM, double frontBogieM, double rearBogieM)
        {
            CenterM = centerM;
            FrontBogieM = frontBogieM;
            RearBogieM = rearBogieM;
        }
    }

    public sealed class TrainTrackPositionOutput
    {
        private readonly List<TrackOccupiedEdgeSpan> occupiedEdges = new();
        private readonly IReadOnlyList<TrackOccupiedEdgeSpan> occupiedEdgesView;
        private TrainTrackSample[] frontBogies = Array.Empty<TrainTrackSample>();
        private TrainTrackSample[] rearBogies = Array.Empty<TrainTrackSample>();
        private bool hasOutput;

        public TrainTrackPositionOutput()
        {
            occupiedEdgesView = occupiedEdges.AsReadOnly();
        }

        public IReadOnlyList<TrackOccupiedEdgeSpan> OccupiedEdges => occupiedEdgesView;
        public int CarCount => hasOutput ? frontBogies.Length : 0;

        public bool TryGetBogies(int carIndex, out TrainTrackSample front, out TrainTrackSample rear)
        {
            front = default;
            rear = default;
            if (!hasOutput || carIndex < 0 || carIndex >= frontBogies.Length)
            {
                return false;
            }

            front = frontBogies[carIndex];
            rear = rearBogies[carIndex];
            return true;
        }

        public bool TryCopyOccupiedEdges(List<TrackOccupiedEdgeSpan> destination)
        {
            if (!hasOutput || destination == null)
            {
                return false;
            }

            destination.Clear();
            destination.AddRange(occupiedEdges);
            return true;
        }

        internal void BeginUpdate(int carCount)
        {
            Invalidate();
            if (frontBogies.Length != carCount)
            {
                frontBogies = new TrainTrackSample[carCount];
                rearBogies = new TrainTrackSample[carCount];
            }
        }

        internal void SetBogies(int carIndex, TrainTrackSample front, TrainTrackSample rear)
        {
            frontBogies[carIndex] = front;
            rearBogies[carIndex] = rear;
        }

        internal TrainTrackSample GetFrontBogie(int carIndex) => frontBogies[carIndex];

        internal void AddOccupiedEdge(TrackOccupiedEdgeSpan span) => occupiedEdges.Add(span);
        internal void CompleteUpdate() => hasOutput = true;

        internal void Invalidate()
        {
            hasOutput = false;
            occupiedEdges.Clear();
        }
    }

    public sealed class TrainTrackPositionContext
    {
        public TrainTrackPositionInput Input { get; } = new();
        public TrainTrackPositionState State { get; } = new();
        public TrainTrackPositionSettings Settings { get; } = new();
        public TrainTrackPositionOutput Output { get; } = new();
    }

    public sealed class TrainTrackPositionSettings
    {
        internal TrainTrackCarOffsets[] CarOffsetsArray = Array.Empty<TrainTrackCarOffsets>();
        private IReadOnlyList<TrainTrackCarOffsets> carOffsetsView =
            Array.AsReadOnly(Array.Empty<TrainTrackCarOffsets>());

        public IReadOnlyList<TrainTrackCarOffsets> CarOffsets => carOffsetsView;
        public int CarCount => CarOffsetsArray.Length;

        internal void SetCarOffsets(TrainTrackCarOffsets[] offsets)
        {
            CarOffsetsArray = offsets;
            carOffsetsView = Array.AsReadOnly(offsets);
        }
    }
}
