using MidnightChaos.Inventory;
using MidnightChaos.Procedural;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class DiagnosticChaosEvolutionService : MonoBehaviour
    {
        private static bool fallbackWarningLogged;

        [SerializeField] private GameObject chaosShardPrefab;
        [SerializeField] private VerticalSliceGameplaySettings gameplaySettings;
        [SerializeField] private ChaosEvolutionProfile evolutionProfile;

        private NetworkManager networkManager;

        public void Configure(
            VerticalSliceGameplaySettings configuredGameplaySettings,
            ChaosEvolutionProfile configuredEvolutionProfile,
            GameObject shardPrefab)
        {
            gameplaySettings = configuredGameplaySettings;
            evolutionProfile = configuredEvolutionProfile;
            chaosShardPrefab = shardPrefab;
        }

        public void Configure(GameObject shardPrefab)
        {
            chaosShardPrefab = shardPrefab;
        }

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
        }

        private void Start()
        {
            EnsureDependencies();
        }

        public void CommitEnemyDeathServer(DiagnosticEnemyEvolution deadEnemy)
        {
            EnsureDependencies();
            if (!enabled || networkManager == null || !networkManager.IsServer ||
                deadEnemy == null || !deadEnemy.IsSpawned ||
                gameplaySettings == null || evolutionProfile == null)
            {
                return;
            }

            if (deadEnemy.IsFinalTier)
            {
                TrySpawnChaosShardServer(deadEnemy);
                return;
            }

            int transferAmount = deadEnemy.CurrentCharge +
                                 evolutionProfile.BaseDeathCharge;
            DiagnosticEnemyEvolution receiver =
                FindNearestEligibleReceiverServer(deadEnemy);
            if (receiver == null)
            {
                Debug.Log(
                    $"[ChaosTransfer] Enemy {deadEnemy.NetworkObjectId} died: " +
                    $"stored={deadEnemy.CurrentCharge}, death=" +
                    $"{evolutionProfile.BaseDeathCharge}, no target; " +
                    $"charge {transferAmount} lost.");
                return;
            }
            Debug.Log(
                $"[ChaosTransfer] Enemy {deadEnemy.NetworkObjectId} died: " +
                $"stored={deadEnemy.CurrentCharge}, death=" +
                $"{evolutionProfile.BaseDeathCharge}, transferred=" +
                $"{transferAmount} -> {receiver.NetworkObjectId}.");
            receiver.TryReceiveChaosChargeServer(
                deadEnemy.NetworkObjectId,
                transferAmount);
        }

        private void EnsureDependencies()
        {
            bool usedFallback = false;
            if (gameplaySettings == null)
            {
                gameplaySettings =
                    UnityEngine.Resources.Load<VerticalSliceGameplaySettings>(
                        VerticalSliceGameplaySettings.ResourcePath);
                usedFallback = true;
            }
            if (evolutionProfile == null)
            {
                evolutionProfile =
                    UnityEngine.Resources.Load<ChaosEvolutionProfile>(
                        ChaosEvolutionProfile.ResourcePath);
                usedFallback = true;
            }
            if (!usedFallback || fallbackWarningLogged)
            {
                return;
            }
            fallbackWarningLogged = true;
            Debug.LogWarning(
                "[Settings] DiagnosticChaosEvolutionService had missing " +
                "injected settings; using Resources compatibility fallback.",
                this);
        }

        private DiagnosticEnemyEvolution FindNearestEligibleReceiverServer(
            DiagnosticEnemyEvolution deadEnemy)
        {
            float maximumDistanceSquared =
                evolutionProfile.EvolutionRadius *
                evolutionProfile.EvolutionRadius;
            float bestDistanceSquared = float.PositiveInfinity;
            DiagnosticEnemyEvolution best = null;
            foreach (DiagnosticEnemyEvolution candidate in
                     FindObjectsByType<DiagnosticEnemyEvolution>(
                         FindObjectsSortMode.None))
            {
                if (candidate == null || candidate == deadEnemy ||
                    !candidate.IsSpawned || !candidate.CanReceiveCharge ||
                    candidate.SpeciesId != deadEnemy.SpeciesId)
                {
                    continue;
                }
                float distanceSquared = Vector3.ProjectOnPlane(
                    candidate.transform.position - deadEnemy.transform.position,
                    Vector3.up).sqrMagnitude;
                if (distanceSquared > maximumDistanceSquared)
                {
                    continue;
                }
                bool closer = distanceSquared < bestDistanceSquared - 0.0001f;
                bool tie = Mathf.Abs(distanceSquared - bestDistanceSquared) <=
                           0.0001f &&
                           (best == null || candidate.NetworkObjectId <
                            best.NetworkObjectId);
                if (!closer && !tie) continue;
                bestDistanceSquared = distanceSquared;
                best = candidate;
            }
            return best;
        }

        private void TrySpawnChaosShardServer(
            DiagnosticEnemyEvolution deadEnemy)
        {
            GameObject prefab = gameplaySettings.WorldItemNetworkPrefab != null
                ? gameplaySettings.WorldItemNetworkPrefab
                : chaosShardPrefab;
            if (prefab == null || !deadEnemy.TryMarkShardDroppedServer())
            {
                Debug.LogError("[ChaosDrop] World item prefab is missing.");
                return;
            }
            NetworkObject shard = DiagnosticWorldPickup.SpawnServer(
                prefab,
                deadEnemy.transform.position + Vector3.up * 0.5f,
                Quaternion.Euler(0f, 45f, 45f),
                VerticalSliceItemId.ChaosShard,
                evolutionProfile.ChaosShardAmount);
            if (shard != null)
            {
                Debug.Log(
                    $"[ChaosDrop] Final enemy {deadEnemy.NetworkObjectId} " +
                    $"dropped ChaosShard x" +
                    $"{evolutionProfile.ChaosShardAmount}.");
            }
        }
    }
}
