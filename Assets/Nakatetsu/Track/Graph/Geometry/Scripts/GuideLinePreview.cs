using UnityEngine;

namespace Nakatetsu.Track.GuideLine
{
    /// <summary>Scene-view preview. Coordinates come from the asset, not this Transform.</summary>
    public sealed class GuideLinePreview : MonoBehaviour
    {
        [SerializeField] private GuideLineAsset guideLine;
        [SerializeField, Min(0.1f)] private float sampleIntervalM = 5f;
        [SerializeField] private Color lineColor = Color.cyan;
        [SerializeField, Min(0f)] private float probeDistanceM;
        private const int MaximumSegments = 10000;

        private void OnDrawGizmos()
        {
            if (guideLine == null || !guideLine.TryEvaluate(0f, out GuideLineSample previous)) return;
            float length = guideLine.Definition.lengthM;
            float interval = float.IsNaN(sampleIntervalM) || float.IsInfinity(sampleIntervalM)
                ? 5f : Mathf.Max(0.1f, sampleIntervalM);
            int count = Mathf.CeilToInt(Mathf.Min(MaximumSegments, length / interval));
            count = Mathf.Max(1, count);
            Color oldColor = Gizmos.color;
            Gizmos.color = lineColor;
            for (int i = 1; i <= count; i++)
            {
                if (!guideLine.TryEvaluate(length * ((float)i / count), out GuideLineSample current)) break;
                Gizmos.DrawLine(previous.Position, current.Position);
                previous = current;
            }

            if (guideLine.TryEvaluate(probeDistanceM, out GuideLineSample probe))
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
