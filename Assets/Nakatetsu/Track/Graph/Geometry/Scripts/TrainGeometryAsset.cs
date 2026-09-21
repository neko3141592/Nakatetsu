using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Nakatetsu.Track.TrainGeometry
{
    [CreateAssetMenu(fileName = "TrainGeometry", menuName = "Nakatetsu/Track/Train Geometry")]
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.GuideLine", sourceAssembly: "Nakatetsu.Track", sourceClassName: "GuideLineAsset")]
    public sealed class TrainGeometryAsset : ScriptableObject
    {
        [SerializeField] private TrainGeometryDefinition definition = new TrainGeometryDefinition();

        /// <summary>Fixed authoring data. Do not modify it during simulation.</summary>
        public TrainGeometryDefinition Definition => definition;

        public bool TryEvaluate(float distanceM, out TrainGeometrySample sample)
        {
            return TrainGeometryCalculator.TryEvaluate(definition, distanceM, out sample);
        }
    }
}
