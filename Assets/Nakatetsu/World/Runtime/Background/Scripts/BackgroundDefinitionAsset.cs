using System.Collections.Generic;
using UnityEngine;

namespace Nakatetsu.World.Background
{
    [CreateAssetMenu(fileName = "BackgroundDefinition", menuName = "Nakatetsu/World/Background Definition")]
    public sealed class BackgroundDefinitionAsset : ScriptableObject
    {
        [SerializeField] private List<GuideLineDefinition> guideLines = new();
        [SerializeField] private List<BackgroundVertexDefinition> vertices = new();
        [SerializeField] private List<BackgroundSinglePrefabPlacementDefinition> singlePrefabPlacements = new();
        [SerializeField] private List<BackgroundRepeatedPrefabPlacementDefinition> repeatedPrefabPlacements = new();
        [SerializeField] private List<BackgroundVertexMeshDefinition> vertexMeshes = new();
        [SerializeField] private List<BackgroundGuideLineMeshDefinition> guideLineMeshes = new();

        public IReadOnlyList<GuideLineDefinition> GuideLines => guideLines;
        public IReadOnlyList<BackgroundVertexDefinition> Vertices => vertices;
        public IReadOnlyList<BackgroundSinglePrefabPlacementDefinition> SinglePrefabPlacements => singlePrefabPlacements;
        public IReadOnlyList<BackgroundRepeatedPrefabPlacementDefinition> RepeatedPrefabPlacements => repeatedPrefabPlacements;
        public IReadOnlyList<BackgroundVertexMeshDefinition> VertexMeshes => vertexMeshes;
        public IReadOnlyList<BackgroundGuideLineMeshDefinition> GuideLineMeshes => guideLineMeshes;
    }
}
