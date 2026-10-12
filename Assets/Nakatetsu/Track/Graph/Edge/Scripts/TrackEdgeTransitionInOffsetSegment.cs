using System;

namespace Nakatetsu.Track.Graph.Edge
{
    [Serializable]
    public sealed class TrackEdgeTransitionInOffsetSegment : TrackEdgeTransitionOffsetSegment
    {
        protected override bool IsTransitionIn => true;
    }
}
