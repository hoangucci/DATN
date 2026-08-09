using System.Collections.Generic;
using Unity.Netcode;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace MidnightChaos.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class DiagnosticEnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Vector3 clusterCenter =
            new Vector3(0f, 1f, 14f);
        [SerializeField, Min(1f)] private float clusterRadius = 8f;
        [SerializeField, Range(2, 12)] private int surroundingEnemyCount = 6;
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 4f;
        [SerializeField, Range(8, 128)] private int maximumActiveEnemies = 32;

        private NetworkManager networkManager;
        private NavMeshSurface navMeshSurface;
        private int manualSpawnSequence;
        private bool movementEnabled = true;
        private readonly List<NetworkObject> spawnedEnemies =
            new List<NetworkObject>();

        public int ActiveEnemyCount
        {
            get
            {
                PruneSpawnedEnemies();
                return spawnedEnemies.Count;
            }
        }
        public int MaximumActiveEnemies => maximumActiveEnemies;
        public bool MovementEnabled => movementEnabled;

        public void Configure(GameObject prefab)
        {
            enemyPrefab = prefab;
        }

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();

            if (enemyPrefab == null)
            {
                Debug.LogError("[Gate F] Enemy prefab is missing.");
                enabled = false;
                return;
            }

            networkManager.OnServerStarted += HandleServerStarted;
            networkManager.OnServerStopped += HandleServerStopped;
        }

        private void Start()
        {
            if (!TryEnsureNavigation(out string error))
            {
                Debug.LogError($"[Gate F] {error}", this);
            }
        }

        private void HandleServerStarted()
        {
            if (!networkManager.IsServer || HasSpawnedEnemy())
            {
                return;
            }
            if (!TryEnsureNavigation(out string navigationError))
            {
                Debug.LogError($"[Gate F] {navigationError}", this);
                return;
            }

            // Spawn the center enemy first so NetworkObjectId is the stable
            // tie-break winner while the diagnostic cluster is still intact.
            SpawnInitialEnemy(clusterCenter);

            for (int index = 0; index < surroundingEnemyCount; index++)
            {
                float angle =
                    index * Mathf.PI * 2f / surroundingEnemyCount;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * clusterRadius,
                    0f,
                    Mathf.Sin(angle) * clusterRadius);

                SpawnInitialEnemy(clusterCenter + offset);
            }
        }

        private void HandleServerStopped(bool wasHost)
        {
            spawnedEnemies.Clear();
            manualSpawnSequence = 0;
            movementEnabled = true;
        }

        private void OnDestroy()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnServerStarted -= HandleServerStarted;
            networkManager.OnServerStopped -= HandleServerStopped;
        }

        private bool HasSpawnedEnemy()
        {
            foreach (NetworkObject enemy in spawnedEnemies)
            {
                if (enemy != null && enemy.IsSpawned)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TrySpawnEnemy(out string error)
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                error = "Spawn Enemy chỉ khả dụng trên Host.";
                return false;
            }
            if (!TryEnsureNavigation(out error))
            {
                return false;
            }
            if (ActiveEnemyCount >= maximumActiveEnemies)
            {
                error = $"Đã đạt giới hạn {maximumActiveEnemies} enemy.";
                return false;
            }

            const float goldenAngleDegrees = 137.507764f;
            float angle = manualSpawnSequence * goldenAngleDegrees *
                          Mathf.Deg2Rad;
            float radius = clusterRadius *
                           (1.25f + manualSpawnSequence % 3 * 0.2f);
            manualSpawnSequence++;
            Vector3 planned = clusterCenter + new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius);
            return TrySpawnEnemyServer(planned, out error);
        }

        public bool TrySetMovementEnabled(
            bool enabledForAll,
            out string error)
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                error = "Enemy Move chỉ có thể thay đổi trên Host.";
                return false;
            }

            PruneSpawnedEnemies();
            foreach (NetworkObject networkObject in spawnedEnemies)
            {
                DiagnosticMeleeEnemy enemy =
                    networkObject.GetComponent<DiagnosticMeleeEnemy>();
                string enemyError = string.Empty;
                if (enemy == null ||
                    !enemy.SetServerMovementEnabled(
                        enabledForAll,
                        out enemyError))
                {
                    if (enemy != null && enemy.CurrentState ==
                        DiagnosticEnemyState.Dead)
                    {
                        continue;
                    }

                    error = enemy == null
                        ? "Spawned enemy is missing DiagnosticMeleeEnemy."
                        : enemyError;
                    return false;
                }
            }

            movementEnabled = enabledForAll;
            error = string.Empty;
            return true;
        }

        private void SpawnInitialEnemy(Vector3 position)
        {
            if (!TrySpawnEnemyServer(position, out string error))
            {
                Debug.LogError($"[Gate F] {error}", this);
            }
        }

        private bool TrySpawnEnemyServer(
            Vector3 position,
            out string error)
        {
            if (!NavMesh.SamplePosition(
                    position,
                    out NavMeshHit hit,
                    navMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                error = $"No NavMesh position found near {position}.";
                return false;
            }

            GameObject instance = Instantiate(
                enemyPrefab,
                hit.position,
                Quaternion.identity);

            NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                Destroy(instance);
                error = "Spawned enemy has no NavMeshAgent.";
                return false;
            }

            agent.enabled = true;
            if (!agent.Warp(hit.position) || !agent.isOnNavMesh)
            {
                Destroy(instance);
                error = $"Enemy could not attach to NavMesh at {hit.position}.";
                return false;
            }

            NetworkObject networkObject =
                instance.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                Destroy(instance);
                error = "Spawned enemy has no NetworkObject.";
                return false;
            }

            networkObject.Spawn(true);
            DiagnosticMeleeEnemy enemy =
                instance.GetComponent<DiagnosticMeleeEnemy>();
            if (!movementEnabled && enemy != null)
            {
                enemy.SetServerMovementEnabled(false, out _);
            }
            spawnedEnemies.Add(networkObject);
            error = string.Empty;
            return true;
        }

        private void PruneSpawnedEnemies()
        {
            for (int index = spawnedEnemies.Count - 1; index >= 0; index--)
            {
                NetworkObject enemy = spawnedEnemies[index];
                if (enemy == null || !enemy.IsSpawned)
                {
                    spawnedEnemies.RemoveAt(index);
                }
            }
        }

        private bool TryEnsureNavigation(out string error)
        {
            NavMeshAgent prefabAgent =
                enemyPrefab != null
                    ? enemyPrefab.GetComponent<NavMeshAgent>()
                    : null;
            if (prefabAgent == null)
            {
                error = "Enemy prefab has no NavMeshAgent.";
                return false;
            }

            navMeshSurface ??= GetComponent<NavMeshSurface>();
            if (navMeshSurface == null)
            {
                navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
                navMeshSurface.agentTypeID = prefabAgent.agentTypeID;
                navMeshSurface.collectObjects = CollectObjects.All;
                navMeshSurface.useGeometry =
                    NavMeshCollectGeometry.RenderMeshes;
            }
            else if (navMeshSurface.agentTypeID != prefabAgent.agentTypeID)
            {
                error = $"NavMeshSurface Agent Type ID " +
                        $"{navMeshSurface.agentTypeID} does not match enemy " +
                        $"Agent Type ID {prefabAgent.agentTypeID}.";
                return false;
            }

            if (navMeshSurface.navMeshData == null)
            {
                navMeshSurface.BuildNavMesh();
            }
            if (navMeshSurface.navMeshData == null)
            {
                error = "LAN_Bootstrap NavMeshSurface could not build NavMesh data.";
                return false;
            }
            if (!NavMesh.SamplePosition(
                    clusterCenter,
                    out _,
                    navMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                error = $"LAN_Bootstrap has no walkable NavMesh near enemy " +
                        $"cluster center {clusterCenter}.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
