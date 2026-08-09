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
        private ChaosEvolutionProfile evolutionProfile;
        private ProceduralWorldCoordinator world;
        private ProceduralEnemySpawnManager enemies;
        private bool gameplayGroupSpawned;

        public void Initialize(
            NetworkManager configuredNetworkManager,
            ProceduralWorldSettings configuredSettings,
            VerticalSliceGameplaySettings configuredGameplaySettings,
            ChaosEvolutionProfile configuredEvolutionProfile,
            ProceduralWorldCoordinator configuredWorld,
            ProceduralEnemySpawnManager configuredEnemies)
        {
            networkManager = configuredNetworkManager;
            settings = configuredSettings;
            gameplaySettings = configuredGameplaySettings;
            evolutionProfile = configuredEvolutionProfile;
            world = configuredWorld;
            enemies = configuredEnemies;
            world.HostGenerationStarted += HandleHostGenerationStarted;
            world.HostWorldReady += HandleHostWorldReady;
        }

        private void HandleHostGenerationStarted()
        {
            if (networkManager == null || !networkManager.IsServer) return;
            gameplayGroupSpawned = false;
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
            SpawnInitialGameplayGroup();
        }

        private void SpawnInitialGameplayGroup()
        {
            if (gameplayGroupSpawned || networkManager == null ||
                !networkManager.IsServer || world == null ||
                !world.IsWorldReady)
            {
                return;
            }
            int requested = evolutionProfile.RequiredEnemyGroupSize;
            int actual = enemies.SpawnGameplayGroupServer(requested);
            gameplayGroupSpawned = actual == requested;
            if (gameplayGroupSpawned)
            {
                Debug.Log(
                    $"[EnemySpawn] Automatic gameplay group ready: " +
                    $"{actual}/{requested} after Host world/NavMesh ready.");
                return;
            }

            if (actual > 0)
            {
                enemies.ClearEnemiesServer();
            }
            Debug.LogError(
                $"[EnemySpawn] Automatic gameplay group failed: " +
                $"{actual}/{requested}. Any partial group was rolled back; " +
                "increase valid enemy spawn points.");
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
