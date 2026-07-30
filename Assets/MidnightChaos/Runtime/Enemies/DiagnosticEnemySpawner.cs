using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

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

        private NetworkManager networkManager;
        private readonly List<NetworkObject> spawnedEnemies =
            new List<NetworkObject>();

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

        private void HandleServerStarted()
        {
            if (!networkManager.IsServer || HasSpawnedEnemy())
            {
                return;
            }

            // Spawn the center enemy first so NetworkObjectId is the stable
            // tie-break winner while the diagnostic cluster is still intact.
            SpawnEnemyServer(clusterCenter);

            for (int index = 0; index < surroundingEnemyCount; index++)
            {
                float angle =
                    index * Mathf.PI * 2f / surroundingEnemyCount;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * clusterRadius,
                    0f,
                    Mathf.Sin(angle) * clusterRadius);

                SpawnEnemyServer(clusterCenter + offset);
            }
        }

        private void HandleServerStopped(bool wasHost)
        {
            spawnedEnemies.Clear();
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

        private void SpawnEnemyServer(Vector3 position)
        {
            GameObject instance = Instantiate(
                enemyPrefab,
                position,
                Quaternion.identity);

            NetworkObject networkObject =
                instance.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                Debug.LogError(
                    "[Gate F] Spawned enemy has no NetworkObject.");
                Destroy(instance);
                return;
            }

            networkObject.Spawn(true);
            spawnedEnemies.Add(networkObject);
        }
    }
}
