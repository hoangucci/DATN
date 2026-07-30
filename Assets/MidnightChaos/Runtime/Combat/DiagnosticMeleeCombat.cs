using System.Collections;
using MidnightChaos.Equipment;
using MidnightChaos.Resources;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidnightChaos.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkHealth))]
    [RequireComponent(typeof(DiagnosticPlayerEquipment))]
    [RequireComponent(typeof(DiagnosticResourceGatherer))]
    public sealed class DiagnosticMeleeCombat : NetworkBehaviour
    {
        private const string AttackIndicatorName = "AttackIndicator";

        [Header("Gate E - Host Validated Attack")]
        [SerializeField, Min(0.1f)] private float attackReach = 2.6f;
        [SerializeField, Range(1f, 180f)] private float attackHalfAngle = 65f;
        [SerializeField, Min(1)] private int unarmedDamage = 25;
        [SerializeField, Min(1)] private int swordDamage = 40;
        [SerializeField, Min(0.05f)] private float cooldownSeconds = 0.65f;
        [SerializeField, Min(0.02f)] private float indicatorDuration = 0.14f;

        private NetworkVariable<uint> attackSequence =
            new NetworkVariable<uint>(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkHealth health;
        private DiagnosticPlayerEquipment equipment;
        private DiagnosticResourceGatherer resourceGatherer;
        private Renderer attackIndicator;
        private Coroutine indicatorRoutine;
        private double nextAllowedServerAttackTime;

        public int CurrentDamage =>
            equipment != null && equipment.HasSword
                ? swordDamage
                : unarmedDamage;

        private void Awake()
        {
            health = GetComponent<NetworkHealth>();
            equipment = GetComponent<DiagnosticPlayerEquipment>();
            resourceGatherer = GetComponent<DiagnosticResourceGatherer>();

            Transform indicatorTransform = transform.Find(AttackIndicatorName);
            if (indicatorTransform != null)
            {
                attackIndicator = indicatorTransform.GetComponent<Renderer>();
                attackIndicator.enabled = false;
            }
        }

        public override void OnNetworkSpawn()
        {
            attackSequence.OnValueChanged += HandleAttackSequenceChanged;
        }

        public override void OnNetworkDespawn()
        {
            attackSequence.OnValueChanged -= HandleAttackSequenceChanged;

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
            if (!IsSpawned || !IsOwner || health.IsDead)
            {
                return;
            }

            bool keyboardAttack =
                Keyboard.current != null &&
                Keyboard.current.fKey.wasPressedThisFrame;

            bool mouseAttack =
                Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame;

            if (keyboardAttack || mouseAttack)
            {
                RequestAttackRpc();
            }
        }

        [Rpc(
            SendTo.Server,
            InvokePermission = RpcInvokePermission.Owner)]
        private void RequestAttackRpc()
        {
            if (!IsServer || health.IsDead)
            {
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextAllowedServerAttackTime)
            {
                return;
            }

            nextAllowedServerAttackTime = now + cooldownSeconds;

            // The Host accepts the attack attempt, then owns every gameplay
            // consequence. The client never sends target or damage values.
            attackSequence.Value++;

            NetworkHealth healthTarget =
                FindBestHealthTargetServer(out float healthDistanceSquared);

            DiagnosticResourceNode resourceTarget = null;
            float resourceDistanceSquared = float.PositiveInfinity;

            if (resourceGatherer != null)
            {
                resourceTarget = resourceGatherer.FindBestResourceServer(
                    attackReach,
                    attackHalfAngle,
                    out resourceDistanceSquared);
            }

            // One accepted attack can commit exactly one gameplay consequence.
            // The nearest valid target wins across both combat and resources.
            if (healthTarget != null &&
                (resourceTarget == null ||
                 healthDistanceSquared <= resourceDistanceSquared))
            {
                healthTarget.TryApplyDamageServer(CurrentDamage, NetworkObject);
            }
            else if (resourceTarget != null)
            {
                resourceGatherer.TryHarvestServer(resourceTarget);
            }
        }

        private NetworkHealth FindBestHealthTargetServer(
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

        private void HandleAttackSequenceChanged(uint previous, uint current)
        {
            if (attackIndicator == null || current == previous)
            {
                return;
            }

            if (indicatorRoutine != null)
            {
                StopCoroutine(indicatorRoutine);
            }

            indicatorRoutine = StartCoroutine(ShowAttackIndicator());
        }

        private IEnumerator ShowAttackIndicator()
        {
            attackIndicator.enabled = true;
            yield return new WaitForSecondsRealtime(indicatorDuration);
            attackIndicator.enabled = false;
            indicatorRoutine = null;
        }
    }
}
