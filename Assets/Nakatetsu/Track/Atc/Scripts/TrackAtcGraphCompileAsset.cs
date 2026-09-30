using UnityEngine;

namespace Nakatetsu.Track.Atc
{
    [CreateAssetMenu(fileName = "TrackAtcGraphSource", menuName = "Nakatetsu/Track/ATC Graph Source")]
    public sealed class TrackAtcGraphCompileAsset : ScriptableObject
    {
        [SerializeField] private TrackAtcGraphCompileDefinition definition = new();

        public TrackAtcGraphCompileDefinition Definition => definition;
    }
}
