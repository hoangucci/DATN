using System.Collections.Generic;
using MidnightChaos.Inventory;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace MidnightChaos.Procedural
{
    [DisallowMultipleComponent]
    public sealed class ProceduralEnemySpawnManager : MonoBehaviour
    {
        private const int GroupPlacementAttemptsPerMember = 16;
        private const float GoldenAngleRadians = 2.39996323f;

        private readonly List<NetworkObject> activeEnemies = new();
        private readonly HashSet<ulong> gameplayGroupEnemyIds = new();
        private readonly HashSet<ulong> debugEnemyIds = new();
        private NetworkManager networkManager;
        private VerticalSliceGameplaySettings gameplaySettings;
        private ProceduralNavigationSettings navigationSettings;
        private ProceduralSpawnPointRegistry spawnPoints;
        private RuntimeNavMeshBuilder navMeshBuilder;
        private int nextSpawnPointIndex;

        public int ActiveEnemyCount
        {
            get
            {
                if (networkManager != null && networkManager.IsClient &&
                    !networkManager.IsServer)
                {
                    return CountReplicatedEnemies();
                }
                PruneDestroyedEnemies();
                return activeEnemies.Count;
            }
        }

        public int GameplayGroupSize => gameplayGroupEnemyIds.Count;
        public string LastSpawnMessage { get; private set; } =
            "No enemy has been spawned.";

        public void Initialize(
            NetworkManager configuredNetworkManager,
            VerticalSliceGameplaySettings configuredGameplaySettings,
            ProceduralNavigationSettings configuredNavigationSettings,
            ProceduralSpawnPointRegistry configuredSpawnPoints,
            RuntimeNavMeshBuilder configuredNavMeshBuilder)
        {
            networkManager = configuredNetworkManager;
            gameplaySettings = configuredGameplaySettings;
            navigationSettings = configuredNavigationSettings;
            spawnPoints = configuredSpawnPoints;
            navMeshBuilder = configuredNavMeshBuilder;
            nextSpawnPointIndex = 0;
        }

        // Debug path: deliberately separate from the automatic group state.
        public bool TrySpawnEnemy(out string error)
        {
            if (!ValidateCommon(out error)) return false;
            NetworkObject player = networkManager.LocalClient?.PlayerObject;
            if (player == null)
            {
                error = "Host local player chưa spawn.";
                return false;
            }
            Vector3 forward = Vector3.ProjectOnPlane(
                player.transform.forward,
                Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            Vector3 planned = player.transform.position + forward *
                              gameplaySettings.DebugSpawnDistance;
            if (!TryResolveNavMeshPosition(planned, out Vector3 position))
            {
                error = "Không tìm thấy NavMesh trước Local Player.";
                return false;
            }
            if (!TrySpawnAt(position, false, out NetworkObject enemy, out error))
            {
                return false;
            }
            LastSpawnMessage =
                $"Debug enemy {enemy.NetworkObjectId} spawned in front of Host.";
            debugEnemyIds.Add(enemy.NetworkObjectId);
            Debug.Log($"[EnemySpawn] {LastSpawnMessage}");
            return true;
        }

        public int SpawnGameplayGroupServer(int requestedCount)
        {
            if (!ValidateCommon(out string error))
            {
                Debug.LogError($"[EnemySpawn] Group rejected: {error}");
                return 0;
            }

            requestedCount = Mathf.Max(1, requestedCount);
            int centerCount = spawnPoints.EnemySpawnPoints.Count;
            string lastError = "Không tìm được group center hợp lệ.";
            for (int centerAttempt = 0;
                 centerAttempt < centerCount;
                 centerAttempt++)
            {
                int centerIndex = nextSpawnPointIndex % centerCount;
                nextSpawnPointIndex = (nextSpawnPointIndex + 1) % centerCount;
                if (!spawnPoints.TryGetEnemySpawnPoint(
                        centerIndex,
                        out Vector3 center))
                {
                    continue;
                }

                List<Vector3> plannedPositions =
                    new List<Vector3>(requestedCount);
                if (!TryPlanGameplayGroup(
                        center,
                        centerIndex,
                        requestedCount,
                        plannedPositions,
                        out lastError))
                {
                    continue;
                }

                List<NetworkObject> spawnedGroup =
                    new List<NetworkObject>(requestedCount);
                bool spawnSucceeded = true;
                foreach (Vector3 position in plannedPositions)
                {
                    if (!TrySpawnAt(
                            position,
                            true,
                            out NetworkObject enemy,
                            out lastError))
                    {
                        spawnSucceeded = false;
                        break;
                    }
                    spawnedGroup.Add(enemy);
                }

                if (!spawnSucceeded)
                {
                    RollbackGameplayGroup(spawnedGroup);
                    continue;
                }

                foreach (NetworkObject enemy in spawnedGroup)
                {
                    gameplayGroupEnemyIds.Add(enemy.NetworkObjectId);
                }
                LastSpawnMessage =
                    $"Gameplay group: {spawnedGroup.Count}/" +
                    $"{requestedCount} enemies around group center " +
                    $"{centerIndex}.";
                Debug.Log($"[EnemySpawn] {LastSpawnMessage}");
                return spawnedGroup.Count;
            }

            LastSpawnMessage =
                $"Gameplay group failed: 0/{requestedCount}. {lastError}";
            Debug.LogError($"[EnemySpawn] {LastSpawnMessage}");
            return 0;
        }

        private bool TryPlanGameplayGroup(
            Vector3 center,
            int centerIndex,
            int requestedCount,
            List<Vector3> plannedPositions,
            out string error)
        {
            for (int memberIndex = 0;
                 memberIndex < requestedCount;
                 memberIndex++)
            {
                if (!TryResolveGameplayGroupPosition(
                        center,
                        centerIndex,
                        memberIndex,
                        requestedCount,
                        plannedPositions,
                        out Vector3 position))
                {
                    error =
                        $"Group center {centerIndex} không đủ NavMesh trống " +
                        $"cho member {memberIndex + 1}/{requestedCount}. " +
                        "Tăng Gameplay Group Radius hoặc giảm Minimum Spacing.";
                    return false;
                }
                plannedPositions.Add(position);
            }

            error = string.Empty;
            return true;
        }

        private bool TryResolveGameplayGroupPosition(
            Vector3 center,
            int centerIndex,
            int memberIndex,
            int requestedCount,
            IReadOnlyList<Vector3> plannedPositions,
            out Vector3 position)
        {
            float groupRadius = gameplaySettings.GameplayGroupRadius;
            float minimumSpacing =
                gameplaySettings.GameplayGroupMinimumSpacing;
            float maximumDistanceSquared = groupRadius * groupRadius;
            float minimumSpacingSquared = minimumSpacing * minimumSpacing;

            for (int attempt = 0;
                 attempt < GroupPlacementAttemptsPerMember;
                 attempt++)
            {
                Vector3 planned;
                if (memberIndex == 0 && attempt == 0)
                {
                    planned = center;
                }
                else
                {
                    int sequence = memberIndex + attempt * requestedCount;
                    float radialSample = Mathf.Repeat(
                        (memberIndex + 1) * 0.61803399f +
                        attempt * 0.38196602f,
                        1f);
                    float radius = Mathf.Lerp(
                        minimumSpacing,
                        groupRadius,
                        Mathf.Sqrt(radialSample));
                    float angle =
                        sequence * GoldenAngleRadians +
                        centerIndex * 0.75487769f;
                    planned = center + new Vector3(
                        Mathf.Cos(angle) * radius,
                        0f,
                        Mathf.Sin(angle) * radius);
                }

                if (!NavMesh.SamplePosition(
                        planned,
                        out NavMeshHit hit,
                        navigationSettings.NavMeshSampleRadius,
                        NavMesh.AllAreas))
                {
                    continue;
                }
                Vector3 centerDelta = Vector3.ProjectOnPlane(
                    hit.position - center,
                    Vector3.up);
                if (centerDelta.sqrMagnitude > maximumDistanceSquared ||
                    IsOccupied(hit.position))
                {
                    continue;
                }

                bool overlapsPlannedMember = false;
                foreach (Vector3 other in plannedPositions)
                {
                    Vector3 delta = Vector3.ProjectOnPlane(
                        hit.position - other,
                        Vector3.up);
                    if (delta.sqrMagnitude < minimumSpacingSquared)
                    {
                        overlapsPlannedMember = true;
                        break;
                    }
                }
                if (overlapsPlannedMember)
                {
                    continue;
                }

                position = hit.position;
                return true;
            }

            position = default;
            return false;
        }

        private void RollbackGameplayGroup(
            IReadOnlyList<NetworkObject> spawnedGroup)
        {
            for (int index = spawnedGroup.Count - 1; index >= 0; index--)
            {
                NetworkObject enemy = spawnedGroup[index];
                activeEnemies.Remove(enemy);
                if (enemy == null)
                {
                    continue;
                }
                if (enemy.IsSpawned)
                {
                    enemy.Despawn(true);
                }
                else
                {
                    Destroy(enemy.gameObject);
                }
            }
        }

        public void ClearEnemiesServer()
        {
            if (networkManager != null && networkManager.IsServer)
            {
                for (int index = activeEnemies.Count - 1; index >= 0; index--)
                {
                    NetworkObject enemy = activeEnemies[index];
                    if (enemy != null && enemy.IsSpawned) enemy.Despawn(true);
                }
            }
            ResetTracking();
        }

        public void ResetTracking()
        {
            activeEnemies.Clear();
            gameplayGroupEnemyIds.Clear();
            debugEnemyIds.Clear();
            nextSpawnPointIndex = 0;
            LastSpawnMessage = "No enemy has been spawned.";
        }

        private bool ValidateCommon(out string error)
        {
            PruneDestroyedEnemies();
            if (networkManager == null || !networkManager.IsServer)
            {
                error = "Chỉ Host được spawn enemy.";
                return false;
            }
            if (gameplaySettings == null || gameplaySettings.EnemyPrefab == null)
            {
                error = "VerticalSliceGameplaySettings chưa có Enemy Prefab.";
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
            NavMeshAgent prefabAgent = gameplaySettings.EnemyPrefab
                .GetComponent<NavMeshAgent>();
            if (prefabAgent == null || navigationSettings == null ||
                prefabAgent.agentTypeID != navigationSettings.NavMeshAgentTypeId)
            {
                error = "Enemy NavMeshAgent thiếu hoặc sai Agent Type ID.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private bool TryResolveNavMeshPosition(Vector3 planned, out Vector3 position)
        {
            if (NavMesh.SamplePosition(
                    planned,
                    out NavMeshHit hit,
                    navigationSettings.NavMeshSampleRadius * 3f,
                    NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }
            position = default;
            return false;
        }

        private bool TrySpawnAt(
            Vector3 position,
            bool gameplayGroup,
            out NetworkObject networkObject,
            out string error)
        {
            networkObject = null;
            if (!gameplayGroup && debugEnemyIds.Count >=
                gameplaySettings.MaximumActiveEnemies)
            {
                error = $"Đã đạt giới hạn " +
                        $"{gameplaySettings.MaximumActiveEnemies} debug enemy.";
                return false;
            }
            GameObject instance = Instantiate(
                gameplaySettings.EnemyPrefab,
                position,
                Quaternion.identity);
            NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
            networkObject = instance.GetComponent<NetworkObject>();
            if (agent == null || networkObject == null)
            {
                Destroy(instance);
                error = "Enemy prefab thiếu NavMeshAgent hoặc NetworkObject.";
                return false;
            }
            agent.enabled = true;
            if (!agent.Warp(position) || !agent.isOnNavMesh)
            {
                agent.enabled = false;
                Destroy(instance);
                networkObject = null;
                error = "Không đặt được enemy lên NavMesh.";
                return false;
            }
            networkObject.Spawn(true);
            activeEnemies.Add(networkObject);
            error = string.Empty;
            return true;
        }

        private bool IsOccupied(Vector3 candidate)
        {
            NavMeshAgent prefabAgent = gameplaySettings.EnemyPrefab
                .GetComponent<NavMeshAgent>();
            float distance = Mathf.Max(
                gameplaySettings.GameplayGroupMinimumSpacing,
                Mathf.Max(0.5f, prefabAgent.radius) * 2f);
            foreach (NetworkObject enemy in activeEnemies)
            {
                if (enemy != null &&
                    (enemy.transform.position - candidate).sqrMagnitude <
                    distance * distance)
                {
                    return true;
                }
            }
            return false;
        }

        private void PruneDestroyedEnemies()
        {
            activeEnemies.RemoveAll(enemy => enemy == null || !enemy.IsSpawned);
            gameplayGroupEnemyIds.RemoveWhere(id =>
                networkManager == null || networkManager.SpawnManager == null ||
                !networkManager.SpawnManager.SpawnedObjects.ContainsKey(id));
            debugEnemyIds.RemoveWhere(id =>
                networkManager == null || networkManager.SpawnManager == null ||
                !networkManager.SpawnManager.SpawnedObjects.ContainsKey(id));
        }

        private int CountReplicatedEnemies()
        {
            if (gameplaySettings == null || gameplaySettings.EnemyPrefab == null ||
                networkManager.SpawnManager == null) return 0;
            string prefabName = gameplaySettings.EnemyPrefab.name;
            int count = 0;
            foreach (NetworkObject spawned in networkManager.SpawnManager.SpawnedObjectsList)
            {
                if (spawned != null && (spawned.name == prefabName ||
                    spawned.name == prefabName + "(Clone)")) count++;
            }
            return count;
        }
    }
}
