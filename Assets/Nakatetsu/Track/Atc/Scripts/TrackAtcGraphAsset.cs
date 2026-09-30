using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Interlocking;
using UnityEngine;

namespace Nakatetsu.Track.Atc
{
    [CreateAssetMenu(fileName = "TrackAtcGraph", menuName = "Nakatetsu/Track/ATC Graph")]
    public sealed class TrackAtcGraphAsset : ScriptableObject
    {
        [SerializeField] private TrackGraphAsset trackGraph;
        [SerializeField] private TrackAtcGraphCompileAsset source;
        [SerializeField] private List<TrackInterlockingAsset> interlockings = new();
        [SerializeField, HideInInspector] private TrackAtcGraphDefinition definition;

        public TrackGraphAsset TrackGraph => trackGraph;
        public TrackAtcGraphCompileAsset Source => source;
        public IReadOnlyList<TrackInterlockingAsset> Interlockings => interlockings;
        public TrackAtcGraphDefinition Definition => definition;

#if UNITY_EDITOR
        public void SetCompiledDefinition(TrackAtcGraphDefinition compiled) => definition = compiled;
#endif
    }
}
