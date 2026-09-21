using System;
using UnityEngine;

namespace Nakatetsu.Track.TrainGeometry
{
    public static class TrainGeometryCompiler
    {
        /// <summary>Rebuild after loading or editing an ordered, valid definition.</summary>
        public static void Rebuild(TrainGeometryDefinition definition, TrainGeometryContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var workspace = context.Workspace;
            workspace.horizontalStarts.Clear();
            workspace.verticalStarts.Clear();
            if (definition == null) return;

            Vector3 position = definition.originPosition;
            Quaternion rotation = TrainGeometryCalculator.GetPlanRotation(definition.originRotation);
            if (definition.horizontalSegments != null)
            {
                for (int i = 0; i < definition.horizontalSegments.Count; i++)
                {
                    var segment = definition.horizontalSegments[i];
                    if (segment == null) continue;
                    float start = Mathf.Max(0f, segment.startDistanceM);
                    float length = Mathf.Max(0f, segment.lengthM);
                    workspace.horizontalStarts.Add(new TrainGeometryHorizontalStart
                    {
                        segmentIndex = i, startDistanceM = start, endDistanceM = start + length,
                        position = position, rotation = rotation
                    });
                    TrainGeometryCalculator.CalculateHorizontal(segment.trackCurveType, length, length,
                        segment.radiusM, out float x, out float z, out float angle);
                    position += rotation * new Vector3(x, 0f, z);
                    rotation *= Quaternion.Euler(0f, angle, 0f);
                }
            }

            float height = 0f;
            float previousEnd = 0f;
            float previousGradient = 0f;
            if (definition.verticalSegments == null) return;
            for (int i = 0; i < definition.verticalSegments.Count; i++)
            {
                var segment = definition.verticalSegments[i];
                if (segment == null || segment.lengthM <= 0.001f) continue;
                float start = Mathf.Max(0f, segment.startDistanceM);
                height += previousGradient * Mathf.Max(0f, start - previousEnd) / 1000f;
                workspace.verticalStarts.Add(new TrainGeometryVerticalStart
                {
                    segmentIndex = i, startDistanceM = start,
                    endDistanceM = start + segment.lengthM, heightM = height
                });
                height += TrainGeometryProfileCalculator.GetHeightDeltaM(segment.lengthM,
                    segment.lengthM, segment.startGradientPermille, segment.endGradientPermille);
                previousEnd = start + segment.lengthM;
                previousGradient = segment.endGradientPermille;
            }
        }
    }
}
