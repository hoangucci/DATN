using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Resources
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class DiagnosticResourceSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject resourceNodePrefab;

        private readonly List<NetworkObject> spawnedNodes =
            new List<NetworkObject>();

        private NetworkManager networkManager;

        public void Configure(GameObject prefab)
        {
            resourceNodePrefab = prefab;
        }

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();

            if (resourceNodePrefab == null)
            {
                Debug.LogError("[Gate C] Resource node prefab is missing.");
                enabled = false;
                return;
            }

            networkManager.OnServerStarted += HandleServerStarted;
            networkManager.OnServerStopped += HandleServerStopped;
        }

        private void HandleServerStarted()
        {
            if (!networkManager.IsServer)
            {
                return;
            }

            Vector3[] positions =
            {
                new Vector3(0f, 1.3f, 4f),
                new Vector3(4f, 1.3f, 0f),
                new Vector3(-4f, 1.3f, -1f)
            };

            foreach (Vector3 position in positions)
            {
                GameObject instance = Instantiate(
                    resourceNodePrefab,
                    position,
                    Quaternion.identity);

                NetworkObject networkObject =
                    instance.GetComponent<NetworkObject>();

                networkObject.Spawn(true);
                spawnedNodes.Add(networkObject);
            }
        }

        private void HandleServerStopped(bool wasHost)
        {
            spawnedNodes.Clear();
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
    }
}
