using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace MidnightChaos.Procedural
{
    [DisallowMultipleComponent]
    public sealed class ProceduralEnemySpawnManager : MonoBehaviour
    {
        private readonly List<NetworkObject> activeEnemies =
            new List<NetworkObject>();

        private NetworkManager networkManager;
        private ProceduralWorldSettings settings;
        private ProceduralSpawnPointRegistry spawnPoints;
        private RuntimeNavMeshBuilder navMeshBuilder;
        private int nextSpawnPointIndex;

        public int ActiveEnemyCount
        {
            get
            {
                if (networkManager != null &&
                    networkManager.IsClient &&
                    !networkManager.IsServer)
                {
                    return CountReplicatedEnemies();
                }

                PruneDestroyedEnemies();
                return activeEnemies.Count;
            }
        }

        public string LastSpawnMessage { get; private set; } =
            "No enemy has been spawned.";

        public void Initialize(
            NetworkManager configuredNetworkManager,
            ProceduralWorldSettings configuredSettings,
            ProceduralSpawnPointRegistry configuredSpawnPoints,
            RuntimeNavMeshBuilder configuredNavMeshBuilder)
        {
            networkManager = configuredNetworkManager;
            settings = configuredSettings;
            spawnPoints = configuredSpawnPoints;
            navMeshBuilder = configuredNavMeshBuilder;
            nextSpawnPointIndex = 0;
        }

        public bool TrySpawnEnemy(out string error)
        {
            PruneDestroyedEnemies();

            if (networkManager == null || !networkManager.IsServer)
            {
                error = "Chỉ Host được spawn enemy.";
                return false;
            }
            if (settings == null || settings.EnemyPrefab == null)
            {
                error = "ProceduralWorldSettings chưa có Enemy Prefab.";
                return false;
            }
            if (navMeshBuilder == null || !navMeshBuilder.IsReady)
            {
                error = "NavMesh chưa hoàn tất.";
                return false;
            }
            if (spawnPoints == null || spawnPoints.EnemySpawnPoints.Count == 0)
            {
                error = "Map không có enemy spawn point hợp lệ.";
                return false;
            }
            if (activeEnemies.Count >= settings.MaximumActiveEnemies)
            {
                error = $"Đã đạt giới hạn {settings.MaximumActiveEnemies} enemy.";
                return false;
            }

            NavMeshAgent prefabAgent =
                settings.EnemyPrefab.GetComponent<NavMeshAgent>();
            if (prefabAgent == null)
            {
                error = "Enemy Prefab không có NavMeshAgent. Hãy cấu hình " +
                        "radius/height/speed/acceleration trực tiếp trên prefab.";
                return false;
            }
            if (prefabAgent.agentTypeID != settings.NavMeshAgentTypeId)
            {
                error = $"Enemy NavMeshAgent dùng Agent Type ID " +
                        $"{prefabAgent.agentTypeID}, nhưng RuntimeNavMeshSurface " +
                        $"được build bằng ID {settings.NavMeshAgentTypeId}.";
                return false;
            }

            int pointCount = spawnPoints.EnemySpawnPoints.Count;
            for (int offset = 0; offset < pointCount; offset++)
            {
                int index = (nextSpawnPointIndex + offset) % pointCount;
                if (!spawnPoints.TryGetEnemySpawnPoint(index, out Vector3 planned) ||
                    !NavMesh.SamplePosition(
                        planned,
                        out NavMeshHit hit,
                        settings.NavMeshSampleRadius,
                        NavMesh.AllAreas) ||
                    IsOccupied(hit.position))
                {
                    continue;
                }

                GameObject instance = Instantiate(
                    settings.EnemyPrefab,
                    hit.position,
                    Quaternion.identity);
                NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
                if (agent == null)
                {
                    Destroy(instance);
                    error = "Enemy Prefab không có NavMeshAgent hợp lệ.";
                    return false;
                }
                // Keep the prefab agent disabled so Client instances never try
                // to attach to the Host-only runtime NavMesh during Awake.
                agent.enabled = true;

                NetworkObject networkObject = instance.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    Destroy(instance);
                    error = "Enemy Prefab không có NetworkObject.";
                    return false;
                }

                if (!agent.Warp(hit.position))
                {
                    Destroy(instance);
                    error = $"Không đặt được enemy lên NavMesh tại point {index}.";
                    return false;
                }

                networkObject.Spawn(true);
                activeEnemies.Add(networkObject);
                nextSpawnPointIndex = (index + 1) % pointCount;
                LastSpawnMessage =
                    $"Spawned enemy {networkObject.NetworkObjectId} at point {index}.";
                error = string.Empty;
                return true;
            }

            error = "Không còn enemy spawn point trống trên NavMesh.";
            return false;
        }

        public void ClearEnemiesServer()
        {
            if (networkManager != null && networkManager.IsServer)
            {
                for (int index = activeEnemies.Count - 1; index >= 0; index--)
                {
                    NetworkObject enemy = activeEnemies[index];
                    if (enemy != null && enemy.IsSpawned)
                    {
                        enemy.Despawn(true);
                    }
                }
            }

            activeEnemies.Clear();
            nextSpawnPointIndex = 0;
            LastSpawnMessage = "Enemies cleared.";
        }

        public void ResetTracking()
        {
            activeEnemies.Clear();
            nextSpawnPointIndex = 0;
            LastSpawnMessage = "No enemy has been spawned.";
        }

        private bool IsOccupied(Vector3 candidate)
        {
            NavMeshAgent prefabAgent = settings.EnemyPrefab != null
                ? settings.EnemyPrefab.GetComponent<NavMeshAgent>()
                : null;
            float minimumDistance =
                Mathf.Max(0.1f, prefabAgent != null ? prefabAgent.radius : 0.5f) *
                3f;
            float minimumDistanceSquared = minimumDistance * minimumDistance;

            foreach (NetworkObject enemy in activeEnemies)
            {
                if (enemy != null &&
                    (enemy.transform.position - candidate).sqrMagnitude <
                    minimumDistanceSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private void PruneDestroyedEnemies()
        {
            activeEnemies.RemoveAll(
                enemy => enemy == null || !enemy.IsSpawned);
        }

        private int CountReplicatedEnemies()
        {
            if (settings == null ||
                settings.EnemyPrefab == null ||
                networkManager.SpawnManager == null)
            {
                return 0;
            }

            string prefabName = settings.EnemyPrefab.name;
            string cloneName = prefabName + "(Clone)";
            int count = 0;
            foreach (NetworkObject spawned in
                     networkManager.SpawnManager.SpawnedObjectsList)
            {
                if (spawned != null &&
                    (spawned.name == prefabName || spawned.name == cloneName))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
