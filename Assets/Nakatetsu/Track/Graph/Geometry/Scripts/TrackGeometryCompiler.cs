using System;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry
{
    public static class TrackGeometryCompiler
    {
        // 検証済みの定義を読み込んだ後や編集した後に、評価用の開始位置を作り直す。
        public static void Rebuild(TrackGeometryDefinition definition, TrackGeometryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var workspace = context.Workspace;
            workspace.horizontalStarts.Clear();
            workspace.verticalStarts.Clear();

            if (definition == null)
            {
                return;
            }

            Vector3 position = definition.originPosition;
            Quaternion rotation = TrackGeometryCalculator.GetPlanRotation(definition.originRotation);
            if (definition.horizontalSegments != null)
            {
                for (int i = 0; i < definition.horizontalSegments.Count; i++)
                {
                    var segment = definition.horizontalSegments[i];
                    if (segment == null)
                    {
                        continue;
                    }

                    float start = Mathf.Max(0f, segment.startDistanceM);
                    float length = Mathf.Max(0f, segment.lengthM);
                    workspace.horizontalStarts.Add(new TrackGeometryHorizontalStart
                    {
                        segmentIndex = i, startDistanceM = start, endDistanceM = start + length,
                        position = position, rotation = rotation
                    });

                    segment.EvaluatePosition(start + length, out Vector3 localPosition, out float angle);
                    position += rotation * localPosition;
                    rotation *= Quaternion.Euler(0f, angle, 0f);
                }
            }

            float height = 0f;
            float previousEnd = 0f;
            float previousDerivative = 0f;
            if (definition.verticalSegments == null)
            {
                return;
            }

            for (int i = 0; i < definition.verticalSegments.Count; i++)
            {
                var segment = definition.verticalSegments[i];
                if (segment == null || segment.lengthM <= 0.001f)
                {
                    continue;
                }

                float start = Mathf.Max(0f, segment.startDistanceM);
                height += previousDerivative * Mathf.Max(0f, start - previousEnd);
                workspace.verticalStarts.Add(new TrackGeometryVerticalStart
                {
                    segmentIndex = i, startDistanceM = start,
                    endDistanceM = start + segment.lengthM, heightM = height
                });
                height += segment.EvaluateHeightDeltaM(start + segment.lengthM);
                previousEnd = start + segment.lengthM;
                previousDerivative = segment.EvaluateDerivative(previousEnd);
            }
        }
    }
}
