using UnityEngine.Serialization;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Nakatetsu.Track.TrainGeometry
{
    /// <summary>Scene-view preview. Coordinates come from the asset, not this Transform.</summary>
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.GuideLine", sourceAssembly: "Nakatetsu.Track", sourceClassName: "GuideLinePreview")]
    public sealed class TrainGeometryPreview : MonoBehaviour
    {
        [FormerlySerializedAs("guideLine")]
        [SerializeField] private TrainGeometryAsset trainGeometry;
        [SerializeField, Min(0.1f)] private float sampleIntervalM = 5f;
        [SerializeField] private Color lineColor = Color.cyan;
        [SerializeField, Min(0f)] private float probeDistanceM;
        private const int MaximumSegments = 10000;

        private void OnDrawGizmos()
        {
            if (trainGeometry == null || !trainGeometry.TryEvaluate(0f, out TrainGeometrySample previous)) return;
            float length = trainGeometry.Definition.lengthM;
            float interval = float.IsNaN(sampleIntervalM) || float.IsInfinity(sampleIntervalM)
                ? 5f : Mathf.Max(0.1f, sampleIntervalM);
            int count = Mathf.CeilToInt(Mathf.Min(MaximumSegments, length / interval));
            count = Mathf.Max(1, count);
            Color oldColor = Gizmos.color;
            Gizmos.color = lineColor;
            for (int i = 1; i <= count; i++)
            {
                if (!trainGeometry.TryEvaluate(length * ((float)i / count), out TrainGeometrySample current)) break;
                Gizmos.DrawLine(previous.Position, current.Position);
                previous = current;
            }

            if (trainGeometry.TryEvaluate(probeDistanceM, out TrainGeometrySample probe))
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(probe.Position, probe.Position + probe.Rotation * Vector3.up * 2f);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(probe.Position, probe.Position + probe.Rotation * Vector3.right * 2f);
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(probe.Position, probe.Position + probe.Tangent * 2f);
            }
            Gizmos.color = oldColor;
        }
    }
}
