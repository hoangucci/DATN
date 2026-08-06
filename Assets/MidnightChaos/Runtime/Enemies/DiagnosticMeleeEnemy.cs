using System.Collections;
using MidnightChaos.Combat;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

namespace MidnightChaos.Enemies
{
    public enum DiagnosticEnemyState : byte
    {
        Idle = 0,
        Chase = 1,
        Attack = 2,
        Recover = 3,
        Dead = 4
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(NetworkHealth))]
    [RequireComponent(typeof(DiagnosticEnemyEvolution))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class DiagnosticMeleeEnemy : NetworkBehaviour
    {
        [Header("Gate F - Stage-Aware Host Melee AI")]
        [SerializeField, Min(0.1f)] private float detectionRange = 7.5f;
        [SerializeField, Min(0.1f)] private float loseTargetRange = 12f;
        [SerializeField, Min(0.1f)] private float attackReach = 1.8f;
        [SerializeField, Min(1)] private int attackDamage = 20;
        [SerializeField, Min(0.05f)] private float attackCooldownSeconds = 1.15f;
        [SerializeField, Min(0.02f)] private float attackPoseSeconds = 0.18f;

        private NetworkVariable<byte> replicatedState =
            new NetworkVariable<byte>(
                (byte)DiagnosticEnemyState.Idle,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkHealth health;
        private DiagnosticEnemyEvolution evolution;
        private NetworkObject targetPlayer;
        private Renderer bodyRenderer;
        private NavMeshAgent navMeshAgent;
        private Coroutine feedbackRoutine;
        private bool feedbackFlashActive;
        private double nextAllowedAttackTime;
        private double attackPoseEndsAt;
        private float baseAgentSpeed;

        public DiagnosticEnemyState CurrentState =>
            (DiagnosticEnemyState)replicatedState.Value;
        public DiagnosticEnemyEvolution Evolution => evolution;

        private float CurrentMoveSpeed =>
            baseAgentSpeed * evolution.SpeedMultiplier;
        private float CurrentAttackReach =>
            attackReach * evolution.AttackReachMultiplier;
        private int CurrentAttackDamage =>
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    attackDamage * evolution.DamageMultiplier));

        private void Awake()
        {
            health = GetComponent<NetworkHealth>();
            evolution = GetComponent<DiagnosticEnemyEvolution>();
            bodyRenderer = GetComponentInChildren<Renderer>();
            navMeshAgent = GetComponent<NavMeshAgent>();
            baseAgentSpeed = navMeshAgent.speed;
        }

        public override void OnNetworkSpawn()
        {
            navMeshAgent.enabled = IsServer;
            replicatedState.OnValueChanged += HandleStateChanged;
            health.HealthChanged += HandleHealthChanged;
            evolution.StageChanged += HandleStageChanged;
            evolution.FeedbackRequested += HandleFeedbackRequested;

            if (IsServer)
            {
                SetStateServer(DiagnosticEnemyState.Idle);
            }

            RefreshVisuals();
        }

        public override void OnNetworkDespawn()
        {
            StopMovingServer();
            navMeshAgent.enabled = false;
            replicatedState.OnValueChanged -= HandleStateChanged;
            health.HealthChanged -= HandleHealthChanged;
            evolution.StageChanged -= HandleStageChanged;
            evolution.FeedbackRequested -= HandleFeedbackRequested;
            targetPlayer = null;

            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
                feedbackRoutine = null;
            }

            feedbackFlashActive = false;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            if (health.IsDead)
            {
                targetPlayer = null;
                StopMovingServer();
                SetStateServer(DiagnosticEnemyState.Dead);
                return;
            }

            if (!IsTargetValidServer(targetPlayer))
            {
                targetPlayer = FindNearestLivingPlayerServer();
            }

            if (targetPlayer == null)
            {
                StopMovingServer();
                SetStateServer(DiagnosticEnemyState.Idle);
                return;
            }

            Vector3 toTarget = Vector3.ProjectOnPlane(
                targetPlayer.transform.position - transform.position,
                Vector3.up);

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < attackPoseEndsAt)
            {
                StopMovingServer();
                SetStateServer(DiagnosticEnemyState.Attack);
                return;
            }

            float distanceSquared = toTarget.sqrMagnitude;
            float currentAttackReach = CurrentAttackReach;
            if (distanceSquared <= currentAttackReach * currentAttackReach)
            {
                StopMovingServer();
                if (now < nextAllowedAttackTime)
                {
                    SetStateServer(DiagnosticEnemyState.Recover);
                    return;
                }

                NetworkHealth targetHealth =
                    targetPlayer.GetComponent<NetworkHealth>();

                if (targetHealth != null &&
                    targetHealth.TryApplyDamageServer(
                        CurrentAttackDamage,
                        NetworkObject))
                {
                    nextAllowedAttackTime = now + attackCooldownSeconds;
                    attackPoseEndsAt = now + attackPoseSeconds;
                    SetStateServer(DiagnosticEnemyState.Attack);
                }

                return;
            }

            SetStateServer(DiagnosticEnemyState.Chase);
            if (navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.speed = CurrentMoveSpeed;
                navMeshAgent.stoppingDistance = currentAttackReach;
                navMeshAgent.SetDestination(targetPlayer.transform.position);
            }
        }

        private NetworkObject FindNearestLivingPlayerServer()
        {
            if (!IsServer || NetworkManager == null)
            {
                return null;
            }

            float maximumDistanceSquared = detectionRange * detectionRange;
            float bestDistanceSquared = float.PositiveInfinity;
            NetworkObject bestTarget = null;

            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
            {
                NetworkObject candidate = client.PlayerObject;
                if (candidate == null || !candidate.IsSpawned)
                {
                    continue;
                }

                NetworkHealth candidateHealth =
                    candidate.GetComponent<NetworkHealth>();

                if (candidateHealth == null || candidateHealth.IsDead)
                {
                    continue;
                }

                Vector3 delta = Vector3.ProjectOnPlane(
                    candidate.transform.position - transform.position,
                    Vector3.up);

                float distanceSquared = delta.sqrMagnitude;
                if (distanceSquared > maximumDistanceSquared ||
                    distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                bestTarget = candidate;
            }

            return bestTarget;
        }

        private bool IsTargetValidServer(NetworkObject candidate)
        {
            if (!IsServer || candidate == null || !candidate.IsSpawned)
            {
                return false;
            }

            NetworkHealth candidateHealth =
                candidate.GetComponent<NetworkHealth>();

            if (candidateHealth == null || candidateHealth.IsDead)
            {
                return false;
            }

            Vector3 delta = Vector3.ProjectOnPlane(
                candidate.transform.position - transform.position,
                Vector3.up);

            return delta.sqrMagnitude <= loseTargetRange * loseTargetRange;
        }

        private void StopMovingServer()
        {
            if (navMeshAgent == null ||
                !navMeshAgent.enabled ||
                !navMeshAgent.isOnNavMesh)
            {
                return;
            }

            navMeshAgent.ResetPath();
        }

        private void SetStateServer(DiagnosticEnemyState state)
        {
            if (!IsServer || replicatedState.Value == (byte)state)
            {
                return;
            }

            replicatedState.Value = (byte)state;
            RefreshVisuals();
        }

        private void HandleStateChanged(byte previous, byte current)
        {
            RefreshVisuals();
        }

        private void HandleHealthChanged(int previousHealth, int currentHealth)
        {
            if (IsServer && currentHealth <= 0)
            {
                targetPlayer = null;
                SetStateServer(DiagnosticEnemyState.Dead);
            }

            RefreshVisuals();
        }

        private void HandleStageChanged(
            DiagnosticEnemyStage previousStage,
            DiagnosticEnemyStage currentStage)
        {
            RefreshVisuals();
        }

        private void HandleFeedbackRequested(uint previous, uint current)
        {
            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
            }

            feedbackRoutine = StartCoroutine(ShowEvolutionFeedback());
        }

        private IEnumerator ShowEvolutionFeedback()
        {
            feedbackFlashActive = true;
            RefreshVisuals();
            yield return new WaitForSecondsRealtime(0.24f);
            feedbackFlashActive = false;
            RefreshVisuals();
            feedbackRoutine = null;
        }

        private void RefreshVisuals()
        {
            if (bodyRenderer == null)
            {
                return;
            }

            if (feedbackFlashActive)
            {
                bodyRenderer.material.color =
                    new Color(0.95f, 0.62f, 1f);
                return;
            }

            Color stageColor = evolution.CurrentStage switch
            {
                DiagnosticEnemyStage.Small =>
                    new Color(0.48f, 0.26f, 0.78f),
                DiagnosticEnemyStage.Mature =>
                    new Color(0.72f, 0.20f, 0.86f),
                DiagnosticEnemyStage.Alpha =>
                    new Color(0.94f, 0.18f, 1f),
                _ => Color.magenta
            };

            bodyRenderer.material.color = CurrentState switch
            {
                DiagnosticEnemyState.Idle => stageColor,
                DiagnosticEnemyState.Chase =>
                    Color.Lerp(
                        stageColor,
                        new Color(1f, 0.48f, 0.08f),
                        0.48f),
                DiagnosticEnemyState.Attack => new Color(1f, 0.08f, 0.08f),
                DiagnosticEnemyState.Recover => new Color(0.95f, 0.78f, 0.16f),
                DiagnosticEnemyState.Dead => new Color(0.22f, 0.05f, 0.05f),
                _ => Color.magenta
            };
        }
    }
}
