using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MidnightChaos.Procedural
{
    [DisallowMultipleComponent]
    public sealed class ProceduralSpawnPointRegistry : MonoBehaviour
    {
        private readonly List<Vector3> playerSpawnPoints = new List<Vector3>();
        private readonly List<Vector3> enemySpawnPoints = new List<Vector3>();
        private Material playerMarkerMaterial;
        private Material enemyMarkerMaterial;

        public IReadOnlyList<Vector3> PlayerSpawnPoints => playerSpawnPoints;
        public IReadOnlyList<Vector3> EnemySpawnPoints => enemySpawnPoints;
        public int ValidPlayerSpawnCount { get; private set; }
        public int ValidEnemySpawnCount { get; private set; }

        public void ApplyLayout(
            ProceduralWorldLayout layout,
            Transform generatedRoot,
            ProceduralRenderingSettings settings)
        {
            playerSpawnPoints.Clear();
            enemySpawnPoints.Clear();
            ValidPlayerSpawnCount = 0;
            ValidEnemySpawnCount = 0;

            if (layout == null)
            {
                return;
            }

            playerSpawnPoints.AddRange(layout.PlayerSpawnPoints);
            enemySpawnPoints.AddRange(layout.EnemySpawnPoints);
            ValidPlayerSpawnCount = playerSpawnPoints.Count;
            ValidEnemySpawnCount = enemySpawnPoints.Count;

            if (!settings.ShowSpawnMarkers || generatedRoot == null)
            {
                return;
            }

            Transform markerRoot = new GameObject("SpawnPointMarkers").transform;
            markerRoot.SetParent(generatedRoot, false);

            for (int index = 0; index < playerSpawnPoints.Count; index++)
            {
                CreateMarker(
                    markerRoot,
                    $"PlayerSpawn_{index:00}",
                    playerSpawnPoints[index],
                    settings.SpawnMarkerScale,
                    GetPlayerMarkerMaterial(),
                    PrimitiveType.Cylinder);
            }

            for (int index = 0; index < enemySpawnPoints.Count; index++)
            {
                CreateMarker(
                    markerRoot,
                    $"EnemySpawn_{index:00}",
                    enemySpawnPoints[index],
                    settings.SpawnMarkerScale,
                    GetEnemyMarkerMaterial(),
                    PrimitiveType.Sphere);
            }
        }

        public void ValidateAfterNavMesh(ProceduralNavigationSettings settings)
        {
            ValidPlayerSpawnCount = 0;
            ValidEnemySpawnCount = 0;

            foreach (Vector3 point in playerSpawnPoints)
            {
                if (!NavMesh.SamplePosition(
                        point,
                        out NavMeshHit hit,
                        settings.NavMeshSampleRadius,
                        NavMesh.AllAreas))
                {
                    continue;
                }

                Vector3 capsuleBottom = hit.position + Vector3.up * 0.55f;
                Vector3 capsuleTop = hit.position + Vector3.up * 1.35f;
                int mask = ~(1 << 2);
                if (!Physics.CheckCapsule(
                        capsuleBottom,
                        capsuleTop,
                        0.4f,
                        mask,
                        QueryTriggerInteraction.Ignore))
                {
                    ValidPlayerSpawnCount++;
                }
            }

            foreach (Vector3 point in enemySpawnPoints)
            {
                if (NavMesh.SamplePosition(
                        point,
                        out _,
                        settings.NavMeshSampleRadius,
                        NavMesh.AllAreas))
                {
                    ValidEnemySpawnCount++;
                }
            }
        }

        public bool TryGetEnemySpawnPoint(int index, out Vector3 point)
        {
            if (index < 0 || index >= enemySpawnPoints.Count)
            {
                point = default;
                return false;
            }

            point = enemySpawnPoints[index];
            return true;
        }

        public bool TryGetPlayerSpawnPoint(int index, out Vector3 point)
        {
            if (index < 0 || index >= playerSpawnPoints.Count)
            {
                point = default;
                return false;
            }

            point = playerSpawnPoints[index];
            return true;
        }

        public void Clear()
        {
            playerSpawnPoints.Clear();
            enemySpawnPoints.Clear();
            ValidPlayerSpawnCount = 0;
            ValidEnemySpawnCount = 0;
        }

        private static void CreateMarker(
            Transform parent,
            string markerName,
            Vector3 point,
            float scale,
            Material material,
            PrimitiveType primitiveType)
        {
            GameObject marker = GameObject.CreatePrimitive(primitiveType);
            marker.name = markerName;
            marker.layer = 2;
            marker.transform.SetParent(parent, true);
            marker.transform.position = point + Vector3.up * 0.12f;
            marker.transform.localScale = primitiveType == PrimitiveType.Cylinder
                ? new Vector3(scale, 0.06f, scale)
                : Vector3.one * scale;
            marker.GetComponent<Renderer>().sharedMaterial = material;

            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        private Material GetPlayerMarkerMaterial()
        {
            playerMarkerMaterial ??= CreateMarkerMaterial(
                "PlayerSpawnMarker",
                new Color(0.12f, 0.65f, 1f, 0.9f));
            return playerMarkerMaterial;
        }

        private Material GetEnemyMarkerMaterial()
        {
            enemyMarkerMaterial ??= CreateMarkerMaterial(
                "EnemySpawnMarker",
                new Color(1f, 0.18f, 0.42f, 0.9f));
            return enemyMarkerMaterial;
        }

        private static Material CreateMarkerMaterial(string name, Color color)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Standard");
            Material material = new Material(shader)
            {
                name = name,
                color = color
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            return material;
        }

        private void OnDestroy()
        {
            if (playerMarkerMaterial != null)
            {
                Destroy(playerMarkerMaterial);
            }
            if (enemyMarkerMaterial != null)
            {
                Destroy(enemyMarkerMaterial);
            }
        }
    }
}
