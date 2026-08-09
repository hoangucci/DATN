using System;
using System.Collections;
using System.Diagnostics;
using System.Collections.Generic;
using MidnightChaos.Inventory;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Procedural
{
    [DisallowMultipleComponent]
    public sealed class ProceduralWorldCoordinator : MonoBehaviour
    {
        private const string WorldDescriptorMessage =
            "MidnightChaos.Procedural.WorldDescriptor.v1";
        private const int DescriptorCapacity = 32;

        private NetworkManager networkManager;
        private ProceduralWorldSettings settings;
        private ProceduralRenderingSettings renderingSettings;
        private VerticalSliceGameplaySettings gameplaySettings;
        private ProceduralNavigationSettings navigationSettings;
        private ProceduralWorldGenerator generator;
        private RuntimeNavMeshBuilder navMeshBuilder;
        private ProceduralSpawnPointRegistry spawnPoints;
        private ProceduralEnemySpawnManager enemySpawnManager;
        private Coroutine generationRoutine;
        private uint generationToken;
        private bool clientHandlerRegistered;
        private bool hasPendingHostGeneration;
        private int pendingHostSeed;
        private uint pendingHostRevision;

        public int CurrentSeed { get; private set; }
        public uint Revision { get; private set; }
        public ulong LayoutHash { get; private set; }
        public ulong HostLayoutHash { get; private set; }
        public float GenerationTimeSeconds { get; private set; }
        public bool IsGenerating { get; private set; }
        public bool IsWorldReady { get; private set; }
        public bool LayoutMatchesHost { get; private set; } = true;
        public string StatusText { get; private set; } = "Waiting for LAN session";
        public string LastError { get; private set; } = string.Empty;

        public event Action HostGenerationStarted;
        public event Action HostWorldReady;

        public int GeneratedObjectCount =>
            generator != null ? generator.GeneratedObjectCount : 0;
        public int GeneratedTreeCount =>
            generator != null ? generator.GeneratedTreeCount : 0;
        public int GeneratedRockCount =>
            generator != null ? generator.GeneratedRockCount : 0;
        public int GeneratedOreCount =>
            generator != null ? generator.GeneratedOreCount : 0;
        public int GeneratedVegetationCount =>
            generator != null ? generator.GeneratedVegetationCount : 0;
        public int GeneratedGrassCount =>
            generator != null ? generator.GeneratedGrassCount : 0;
        public int GeneratedVegetationGameObjectCount =>
            generator != null
                ? generator.GeneratedVegetationGameObjectCount
                : 0;
        public int GeneratedGrassGameObjectCount =>
            generator != null ? generator.GeneratedGrassGameObjectCount : 0;
        public int InstancedVegetationCount =>
            generator != null ? generator.InstancedVegetationCount : 0;
        public int VegetationChunkCount =>
            generator != null ? generator.VegetationChunkCount : 0;
        public int VegetationDrawBatchCount =>
            generator != null ? generator.VegetationDrawBatchCount : 0;
        public int VisibleVegetationChunkCount =>
            generator != null
                ? generator.VisibleVegetationChunkCount
                : 0;
        public int SubmittedVegetationDrawCount =>
            generator != null
                ? generator.SubmittedVegetationDrawCount
                : 0;
        public int GrassClusterCount =>
            generator != null ? generator.GrassClusterCount : 0;
        public int GrassTargetCount =>
            generator != null ? generator.GrassTargetCount : 0;
        public int GrassSuccessfullyPlacedCount =>
            generator != null ? generator.GrassSuccessfullyPlacedCount : 0;
        public int GrassRejectedPlacementCount =>
            generator != null ? generator.GrassRejectedPlacementCount : 0;
        public IReadOnlyDictionary<string, int> GrassClusterCountsByStableId =>
            generator != null
                ? generator.GrassClusterCountsByStableId
                : EmptyGrassClusterCounts;
        private static readonly IReadOnlyDictionary<string, int>
            EmptyGrassClusterCounts = new Dictionary<string, int>();
        public int PlannedPlayerSpawnCount =>
            spawnPoints != null ? spawnPoints.PlayerSpawnPoints.Count : 0;
        public int PlannedEnemySpawnCount =>
            spawnPoints != null ? spawnPoints.EnemySpawnPoints.Count : 0;
        public int ValidPlayerSpawnCount =>
            spawnPoints != null ? spawnPoints.ValidPlayerSpawnCount : 0;
        public int ValidEnemySpawnCount =>
            spawnPoints != null ? spawnPoints.ValidEnemySpawnCount : 0;
        public IReadOnlyList<Vector3> SmallRockPickupPoints =>
            generator != null && generator.CurrentLayout != null
                ? generator.CurrentLayout.SmallRockPickupPoints
                : System.Array.Empty<Vector3>();
        public IReadOnlyList<Quaternion> SmallRockPickupRotations =>
            generator != null && generator.CurrentLayout != null
                ? generator.CurrentLayout.SmallRockPickupRotations
                : System.Array.Empty<Quaternion>();

        public void Initialize(
            NetworkManager configuredNetworkManager,
            ProceduralWorldSettings configuredSettings,
            ProceduralRenderingSettings configuredRenderingSettings,
            VerticalSliceGameplaySettings configuredGameplaySettings,
            ProceduralNavigationSettings configuredNavigationSettings,
            ProceduralWorldGenerator configuredGenerator,
            RuntimeNavMeshBuilder configuredNavMeshBuilder,
            ProceduralSpawnPointRegistry configuredSpawnPoints,
            ProceduralEnemySpawnManager configuredEnemySpawnManager)
        {
            networkManager = configuredNetworkManager;
            settings = configuredSettings;
            renderingSettings = configuredRenderingSettings;
            gameplaySettings = configuredGameplaySettings;
            navigationSettings = configuredNavigationSettings;
            generator = configuredGenerator;
            navMeshBuilder = configuredNavMeshBuilder;
            spawnPoints = configuredSpawnPoints;
            enemySpawnManager = configuredEnemySpawnManager;

            networkManager.OnServerStarted += HandleServerStarted;
            networkManager.OnClientStarted += HandleClientStarted;
            networkManager.OnServerStopped += HandleServerStopped;
            networkManager.OnClientStopped += HandleClientStopped;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
        }

        public bool TryRecreate(out string error)
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                error = "Chỉ Host được recreate map.";
                return false;
            }
            if (IsGenerating || navMeshBuilder.IsBuilding)
            {
                error = "Map hoặc NavMesh đang được tạo.";
                return false;
            }

            uint nextRevision = Revision + 1u;
            int nextSeed = DeterministicRandom.CreateNextSeed(
                CurrentSeed,
                nextRevision);
            BeginHostGeneration(nextSeed, nextRevision);
            error = string.Empty;
            return true;
        }

        private void HandleServerStarted()
        {
            int seed = settings.InitialSeed != 0
                ? settings.InitialSeed
                : CreateHostSeed();
            BeginHostGeneration(seed, 1u);
        }

        private void HandleClientStarted()
        {
            if (networkManager.IsHost)
            {
                return;
            }

            RegisterClientHandler();
            StatusText = "Connected - waiting for Host seed";
            LastError = string.Empty;
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!networkManager.IsServer ||
                clientId == NetworkManager.ServerClientId ||
                Revision == 0)
            {
                return;
            }

            SendDescriptor(clientId);
        }

        private void HandleServerStopped(bool wasHost)
        {
            ResetWorldState();
        }

        private void HandleClientStopped(bool wasHost)
        {
            UnregisterClientHandler();
            ResetWorldState();
        }

        private void BeginHostGeneration(int seed, uint revision)
        {
            if (generationRoutine != null)
            {
                if (navMeshBuilder.IsBuilding)
                {
                    // Unity does not expose cancellation for UpdateNavMesh.
                    // Let its owning coroutine finish and immediately replace
                    // the stale data with this pending Host generation.
                    pendingHostSeed = seed;
                    pendingHostRevision = revision;
                    hasPendingHostGeneration = true;
                    generationToken++;
                    StatusText = "Waiting for previous NavMesh build to finish...";
                    return;
                }

                StopCoroutine(generationRoutine);
                generationRoutine = null;
            }

            hasPendingHostGeneration = false;
            uint token = ++generationToken;
            generationRoutine = StartCoroutine(
                GenerateHostWorld(seed, revision, token));
        }

        private IEnumerator GenerateHostWorld(
            int seed,
            uint revision,
            uint token)
        {
            IsGenerating = true;
            IsWorldReady = false;
            LayoutMatchesHost = true;
            LastError = string.Empty;
            StatusText = "Host generating deterministic layout...";
            HostGenerationStarted?.Invoke();
            enemySpawnManager.ClearEnemiesServer();
            navMeshBuilder.Clear();
            generator.ClearGeneratedContent();
            spawnPoints.Clear();

            // Destroy(), NavMeshObstacle removal, and native NavMesh cleanup
            // settle at the frame boundary. Rebuilding in the same frame can
            // retain stale carving state after repeated Recreate operations.
            StatusText = "Clearing previous procedural world...";
            yield return null;
            if (token != generationToken)
            {
                IsGenerating = false;
                generationRoutine = null;
                yield break;
            }

            long startedAt = Stopwatch.GetTimestamp();
            ProceduralWorldLayout layout;
            try
            {
                layout = generator.Generate(
                    settings,
                    renderingSettings,
                    gameplaySettings,
                    seed,
                    revision);
            }
            catch (Exception exception)
            {
                FailGeneration(
                    $"Layout generation failed: {exception.Message}",
                    exception);
                yield break;
            }
            spawnPoints.ApplyLayout(
                layout,
                generator.GeneratedRoot,
                renderingSettings);

            CurrentSeed = seed;
            Revision = revision;
            LayoutHash = layout.LayoutHash;
            HostLayoutHash = layout.LayoutHash;
            StatusText = "Host building runtime NavMesh...";

            // Share the authoritative descriptor as soon as the deterministic
            // layout exists. Clients can build the same visible map while the
            // Host continues the authoritative NavMesh phase.
            BroadcastDescriptor();

            yield return navMeshBuilder.Rebuild(settings, navigationSettings);

            if (navigationSettings.NavMeshCarvingSettleSeconds > 0f)
            {
                StatusText = "Waiting for NavMeshObstacle carving...";
                yield return new WaitForSecondsRealtime(
                    navigationSettings.NavMeshCarvingSettleSeconds);
            }
            else
            {
                // At minimum allow NavMesh.onPreUpdate to process obstacles.
                yield return null;
            }

            if (token != generationToken)
            {
                // A regenerate/disconnect may cancel this coroutine while the
                // asynchronous NavMesh operation is still in flight. Remove
                // the now-stale NavMesh data before a newer world can use it.
                if (!navMeshBuilder.IsBuilding)
                {
                    navMeshBuilder.Clear();
                }

                IsGenerating = false;
                generationRoutine = null;

                if (hasPendingHostGeneration &&
                    networkManager != null &&
                    networkManager.IsServer)
                {
                    int pendingSeed = pendingHostSeed;
                    uint pendingRevision = pendingHostRevision;
                    hasPendingHostGeneration = false;
                    BeginHostGeneration(pendingSeed, pendingRevision);
                }

                yield break;
            }

            GenerationTimeSeconds = ElapsedSeconds(startedAt);
            IsGenerating = false;
            generationRoutine = null;

            if (!navMeshBuilder.IsReady)
            {
                FailGeneration(navMeshBuilder.StatusText);
                yield break;
            }

            spawnPoints.ValidateAfterNavMesh(navigationSettings);
            if (spawnPoints.ValidPlayerSpawnCount !=
                spawnPoints.PlayerSpawnPoints.Count)
            {
                string error =
                    $"Player spawn validation failed: " +
                    $"{spawnPoints.ValidPlayerSpawnCount}/" +
                    $"{spawnPoints.PlayerSpawnPoints.Count} points are safe.";
                FailGeneration(error);
                yield break;
            }
            if (spawnPoints.ValidEnemySpawnCount !=
                spawnPoints.EnemySpawnPoints.Count)
            {
                string error =
                    $"Enemy spawn validation failed: " +
                    $"{spawnPoints.ValidEnemySpawnCount}/" +
                    $"{spawnPoints.EnemySpawnPoints.Count} points are on NavMesh.";
                FailGeneration(error);
                yield break;
            }

            IsWorldReady = true;
            StatusText =
                "World ready - automatic gameplay group starts now; " +
                "Spawn Enemy is debug only";
            HostWorldReady?.Invoke();
        }

        private void ReceiveDescriptor(
            ulong senderClientId,
            FastBufferReader reader)
        {
            if (networkManager == null ||
                !networkManager.IsClient ||
                networkManager.IsServer ||
                senderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            reader.ReadValueSafe(out int seed);
            reader.ReadValueSafe(out uint revision);
            reader.ReadValueSafe(out int generatorVersion);
            reader.ReadValueSafe(out ulong hostHash);

            if (generatorVersion != settings.GeneratorVersion)
            {
                IsWorldReady = false;
                LayoutMatchesHost = false;
                LastError =
                    $"GeneratorVersion mismatch: Host {generatorVersion}, " +
                    $"Client {settings.GeneratorVersion}.";
                StatusText = "Generation rejected";
                return;
            }
            if (revision < Revision)
            {
                return;
            }

            IsGenerating = true;
            IsWorldReady = false;
            LastError = string.Empty;
            StatusText = "Client generating map from Host seed...";
            long startedAt = Stopwatch.GetTimestamp();

            ProceduralWorldLayout layout;
            try
            {
                layout = generator.Generate(
                    settings,
                    renderingSettings,
                    gameplaySettings,
                    seed,
                    revision);
            }
            catch (Exception exception)
            {
                LayoutMatchesHost = false;
                FailGeneration(
                    $"Client layout generation failed: {exception.Message}",
                    exception);
                return;
            }
            spawnPoints.ApplyLayout(
                layout,
                generator.GeneratedRoot,
                renderingSettings);

            CurrentSeed = seed;
            Revision = revision;
            LayoutHash = layout.LayoutHash;
            HostLayoutHash = hostHash;
            LayoutMatchesHost = LayoutHash == HostLayoutHash;
            GenerationTimeSeconds = ElapsedSeconds(startedAt);
            IsGenerating = false;
            IsWorldReady = LayoutMatchesHost;

            if (LayoutMatchesHost)
            {
                StatusText = "Client world ready - layout hash matches Host";
            }
            else
            {
                LastError =
                    $"Layout mismatch. Host {HostLayoutHash:X16}, " +
                    $"Client {LayoutHash:X16}.";
                StatusText = "Generation mismatch";
            }
        }

        private void BroadcastDescriptor()
        {
            if (!networkManager.IsServer || Revision == 0)
            {
                return;
            }

            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                if (clientId != NetworkManager.ServerClientId)
                {
                    SendDescriptor(clientId);
                }
            }
        }

        private void SendDescriptor(ulong clientId)
        {
            using FastBufferWriter writer = new FastBufferWriter(
                DescriptorCapacity,
                Allocator.Temp);
            writer.WriteValueSafe(CurrentSeed);
            writer.WriteValueSafe(Revision);
            writer.WriteValueSafe(settings.GeneratorVersion);
            writer.WriteValueSafe(LayoutHash);
            networkManager.CustomMessagingManager.SendNamedMessage(
                WorldDescriptorMessage,
                clientId,
                writer,
                NetworkDelivery.ReliableSequenced);
        }

        private void RegisterClientHandler()
        {
            if (clientHandlerRegistered ||
                networkManager.CustomMessagingManager == null)
            {
                return;
            }

            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                WorldDescriptorMessage,
                ReceiveDescriptor);
            clientHandlerRegistered = true;
        }

        private void UnregisterClientHandler()
        {
            if (!clientHandlerRegistered ||
                networkManager == null ||
                networkManager.CustomMessagingManager == null)
            {
                clientHandlerRegistered = false;
                return;
            }

            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                WorldDescriptorMessage);
            clientHandlerRegistered = false;
        }

        private void ResetWorldState()
        {
            generationToken++;
            hasPendingHostGeneration = false;
            IsGenerating = false;
            IsWorldReady = false;
            LayoutMatchesHost = true;
            CurrentSeed = 0;
            Revision = 0;
            LayoutHash = 0;
            HostLayoutHash = 0;
            GenerationTimeSeconds = 0f;
            LastError = string.Empty;
            StatusText = "Waiting for LAN session";
            enemySpawnManager.ResetTracking();
            generator.ClearGeneratedContent();
            spawnPoints.Clear();
            if (!navMeshBuilder.IsBuilding)
            {
                navMeshBuilder.Clear();
            }
        }

        private void FailGeneration(
            string error,
            Exception exception = null)
        {
            LastError = string.IsNullOrWhiteSpace(error)
                ? "Unknown procedural generation failure."
                : error;
            StatusText = "Generation failed";
            IsGenerating = false;
            IsWorldReady = false;
            generationRoutine = null;

            if (exception != null)
            {
                UnityEngine.Debug.LogException(exception, this);
            }
            UnityEngine.Debug.LogError(
                $"[Procedural] {LastError}",
                this);
        }

        private static int CreateHostSeed()
        {
            long ticks = DateTime.UtcNow.Ticks;
            int folded = unchecked((int)(ticks ^ (ticks >> 32)));
            return DeterministicRandom.DeriveSeed(folded, 0xC001D00Du);
        }

        private static float ElapsedSeconds(long startedAt)
        {
            return (float)((Stopwatch.GetTimestamp() - startedAt) /
                           (double)Stopwatch.Frequency);
        }

        private void OnDestroy()
        {
            UnregisterClientHandler();
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnServerStarted -= HandleServerStarted;
            networkManager.OnClientStarted -= HandleClientStarted;
            networkManager.OnServerStopped -= HandleServerStopped;
            networkManager.OnClientStopped -= HandleClientStopped;
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
        }
    }
}
