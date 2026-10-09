using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackStationInterlockingValidationState
    {
        public bool isDefinitionValid;
        public bool hasValidTime;
        internal readonly Dictionary<string, bool> availableByRouteId = new();
    }
}
