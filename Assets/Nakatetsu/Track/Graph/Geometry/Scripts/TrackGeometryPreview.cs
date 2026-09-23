using UnityEngine.Serialization;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Nakatetsu.Track.Graph.Geometry
{
    // SceneビューでAssetの座標を表示する。このTransformは位置計算に使わない。
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.Graph.Geometry", sourceAssembly: "Nakatetsu.Track", sourceClassName: "TrainGeometryPreview")]
    public sealed class TrackGeometryPreview : MonoBehaviour
    {
        [FormerlySerializedAs("guideLine")]
        [FormerlySerializedAs("trainGeometry")]
        [SerializeField] private TrackGeometryAsset trackGeometry;
        [SerializeField, Min(0.1f)] private float sampleIntervalM = 5f;
        [SerializeField] private Color lineColor = Color.cyan;
        [SerializeField, Min(0f)] private float probeDistanceM;
        private const int MaximumSegments = 10000;

        private void OnDrawGizmos()
        {
            if (trackGeometry == null || !trackGeometry.TryEvaluate(0f, out TrackSample previous))
            {
                return;
            }

            float length = trackGeometry.Definition.lengthM;
            float interval = float.IsNaN(sampleIntervalM) || float.IsInfinity(sampleIntervalM)
                ? 5f : Mathf.Max(0.1f, sampleIntervalM);
            int count = Mathf.CeilToInt(Mathf.Min(MaximumSegments, length / interval));
            count = Mathf.Max(1, count);
            Color oldColor = Gizmos.color;
            Gizmos.color = lineColor;

            for (int i = 1; i <= count; i++)
            {
                if (!trackGeometry.TryEvaluate(length * ((float)i / count), out TrackSample current))
                {
                    break;
                }

                Gizmos.DrawLine(previous.Position, current.Position);
                previous = current;
            }

            if (trackGeometry.TryEvaluate(probeDistanceM, out TrackSample probe))
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
