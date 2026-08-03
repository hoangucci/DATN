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
        private DiagnosticMeleeCombat combat;
        private DiagnosticPlayerEquipment equipment;
        private DiagnosticCameraFollow localCamera;
        private Transform attackPivot;
        private DiagnosticFirstPersonAttackMotionSet activeMotionSet;
        private int activeMotionIndex = -1;
        private float activeAttackSpeedMultiplier = 1f;
        private float animationElapsed;
        private bool animationPlaying;
        private bool warnedAboutMissingPivot;
        private bool warnedAboutMissingMotionSet;

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
            attackPivot = equipment.FirstPersonAttackPivot;
            if (attackPivot != null)
            {
                ApplyCurrentRestPose();
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

            animationPlaying = false;
            activeMotionSet = null;
            activeMotionIndex = -1;
            activeAttackSpeedMultiplier = 1f;
            attackPivot = null;
            localCamera = null;
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            if (attackPivot == null)
            {
                attackPivot = equipment.FirstPersonAttackPivot;
                if (attackPivot == null)
                {
                    WarnAboutMissingPivot();
                    return;
                }
            }

            if (!animationPlaying)
            {
                // Read the asset while idle as well. Rest Pose edits remain
                // visible immediately during Play Mode.
                ApplyCurrentRestPose();
                return;
            }

            if (activeMotionSet == null)
            {
                StopMotionAndApplyRest();
                return;
            }

            // The asset stores seconds at attack speed x1. Multiplying the
            // timeline keeps every phase synchronized with attack-speed buffs
            // without stretching motion across the whole cooldown.
            animationElapsed +=
                Time.unscaledDeltaTime * activeAttackSpeedMultiplier;

            if (!ApplyMotionPose(animationElapsed))
            {
                StopMotionAndApplyRest();
                return;
            }

            if (animationElapsed < activeMotionSet.TotalBaseDuration)
            {
                return;
            }

            StopMotionAndApplyRest();
        }

        private void HandleAttackAccepted(
            DiagnosticAttackPresentation presentation)
        {
            DiagnosticMeleeAttackProfile profile =
                combat.GetAttackProfile(presentation.ProfileSlot);
            DiagnosticFirstPersonAttackMotionSet motionSet =
                profile != null ? profile.FirstPersonMotionSet : null;

            if (motionSet == null || motionSet.MotionCount <= 0)
            {
                if (!warnedAboutMissingMotionSet)
                {
                    warnedAboutMissingMotionSet = true;
                    Debug.LogWarning(
                        "[Gate H3] Attack Profile thiếu First Person Motion " +
                        "Set hoặc Motion Set không có motion.",
                        this);
                }

                return;
            }

            attackPivot = equipment.FirstPersonAttackPivot;
            if (attackPivot == null)
            {
                WarnAboutMissingPivot();
                return;
            }

            int motionIndex = presentation.MotionIndex;
            if (motionIndex < 0 || motionIndex >= motionSet.MotionCount)
            {
                motionIndex = 0;
            }

            if (!motionSet.TryGetMotion(motionIndex, out _))
            {
                return;
            }

            activeMotionSet = motionSet;
            activeMotionIndex = motionIndex;
            activeAttackSpeedMultiplier = Mathf.Max(
                0.01f,
                presentation.AttackSpeedMultiplier);
            animationElapsed = 0f;
            animationPlaying = true;
            ApplyRestPose(activeMotionSet);
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

        private void HandleSwordStateChanged(bool previous, bool current)
        {
            if (!animationPlaying)
            {
                ApplyCurrentRestPose();
            }
        }

        private void HandleViewmodelReady(Transform pivot)
        {
            attackPivot = pivot;
            warnedAboutMissingPivot = false;
            ApplyCurrentRestPose();
        }

        private bool ApplyMotionPose(float timelineTime)
        {
            if (activeMotionSet == null || attackPivot == null)
            {
                return false;
            }

            if (!activeMotionSet.TryGetMotion(
                    activeMotionIndex,
                    out DiagnosticFirstPersonAttackMotionSet.SwingMotion motion))
            {
                return false;
            }

            Vector3 restPosition = activeMotionSet.RestLocalPosition;
            Quaternion restRotation = activeMotionSet.RestLocalRotation;
            Vector3 startPosition = motion.StartLocalPosition;
            Quaternion startRotation =
                restRotation * Quaternion.Euler(motion.StartEulerOffset);
            Vector3 endPosition = motion.EndLocalPosition;
            Quaternion endRotation =
                restRotation * Quaternion.Euler(motion.EndEulerOffset);
            Vector3 overshootPosition = motion.OvershootLocalPosition;
            Quaternion overshootRotation =
                restRotation * Quaternion.Euler(motion.OvershootEulerOffset);

            float windUpEnd = activeMotionSet.WindUpDuration;
            float strikeEnd = windUpEnd + activeMotionSet.StrikeDuration;
            float overshootEnd =
                strikeEnd + activeMotionSet.OvershootDuration;
            float recoveryEnd =
                overshootEnd + activeMotionSet.RecoveryDuration;

            Vector3 position;
            Quaternion rotation;

            if (timelineTime < windUpEnd)
            {
                float phaseTime = Mathf.Clamp01(timelineTime / windUpEnd);
                float curvedTime =
                    activeMotionSet.EvaluateWindUpCurve(phaseTime);
                position = Vector3.LerpUnclamped(
                    restPosition,
                    startPosition,
                    curvedTime);
                rotation = Quaternion.SlerpUnclamped(
                    restRotation,
                    startRotation,
                    curvedTime);
            }
            else if (timelineTime < strikeEnd)
            {
                float phaseTime = Mathf.Clamp01(
                    (timelineTime - windUpEnd) /
                    activeMotionSet.StrikeDuration);
                float curvedTime =
                    activeMotionSet.EvaluateStrikeCurve(phaseTime);
                position = EvaluateQuadraticBezier(
                    startPosition,
                    motion.StrikeControlLocalPosition,
                    endPosition,
                    curvedTime);
                rotation = Quaternion.SlerpUnclamped(
                    startRotation,
                    endRotation,
                    curvedTime);
            }
            else if (timelineTime < overshootEnd)
            {
                float phaseTime = Mathf.Clamp01(
                    (timelineTime - strikeEnd) /
                    activeMotionSet.OvershootDuration);
                float curvedTime =
                    activeMotionSet.EvaluateOvershootCurve(phaseTime);
                position = Vector3.LerpUnclamped(
                    endPosition,
                    overshootPosition,
                    curvedTime);
                rotation = Quaternion.SlerpUnclamped(
                    endRotation,
                    overshootRotation,
                    curvedTime);
            }
            else if (timelineTime < recoveryEnd)
            {
                float phaseTime = Mathf.Clamp01(
                    (timelineTime - overshootEnd) /
                    activeMotionSet.RecoveryDuration);
                float curvedTime =
                    activeMotionSet.EvaluateRecoveryCurve(phaseTime);
                position = Vector3.LerpUnclamped(
                    overshootPosition,
                    restPosition,
                    curvedTime);
                rotation = Quaternion.SlerpUnclamped(
                    overshootRotation,
                    restRotation,
                    curvedTime);
            }
            else
            {
                position = restPosition;
                rotation = restRotation;
            }

            attackPivot.localPosition = position;
            attackPivot.localRotation = rotation;
            attackPivot.localScale = activeMotionSet.RestLocalScale;
            return true;
        }

        private void StopMotionAndApplyRest()
        {
            animationPlaying = false;
            activeMotionIndex = -1;
            activeAttackSpeedMultiplier = 1f;
            ApplyCurrentRestPose();
        }

        private void ApplyCurrentRestPose()
        {
            if (attackPivot == null || combat == null)
            {
                return;
            }

            DiagnosticMeleeAttackProfile profile = combat.CurrentAttackProfile;
            DiagnosticFirstPersonAttackMotionSet motionSet =
                profile != null ? profile.FirstPersonMotionSet : null;

            if (motionSet != null)
            {
                ApplyRestPose(motionSet);
            }
        }

        private void ApplyRestPose(
            DiagnosticFirstPersonAttackMotionSet motionSet)
        {
            if (attackPivot == null || motionSet == null)
            {
                return;
            }

            attackPivot.localPosition = motionSet.RestLocalPosition;
            attackPivot.localRotation = motionSet.RestLocalRotation;
            attackPivot.localScale = motionSet.RestLocalScale;
        }

        private void WarnAboutMissingPivot()
        {
            if (warnedAboutMissingPivot)
            {
                return;
            }

            warnedAboutMissingPivot = true;
            Debug.LogWarning(
                "[Gate H3] Không tìm thấy FirstPersonViewmodelRoot/" +
                "AttackPivot cho local owner.",
                this);
        }

        private static Vector3 EvaluateQuadraticBezier(
            Vector3 start,
            Vector3 control,
            Vector3 end,
            float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            float inverse = 1f - t;
            return inverse * inverse * start +
                   2f * inverse * t * control +
                   t * t * end;
        }
    }
}
