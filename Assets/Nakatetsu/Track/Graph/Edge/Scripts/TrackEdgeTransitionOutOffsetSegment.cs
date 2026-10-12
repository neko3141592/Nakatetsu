using System;

namespace Nakatetsu.Track.Graph.Edge
{
    [Serializable]
    public sealed class TrackEdgeTransitionOutOffsetSegment : TrackEdgeTransitionOffsetSegment
    {
        protected override bool IsTransitionIn => false;
    }
}
