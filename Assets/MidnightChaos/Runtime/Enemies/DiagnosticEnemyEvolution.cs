using System;
using MidnightChaos.Combat;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Enemies
{
    public enum DiagnosticEnemyStage : byte
    {
        Small = 0,
        Mature = 1,
        Alpha = 2
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkHealth))]
    public sealed class DiagnosticEnemyEvolution : NetworkBehaviour
    {
        private const string BodyVisualName = "BodyVisual";
        private static bool fallbackWarningLogged;

        [SerializeField, Min(1)] private int speciesId = 1;
        [SerializeField, Min(1)] private int matureMaxHealth = 120;
        [SerializeField] private ChaosEvolutionProfile evolutionProfile;

        private readonly NetworkVariable<byte> replicatedTier =
            new NetworkVariable<byte>(0);
        private readonly NetworkVariable<int> replicatedCharge =
            new NetworkVariable<int>(0);
        private readonly NetworkVariable<uint> feedbackSequence =
            new NetworkVariable<uint>(0);
        private readonly NetworkVariable<int> replicatedGroupId =
            new NetworkVariable<int>(
                -1,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkHealth health;
        private Transform bodyVisual;
        private DiagnosticEnemyVisual enemyVisual;
        private bool deathProcessedServer;
        private bool missingServiceLoggedServer;
        private bool shardDroppedServer;
        private int configuredGroupIdBeforeSpawn = -1;

        public event Action<DiagnosticEnemyStage, DiagnosticEnemyStage> StageChanged;
        public event Action<uint, uint> FeedbackRequested;

        public int CurrentTierIndex => replicatedTier.Value;
        public DiagnosticEnemyStage CurrentStage =>
            (DiagnosticEnemyStage)Mathf.Clamp(CurrentTierIndex, 0, 2);
        public string CurrentTierName => CurrentTier != null
            ? CurrentTier.DisplayName
            : $"Tier {CurrentTierIndex}";
        public int CurrentCharge => replicatedCharge.Value;
        public int SpeciesId => speciesId;
        public int GroupId => replicatedGroupId.Value;
        public bool IsFinalTier => CurrentTier == null || CurrentTier.FinalTier ||
                                   CurrentTierIndex >= Tiers.Length - 1;
        public bool CanReceiveCharge =>
            IsSpawned && health != null && !health.IsDead && !IsFinalTier;
        public int ChargeRequirement => CanReceiveCharge
            ? CurrentTier.ChargesToNextTier
            : 0;
        public float DamageMultiplier => CurrentTier?.DamageMultiplier ?? 1f;
        public float SpeedMultiplier => CurrentTier?.SpeedMultiplier ?? 1f;
        public float AttackReachMultiplier =>
            CurrentTier?.AttackReachMultiplier ?? 1f;
        public float WorldLabelHeightOffset =>
            GetBodyScale().y * 2f - 0.65f;

        private ChaosEvolutionTierSettings[] Tiers =>
            evolutionProfile != null
                ? evolutionProfile.Tiers
                : Array.Empty<ChaosEvolutionTierSettings>();
        private ChaosEvolutionTierSettings CurrentTier =>
            CurrentTierIndex >= 0 && CurrentTierIndex < Tiers.Length
                ? Tiers[CurrentTierIndex]
                : null;

        public void Configure(ChaosEvolutionProfile configuredEvolutionProfile)
        {
            evolutionProfile = configuredEvolutionProfile;
        }

        public bool ConfigureGroupIdServer(int groupId)
        {
            if (IsSpawned && !IsServer)
            {
                return false;
            }

            configuredGroupIdBeforeSpawn = groupId;
            if (IsSpawned)
            {
                replicatedGroupId.Value = groupId;
            }
            return true;
        }

        private void Awake()
        {
            health = GetComponent<NetworkHealth>();
            bodyVisual = transform.Find(BodyVisualName);
            enemyVisual = GetComponent<DiagnosticEnemyVisual>();
            if (evolutionProfile == null)
            {
                evolutionProfile =
                    UnityEngine.Resources.Load<ChaosEvolutionProfile>(
                        ChaosEvolutionProfile.ResourcePath);
                LogFallbackWarningOnce();
            }
        }

        private void LogFallbackWarningOnce()
        {
            if (fallbackWarningLogged)
            {
                return;
            }
            fallbackWarningLogged = true;
            Debug.LogWarning(
                "[Settings] DiagnosticEnemyEvolution had no injected Chaos " +
                "Evolution Profile; using Resources compatibility fallback.",
                this);
        }

        public override void OnNetworkSpawn()
        {
            replicatedTier.OnValueChanged += HandleTierChanged;
            feedbackSequence.OnValueChanged += HandleFeedbackSequenceChanged;
            if (Tiers.Length < 2 || Tiers[0] == null)
            {
                Debug.LogError(
                    "[ChaosEvolution] Chaos Evolution Profile tiers are invalid.",
                    this);
                enabled = false;
                return;
            }
            if (IsServer)
            {
                deathProcessedServer = false;
                missingServiceLoggedServer = false;
                shardDroppedServer = false;
                replicatedTier.Value = 0;
                replicatedCharge.Value = 0;
                replicatedGroupId.Value = configuredGroupIdBeforeSpawn;
                health.TrySetMaxHealthPreserveRatioServer(GetMaxHealth(0));
            }
            ApplyTierPresentation();
        }

        public override void OnNetworkDespawn()
        {
            replicatedTier.OnValueChanged -= HandleTierChanged;
            feedbackSequence.OnValueChanged -= HandleFeedbackSequenceChanged;
        }

        private void Update()
        {
            if (IsServer && IsSpawned && !deathProcessedServer &&
                health != null && health.IsDead)
            {
                CommitDeathServer();
            }
        }

        public bool TryReceiveChaosChargeServer(ulong sourceEnemyId) =>
            TryReceiveChaosChargeServer(sourceEnemyId, 1);

        public bool TryReceiveChaosChargeServer(
            ulong sourceEnemyId,
            int transferAmount)
        {
            if (!IsServer || !CanReceiveCharge || transferAmount <= 0)
            {
                return false;
            }
            replicatedCharge.Value = Mathf.Max(
                0,
                replicatedCharge.Value + transferAmount);
            feedbackSequence.Value++;
            Debug.Log(
                $"[ChaosTransfer] Enemy {NetworkObjectId} received " +
                $"{transferAmount} from {sourceEnemyId}; tier={CurrentTierName}, " +
                $"charge={CurrentCharge}.");

            int protection = Tiers.Length + 1;
            while (CanReceiveCharge && protection-- > 0)
            {
                int cost = ChargeRequirement;
                if (cost <= 0 || CurrentCharge < cost)
                {
                    break;
                }
                int previousTier = CurrentTierIndex;
                replicatedCharge.Value -= cost;
                ApplyTierServer(previousTier + 1);
                Debug.Log(
                    $"[ChaosEvolution] Enemy {NetworkObjectId}: " +
                    $"tier {previousTier} -> {CurrentTierIndex}, " +
                    $"consumed={cost}, remaining={CurrentCharge}.");
            }
            if (protection < 0)
            {
                Debug.LogError("[ChaosEvolution] Chain protection stopped invalid tier config.", this);
            }
            return true;
        }

        public bool TryMarkShardDroppedServer()
        {
            if (!IsServer || !IsFinalTier || shardDroppedServer)
            {
                return false;
            }
            shardDroppedServer = true;
            return true;
        }

        private void ApplyTierServer(int newTier)
        {
            if (!IsServer || health.IsDead || newTier <= CurrentTierIndex ||
                newTier < 0 || newTier >= Tiers.Length || Tiers[newTier] == null)
            {
                return;
            }
            health.TrySetMaxHealthPreserveRatioServer(GetMaxHealth(newTier));
            replicatedTier.Value = (byte)newTier;
            feedbackSequence.Value++;
        }

        private void CommitDeathServer()
        {
            DiagnosticChaosEvolutionService service =
                FindFirstObjectByType<DiagnosticChaosEvolutionService>();
            if (service == null)
            {
                if (!missingServiceLoggedServer)
                {
                    missingServiceLoggedServer = true;
                    Debug.LogError("[ChaosEvolution] Evolution service is missing.");
                }
                return;
            }
            deathProcessedServer = true;
            service.CommitEnemyDeathServer(this);
        }

        private int GetMaxHealth(int tierIndex)
        {
            ChaosEvolutionTierSettings tier =
                tierIndex >= 0 && tierIndex < Tiers.Length
                    ? Tiers[tierIndex]
                    : null;
            return Mathf.Max(
                1,
                Mathf.RoundToInt(
                    matureMaxHealth * (tier?.HealthMultiplier ?? 1f)));
        }

        private void HandleTierChanged(byte previous, byte current)
        {
            ApplyTierPresentation();
            StageChanged?.Invoke(
                (DiagnosticEnemyStage)Mathf.Clamp(previous, 0, 2),
                (DiagnosticEnemyStage)Mathf.Clamp(current, 0, 2));
        }

        private void HandleFeedbackSequenceChanged(uint previous, uint current)
        {
            if (current != previous) FeedbackRequested?.Invoke(previous, current);
        }

        private void ApplyTierPresentation()
        {
            if (bodyVisual == null) return;
            Vector3 scale = GetBodyScale();
            enemyVisual?.ApplyEvolutionScale(scale);
            bodyVisual.localScale = scale;
            bodyVisual.localPosition = new Vector3(0f, scale.y - 1f, 0f);
        }

        private Vector3 GetBodyScale() => CurrentTier?.BodyScale ?? Vector3.one;
    }
}
