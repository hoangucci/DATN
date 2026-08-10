using System;
using MidnightChaos.Combat;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

namespace MidnightChaos.Enemies
{
    public enum DiagnosticEnemyState : byte
    {
        Patrol = 0,
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
        private const int PatrolSampleAttempts = 8;
        private const float PatrolArrivalTolerance = 0.2f;
        private const int LineOfSightHitCapacity = 16;

        [Header("Enemy Content")]
        [SerializeField, Tooltip(
            "Single source for patrol, detection, chase, combat, visual, and animation tuning.")]
        private EnemyDefinition definition;

        private readonly RaycastHit[] lineOfSightHits =
            new RaycastHit[LineOfSightHitCapacity];

        private readonly NetworkVariable<byte> replicatedState =
            new NetworkVariable<byte>(
                (byte)DiagnosticEnemyState.Patrol,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<uint> replicatedAttackSequence =
            new NetworkVariable<uint>(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<uint> replicatedHitSequence =
            new NetworkVariable<uint>(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<double> replicatedDeathEndsAt =
            new NetworkVariable<double>(
                0d,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<double> replicatedSpawnEndsAt =
            new NetworkVariable<double>(
                0d,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> replicatedSuspended =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkHealth health;
        private DiagnosticEnemyEvolution evolution;
        private NavMeshAgent navMeshAgent;
        private NetworkObject targetPlayer;
        private NetworkObject lastRetargetCandidate;
        private double nextAllowedAttackTime;
        private double attackPoseEndsAt;
        private double attackImpactAt;
        private double hitReactionEndsAt;
        private double nextAllowedHitVisualTime;
        private double nextTargetEvaluationTime;
        private double nextRepathTime;
        private double nextPatrolDecisionTime;
        private double lastTargetVisibleTime;
        private bool waitingAtPatrolPoint;
        private bool serverNavigationReady;
        private bool serverMovementEnabled = true;
        private bool navigationFailureLogged;
        private Vector3 patrolCenter;
        private Vector3 currentDestination;
        private bool hasCurrentDestination;
        private Vector3 lastLineOfSightStart;
        private Vector3 lastLineOfSightEnd;
        private Vector3 lastLineOfSightHitPoint;
        private bool hasLineOfSightSample;
        private bool lastLineOfSightVisible;
        private NetworkObject pendingAttackTarget;
        private Vector3 lockedAttackDirection;
        private Vector3 lockedAttackTargetPosition;
        private bool attackImpactPending;
        private bool hasLockedAttackSample;
        private bool lastAttackImpactHit;
        private Collider[] gameplayColliders;
        private bool deathCommittedServer;
        private bool despawnCommittedServer;

        public event Action<DiagnosticEnemyState, DiagnosticEnemyState>
            StateChanged;
        public event Action<uint, uint> AttackSequenceChanged;
        public event Action<uint, uint> HitSequenceChanged;
        public event Action<bool, bool> SuspensionChanged;

        public DiagnosticEnemyState CurrentState =>
            (DiagnosticEnemyState)replicatedState.Value;
        public uint AttackSequence => replicatedAttackSequence.Value;
        public uint HitSequence => replicatedHitSequence.Value;
        public DiagnosticEnemyEvolution Evolution => evolution;
        public EnemyDefinition Definition => definition;
        public NetworkObject TargetPlayer => targetPlayer;
        public NetworkObject LastRetargetCandidate => lastRetargetCandidate;
        public Vector3 PatrolCenter => patrolCenter;
        public Vector3 CurrentDestination => currentDestination;
        public bool HasCurrentDestination => hasCurrentDestination;
        public bool ServerNavigationReady => serverNavigationReady;
        public bool ServerMovementEnabled => serverMovementEnabled;
        public bool IsSuspended => replicatedSuspended.Value;
        public Vector3 LastLineOfSightStart => lastLineOfSightStart;
        public Vector3 LastLineOfSightEnd => lastLineOfSightEnd;
        public Vector3 LastLineOfSightHitPoint => lastLineOfSightHitPoint;
        public bool HasLineOfSightSample => hasLineOfSightSample;
        public bool LastLineOfSightVisible => lastLineOfSightVisible;
        public Vector3 LockedAttackDirection => lockedAttackDirection;
        public Vector3 LockedAttackTargetPosition => lockedAttackTargetPosition;
        public bool HasLockedAttackSample => hasLockedAttackSample;
        public bool LastAttackImpactHit => lastAttackImpactHit;
        public double DeathEndsAt => replicatedDeathEndsAt.Value;
        public double SpawnEndsAt => replicatedSpawnEndsAt.Value;
        public double SynchronizedNetworkTime =>
            NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Time
                : Time.realtimeSinceStartupAsDouble;
        public NavMeshAgent Agent => navMeshAgent;

        private float CurrentAttackReach =>
            definition.AttackReach * evolution.AttackReachMultiplier;
        private int CurrentAttackDamage =>
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    definition.AttackDamage * evolution.DamageMultiplier));

        private void Awake()
        {
            health = GetComponent<NetworkHealth>();
            evolution = GetComponent<DiagnosticEnemyEvolution>();
            navMeshAgent = GetComponent<NavMeshAgent>();
            definition ??= GetComponent<DiagnosticEnemyVisual>()?.Definition;
            CacheGameplayColliders();
        }

        public override void OnNetworkSpawn()
        {
            replicatedState.OnValueChanged += HandleStateChanged;
            replicatedAttackSequence.OnValueChanged +=
                HandleAttackSequenceChanged;
            replicatedHitSequence.OnValueChanged += HandleHitSequenceChanged;
            replicatedSuspended.OnValueChanged += HandleSuspensionChanged;
            health.HealthChanged += HandleHealthChanged;

            if (CurrentState == DiagnosticEnemyState.Dead)
            {
                ApplyDeadGameplayState();
            }

            if (!IsServer)
            {
                navMeshAgent.enabled = false;
                return;
            }

            if (!ValidateServerConfiguration())
            {
                return;
            }

            patrolCenter = transform.position;
            currentDestination = patrolCenter;
            hasCurrentDestination = false;
            waitingAtPatrolPoint = false;
            double now = Time.realtimeSinceStartupAsDouble;
            nextTargetEvaluationTime = now;
            nextPatrolDecisionTime = now;
            lastTargetVisibleTime = now;
            replicatedSpawnEndsAt.Value = SynchronizedNetworkTime +
                                          definition.SpawnPresentationSeconds;
            serverNavigationReady = true;
            navMeshAgent.isStopped = IsSuspended || !serverMovementEnabled;
            SetStateServer(DiagnosticEnemyState.Patrol);
        }

        public override void OnNetworkDespawn()
        {
            StopMovingServer();
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = false;
            }

            replicatedState.OnValueChanged -= HandleStateChanged;
            replicatedAttackSequence.OnValueChanged -=
                HandleAttackSequenceChanged;
            replicatedHitSequence.OnValueChanged -= HandleHitSequenceChanged;
            replicatedSuspended.OnValueChanged -= HandleSuspensionChanged;
            health.HealthChanged -= HandleHealthChanged;
            targetPlayer = null;
            lastRetargetCandidate = null;
            pendingAttackTarget = null;
            attackImpactPending = false;
            serverNavigationReady = false;
            hasCurrentDestination = false;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || !serverNavigationReady)
            {
                return;
            }

            if (health.IsDead)
            {
                UpdateDeathServer();
                return;
            }

            if (IsSuspended)
            {
                return;
            }

            if (!navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            {
                FailNavigationOnce(
                    "NavMeshAgent left the NavMesh; server AI has been disabled.");
                return;
            }

            if (SynchronizedNetworkTime < replicatedSpawnEndsAt.Value)
            {
                StopMovingServer();
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < hitReactionEndsAt)
            {
                StopMovingServer();
                return;
            }

            if (UpdatePendingAttackServer(now))
            {
                return;
            }

            if (now >= nextTargetEvaluationTime)
            {
                EvaluateTargetServer(now);
                nextTargetEvaluationTime = now + definition.RetargetInterval;
            }

            if (targetPlayer == null)
            {
                UpdatePatrolServer(now);
                return;
            }

            UpdateCombatAndChaseServer(now);
        }

        public bool SetServerMovementEnabled(
            bool movementEnabled,
            out string error)
        {
            if (!IsServer || !IsSpawned)
            {
                error = "Enemy movement can only be changed by the active server.";
                return false;
            }
            if (health == null || health.IsDead)
            {
                error = "Dead enemy movement cannot be changed.";
                return false;
            }
            if (navMeshAgent == null ||
                !navMeshAgent.enabled ||
                !navMeshAgent.isOnNavMesh)
            {
                error = "Enemy NavMeshAgent is not ready.";
                return false;
            }

            serverMovementEnabled = movementEnabled;
            if (!movementEnabled)
            {
                navMeshAgent.ResetPath();
                hasCurrentDestination = false;
            }
            navMeshAgent.isStopped = IsSuspended || !movementEnabled;

            error = string.Empty;
            return true;
        }

        public bool SetServerSuspended(bool suspended, out string error)
        {
            if (!IsServer || !IsSpawned)
            {
                error =
                    "Enemy suspension can only be changed by the active server.";
                return false;
            }
            if (health == null || health.IsDead)
            {
                error = "Dead enemy suspension cannot be changed.";
                return false;
            }
            if (IsSuspended == suspended)
            {
                error = string.Empty;
                return true;
            }
            if (navMeshAgent == null || !navMeshAgent.enabled ||
                !navMeshAgent.isOnNavMesh)
            {
                error = "Enemy NavMeshAgent is not ready for suspension.";
                return false;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            ClearTransientAiStateServer(now);
            StopMovingServer();
            if (suspended)
            {
                navMeshAgent.isStopped = true;
                SetStateServer(DiagnosticEnemyState.Patrol);
                replicatedSuspended.Value = true;
                error = string.Empty;
                return true;
            }

            navMeshAgent.isStopped = !serverMovementEnabled;
            SetStateServer(DiagnosticEnemyState.Patrol);
            replicatedSuspended.Value = false;
            error = string.Empty;
            return true;
        }

        private void ClearTransientAiStateServer(double now)
        {
            targetPlayer = null;
            lastRetargetCandidate = null;
            pendingAttackTarget = null;
            attackImpactPending = false;
            attackPoseEndsAt = 0d;
            attackImpactAt = 0d;
            hitReactionEndsAt = 0d;
            nextAllowedAttackTime = now;
            nextTargetEvaluationTime = now;
            nextRepathTime = now;
            nextPatrolDecisionTime = now;
            lastTargetVisibleTime = now;
            waitingAtPatrolPoint = false;
            hasCurrentDestination = false;
            hasLockedAttackSample = false;
            lastAttackImpactHit = false;
        }

        private bool ValidateServerConfiguration()
        {
            if (definition == null)
            {
                FailNavigationOnce(
                    "EnemyDefinition is missing; server AI cannot start.");
                return false;
            }
            if (!navMeshAgent.enabled)
            {
                FailNavigationOnce(
                    "Host NavMeshAgent was not enabled by the spawn manager.");
                return false;
            }
            if (!navMeshAgent.isOnNavMesh)
            {
                FailNavigationOnce(
                    "Host NavMeshAgent is not placed on a NavMesh.");
                return false;
            }

            return true;
        }

        private void EvaluateTargetServer(double now)
        {
            NetworkObject previousTarget = targetPlayer;
            bool currentValid = IsLivingSpawnedPlayer(targetPlayer);
            float currentDistance = currentValid
                ? HorizontalDistance(transform.position, targetPlayer.transform.position)
                : float.PositiveInfinity;

            bool currentVisible = false;
            if (currentValid && currentDistance <= definition.LoseTargetRange)
            {
                currentVisible = HasLineOfSight(targetPlayer, true);
                if (currentVisible)
                {
                    lastTargetVisibleTime = now;
                }
                else if (now - lastTargetVisibleTime >
                         definition.LoseSightGraceSeconds)
                {
                    currentValid = false;
                }
            }
            else
            {
                currentValid = false;
            }

            NetworkObject nearestVisible =
                FindNearestVisibleLivingPlayerServer(out float nearestDistance);
            lastRetargetCandidate = nearestVisible;

            if (!currentValid)
            {
                targetPlayer = nearestVisible;
                if (targetPlayer != null)
                {
                    lastTargetVisibleTime = now;
                    ResetForChaseServer(now);
                }
            }
            else if (nearestVisible != null && nearestVisible != targetPlayer)
            {
                bool substantiallyCloser = nearestDistance +
                    definition.TargetSwitchAdvantage < currentDistance;
                bool tiedButStableWinner = Mathf.Approximately(
                        nearestDistance + definition.TargetSwitchAdvantage,
                        currentDistance) &&
                    nearestVisible.NetworkObjectId < targetPlayer.NetworkObjectId;

                if (substantiallyCloser || tiedButStableWinner)
                {
                    targetPlayer = nearestVisible;
                    lastTargetVisibleTime = now;
                    ResetForChaseServer(now);
                }
            }

            if (previousTarget != null && targetPlayer == null)
            {
                StopMovingServer();
                waitingAtPatrolPoint = false;
                nextPatrolDecisionTime = now;
            }

            if (targetPlayer != null && !currentVisible &&
                targetPlayer == previousTarget)
            {
                // Keep the current target during the configured LOS grace.
                HasLineOfSight(targetPlayer, true);
            }
        }

        private NetworkObject FindNearestVisibleLivingPlayerServer(
            out float bestDistance)
        {
            bestDistance = float.PositiveInfinity;
            if (NetworkManager == null)
            {
                return null;
            }

            float maximumDistanceSquared =
                definition.DetectionRange * definition.DetectionRange;
            float bestDistanceSquared = float.PositiveInfinity;
            NetworkObject bestTarget = null;

            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
            {
                NetworkObject candidate = client.PlayerObject;
                if (!IsLivingSpawnedPlayer(candidate))
                {
                    continue;
                }

                Vector3 delta = Vector3.ProjectOnPlane(
                    candidate.transform.position - transform.position,
                    Vector3.up);
                float distanceSquared = delta.sqrMagnitude;
                if (distanceSquared > maximumDistanceSquared)
                {
                    continue;
                }

                bool farther = distanceSquared > bestDistanceSquared;
                bool sameDistanceHigherId = Mathf.Approximately(
                        distanceSquared,
                        bestDistanceSquared) &&
                    bestTarget != null &&
                    candidate.NetworkObjectId > bestTarget.NetworkObjectId;
                if (farther || sameDistanceHigherId ||
                    !HasLineOfSight(candidate, candidate == targetPlayer))
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                bestTarget = candidate;
            }

            if (bestTarget != null)
            {
                bestDistance = Mathf.Sqrt(bestDistanceSquared);
            }

            return bestTarget;
        }

        private bool HasLineOfSight(
            NetworkObject candidate,
            bool storeDebugSample)
        {
            Vector3 start = transform.TransformPoint(definition.EyeOffset);
            Vector3 end = candidate.transform.TransformPoint(
                definition.TargetEyeOffset);
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                StoreLineOfSightDebug(start, end, end, true, storeDebugSample);
                return true;
            }

            int hitCount = Physics.RaycastNonAlloc(
                start,
                delta / distance,
                lineOfSightHits,
                distance,
                definition.LineOfSightMask,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.PositiveInfinity;
            Collider closestCollider = null;
            Vector3 closestPoint = end;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = lineOfSightHits[index];
                if (hit.collider == null ||
                    hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }
                if (hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                closestCollider = hit.collider;
                closestPoint = hit.point;
            }

            bool visible = closestCollider == null ||
                           closestCollider.transform.IsChildOf(
                               candidate.transform);
            StoreLineOfSightDebug(
                start,
                end,
                visible ? end : closestPoint,
                visible,
                storeDebugSample);
            return visible;
        }

        private void StoreLineOfSightDebug(
            Vector3 start,
            Vector3 end,
            Vector3 hitPoint,
            bool visible,
            bool shouldStore)
        {
            if (!shouldStore)
            {
                return;
            }

            lastLineOfSightStart = start;
            lastLineOfSightEnd = end;
            lastLineOfSightHitPoint = hitPoint;
            lastLineOfSightVisible = visible;
            hasLineOfSightSample = true;
        }

        private void UpdatePatrolServer(double now)
        {
            SetStateServer(DiagnosticEnemyState.Patrol);
            navMeshAgent.speed =
                definition.PatrolSpeed * evolution.SpeedMultiplier;
            navMeshAgent.stoppingDistance = 0f;

            if (navMeshAgent.pathPending)
            {
                return;
            }

            bool moving = navMeshAgent.hasPath &&
                          navMeshAgent.remainingDistance >
                          navMeshAgent.stoppingDistance + PatrolArrivalTolerance;
            if (moving)
            {
                return;
            }

            if (!waitingAtPatrolPoint)
            {
                navMeshAgent.ResetPath();
                hasCurrentDestination = false;
                waitingAtPatrolPoint = true;
                nextPatrolDecisionTime = now + UnityEngine.Random.Range(
                    definition.MinimumPatrolWait,
                    definition.MaximumPatrolWait);
                return;
            }
            if (now < nextPatrolDecisionTime)
            {
                return;
            }

            if (TryChoosePatrolDestinationServer(out Vector3 destination))
            {
                currentDestination = destination;
                hasCurrentDestination = true;
                waitingAtPatrolPoint = false;
                navMeshAgent.SetDestination(destination);
                return;
            }

            nextPatrolDecisionTime = now + definition.MinimumPatrolWait;
        }

        private bool TryChoosePatrolDestinationServer(out Vector3 destination)
        {
            float sampleRadius = Mathf.Max(1f, navMeshAgent.radius * 4f);
            for (int attempt = 0; attempt < PatrolSampleAttempts; attempt++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle *
                                 definition.PatrolRadius;
                Vector3 candidate = patrolCenter +
                    new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(
                        candidate,
                        out NavMeshHit hit,
                        sampleRadius,
                        navMeshAgent.areaMask))
                {
                    continue;
                }

                destination = hit.position;
                return true;
            }

            destination = patrolCenter;
            return false;
        }

        private void UpdateCombatAndChaseServer(double now)
        {
            Vector3 toTarget = Vector3.ProjectOnPlane(
                targetPlayer.transform.position - transform.position,
                Vector3.up);

            float attackReach = CurrentAttackReach;
            if (toTarget.sqrMagnitude <= attackReach * attackReach)
            {
                StopMovingServer();
                if (now < nextAllowedAttackTime)
                {
                    SetStateServer(DiagnosticEnemyState.Recover);
                    return;
                }

                BeginAttackServer(now, toTarget);

                return;
            }

            SetStateServer(DiagnosticEnemyState.Chase);
            navMeshAgent.speed =
                definition.ChaseSpeed * evolution.SpeedMultiplier;
            navMeshAgent.stoppingDistance = attackReach;
            if (now < nextRepathTime)
            {
                return;
            }

            currentDestination = targetPlayer.transform.position;
            hasCurrentDestination = true;
            navMeshAgent.SetDestination(currentDestination);
            nextRepathTime = now + definition.RepathInterval;
        }

        private void BeginAttackServer(double now, Vector3 toTarget)
        {
            if (toTarget.sqrMagnitude <= Mathf.Epsilon)
            {
                toTarget = transform.forward;
            }

            lockedAttackDirection = toTarget.normalized;
            lockedAttackTargetPosition = targetPlayer.transform.position;
            hasLockedAttackSample = true;
            lastAttackImpactHit = false;
            transform.rotation = Quaternion.LookRotation(
                lockedAttackDirection,
                Vector3.up);

            pendingAttackTarget = targetPlayer;
            attackImpactPending = true;
            attackImpactAt = now + definition.AttackImpactDelaySeconds;
            attackPoseEndsAt = now + Mathf.Max(
                definition.AttackPoseSeconds,
                definition.AttackImpactDelaySeconds);
            nextAllowedAttackTime = now + definition.AttackCooldownSeconds;
            replicatedAttackSequence.Value++;
            SetStateServer(DiagnosticEnemyState.Attack);
        }

        private bool UpdatePendingAttackServer(double now)
        {
            if (!attackImpactPending && now >= attackPoseEndsAt)
            {
                return false;
            }

            StopMovingServer();
            SetStateServer(DiagnosticEnemyState.Attack);
            if (attackImpactPending && now >= attackImpactAt)
            {
                attackImpactPending = false;
                lastAttackImpactHit = TryResolveAttackImpactServer();
                pendingAttackTarget = null;
            }

            return now < attackPoseEndsAt;
        }

        private bool TryResolveAttackImpactServer()
        {
            NetworkObject impactTarget = pendingAttackTarget;
            if (!IsLivingSpawnedPlayer(impactTarget))
            {
                return false;
            }

            Vector3 toTarget = Vector3.ProjectOnPlane(
                impactTarget.transform.position - transform.position,
                Vector3.up);
            float attackReach = CurrentAttackReach;
            if (toTarget.sqrMagnitude > attackReach * attackReach)
            {
                return false;
            }

            if (toTarget.sqrMagnitude > Mathf.Epsilon)
            {
                float minimumDot = Mathf.Cos(
                    definition.AttackConeHalfAngleDegrees * Mathf.Deg2Rad);
                if (Vector3.Dot(
                        lockedAttackDirection,
                        toTarget.normalized) < minimumDot)
                {
                    return false;
                }
            }

            if (!HasLineOfSight(impactTarget, true))
            {
                return false;
            }

            NetworkHealth targetHealth =
                impactTarget.GetComponent<NetworkHealth>();
            return targetHealth != null &&
                   targetHealth.TryApplyDamageServer(
                       CurrentAttackDamage,
                       NetworkObject);
        }

        private void ResetForChaseServer(double now)
        {
            waitingAtPatrolPoint = false;
            nextRepathTime = now;
        }

        private bool IsLivingSpawnedPlayer(NetworkObject candidate)
        {
            if (candidate == null || !candidate.IsSpawned)
            {
                return false;
            }

            NetworkHealth candidateHealth =
                candidate.GetComponent<NetworkHealth>();
            return candidateHealth != null && !candidateHealth.IsDead;
        }

        private void StopMovingServer()
        {
            hasCurrentDestination = false;
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
        }

        private void HandleStateChanged(byte previous, byte current)
        {
            if ((DiagnosticEnemyState)current == DiagnosticEnemyState.Dead)
            {
                ApplyDeadGameplayState();
            }

            StateChanged?.Invoke(
                (DiagnosticEnemyState)previous,
                (DiagnosticEnemyState)current);
        }

        private void HandleAttackSequenceChanged(uint previous, uint current)
        {
            if (previous != current)
            {
                AttackSequenceChanged?.Invoke(previous, current);
            }
        }

        private void HandleHitSequenceChanged(uint previous, uint current)
        {
            if (previous != current)
            {
                HitSequenceChanged?.Invoke(previous, current);
            }
        }

        private void HandleSuspensionChanged(bool previous, bool current)
        {
            SuspensionChanged?.Invoke(previous, current);
        }

        private void HandleHealthChanged(int previousHealth, int currentHealth)
        {
            if (!IsServer || currentHealth >= previousHealth)
            {
                return;
            }

            if (currentHealth > 0)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (now < attackPoseEndsAt ||
                    SynchronizedNetworkTime < replicatedSpawnEndsAt.Value)
                {
                    return;
                }

                hitReactionEndsAt = Math.Max(
                    hitReactionEndsAt,
                    now + definition.HitReactionSeconds);
                StopMovingServer();
                if (now >= nextAllowedHitVisualTime)
                {
                    nextAllowedHitVisualTime = now +
                                               definition.HitVisualCooldownSeconds;
                    replicatedHitSequence.Value++;
                }

                return;
            }

            targetPlayer = null;
            pendingAttackTarget = null;
            attackImpactPending = false;
            BeginDeathServer();
        }

        private void BeginDeathServer()
        {
            if (!IsServer || deathCommittedServer)
            {
                return;
            }

            deathCommittedServer = true;
            targetPlayer = null;
            pendingAttackTarget = null;
            attackImpactPending = false;
            hitReactionEndsAt = 0d;
            StopMovingServer();
            replicatedDeathEndsAt.Value = SynchronizedNetworkTime +
                                          definition.DeathPresentationSeconds;
            SetStateServer(DiagnosticEnemyState.Dead);
            ApplyDeadGameplayState();
        }

        private void UpdateDeathServer()
        {
            if (!deathCommittedServer)
            {
                BeginDeathServer();
            }

            if (despawnCommittedServer ||
                SynchronizedNetworkTime < replicatedDeathEndsAt.Value)
            {
                return;
            }

            despawnCommittedServer = true;
            NetworkObject.Despawn(true);
        }

        private void CacheGameplayColliders()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            Transform visualRoot =
                GetComponent<DiagnosticEnemyVisual>()?.VisualRoot;
            if (visualRoot == null)
            {
                gameplayColliders = colliders;
                return;
            }

            int gameplayCount = 0;
            foreach (Collider candidate in colliders)
            {
                if (candidate != null &&
                    !candidate.transform.IsChildOf(visualRoot))
                {
                    gameplayCount++;
                }
            }

            gameplayColliders = new Collider[gameplayCount];
            int index = 0;
            foreach (Collider candidate in colliders)
            {
                if (candidate != null &&
                    !candidate.transform.IsChildOf(visualRoot))
                {
                    gameplayColliders[index++] = candidate;
                }
            }
        }

        private void ApplyDeadGameplayState()
        {
            if (gameplayColliders != null)
            {
                foreach (Collider gameplayCollider in gameplayColliders)
                {
                    if (gameplayCollider != null)
                    {
                        gameplayCollider.enabled = false;
                    }
                }
            }

            if (navMeshAgent == null || !navMeshAgent.enabled)
            {
                return;
            }

            if (navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.ResetPath();
            }
            navMeshAgent.enabled = false;
        }

        private void FailNavigationOnce(string reason)
        {
            serverNavigationReady = false;
            if (navigationFailureLogged)
            {
                return;
            }

            navigationFailureLogged = true;
            Debug.LogError(
                $"[Enemy AI] {name}: {reason}",
                this);
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            return Vector3.ProjectOnPlane(second - first, Vector3.up).magnitude;
        }

#if UNITY_EDITOR
        public void ConfigureForDiagnostics(EnemyDefinition configuredDefinition)
        {
            definition = configuredDefinition;
        }
#endif
    }
}
