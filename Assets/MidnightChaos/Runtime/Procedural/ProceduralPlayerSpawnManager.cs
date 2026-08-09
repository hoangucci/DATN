using System.Collections.Generic;
using MidnightChaos.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace MidnightChaos.Procedural
{
    [DisallowMultipleComponent]
    public sealed class ProceduralPlayerSpawnManager : MonoBehaviour
    {
        private readonly Dictionary<ulong, NetworkObject> activePlayers =
            new Dictionary<ulong, NetworkObject>();

        private NetworkManager networkManager;
        private ProceduralNavigationSettings navigationSettings;
        private ProceduralSpawnPointRegistry spawnPoints;
        private ProceduralWorldCoordinator world;
        private GameObject playerPrefab;
        private int nextSpawnPointIndex;

        public string LastSpawnMessage { get; private set; } =
            "Waiting for procedural world.";

        public void Initialize(
            NetworkManager configuredNetworkManager,
            ProceduralNavigationSettings configuredNavigationSettings,
            ProceduralSpawnPointRegistry configuredSpawnPoints,
            ProceduralWorldCoordinator configuredWorld,
            GameObject configuredPlayerPrefab)
        {
            networkManager = configuredNetworkManager;
            navigationSettings = configuredNavigationSettings;
            spawnPoints = configuredSpawnPoints;
            world = configuredWorld;
            playerPrefab = configuredPlayerPrefab;

            world.HostGenerationStarted += HandleHostGenerationStarted;
            world.HostWorldReady += HandleHostWorldReady;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        private void HandleHostGenerationStarted()
        {
            ClearPlayersServer();
        }

        private void HandleHostWorldReady()
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            List<ulong> connectedIds =
                new List<ulong>(networkManager.ConnectedClientsIds);
            connectedIds.Sort();
            foreach (ulong clientId in connectedIds)
            {
                TrySpawnPlayerServer(clientId);
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (networkManager != null &&
                networkManager.IsServer &&
                world != null &&
                world.IsWorldReady)
            {
                TrySpawnPlayerServer(clientId);
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            activePlayers.Remove(clientId);
        }

        private bool TrySpawnPlayerServer(ulong clientId)
        {
            if (networkManager == null || !networkManager.IsServer ||
                playerPrefab == null || navigationSettings == null ||
                spawnPoints == null || !world.IsWorldReady)
            {
                return false;
            }

            if (networkManager.ConnectedClients.TryGetValue(
                    clientId,
                    out NetworkClient client) &&
                client.PlayerObject != null &&
                client.PlayerObject.IsSpawned)
            {
                activePlayers[clientId] = client.PlayerObject;
                return true;
            }

            int count = spawnPoints.PlayerSpawnPoints.Count;
            if (count == 0)
            {
                LastSpawnMessage = "Map has no valid player spawn points.";
                Debug.LogError("[Procedural Player] " + LastSpawnMessage, this);
                return false;
            }

            for (int offset = 0; offset < count; offset++)
            {
                int index = (nextSpawnPointIndex + offset) % count;
                if (!spawnPoints.TryGetPlayerSpawnPoint(index, out Vector3 planned) ||
                    !NavMesh.SamplePosition(
                        planned,
                        out NavMeshHit hit,
                        navigationSettings.NavMeshSampleRadius,
                        NavMesh.AllAreas))
                {
                    continue;
                }

                Vector3 spawnPosition = hit.position + Vector3.up;
                GameObject instance = Instantiate(
                    playerPrefab,
                    spawnPosition,
                    Quaternion.identity);
                NetworkObject networkObject =
                    instance.GetComponent<NetworkObject>();
                DiagnosticNetworkPlayer player =
                    instance.GetComponent<DiagnosticNetworkPlayer>();
                if (networkObject == null || player == null)
                {
                    Destroy(instance);
                    LastSpawnMessage =
                        "Player prefab is missing NetworkObject or " +
                        "DiagnosticNetworkPlayer.";
                    Debug.LogError(
                        "[Procedural Player] " + LastSpawnMessage,
                        this);
                    return false;
                }

                networkObject.SpawnAsPlayerObject(clientId, true);
                activePlayers[clientId] = networkObject;
                nextSpawnPointIndex = (index + 1) % count;
                LastSpawnMessage =
                    $"Spawned client {clientId} at player point {index}.";
                return true;
            }

            LastSpawnMessage =
                $"No NavMesh player spawn is available for client {clientId}.";
            Debug.LogError("[Procedural Player] " + LastSpawnMessage, this);
            return false;
        }

        private void ClearPlayersServer()
        {
            if (networkManager != null && networkManager.IsServer)
            {
                foreach (NetworkObject player in activePlayers.Values)
                {
                    if (player != null && player.IsSpawned)
                    {
                        player.Despawn(true);
                    }
                }
            }

            activePlayers.Clear();
            nextSpawnPointIndex = 0;
            LastSpawnMessage = "Players cleared for world generation.";
        }

        private void OnDestroy()
        {
            if (world != null)
            {
                world.HostGenerationStarted -= HandleHostGenerationStarted;
                world.HostWorldReady -= HandleHostWorldReady;
            }
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= HandleClientConnected;
                networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }
    }
}
