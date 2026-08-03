using System;
using System.Collections;
using System.Collections.Generic;
using MidnightChaos.Equipment;
using MidnightChaos.Resources;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace MidnightChaos.Combat
{
    public readonly struct DiagnosticAttackPresentation
    {
        public byte ProfileSlot { get; }
        public int MotionIndex { get; }
        public float AttackInterval { get; }
        public float AttackSpeedMultiplier { get; }

        public DiagnosticAttackPresentation(
            byte profileSlot,
            int motionIndex,
            float attackInterval,
            float attackSpeedMultiplier)
        {
            ProfileSlot = profileSlot;
            MotionIndex = motionIndex;
            AttackInterval = attackInterval;
            AttackSpeedMultiplier = attackSpeedMultiplier;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkHealth))]
    [RequireComponent(typeof(DiagnosticPlayerEquipment))]
    [RequireComponent(typeof(DiagnosticResourceGatherer))]
    public sealed class DiagnosticMeleeCombat : NetworkBehaviour
    {
        public const byte UnarmedProfileSlot = 0;
        public const byte SwordProfileSlot = 1;

        private const string AttackIndicatorName = "AttackIndicator";

        private struct ReplicatedAttackPresentation :
            INetworkSerializable,
            IEquatable<ReplicatedAttackPresentation>
        {
            public uint Sequence;
            public float AttackInterval;
            public float AttackSpeedMultiplier;
            public byte ProfileSlot;
            public byte MotionIndex;

            public ReplicatedAttackPresentation(
                uint sequence,
                float attackInterval,
                float attackSpeedMultiplier,
                byte profileSlot,
                byte motionIndex)
            {
                Sequence = sequence;
                AttackInterval = attackInterval;
                AttackSpeedMultiplier = attackSpeedMultiplier;
                ProfileSlot = profileSlot;
                MotionIndex = motionIndex;
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer)
                where T : IReaderWriter
            {
                serializer.SerializeValue(ref Sequence);
                serializer.SerializeValue(ref AttackInterval);
                serializer.SerializeValue(ref AttackSpeedMultiplier);
                serializer.SerializeValue(ref ProfileSlot);
                serializer.SerializeValue(ref MotionIndex);
            }

            public bool Equals(ReplicatedAttackPresentation other)
            {
                return Sequence == other.Sequence &&
                       AttackInterval.Equals(other.AttackInterval) &&
                       AttackSpeedMultiplier.Equals(other.AttackSpeedMultiplier) &&
                       ProfileSlot == other.ProfileSlot &&
                       MotionIndex == other.MotionIndex;
            }
        }

        private readonly struct PendingServerHit
        {
            public uint Sequence { get; }
            public double ExecuteAt { get; }
            public int Damage { get; }
            public float AttackReach { get; }
            public float AttackHalfAngle { get; }

            public PendingServerHit(
                uint sequence,
                double executeAt,
                int damage,
                float attackReach,
                float attackHalfAngle)
            {
                Sequence = sequence;
                ExecuteAt = executeAt;
                Damage = damage;
                AttackReach = attackReach;
                AttackHalfAngle = attackHalfAngle;
            }
        }

        [Header("Gate H3 - ScriptableObject Configuration")]
        [SerializeField] private DiagnosticMeleeCombatSettings combatSettings;
        [SerializeField] private DiagnosticMeleeAttackProfile unarmedAttackProfile;
        [SerializeField] private DiagnosticMeleeAttackProfile swordAttackProfile;

        // These hidden fields only preserve v0.8.1.2 Inspector values long
        // enough for the combat migration command to copy them into assets.
        [FormerlySerializedAs("attackReach")]
        [SerializeField, HideInInspector] private float legacyAttackReach = 2.6f;
        [FormerlySerializedAs("attackHalfAngle")]
        [SerializeField, HideInInspector] private float legacyAttackHalfAngle = 65f;
        [FormerlySerializedAs("unarmedDamage")]
        [SerializeField, HideInInspector] private int legacyUnarmedDamage = 25;
        [FormerlySerializedAs("swordDamage")]
        [SerializeField, HideInInspector] private int legacySwordDamage = 40;
        [FormerlySerializedAs("cooldownSeconds")]
        [SerializeField, HideInInspector] private float legacyCooldownSeconds = 0.65f;
        [FormerlySerializedAs("inputBufferSeconds")]
        [SerializeField, HideInInspector] private float legacyInputBufferSeconds = 0.15f;
        [FormerlySerializedAs("indicatorDuration")]
        [SerializeField, HideInInspector] private float legacyIndicatorDuration = 0.14f;

        private NetworkVariable<ReplicatedAttackPresentation>
            replicatedAttackPresentation =
                new NetworkVariable<ReplicatedAttackPresentation>(
                    default,
                    NetworkVariableReadPermission.Everyone,
                    NetworkVariableWritePermission.Server);

        private NetworkVariable<float> replicatedAttackSpeedMultiplier =
            new NetworkVariable<float>(
                1f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkHealth health;
        private DiagnosticPlayerEquipment equipment;
        private DiagnosticResourceGatherer resourceGatherer;
        private Renderer attackIndicator;
        private Coroutine indicatorRoutine;
        private double nextAllowedServerAttackTime;
        private bool serverAttackHeld;
        private bool hasBufferedServerAttack;
        private bool localAttackHeld;
        private int lastServerMotionIndex = -1;
        private bool configurationValid;
        private readonly List<PendingServerHit> pendingServerHits =
            new List<PendingServerHit>();

        public DiagnosticMeleeCombatSettings Settings => combatSettings;
        public DiagnosticMeleeAttackProfile CurrentAttackProfile =>
            GetAttackProfile(GetCurrentProfileSlot());
        public int CurrentDamage => CurrentAttackProfile != null
            ? CurrentAttackProfile.Damage
            : 0;
        public float CurrentAttackSpeedMultiplier =>
            replicatedAttackSpeedMultiplier.Value;
        public float CurrentAttackInterval => CalculateAttackInterval(
            CurrentAttackProfile,
            CurrentAttackSpeedMultiplier);

        public float LegacyAttackReach => legacyAttackReach;
        public float LegacyAttackHalfAngle => legacyAttackHalfAngle;
        public int LegacyUnarmedDamage => legacyUnarmedDamage;
        public int LegacySwordDamage => legacySwordDamage;
        public float LegacyCooldownSeconds => legacyCooldownSeconds;
        public float LegacyInputBufferSeconds => legacyInputBufferSeconds;
        public float LegacyIndicatorDuration => legacyIndicatorDuration;

        public event Action<DiagnosticAttackPresentation> AttackAccepted;
        public event Action<uint> HitConfirmed;

        private void Awake()
        {
            health = GetComponent<NetworkHealth>();
            equipment = GetComponent<DiagnosticPlayerEquipment>();
            resourceGatherer = GetComponent<DiagnosticResourceGatherer>();
            configurationValid = ValidateConfiguration(false);

            Transform indicatorTransform = transform.Find(AttackIndicatorName);
            if (indicatorTransform != null)
            {
                attackIndicator = indicatorTransform.GetComponent<Renderer>();
                attackIndicator.enabled = false;
            }
        }

        public override void OnNetworkSpawn()
        {
            replicatedAttackPresentation.OnValueChanged +=
                HandleAttackPresentationChanged;

            configurationValid = ValidateConfiguration(true);
            localAttackHeld = false;

            if (IsServer)
            {
                nextAllowedServerAttackTime = 0d;
                serverAttackHeld = false;
                hasBufferedServerAttack = false;
                lastServerMotionIndex = -1;
                pendingServerHits.Clear();
                replicatedAttackSpeedMultiplier.Value = 1f;
            }
        }

        public override void OnNetworkDespawn()
        {
            replicatedAttackPresentation.OnValueChanged -=
                HandleAttackPresentationChanged;
            localAttackHeld = false;
            serverAttackHeld = false;
            hasBufferedServerAttack = false;
            pendingServerHits.Clear();

            if (indicatorRoutine != null)
            {
                StopCoroutine(indicatorRoutine);
                indicatorRoutine = null;
            }

            if (attackIndicator != null)
            {
                attackIndicator.enabled = false;
            }
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                ProcessPendingHitsServer();
                ProcessHeldOrBufferedAttackServer();
            }

            if (!IsOwner)
            {
                return;
            }

            bool shouldHoldAttack =
                configurationValid &&
                health != null &&
                !health.IsDead &&
                ReadLocalAttackHeld();

            if (shouldHoldAttack == localAttackHeld)
            {
                return;
            }

            localAttackHeld = shouldHoldAttack;
            SetAttackHeldRpc(localAttackHeld);
        }

        public void Configure(
            DiagnosticMeleeCombatSettings configuredSettings,
            DiagnosticMeleeAttackProfile configuredUnarmedProfile,
            DiagnosticMeleeAttackProfile configuredSwordProfile)
        {
            combatSettings = configuredSettings;
            unarmedAttackProfile = configuredUnarmedProfile;
            swordAttackProfile = configuredSwordProfile;
            configurationValid = ValidateConfiguration(false);
        }

        public DiagnosticMeleeAttackProfile GetAttackProfile(byte profileSlot)
        {
            return profileSlot == SwordProfileSlot
                ? swordAttackProfile
                : unarmedAttackProfile;
        }

        public bool TrySetAttackSpeedMultiplierServer(float multiplier)
        {
            if (!IsServer || !IsSpawned || combatSettings == null ||
                float.IsNaN(multiplier) || float.IsInfinity(multiplier))
            {
                return false;
            }

            replicatedAttackSpeedMultiplier.Value =
                combatSettings.ClampAttackSpeedMultiplier(multiplier);
            return true;
        }

        private bool ReadLocalAttackHeld()
        {
            bool keyboardAttack =
                Keyboard.current != null &&
                Keyboard.current.fKey.isPressed;

            bool mouseAttack =
                Mouse.current != null &&
                Cursor.lockState == CursorLockMode.Locked &&
                Mouse.current.leftButton.isPressed;

            return keyboardAttack || mouseAttack;
        }

        [Rpc(
            SendTo.Server,
            InvokePermission = RpcInvokePermission.Owner)]
        private void SetAttackHeldRpc(bool isHeld)
        {
            if (!IsServer)
            {
                return;
            }

            serverAttackHeld =
                isHeld &&
                configurationValid &&
                health != null &&
                !health.IsDead;

            if (!serverAttackHeld)
            {
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextAllowedServerAttackTime)
            {
                hasBufferedServerAttack = false;
                ExecuteAcceptedAttackServer(now);
                return;
            }

            double remainingCooldown = nextAllowedServerAttackTime - now;
            if (remainingCooldown <= combatSettings.InputBufferSeconds)
            {
                // A quick click may reserve one attack near the end of the
                // cooldown. Repeated presses never create a queue.
                hasBufferedServerAttack = true;
            }
        }

        private void ProcessHeldOrBufferedAttackServer()
        {
            if (!configurationValid || health == null || health.IsDead)
            {
                serverAttackHeld = false;
                hasBufferedServerAttack = false;
                return;
            }

            if (!serverAttackHeld && !hasBufferedServerAttack)
            {
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextAllowedServerAttackTime)
            {
                return;
            }

            hasBufferedServerAttack = false;
            ExecuteAcceptedAttackServer(now);
        }

        private void ExecuteAcceptedAttackServer(double now)
        {
            if (!IsServer || !configurationValid ||
                health == null || health.IsDead)
            {
                return;
            }

            byte profileSlot = GetCurrentProfileSlot();
            DiagnosticMeleeAttackProfile profile = GetAttackProfile(profileSlot);
            if (profile == null)
            {
                serverAttackHeld = false;
                hasBufferedServerAttack = false;
                return;
            }

            float requestedAttackSpeedMultiplier =
                combatSettings.ClampAttackSpeedMultiplier(
                    replicatedAttackSpeedMultiplier.Value);
            float attackInterval = CalculateAttackInterval(
                profile,
                requestedAttackSpeedMultiplier);
            float effectiveAttackSpeedMultiplier = Mathf.Max(
                0.01f,
                profile.BaseAttackInterval / attackInterval);
            int motionIndex = SelectNextServerMotionIndex(profile);

            nextAllowedServerAttackTime = now + attackInterval;
            uint nextSequence = unchecked(
                replicatedAttackPresentation.Value.Sequence + 1u);
            replicatedAttackPresentation.Value =
                new ReplicatedAttackPresentation(
                    nextSequence,
                    attackInterval,
                    effectiveAttackSpeedMultiplier,
                    profileSlot,
                    (byte)motionIndex);

            QueueAcceptedHitServer(
                now,
                nextSequence,
                profile,
                effectiveAttackSpeedMultiplier,
                attackInterval);
        }

        private void QueueAcceptedHitServer(
            double acceptedAt,
            uint sequence,
            DiagnosticMeleeAttackProfile profile,
            float attackSpeedMultiplier,
            float attackInterval)
        {
            DiagnosticFirstPersonAttackMotionSet motionSet =
                profile.FirstPersonMotionSet;
            float hitDelay = motionSet != null
                ? motionSet.GetImpactDelay(attackSpeedMultiplier)
                : Mathf.Min(0.1f, attackInterval * 0.5f);

            pendingServerHits.Add(
                new PendingServerHit(
                    sequence,
                    acceptedAt + Mathf.Max(0f, hitDelay),
                    profile.Damage,
                    profile.AttackReach,
                    profile.AttackHalfAngle));
        }

        private void ProcessPendingHitsServer()
        {
            if (!IsServer || pendingServerHits.Count == 0)
            {
                return;
            }

            if (!configurationValid || health == null || health.IsDead)
            {
                pendingServerHits.Clear();
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            int index = 0;

            while (index < pendingServerHits.Count)
            {
                PendingServerHit pendingHit = pendingServerHits[index];
                if (now < pendingHit.ExecuteAt)
                {
                    index++;
                    continue;
                }

                pendingServerHits.RemoveAt(index);
                ResolvePendingHitServer(pendingHit);
            }
        }

        private void ResolvePendingHitServer(PendingServerHit pendingHit)
        {
            NetworkHealth healthTarget = FindBestHealthTargetServer(
                pendingHit.AttackReach,
                pendingHit.AttackHalfAngle,
                out float healthDistanceSquared);

            DiagnosticResourceNode resourceTarget = null;
            float resourceDistanceSquared = float.PositiveInfinity;

            if (resourceGatherer != null)
            {
                resourceTarget = resourceGatherer.FindBestResourceServer(
                    pendingHit.AttackReach,
                    pendingHit.AttackHalfAngle,
                    out resourceDistanceSquared);
            }

            // Target selection happens at the visual impact moment, not when
            // input is accepted. Exactly one nearest valid consequence wins.
            bool consequenceCommitted = false;

            if (healthTarget != null &&
                (resourceTarget == null ||
                 healthDistanceSquared <= resourceDistanceSquared))
            {
                consequenceCommitted = healthTarget.TryApplyDamageServer(
                    pendingHit.Damage,
                    NetworkObject);
            }
            else if (resourceTarget != null)
            {
                consequenceCommitted =
                    resourceGatherer.TryHarvestServer(resourceTarget);
            }

            if (consequenceCommitted)
            {
                ConfirmHitRpc(pendingHit.Sequence);
            }
        }

        [Rpc(SendTo.Owner)]
        private void ConfirmHitRpc(uint attackSequence)
        {
            if (!IsOwner)
            {
                return;
            }

            HitConfirmed?.Invoke(attackSequence);
        }

        private int SelectNextServerMotionIndex(
            DiagnosticMeleeAttackProfile profile)
        {
            DiagnosticFirstPersonAttackMotionSet motionSet =
                profile.FirstPersonMotionSet;
            int motionCount = motionSet != null
                ? Mathf.Min(motionSet.MotionCount, byte.MaxValue + 1)
                : 0;

            if (motionCount <= 1)
            {
                lastServerMotionIndex = 0;
                return 0;
            }

            bool previousIndexIsValid =
                lastServerMotionIndex >= 0 &&
                lastServerMotionIndex < motionCount;
            int selectedIndex = previousIndexIsValid
                ? UnityEngine.Random.Range(0, motionCount - 1)
                : UnityEngine.Random.Range(0, motionCount);

            if (previousIndexIsValid &&
                selectedIndex >= lastServerMotionIndex)
            {
                selectedIndex++;
            }

            lastServerMotionIndex = selectedIndex;
            return selectedIndex;
        }

        private float CalculateAttackInterval(
            DiagnosticMeleeAttackProfile profile,
            float attackSpeedMultiplier)
        {
            if (combatSettings == null || profile == null)
            {
                return 0f;
            }

            float clampedMultiplier =
                combatSettings.ClampAttackSpeedMultiplier(
                    attackSpeedMultiplier);
            return Mathf.Max(
                combatSettings.MinimumAttackInterval,
                profile.BaseAttackInterval / clampedMultiplier);
        }

        private byte GetCurrentProfileSlot()
        {
            return equipment != null && equipment.HasSword
                ? SwordProfileSlot
                : UnarmedProfileSlot;
        }

        private NetworkHealth FindBestHealthTargetServer(
            float attackReach,
            float attackHalfAngle,
            out float bestDistanceSquared)
        {
            bestDistanceSquared = float.PositiveInfinity;

            if (!IsServer || NetworkManager == null)
            {
                return null;
            }

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            float minimumDot = Mathf.Cos(attackHalfAngle * Mathf.Deg2Rad);
            float maximumDistanceSquared = attackReach * attackReach;
            NetworkHealth bestTarget = null;

            NetworkHealth[] candidates =
                FindObjectsByType<NetworkHealth>(FindObjectsSortMode.None);

            foreach (NetworkHealth candidateHealth in candidates)
            {
                if (candidateHealth == null ||
                    candidateHealth == health ||
                    !candidateHealth.IsSpawned ||
                    candidateHealth.IsDead)
                {
                    continue;
                }

                Vector3 toCandidate = Vector3.ProjectOnPlane(
                    candidateHealth.transform.position - transform.position,
                    Vector3.up);

                float distanceSquared = toCandidate.sqrMagnitude;
                if (distanceSquared < 0.0001f ||
                    distanceSquared > maximumDistanceSquared ||
                    distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                float facingDot = Vector3.Dot(forward, toCandidate.normalized);
                if (facingDot < minimumDot)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                bestTarget = candidateHealth;
            }

            return bestTarget;
        }

        private void HandleAttackPresentationChanged(
            ReplicatedAttackPresentation previous,
            ReplicatedAttackPresentation current)
        {
            if (current.Sequence == previous.Sequence)
            {
                return;
            }

            AttackAccepted?.Invoke(
                new DiagnosticAttackPresentation(
                    current.ProfileSlot,
                    current.MotionIndex,
                    current.AttackInterval,
                    current.AttackSpeedMultiplier));

            if (attackIndicator == null || combatSettings == null)
            {
                return;
            }

            if (indicatorRoutine != null)
            {
                StopCoroutine(indicatorRoutine);
            }

            indicatorRoutine = StartCoroutine(
                ShowAttackIndicator(combatSettings.IndicatorDuration));
        }

        private IEnumerator ShowAttackIndicator(float duration)
        {
            attackIndicator.enabled = true;
            yield return new WaitForSecondsRealtime(duration);
            attackIndicator.enabled = false;
            indicatorRoutine = null;
        }

        private bool ValidateConfiguration(bool logErrors)
        {
            bool valid =
                combatSettings != null &&
                unarmedAttackProfile != null &&
                swordAttackProfile != null;

            if (valid)
            {
                return true;
            }

            if (logErrors)
            {
                Debug.LogError(
                    "[Gate H3] DiagnosticMeleeCombat thiếu Combat Settings " +
                    "hoặc Attack Profile. Chạy Midnight Chaos/Bootstrap/" +
                    "Upgrade Melee Feel to v0.8.3 trước khi test.",
                    this);
            }

            return false;
        }
    }
}
