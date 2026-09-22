using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Nakatetsu.Track.Graph.Geometry
{
    [CreateAssetMenu(fileName = "TrackGeometry", menuName = "Nakatetsu/Track/Track Geometry")]
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.Graph.Geometry", sourceAssembly: "Nakatetsu.Track", sourceClassName: "TrainGeometryAsset")]
    public sealed class TrackGeometryAsset : ScriptableObject
    {
        [SerializeField] private TrackGeometryDefinition definition = new TrackGeometryDefinition();

        /// <summary>Fixed authoring data. Do not modify it during simulation.</summary>
        public TrackGeometryDefinition Definition => definition;

        public bool TryEvaluate(float distanceM, out TrackGeometrySample sample)
        {
            return TrackGeometryCalculator.TryEvaluate(definition, distanceM, out sample);
        }
    }
}
