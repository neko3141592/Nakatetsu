using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using UnityEngine;

namespace Nakatetsu.World.Background
{
    public sealed class BackgroundPrefabPlacementController : MonoBehaviour
    {
        private const int MaxGeneratedPrefabs = 10_000;

        [SerializeField] private TrackGraphController trackGraphController;
        [SerializeField] private BackgroundDefinitionAsset backgroundDefinition;

        private GameObject generatedRoot;

        private void Start()
        {
            Rebuild();
        }

        [ContextMenu("Rebuild Prefab Placements (Play Mode)")]
        public void Rebuild()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (trackGraphController == null || !trackGraphController.IsInitialized || backgroundDefinition == null)
            {
                Debug.LogError("Background prefab placement requires an initialized TrackGraphController and a background asset.", this);
                return;
            }

            var placements = new List<(GameObject Prefab, Pose Pose)>();
            if (!TryCollectPlacements(trackGraphController.Context, placements, out string error))
            {
                Debug.LogError($"Background prefab placement failed: {error}", this);
                return;
            }

            ReplaceGeneratedPrefabs(placements);
        }

        private bool TryCollectPlacements(TrackGraphContext graph, List<(GameObject Prefab, Pose Pose)> placements,
            out string error)
        {
            var singles = backgroundDefinition.SinglePrefabPlacements;
            for (int i = 0; i < singles.Count; i++)
            {
                var rule = singles[i];
                if (!TryResolveGuideLine(rule, out var guideLine, out error)
                    || !TryAddPlacement(graph, guideLine, rule, rule.DistanceOnEdgeM, placements, out error))
                {
                    error = $"single placement [{i}]: {error}";
                    return false;
                }
            }

            var repeats = backgroundDefinition.RepeatedPrefabPlacements;
            for (int i = 0; i < repeats.Count; i++)
            {
                var rule = repeats[i];
                if (!TryResolveGuideLine(rule, out var guideLine, out error)
                    || !TryAddRepeatedPlacements(graph, guideLine, rule, placements, out error))
                {
                    error = $"repeated placement [{i}]: {error}";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private bool TryResolveGuideLine(BackgroundPrefabPlacementRule rule, out GuideLineDefinition guideLine,
            out string error)
        {
            guideLine = null;
            if (rule == null || rule.Prefab == null)
            {
                error = "Assign a prefab.";
                return false;
            }

            if (!GuideLineCalculator.TryFindDefinition(backgroundDefinition.GuideLines, rule.GuideLineId, out guideLine))
            {
                error = $"GuideLine ID '{rule.GuideLineId}' must identify exactly one GuideLine in the asset.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryAddRepeatedPlacements(TrackGraphContext graph, GuideLineDefinition guideLine,
            BackgroundRepeatedPrefabPlacementDefinition rule, List<(GameObject Prefab, Pose Pose)> placements,
            out string error)
        {
            float startM = rule.StartDistanceOnEdgeM;
            float endM = rule.EndDistanceOnEdgeM;
            float intervalM = rule.IntervalM;
            if (!graph.TryGetEdge(guideLine.EdgeId, out var edge) || !float.IsFinite(edge.LengthM)
                || !float.IsFinite(startM) || !float.IsFinite(endM) || startM < 0f || startM > endM
                || endM > edge.LengthM || !float.IsFinite(intervalM) || intervalM <= 0f)
            {
                error = "The repeat range must be inside the Edge and the interval must be positive.";
                return false;
            }

            float previousM = float.NegativeInfinity;
            for (int i = 0; ; i++)
            {
                float distanceM = (float)(startM + i * (double)intervalM);
                if (distanceM > endM)
                {
                    break;
                }

                if (distanceM <= previousM)
                {
                    error = "The interval is too small to advance the placement distance.";
                    return false;
                }

                if (!TryAddPlacement(graph, guideLine, rule, distanceM, placements, out error))
                {
                    return false;
                }

                if (distanceM == endM)
                {
                    break;
                }

                previousM = distanceM;
            }

            error = null;
            return true;
        }

        private static bool TryAddPlacement(TrackGraphContext graph, GuideLineDefinition guideLine,
            BackgroundPrefabPlacementRule rule, float distanceM, List<(GameObject Prefab, Pose Pose)> placements,
            out string error)
        {
            if (placements.Count >= MaxGeneratedPrefabs)
            {
                error = $"A background asset can generate at most {MaxGeneratedPrefabs} prefabs.";
                return false;
            }

            if (!GuideLineCalculator.TryEvaluatePoint(graph, guideLine, distanceM, out Vector3 position)
                || !GuideLineCalculator.TryEvaluateTangent(graph, guideLine, distanceM, out Vector3 forward))
            {
                error = $"Could not evaluate GuideLine '{guideLine.Id}' at {distanceM} m.";
                return false;
            }

            if (!rule.FollowGradient)
            {
                forward.y = 0f;
            }

            Vector3 offset = rule.RotationOffsetEulerDeg;
            if (forward.sqrMagnitude <= 1e-10f || !float.IsFinite(offset.x) || !float.IsFinite(offset.y)
                || !float.IsFinite(offset.z))
            {
                error = $"Invalid orientation on GuideLine '{guideLine.Id}' at {distanceM} m.";
                return false;
            }

            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(offset);
            placements.Add((rule.Prefab, new Pose(position, rotation)));
            error = null;
            return true;
        }

        private void ReplaceGeneratedPrefabs(List<(GameObject Prefab, Pose Pose)> placements)
        {
            var nextRoot = new GameObject("Generated Background Prefabs");
            nextRoot.SetActive(false);
            nextRoot.transform.SetParent(transform, false);
            foreach (var placement in placements)
            {
                Instantiate(placement.Prefab, placement.Pose.position, placement.Pose.rotation, nextRoot.transform);
            }

            if (generatedRoot != null)
            {
                generatedRoot.SetActive(false);
                Destroy(generatedRoot);
            }

            generatedRoot = nextRoot;
            generatedRoot.SetActive(true);
        }
    }
}
