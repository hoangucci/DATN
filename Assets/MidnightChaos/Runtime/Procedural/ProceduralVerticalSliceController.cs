using MidnightChaos.Inventory;
using MidnightChaos.Resources;
using MidnightChaos.Enemies;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Procedural
{
    [DisallowMultipleComponent]
    public sealed class ProceduralVerticalSliceController : MonoBehaviour
    {
        private NetworkManager networkManager;
        private ProceduralWorldSettings settings;
        private VerticalSliceGameplaySettings gameplaySettings;
        private ProceduralWorldCoordinator world;
        private ProceduralEnemySpawnManager enemies;

        public void Initialize(
            NetworkManager configuredNetworkManager,
            ProceduralWorldSettings configuredSettings,
            VerticalSliceGameplaySettings configuredGameplaySettings,
            ProceduralWorldCoordinator configuredWorld,
            ProceduralEnemySpawnManager configuredEnemies)
        {
            networkManager = configuredNetworkManager;
            settings = configuredSettings;
            gameplaySettings = configuredGameplaySettings;
            world = configuredWorld;
            enemies = configuredEnemies;
            world.HostGenerationStarted += HandleHostGenerationStarted;
            world.HostWorldReady += HandleHostWorldReady;
        }

        private void HandleHostGenerationStarted()
        {
            if (networkManager == null || !networkManager.IsServer) return;
            foreach (DiagnosticWorldPickup pickup in
                     FindObjectsByType<DiagnosticWorldPickup>(
                         FindObjectsSortMode.None))
            {
                if (pickup != null && pickup.IsSpawned)
                {
                    pickup.NetworkObject.Despawn(true);
                }
            }
            foreach (DiagnosticResourceGatherer gatherer in
                     FindObjectsByType<DiagnosticResourceGatherer>(
                         FindObjectsSortMode.None))
            {
                gatherer.ClearHarvestStateServer();
            }
        }

        private void HandleHostWorldReady()
        {
            if (networkManager == null || !networkManager.IsServer ||
                settings == null)
            {
                return;
            }
            SpawnInitialSmallRocks();
            InitializeGameplayGroups();
        }

        private void InitializeGameplayGroups()
        {
            if (networkManager == null || !networkManager.IsServer ||
                world == null ||
                !world.IsWorldReady)
            {
                return;
            }
            int actualGroups = enemies.InitializeGameplayGroupsServer();
            Debug.Log(
                $"[EnemyGroup] World ready with {actualGroups} Dormant " +
                "gameplay groups. Proximity activation is server-authoritative.");
        }

        private void SpawnInitialSmallRocks()
        {
            if (gameplaySettings.WorldItemNetworkPrefab == null)
            {
                Debug.LogError(
                    "[Pickup] Cannot spawn procedural Small Rocks: world " +
                    "item prefab is missing.");
                return;
            }

            System.Collections.Generic.IReadOnlyList<Vector3> points =
                world.SmallRockPickupPoints;
            System.Collections.Generic.IReadOnlyList<Quaternion> rotations =
                world.SmallRockPickupRotations;
            int spawned = 0;
            for (int index = 0; index < points.Count; index++)
            {
                if (DiagnosticWorldPickup.SpawnServer(
                        gameplaySettings.WorldItemNetworkPrefab,
                        points[index] +
                        Vector3.up * settings.SmallRocks.GroundOffset,
                        index < rotations.Count
                            ? rotations[index]
                            : Quaternion.Euler(0f, index * 47f, 0f),
                        VerticalSliceItemId.Rock,
                        1) != null)
                {
                    spawned++;
                }
            }
            Debug.Log(
                $"[Pickup] Spawned {spawned}/{points.Count} procedural " +
                "Small Rocks across the world.");
        }

        private void OnDestroy()
        {
            if (world == null) return;
            world.HostGenerationStarted -= HandleHostGenerationStarted;
            world.HostWorldReady -= HandleHostWorldReady;
        }
    }
}
