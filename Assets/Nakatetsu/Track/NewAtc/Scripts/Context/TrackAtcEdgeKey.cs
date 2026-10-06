using System;
using Nakatetsu.Track.Graph.Edge;

namespace Nakatetsu.Track.NewAtc
{
    // ATC Edgeと、そのEdge上の進行方向を識別するキー。
    public readonly struct TrackAtcEdgeKey : IEquatable<TrackAtcEdgeKey>
    {
        public readonly string atcEdgeId;
        public readonly TrackEdgeTravelDirection direction;

        public TrackAtcEdgeKey(string atcEdgeId, TrackEdgeTravelDirection direction)
        {
            this.atcEdgeId = atcEdgeId;
            this.direction = direction;
        }

        public bool Equals(TrackAtcEdgeKey other)
        {
            return atcEdgeId == other.atcEdgeId && direction == other.direction;
        }

        public override bool Equals(object obj)
        {
            return obj is TrackAtcEdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(atcEdgeId, direction);
        }

        public static bool operator ==(TrackAtcEdgeKey left, TrackAtcEdgeKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TrackAtcEdgeKey left, TrackAtcEdgeKey right)
        {
            return !left.Equals(right);
        }
    }
}
