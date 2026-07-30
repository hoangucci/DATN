using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class DiagnosticChaosEvolutionService : MonoBehaviour
    {
        [SerializeField] private GameObject chaosShardPrefab;
        [SerializeField, Min(0.1f)] private float evolutionRadius = 12f;

        private NetworkManager networkManager;

        public void Configure(GameObject shardPrefab)
        {
            chaosShardPrefab = shardPrefab;
        }

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();

            if (chaosShardPrefab == null)
            {
                Debug.LogError(
                    "[Gate F] Chaos Shard prefab is missing. " +
                    "Charge transfer remains active, but an Alpha cannot " +
                    "drop its shard until the scene is rebuilt.");
            }
        }

        public void CommitEnemyDeathServer(
            DiagnosticEnemyEvolution deadEnemy)
        {
            if (!enabled ||
                networkManager == null ||
                !networkManager.IsServer ||
                deadEnemy == null ||
                !deadEnemy.IsSpawned)
            {
                return;
            }

            if (deadEnemy.CurrentStage == DiagnosticEnemyStage.Alpha)
            {
                TrySpawnChaosShardServer(deadEnemy);
                return;
            }

            DiagnosticEnemyEvolution receiver =
                FindNearestEligibleReceiverServer(deadEnemy);

            if (receiver == null)
            {
                Debug.Log(
                    $"[Gate F] Enemy {deadEnemy.NetworkObjectId} died with " +
                    "no eligible same-species receiver within " +
                    $"{evolutionRadius:0.#} m. Its charge was lost.");
                return;
            }

            receiver.TryReceiveChaosChargeServer(
                deadEnemy.NetworkObjectId);
        }

        private DiagnosticEnemyEvolution
            FindNearestEligibleReceiverServer(
                DiagnosticEnemyEvolution deadEnemy)
        {
            DiagnosticEnemyEvolution[] candidates =
                FindObjectsByType<DiagnosticEnemyEvolution>(
                    FindObjectsSortMode.None);

            float maximumDistanceSquared =
                evolutionRadius * evolutionRadius;
            float bestDistanceSquared = float.PositiveInfinity;
            DiagnosticEnemyEvolution bestTarget = null;

            foreach (DiagnosticEnemyEvolution candidate in candidates)
            {
                if (candidate == null ||
                    candidate == deadEnemy ||
                    !candidate.IsSpawned ||
                    !candidate.CanReceiveCharge ||
                    candidate.SpeciesId != deadEnemy.SpeciesId)
                {
                    continue;
                }

                Vector3 delta = Vector3.ProjectOnPlane(
                    candidate.transform.position -
                    deadEnemy.transform.position,
                    Vector3.up);

                float distanceSquared = delta.sqrMagnitude;
                if (distanceSquared > maximumDistanceSquared)
                {
                    continue;
                }

                bool closer =
                    distanceSquared < bestDistanceSquared - 0.0001f;
                bool deterministicTie =
                    Mathf.Abs(distanceSquared - bestDistanceSquared) <=
                    0.0001f &&
                    (bestTarget == null ||
                     candidate.NetworkObjectId <
                     bestTarget.NetworkObjectId);

                if (!closer && !deterministicTie)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                bestTarget = candidate;
            }

            return bestTarget;
        }

        private void TrySpawnChaosShardServer(
            DiagnosticEnemyEvolution deadAlpha)
        {
            if (chaosShardPrefab == null)
            {
                Debug.LogError(
                    "[Gate F] Alpha died, but Chaos Shard prefab is missing. " +
                    "Run Create or Refresh LAN Test Scene.");
                return;
            }

            if (!deadAlpha.TryMarkShardDroppedServer())
            {
                return;
            }

            GameObject shardInstance = Instantiate(
                chaosShardPrefab,
                deadAlpha.transform.position + Vector3.up * 0.35f,
                Quaternion.Euler(0f, 45f, 45f));

            NetworkObject shardNetworkObject =
                shardInstance.GetComponent<NetworkObject>();

            if (shardNetworkObject == null)
            {
                Debug.LogError(
                    "[Gate F] Spawned Chaos Shard has no NetworkObject.");
                Destroy(shardInstance);
                return;
            }

            shardNetworkObject.Spawn(true);
            Debug.Log(
                $"[Gate F] Alpha {deadAlpha.NetworkObjectId} spawned " +
                $"Chaos Shard {shardNetworkObject.NetworkObjectId}.");
        }
    }
}
