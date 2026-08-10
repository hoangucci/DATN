using System.Collections.Generic;
using MidnightChaos.Enemies;
using MidnightChaos.Inventory;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace MidnightChaos.Procedural
{
    public enum GameplayEnemyGroupState : byte
    {
        Dormant = 0,
        Active = 1,
        Suspended = 2,
        Completed = 3
    }

    [DisallowMultipleComponent]
    public sealed class ProceduralEnemySpawnManager : MonoBehaviour
    {
        private sealed class GameplayEnemyGroupRuntime
        {
            public int GroupId;
            public int CenterIndex;
            public Vector3 CenterPosition;
            public GameplayEnemyGroupState State;
            public readonly List<ulong> MemberNetworkObjectIds = new();
            public bool HasSpawned;
            public bool SpawnFailed;
        }

        private const int GroupPlacementAttemptsPerMember = 16;
        private const float GoldenAngleRadians = 2.39996323f;

        private readonly List<NetworkObject> activeEnemies = new();
        private readonly HashSet<ulong> debugEnemyIds = new();
        private readonly List<GameplayEnemyGroupRuntime> gameplayGroups =
            new();
        private readonly Dictionary<ulong, int> gameplayGroupByEnemyId =
            new();
        private NetworkManager networkManager;
        private VerticalSliceGameplaySettings gameplaySettings;
        private ChaosEvolutionProfile evolutionProfile;
        private ProceduralNavigationSettings navigationSettings;
        private ProceduralSpawnPointRegistry spawnPoints;
        private RuntimeNavMeshBuilder navMeshBuilder;
        private bool gameplayGroupsInitialized;
        private double nextProximityCheckTime;

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

        public int TotalGameplayGroupCount => gameplayGroups.Count;
        public int DormantGameplayGroupCount => CountGroups(
            GameplayEnemyGroupState.Dormant);
        public int ActiveGameplayGroupCount => CountGroups(
            GameplayEnemyGroupState.Active);
        public int SuspendedGameplayGroupCount => CountGroups(
            GameplayEnemyGroupState.Suspended);
        public int CompletedGameplayGroupCount => CountGroups(
            GameplayEnemyGroupState.Completed);
        public int ActiveGameplayEnemyCount => CountLivingMembers(
            GameplayEnemyGroupState.Active);
        public int SuspendedGameplayEnemyCount => CountLivingMembers(
            GameplayEnemyGroupState.Suspended);
        public int AliveGameplayEnemyCount => CountLivingMembers(null);
        public int ResolvedGameplayGroupSize => gameplaySettings != null
            ? gameplaySettings.ResolveGameplayGroupSize(evolutionProfile)
            : 0;
        public GameplayGroupSizeMode GroupSizeMode => gameplaySettings != null
            ? gameplaySettings.GroupSizeMode
            : GameplayGroupSizeMode.Auto;
        public string LastSpawnMessage { get; private set; } =
            "No enemy has been spawned.";

        public void Initialize(
            NetworkManager configuredNetworkManager,
            VerticalSliceGameplaySettings configuredGameplaySettings,
            ChaosEvolutionProfile configuredEvolutionProfile,
            ProceduralNavigationSettings configuredNavigationSettings,
            ProceduralSpawnPointRegistry configuredSpawnPoints,
            RuntimeNavMeshBuilder configuredNavMeshBuilder)
        {
            networkManager = configuredNetworkManager;
            gameplaySettings = configuredGameplaySettings;
            evolutionProfile = configuredEvolutionProfile;
            navigationSettings = configuredNavigationSettings;
            spawnPoints = configuredSpawnPoints;
            navMeshBuilder = configuredNavMeshBuilder;
        }

        public int InitializeGameplayGroupsServer()
        {
            if (gameplayGroupsInitialized)
            {
                LastSpawnMessage =
                    "Gameplay groups are already initialized for this " +
                    "world revision.";
                Debug.LogWarning($"[EnemyGroup] {LastSpawnMessage}");
                return gameplayGroups.Count;
            }
            if (!ValidateCommon(out string error))
            {
                LastSpawnMessage =
                    $"Gameplay group initialization failed: {error}";
                Debug.LogError($"[EnemyGroup] {LastSpawnMessage}");
                return 0;
            }

            gameplayGroups.Clear();
            gameplayGroupByEnemyId.Clear();

            for (int centerIndex = 0;
                 centerIndex < spawnPoints.EnemySpawnPoints.Count;
                 centerIndex++)
            {
                if (!spawnPoints.TryGetEnemySpawnPoint(
                        centerIndex,
                        out Vector3 center))
                {
                    continue;
                }

                gameplayGroups.Add(new GameplayEnemyGroupRuntime
                {
                    GroupId = centerIndex,
                    CenterIndex = centerIndex,
                    CenterPosition = center,
                    State = GameplayEnemyGroupState.Dormant
                });
            }

            gameplayGroupsInitialized = true;
            nextProximityCheckTime = Time.realtimeSinceStartupAsDouble;
            LastSpawnMessage =
                $"Initialized {gameplayGroups.Count} Dormant gameplay " +
                "groups from actual Enemy Spawn Points.";
            Debug.Log($"[EnemyGroup] {LastSpawnMessage}");
            return gameplayGroups.Count;
        }

        private void Update()
        {
            if (!gameplayGroupsInitialized || networkManager == null ||
                !networkManager.IsServer || !networkManager.IsListening ||
                navMeshBuilder == null || !navMeshBuilder.IsReady)
            {
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextProximityCheckTime)
            {
                return;
            }

            nextProximityCheckTime = now +
                                     gameplaySettings
                                         .GroupProximityCheckInterval;
            TickGameplayGroupsServer();
        }

        private void TickGameplayGroupsServer()
        {
            PruneDestroyedEnemies();
            UpdateCompletedGroupsServer();

            foreach (GameplayEnemyGroupRuntime group in gameplayGroups)
            {
                if (group.State != GameplayEnemyGroupState.Active ||
                    IsAnyAlivePlayerWithin(
                        group.CenterPosition,
                        gameplaySettings.GroupSuspensionDistance))
                {
                    continue;
                }

                TrySetGroupSuspendedServer(group, true);
            }

            int availableActiveSlots = Mathf.Max(
                0,
                gameplaySettings.MaximumActiveGroups -
                ActiveGameplayGroupCount);
            if (availableActiveSlots <= 0)
            {
                return;
            }

            foreach (GameplayEnemyGroupRuntime group in gameplayGroups)
            {
                if (availableActiveSlots <= 0)
                {
                    break;
                }
                bool canActivateDormant =
                    group.State == GameplayEnemyGroupState.Dormant &&
                    !group.HasSpawned && !group.SpawnFailed;
                bool canResumeSuspended =
                    group.State == GameplayEnemyGroupState.Suspended &&
                    group.HasSpawned;
                if ((!canActivateDormant && !canResumeSuspended) ||
                    !IsAnyAlivePlayerWithin(
                        group.CenterPosition,
                        gameplaySettings.GroupActivationDistance))
                {
                    continue;
                }

                bool activated = canActivateDormant
                    ? TryActivateDormantGroupServer(group)
                    : TrySetGroupSuspendedServer(group, false);
                if (activated)
                {
                    availableActiveSlots--;
                }
            }
        }

        private bool TrySetGroupSuspendedServer(
            GameplayEnemyGroupRuntime group,
            bool suspended)
        {
            List<DiagnosticMeleeEnemy> changedEnemies = new();
            foreach (ulong memberId in group.MemberNetworkObjectIds)
            {
                if (!TryGetLivingGameplayEnemy(
                        memberId,
                        out DiagnosticMeleeEnemy enemy))
                {
                    continue;
                }
                if (enemy.IsSuspended == suspended)
                {
                    continue;
                }
                if (!enemy.SetServerSuspended(suspended, out string error))
                {
                    for (int index = changedEnemies.Count - 1;
                         index >= 0;
                         index--)
                    {
                        changedEnemies[index].SetServerSuspended(
                            !suspended,
                            out _);
                    }
                    LastSpawnMessage =
                        $"Group {group.GroupId} could not " +
                        $"{(suspended ? "suspend" : "resume")}: {error}";
                    Debug.LogError($"[EnemyGroup] {LastSpawnMessage}");
                    return false;
                }
                changedEnemies.Add(enemy);
            }

            group.State = suspended
                ? GameplayEnemyGroupState.Suspended
                : GameplayEnemyGroupState.Active;
            LastSpawnMessage =
                $"Group {group.GroupId} " +
                $"{(suspended ? "suspended" : "resumed")}; " +
                $"living members={changedEnemies.Count}.";
            Debug.Log($"[EnemyGroup] {LastSpawnMessage}");
            return true;
        }

        private bool TryGetLivingGameplayEnemy(
            ulong networkObjectId,
            out DiagnosticMeleeEnemy enemy)
        {
            enemy = null;
            if (networkManager == null || networkManager.SpawnManager == null ||
                !networkManager.SpawnManager.SpawnedObjects.TryGetValue(
                    networkObjectId,
                    out NetworkObject networkObject) ||
                networkObject == null)
            {
                return false;
            }

            MidnightChaos.Combat.NetworkHealth health =
                networkObject.GetComponent<MidnightChaos.Combat.NetworkHealth>();
            enemy = networkObject.GetComponent<DiagnosticMeleeEnemy>();
            return health != null && !health.IsDead && enemy != null;
        }

        private bool IsAnyAlivePlayerWithin(
            Vector3 center,
            float maximumDistance)
        {
            float maximumDistanceSquared =
                maximumDistance * maximumDistance;
            foreach (NetworkClient client in networkManager.ConnectedClientsList)
            {
                NetworkObject player = client.PlayerObject;
                if (player == null || !player.IsSpawned)
                {
                    continue;
                }

                MidnightChaos.Combat.NetworkHealth health =
                    player.GetComponent<MidnightChaos.Combat.NetworkHealth>();
                if (health == null || health.IsDead)
                {
                    continue;
                }

                Vector3 delta = Vector3.ProjectOnPlane(
                    player.transform.position - center,
                    Vector3.up);
                if (delta.sqrMagnitude <= maximumDistanceSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryActivateDormantGroupServer(
            GameplayEnemyGroupRuntime group)
        {
            int requestedCount = ResolvedGameplayGroupSize;
            List<Vector3> plannedPositions =
                new List<Vector3>(requestedCount);
            if (!TryPlanGameplayGroup(
                    group.CenterPosition,
                    group.CenterIndex,
                    requestedCount,
                    plannedPositions,
                    out string error))
            {
                group.SpawnFailed = true;
                LastSpawnMessage =
                    $"Group {group.GroupId} failed to spawn 0/" +
                    $"{requestedCount} members. {error}";
                Debug.LogError($"[EnemyGroup] {LastSpawnMessage}");
                return false;
            }

            List<NetworkObject> spawnedGroup =
                new List<NetworkObject>(requestedCount);
            foreach (Vector3 position in plannedPositions)
            {
                if (!TrySpawnAt(
                        position,
                        group.GroupId,
                        out NetworkObject enemy,
                        out error))
                {
                    int spawnedCount = spawnedGroup.Count;
                    RollbackGameplayGroup(spawnedGroup);
                    group.SpawnFailed = true;
                    LastSpawnMessage =
                        $"Group {group.GroupId} failed to spawn " +
                        $"{spawnedCount}/{requestedCount} members. {error}";
                    Debug.LogError($"[EnemyGroup] {LastSpawnMessage}");
                    return false;
                }
                spawnedGroup.Add(enemy);
            }

            foreach (NetworkObject enemy in spawnedGroup)
            {
                group.MemberNetworkObjectIds.Add(enemy.NetworkObjectId);
                gameplayGroupByEnemyId[enemy.NetworkObjectId] = group.GroupId;
            }
            group.HasSpawned = true;
            group.State = GameplayEnemyGroupState.Active;
            LastSpawnMessage =
                $"Group {group.GroupId} activated with " +
                $"{spawnedGroup.Count}/{requestedCount} members.";
            Debug.Log($"[EnemyGroup] {LastSpawnMessage}");
            return true;
        }

        private void UpdateCompletedGroupsServer()
        {
            foreach (GameplayEnemyGroupRuntime group in gameplayGroups)
            {
                if (!group.HasSpawned ||
                    group.State == GameplayEnemyGroupState.Completed ||
                    group.MemberNetworkObjectIds.Count == 0)
                {
                    continue;
                }

                bool hasLivingMember = false;
                foreach (ulong memberId in group.MemberNetworkObjectIds)
                {
                    if (TryGetLivingGameplayEnemy(memberId, out _))
                    {
                        hasLivingMember = true;
                        break;
                    }
                }
                if (hasLivingMember)
                {
                    continue;
                }

                group.State = GameplayEnemyGroupState.Completed;
                LastSpawnMessage =
                    $"Group {group.GroupId} completed; all " +
                    $"{group.MemberNetworkObjectIds.Count} members are dead.";
                Debug.Log($"[EnemyGroup] {LastSpawnMessage}");
            }
        }

        private int CountGroups(GameplayEnemyGroupState state)
        {
            int count = 0;
            foreach (GameplayEnemyGroupRuntime group in gameplayGroups)
            {
                if (group.State == state)
                {
                    count++;
                }
            }
            return count;
        }

        private int CountLivingMembers(GameplayEnemyGroupState? state)
        {
            int count = 0;
            foreach (GameplayEnemyGroupRuntime group in gameplayGroups)
            {
                if (state.HasValue && group.State != state.Value)
                {
                    continue;
                }
                foreach (ulong memberId in group.MemberNetworkObjectIds)
                {
                    if (TryGetLivingGameplayEnemy(memberId, out _))
                    {
                        count++;
                    }
                }
            }
            return count;
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
            if (!TrySpawnAt(position, -1, out NetworkObject enemy, out error))
            {
                return false;
            }
            LastSpawnMessage =
                $"Debug enemy {enemy.NetworkObjectId} spawned in front of Host.";
            debugEnemyIds.Add(enemy.NetworkObjectId);
            Debug.Log($"[EnemySpawn] {LastSpawnMessage}");
            return true;
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
            debugEnemyIds.Clear();
            gameplayGroups.Clear();
            gameplayGroupByEnemyId.Clear();
            gameplayGroupsInitialized = false;
            nextProximityCheckTime = 0d;
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
            int groupId,
            out NetworkObject networkObject,
            out string error)
        {
            networkObject = null;
            bool gameplayGroup = groupId >= 0;
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
            DiagnosticEnemyEvolution evolution =
                instance.GetComponent<DiagnosticEnemyEvolution>();
            if (agent == null || networkObject == null || evolution == null)
            {
                Destroy(instance);
                error = "Enemy prefab thiếu NavMeshAgent, NetworkObject " +
                        "hoặc DiagnosticEnemyEvolution.";
                return false;
            }
            evolution.ConfigureGroupIdServer(groupId);
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
            if (networkManager == null || networkManager.SpawnManager == null)
            {
                gameplayGroupByEnemyId.Clear();
            }
            else
            {
                List<ulong> staleGameplayIds = null;
                foreach (ulong id in gameplayGroupByEnemyId.Keys)
                {
                    if (networkManager.SpawnManager.SpawnedObjects.ContainsKey(id))
                    {
                        continue;
                    }
                    staleGameplayIds ??= new List<ulong>();
                    staleGameplayIds.Add(id);
                }
                if (staleGameplayIds != null)
                {
                    foreach (ulong id in staleGameplayIds)
                    {
                        gameplayGroupByEnemyId.Remove(id);
                    }
                }
            }
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
