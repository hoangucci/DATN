using MidnightChaos.Combat;
using MidnightChaos.Equipment;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Player
{
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(DiagnosticMeleeCombat))]
    [RequireComponent(typeof(DiagnosticPlayerEquipment))]
    public sealed class DiagnosticFirstPersonAttackAnimator : NetworkBehaviour
    {
        private const int BaseLayerIndex = 0;

        private static readonly int AttackSpeedParameterHash =
            Animator.StringToHash("AttackSpeed");
        private static readonly int IdleStateHash =
            Animator.StringToHash("Base Layer.Idle");
        private static readonly int EquipStateHash =
            Animator.StringToHash("Base Layer.Equip");
        private static readonly int[] AttackStateHashes =
        {
            Animator.StringToHash("Base Layer.Attack1"),
            Animator.StringToHash("Base Layer.Attack2"),
            Animator.StringToHash("Base Layer.Attack3")
        };

        private DiagnosticMeleeCombat combat;
        private DiagnosticPlayerEquipment equipment;
        private DiagnosticCameraFollow localCamera;
        private Animator viewmodelAnimator;
        private bool warnedAboutMissingAnimator;
        private bool warnedAboutControllerMismatch;

        private void Awake()
        {
            combat = GetComponent<DiagnosticMeleeCombat>();
            equipment = GetComponent<DiagnosticPlayerEquipment>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                return;
            }

            combat.AttackAccepted += HandleAttackAccepted;
            combat.HitConfirmed += HandleHitConfirmed;
            equipment.SwordStateChanged += HandleSwordStateChanged;
            equipment.FirstPersonViewmodelReady += HandleViewmodelReady;

            localCamera = FindFirstObjectByType<DiagnosticCameraFollow>();
            if (TryResolveAnimator())
            {
                PlayState(IdleStateHash, 0f);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (combat != null)
            {
                combat.AttackAccepted -= HandleAttackAccepted;
                combat.HitConfirmed -= HandleHitConfirmed;
            }

            if (equipment != null)
            {
                equipment.SwordStateChanged -= HandleSwordStateChanged;
                equipment.FirstPersonViewmodelReady -= HandleViewmodelReady;
            }

            viewmodelAnimator = null;
            localCamera = null;
            warnedAboutMissingAnimator = false;
            warnedAboutControllerMismatch = false;
        }

        private void HandleAttackAccepted(
            DiagnosticAttackPresentation presentation)
        {
            if (!IsOwner || !TryResolveAnimator())
            {
                WarnAboutMissingAnimator();
                return;
            }

            int attackIndex = Mathf.Clamp(
                presentation.MotionIndex,
                0,
                AttackStateHashes.Length - 1);
            float attackSpeed = Mathf.Max(
                0.01f,
                presentation.AttackSpeedMultiplier);

            viewmodelAnimator.SetFloat(
                AttackSpeedParameterHash,
                attackSpeed);
            PlayState(AttackStateHashes[attackIndex], 0f);
        }

        private void HandleHitConfirmed(uint _)
        {
            if (!IsOwner || combat == null || combat.Settings == null)
            {
                return;
            }

            if (localCamera == null)
            {
                localCamera = FindFirstObjectByType<DiagnosticCameraFollow>();
            }
            if (localCamera == null)
            {
                return;
            }

            DiagnosticMeleeCombatSettings settings = combat.Settings;
            localCamera.PlayConfirmedHitShake(
                settings.HitShakeDuration,
                settings.HitShakePositionAmplitude,
                settings.HitShakeRotationAmplitude,
                settings.HitShakeFrequency);
        }

        private void HandleSwordStateChanged(bool _, bool hasSword)
        {
            if (!IsOwner || !TryResolveAnimator())
            {
                return;
            }

            viewmodelAnimator.SetFloat(AttackSpeedParameterHash, 1f);
            PlayState(hasSword ? EquipStateHash : IdleStateHash, 0f);
        }

        private void HandleViewmodelReady(Transform _)
        {
            viewmodelAnimator = equipment != null
                ? equipment.FirstPersonViewmodelAnimator
                : null;
            warnedAboutMissingAnimator = false;
            warnedAboutControllerMismatch = false;

            if (TryResolveAnimator())
            {
                PlayState(IdleStateHash, 0f);
            }
        }

        private bool TryResolveAnimator()
        {
            if (viewmodelAnimator == null && equipment != null)
            {
                viewmodelAnimator = equipment.FirstPersonViewmodelAnimator;
            }

            return viewmodelAnimator != null &&
                   viewmodelAnimator.runtimeAnimatorController != null;
        }

        private void PlayState(int stateHash, float normalizedTime)
        {
            if (!TryResolveAnimator())
            {
                WarnAboutMissingAnimator();
                return;
            }

            if (!viewmodelAnimator.HasState(BaseLayerIndex, stateHash))
            {
                if (!warnedAboutControllerMismatch)
                {
                    warnedAboutControllerMismatch = true;
                    Debug.LogError(
                        "[Gate H4] First-person Animator Controller không có " +
                        "đủ state Idle, Equip, Attack1, Attack2, Attack3.",
                        viewmodelAnimator);
                }

                return;
            }

            viewmodelAnimator.Play(
                stateHash,
                BaseLayerIndex,
                normalizedTime);
        }

        private void WarnAboutMissingAnimator()
        {
            if (warnedAboutMissingAnimator)
            {
                return;
            }

            warnedAboutMissingAnimator = true;
            Debug.LogWarning(
                "[Gate H4] Local first-person viewmodel thiếu Animator hoặc " +
                "Cube.controller.",
                this);
        }
    }
}
