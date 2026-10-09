using UnityEngine;

namespace Nakatetsu.Track.Interlocking
{
    [CreateAssetMenu(fileName = "TrackInterlocking", menuName = "Nakatetsu/Track/Interlocking")]
    public sealed class TrackStationInterlockingAsset : ScriptableObject
    {
        [SerializeField] private TrackStationInterlockingDefinition definition = new();

        public TrackStationInterlockingDefinition Definition => definition;
    }
}
