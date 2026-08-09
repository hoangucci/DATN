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
        private const byte SmallChargeRequirement = 2;
        private const byte MatureChargeRequirement = 3;

        [Header("Gate F - Chaos Evolution")]
        [SerializeField, Min(1)] private int speciesId = 1;
        [SerializeField, Min(1)] private int matureMaxHealth = 120;
        [SerializeField] private Vector3 smallBodyScale =
            new Vector3(0.68f, 0.72f, 0.68f);
        [SerializeField] private Vector3 matureBodyScale =
            new Vector3(0.9f, 1.05f, 0.9f);
        [SerializeField] private Vector3 alphaBodyScale =
            new Vector3(1.35f, 1.55f, 1.35f);

        private NetworkVariable<byte> replicatedStage =
            new NetworkVariable<byte>(
                (byte)DiagnosticEnemyStage.Small,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkVariable<byte> replicatedCharge =
            new NetworkVariable<byte>(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkVariable<uint> feedbackSequence =
            new NetworkVariable<uint>(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkHealth health;
        private Transform bodyVisual;
        private DiagnosticEnemyVisual enemyVisual;
        private bool deathProcessedServer;
        private bool missingServiceLoggedServer;
        private bool shardDroppedServer;

        public event Action<DiagnosticEnemyStage, DiagnosticEnemyStage>
            StageChanged;
        public event Action<uint, uint> FeedbackRequested;

        public DiagnosticEnemyStage CurrentStage =>
            (DiagnosticEnemyStage)replicatedStage.Value;
        public byte CurrentCharge => replicatedCharge.Value;
        public int SpeciesId => speciesId;
        public bool CanReceiveCharge =>
            IsSpawned && !health.IsDead &&
            CurrentStage != DiagnosticEnemyStage.Alpha;
        public byte ChargeRequirement => CurrentStage switch
        {
            DiagnosticEnemyStage.Small => SmallChargeRequirement,
            DiagnosticEnemyStage.Mature => MatureChargeRequirement,
            _ => 0
        };
        public float DamageMultiplier => CurrentStage switch
        {
            DiagnosticEnemyStage.Small => 0.70f,
            DiagnosticEnemyStage.Mature => 1f,
            DiagnosticEnemyStage.Alpha => 1.50f,
            _ => 1f
        };
        public float SpeedMultiplier => CurrentStage switch
        {
            DiagnosticEnemyStage.Small => 1.15f,
            DiagnosticEnemyStage.Mature => 1f,
            DiagnosticEnemyStage.Alpha => 0.85f,
            _ => 1f
        };
        public float AttackReachMultiplier => CurrentStage switch
        {
            DiagnosticEnemyStage.Small => 0.85f,
            DiagnosticEnemyStage.Mature => 1f,
            DiagnosticEnemyStage.Alpha => 1.25f,
            _ => 1f
        };
        public float WorldLabelHeightOffset =>
            GetBodyScale(CurrentStage).y * 2f - 0.65f;

        private void Awake()
        {
            health = GetComponent<NetworkHealth>();
            bodyVisual = transform.Find(BodyVisualName);
            enemyVisual = GetComponent<DiagnosticEnemyVisual>();
        }

        public override void OnNetworkSpawn()
        {
            replicatedStage.OnValueChanged += HandleStageChanged;
            feedbackSequence.OnValueChanged += HandleFeedbackSequenceChanged;

            if (IsServer)
            {
                deathProcessedServer = false;
                missingServiceLoggedServer = false;
                shardDroppedServer = false;
                replicatedStage.Value = (byte)DiagnosticEnemyStage.Small;
                replicatedCharge.Value = 0;
                health.TrySetMaxHealthPreserveRatioServer(
                    GetMaxHealth(DiagnosticEnemyStage.Small));
            }

            ApplyStagePresentation(CurrentStage);
        }

        public override void OnNetworkDespawn()
        {
            replicatedStage.OnValueChanged -= HandleStageChanged;
            feedbackSequence.OnValueChanged -= HandleFeedbackSequenceChanged;
        }

        private void Update()
        {
            if (!IsServer ||
                !IsSpawned ||
                deathProcessedServer ||
                health == null ||
                !health.IsDead)
            {
                return;
            }

            CommitDeathServer();
        }

        public bool TryReceiveChaosChargeServer(ulong sourceEnemyId)
        {
            if (!IsServer || !CanReceiveCharge)
            {
                return false;
            }

            byte newCharge = (byte)(replicatedCharge.Value + 1);
            replicatedCharge.Value = newCharge;
            feedbackSequence.Value++;

            Debug.Log(
                $"[Gate F] Enemy {NetworkObjectId} received Chaos Charge " +
                $"from enemy {sourceEnemyId}. Stage {CurrentStage}, " +
                $"charge {newCharge}/{ChargeRequirement}.");

            if (newCharge < ChargeRequirement)
            {
                return true;
            }

            DiagnosticEnemyStage nextStage = CurrentStage switch
            {
                DiagnosticEnemyStage.Small => DiagnosticEnemyStage.Mature,
                DiagnosticEnemyStage.Mature => DiagnosticEnemyStage.Alpha,
                _ => DiagnosticEnemyStage.Alpha
            };

            ApplyStageServer(nextStage);
            return true;
        }

        public bool TryMarkShardDroppedServer()
        {
            if (!IsServer ||
                CurrentStage != DiagnosticEnemyStage.Alpha ||
                shardDroppedServer)
            {
                return false;
            }

            shardDroppedServer = true;
            return true;
        }

        private void ApplyStageServer(DiagnosticEnemyStage newStage)
        {
            if (!IsServer ||
                health.IsDead ||
                newStage == CurrentStage)
            {
                return;
            }

            DiagnosticEnemyStage previousStage = CurrentStage;
            int previousHealth = health.CurrentHealth;
            int previousMaxHealth = health.MaxHealth;

            health.TrySetMaxHealthPreserveRatioServer(GetMaxHealth(newStage));
            replicatedCharge.Value = 0;
            replicatedStage.Value = (byte)newStage;
            feedbackSequence.Value++;

            Debug.Log(
                $"[Gate F] Enemy {NetworkObjectId} evolved " +
                $"{previousStage} -> {newStage}. HP ratio preserved: " +
                $"{previousHealth}/{previousMaxHealth} -> " +
                $"{health.CurrentHealth}/{health.MaxHealth}.");
        }

        private void CommitDeathServer()
        {
            if (!IsServer || deathProcessedServer || !health.IsDead)
            {
                return;
            }

            DiagnosticChaosEvolutionService service =
                FindFirstObjectByType<DiagnosticChaosEvolutionService>();

            if (service == null)
            {
                if (!missingServiceLoggedServer)
                {
                    missingServiceLoggedServer = true;
                    Debug.LogError(
                        "[Gate F] Chaos Evolution service is missing. " +
                        $"Death {NetworkObjectId} is waiting for the service.");
                }

                return;
            }

            deathProcessedServer = true;
            service.CommitEnemyDeathServer(this);
        }

        private int GetMaxHealth(DiagnosticEnemyStage stage)
        {
            float multiplier = stage switch
            {
                DiagnosticEnemyStage.Small => 0.55f,
                DiagnosticEnemyStage.Mature => 1f,
                DiagnosticEnemyStage.Alpha => 2.20f,
                _ => 1f
            };

            return Mathf.Max(1, Mathf.RoundToInt(matureMaxHealth * multiplier));
        }

        private void HandleStageChanged(byte previous, byte current)
        {
            DiagnosticEnemyStage previousStage =
                (DiagnosticEnemyStage)previous;
            DiagnosticEnemyStage currentStage =
                (DiagnosticEnemyStage)current;

            ApplyStagePresentation(currentStage);
            StageChanged?.Invoke(previousStage, currentStage);
        }

        private void HandleFeedbackSequenceChanged(uint previous, uint current)
        {
            if (current != previous)
            {
                FeedbackRequested?.Invoke(previous, current);
            }
        }

        private void ApplyStagePresentation(DiagnosticEnemyStage stage)
        {
            if (bodyVisual == null)
            {
                return;
            }

            Vector3 bodyScale = GetBodyScale(stage);
            enemyVisual?.ApplyEvolutionScale(bodyScale);

            if (bodyVisual == null)
            {
                return;
            }

            bodyVisual.localScale = bodyScale;

            // The root stays at the same network position. Moving only the
            // visual/collider child keeps the capsule resting on the ground.
            bodyVisual.localPosition =
                new Vector3(0f, bodyScale.y - 1f, 0f);
        }

        private Vector3 GetBodyScale(DiagnosticEnemyStage stage)
        {
            return stage switch
            {
                DiagnosticEnemyStage.Small => smallBodyScale,
                DiagnosticEnemyStage.Mature => matureBodyScale,
                DiagnosticEnemyStage.Alpha => alphaBodyScale,
                _ => matureBodyScale
            };
        }
    }
}
