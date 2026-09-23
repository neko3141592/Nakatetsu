using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Nakatetsu.Track.Graph.Geometry
{
    [Serializable]
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.Graph.Geometry", sourceAssembly: "Nakatetsu.Track", sourceClassName: "TrainGeometryDefinition")]
    public sealed class TrackGeometryDefinition : ISerializationCallbackReceiver
    {
        public string displayName;
        [FormerlySerializedAs("guideLineId")]
        [FormerlySerializedAs("trainGeometryId")]
        public string trackGeometryId;
        [Min(0f)] public float lengthM;

        public Vector3 originPosition;
        public Quaternion originRotation = Quaternion.identity;

        [SerializeReference] private List<TrackGeometryHorizontalSegment> horizontalSegmentDefinitions = new();

        // 旧形式のインライン値はSerializeReferenceで読めないため、別名で受け取る。
        [SerializeField, HideInInspector, FormerlySerializedAs("horizontalSegments")]
        private List<LegacyTrackGeometryHorizontalSegment> legacyHorizontalSegments;

        public List<TrackGeometryHorizontalSegment> horizontalSegments
        {
            get => horizontalSegmentDefinitions;
            set
            {
                horizontalSegmentDefinitions = value;
                legacyHorizontalSegments = null;
            }
        }

        [SerializeReference] private List<TrackGeometryVerticalSegment> verticalSegmentDefinitions = new();

        [SerializeField, HideInInspector, FormerlySerializedAs("verticalSegments")]
        private List<LegacyTrackGeometryVerticalSegment> legacyVerticalSegments;

        public List<TrackGeometryVerticalSegment> verticalSegments
        {
            get => verticalSegmentDefinitions;
            set
            {
                verticalSegmentDefinitions = value;
                legacyVerticalSegments = null;
            }
        }

        // Geometry距離から位置と一次微分を取得する。区間の共有端点では手前側を採用する。
        public bool TryEvaluateAtGeometryDistance(float distanceOnGeometryM, out Vector3 position,
            out Vector3 derivative)
        {
            return TrackGeometryCalculator.TryEvaluateAtGeometryDistance(this, distanceOnGeometryM,
                out position, out derivative);
        }

        // 位置と一次微分に加え、正規化しない二階微分を取得する。
        public bool TryEvaluateAtGeometryDistance(float distanceOnGeometryM, out Vector3 position,
            out Vector3 derivative, out Vector3 secondDerivative)
        {
            return TrackGeometryCalculator.TryEvaluateAtGeometryDistance(this, distanceOnGeometryM,
                out position, out derivative, out secondDerivative);
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            MigrateHorizontalSegments();
            MigrateVerticalSegments();
        }

        private void MigrateVerticalSegments()
        {
            if (legacyVerticalSegments == null || legacyVerticalSegments.Count == 0)
            {
                return;
            }

            if (verticalSegmentDefinitions == null || verticalSegmentDefinitions.Count == 0)
            {
                verticalSegmentDefinitions = new List<TrackGeometryVerticalSegment>(legacyVerticalSegments.Count);
                foreach (var legacy in legacyVerticalSegments)
                {
                    verticalSegmentDefinitions.Add(legacy == null ? null : new TrackGeometryLinearGradientSegment
                    {
                        startDistanceM = legacy.startDistanceM,
                        lengthM = legacy.lengthM,
                        startGradientPermille = legacy.startGradientPermille,
                        endGradientPermille = legacy.endGradientPermille
                    });
                }
            }

            legacyVerticalSegments = null;
        }

        private void MigrateHorizontalSegments()
        {
            if (legacyHorizontalSegments == null || legacyHorizontalSegments.Count == 0)
            {
                return;
            }

            if (horizontalSegmentDefinitions == null || horizontalSegmentDefinitions.Count == 0)
            {
                horizontalSegmentDefinitions = new List<TrackGeometryHorizontalSegment>(legacyHorizontalSegments.Count);
                foreach (var legacy in legacyHorizontalSegments)
                {
                    if (legacy == null)
                    {
                        horizontalSegmentDefinitions.Add(null);
                        continue;
                    }

                    TrackGeometryHorizontalSegment segment;
                    switch (legacy.trackCurveType)
                    {
                        case TrackGeometryCurveType.Curve:
                            segment = new TrackGeometryCircularSegment { radiusM = legacy.radiusM };
                            break;
                        case TrackGeometryCurveType.TransitionIn:
                            segment = new TrackGeometryTransitionInSegment { radiusM = legacy.radiusM };
                            break;
                        case TrackGeometryCurveType.TransitionOut:
                            segment = new TrackGeometryTransitionOutSegment { radiusM = legacy.radiusM };
                            break;
                        default:
                            // 不明な列挙値は旧評価処理と同じく直線として扱う。
                            segment = new TrackGeometryStraightSegment();
                            break;
                    }

                    segment.startDistanceM = legacy.startDistanceM;
                    segment.lengthM = legacy.lengthM;
                    horizontalSegmentDefinitions.Add(segment);
                }
            }

            legacyHorizontalSegments = null;
        }
    }
}
