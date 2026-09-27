using UnityEngine;

namespace Nakatetsu.Track.Interlocking
{
    [CreateAssetMenu(fileName = "TrackInterlocking", menuName = "Nakatetsu/Track/Interlocking")]
    public sealed class TrackInterlockingAsset : ScriptableObject
    {
        [SerializeField] private TrackInterlockingDefinition definition = new();

        public TrackInterlockingDefinition Definition => definition;
    }
}
