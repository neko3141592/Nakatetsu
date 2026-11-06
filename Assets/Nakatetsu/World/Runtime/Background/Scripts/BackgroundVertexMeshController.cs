using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using UnityEngine;

namespace Nakatetsu.World.Background
{
    public sealed class BackgroundVertexMeshController : MonoBehaviour
    {
        [SerializeField] private TrackGraphController trackGraphController;
        [SerializeField] private BackgroundDefinitionAsset backgroundDefinition;

        private GameObject generatedRoot;
        private List<Mesh> generatedMeshes = new();

        private void Start()
        {
            Rebuild();
        }

        private void OnDestroy()
        {
            ReleaseGenerated();
        }

        [ContextMenu("Rebuild Background Meshes (Play Mode)")]
        public void Rebuild()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (trackGraphController == null || !trackGraphController.IsInitialized || backgroundDefinition == null)
            {
                Debug.LogError("Background meshes require an initialized TrackGraphController and a background asset.", this);
                return;
            }

            var vertexDefinitions = backgroundDefinition.VertexMeshes;
            var guideLineDefinitions = backgroundDefinition.GuideLineMeshes;
            var meshes = new List<Mesh>(vertexDefinitions.Count + guideLineDefinitions.Count);
            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            for (int i = 0; i < vertexDefinitions.Count; i++)
            {
                var definition = vertexDefinitions[i];
                string error = "Assign a material.";
                if (definition?.Material != null && BackgroundVertexMeshBuilder.TryBuild(trackGraphController.Context,
                    backgroundDefinition, definition, worldToLocal, out Mesh mesh, out error))
                {
                    meshes.Add(mesh);
                    continue;
                }

                DestroyMeshes(meshes);
                Debug.LogError($"Background vertex mesh [{i}]: {error}", this);
                return;
            }

            for (int i = 0; i < guideLineDefinitions.Count; i++)
            {
                var definition = guideLineDefinitions[i];
                string error = "Assign a material.";
                if (definition?.Material != null && BackgroundGuideLineMeshBuilder.TryBuild(trackGraphController.Context,
                    backgroundDefinition, definition, worldToLocal, out Mesh mesh, out error))
                {
                    meshes.Add(mesh);
                    continue;
                }

                DestroyMeshes(meshes);
                Debug.LogError($"Background GuideLine mesh [{i}]: {error}", this);
                return;
            }

            if (generatedRoot != null)
            {
                generatedRoot.SetActive(false);
            }

            ReleaseGenerated();
            generatedRoot = new GameObject(guideLineDefinitions.Count > 0
                ? "Generated Background Meshes"
                : "Generated Background Vertex Meshes");
            generatedRoot.transform.SetParent(transform, false);
            generatedMeshes = meshes;
            for (int i = 0; i < vertexDefinitions.Count; i++)
            {
                CreateMeshObject($"Vertex Mesh {i + 1}", meshes[i], vertexDefinitions[i].Material);
            }

            for (int i = 0; i < guideLineDefinitions.Count; i++)
            {
                CreateMeshObject($"GuideLine Mesh {i + 1}", meshes[vertexDefinitions.Count + i], guideLineDefinitions[i].Material);
            }
        }

        private void CreateMeshObject(string name, Mesh mesh, Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(generatedRoot.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void ReleaseGenerated()
        {
            if (generatedRoot != null)
            {
                Destroy(generatedRoot);
                generatedRoot = null;
            }

            DestroyMeshes(generatedMeshes);
            generatedMeshes.Clear();
        }

        private static void DestroyMeshes(List<Mesh> meshes)
        {
            foreach (Mesh mesh in meshes)
            {
                Destroy(mesh);
            }
        }
    }
}
