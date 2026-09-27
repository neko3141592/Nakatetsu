using System;
using System.Collections.Generic;

namespace Nakatetsu.Track.Graph.Circuit
{
    [Serializable]
    public sealed class TrackCircuitDefinition
    {
        public string circuitId;
        public List<TrackCircuitSection> sections = new();
    }

    [Serializable]
    public sealed class TrackCircuitSection
    {
        public string edgeId;
        public float startDistanceOnGeometryM;
        public float endDistanceOnGeometryM;
    } 
}
