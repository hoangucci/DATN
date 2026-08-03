using UnityEngine;

namespace MidnightChaos.Combat
{
    [CreateAssetMenu(
        fileName = "DiagnosticMeleeCombatSettings",
        menuName = "Midnight Chaos/Combat/Melee Combat Settings")]
    public sealed class DiagnosticMeleeCombatSettings : ScriptableObject
    {
        private const int CurrentHitFeedbackVersion = 1;

        [Header("Authoritative Attack Timing")]
        [SerializeField, Min(0f)] private float inputBufferSeconds = 0.15f;
        [SerializeField, Min(0.01f)] private float minimumAttackInterval = 0.08f;
        [SerializeField, Min(0.01f)] private float minimumAttackSpeedMultiplier = 0.1f;
        [SerializeField, Min(0.01f)] private float maximumAttackSpeedMultiplier = 8f;

        [Header("Diagnostic Presentation")]
        [SerializeField, Min(0.02f)] private float indicatorDuration = 0.14f;

        [Header("Third-Person Attack Layer")]
        [SerializeField, Range(0.1f, 1f)]
        private float attackAnimationCycleRatio = 0.9f;
        [SerializeField, Min(0f)] private float attackBlendInSeconds = 0.08f;
        [SerializeField, Range(0.5f, 1f)]
        private float attackExitNormalizedTime = 0.95f;
        [SerializeField, Min(0f)] private float attackBlendOutSeconds = 0.1f;

        [Header("Confirmed Hit Camera Shake")]
        [Tooltip("No target hit means no shake is played.")]
        [SerializeField, Min(0.01f)] private float hitShakeDuration = 0.09f;
        [SerializeField, Min(0f)]
        private float hitShakePositionAmplitude = 0.018f;
        [SerializeField, Min(0f)]
        private float hitShakeRotationAmplitude = 0.65f;
        [SerializeField, Min(0.01f)] private float hitShakeFrequency = 28f;

        [SerializeField, HideInInspector] private int hitFeedbackVersion;

        public float InputBufferSeconds => Mathf.Max(0f, inputBufferSeconds);
        public float MinimumAttackInterval =>
            Mathf.Max(0.01f, minimumAttackInterval);
        public float MinimumAttackSpeedMultiplier =>
            Mathf.Max(0.01f, minimumAttackSpeedMultiplier);
        public float MaximumAttackSpeedMultiplier =>
            Mathf.Max(MinimumAttackSpeedMultiplier, maximumAttackSpeedMultiplier);
        public float IndicatorDuration => Mathf.Max(0.02f, indicatorDuration);
        public float AttackAnimationCycleRatio =>
            Mathf.Clamp(attackAnimationCycleRatio, 0.1f, 1f);
        public float AttackBlendInSeconds => Mathf.Max(0f, attackBlendInSeconds);
        public float AttackExitNormalizedTime =>
            Mathf.Clamp(attackExitNormalizedTime, 0.5f, 1f);
        public float AttackBlendOutSeconds => Mathf.Max(0f, attackBlendOutSeconds);
        public float HitShakeDuration => Mathf.Max(0.01f, hitShakeDuration);
        public float HitShakePositionAmplitude =>
            Mathf.Max(0f, hitShakePositionAmplitude);
        public float HitShakeRotationAmplitude =>
            Mathf.Max(0f, hitShakeRotationAmplitude);
        public float HitShakeFrequency => Mathf.Max(0.01f, hitShakeFrequency);

        public float ClampAttackSpeedMultiplier(float multiplier)
        {
            return Mathf.Clamp(
                multiplier,
                MinimumAttackSpeedMultiplier,
                MaximumAttackSpeedMultiplier);
        }

        public void ConfigureForDiagnostics(
            float configuredInputBufferSeconds,
            float configuredIndicatorDuration,
            float configuredAttackBlendInSeconds,
            float configuredAttackExitNormalizedTime,
            float configuredAttackBlendOutSeconds)
        {
            inputBufferSeconds = Mathf.Max(0f, configuredInputBufferSeconds);
            indicatorDuration = Mathf.Max(0.02f, configuredIndicatorDuration);
            attackBlendInSeconds = Mathf.Max(0f, configuredAttackBlendInSeconds);
            attackExitNormalizedTime = Mathf.Clamp(
                configuredAttackExitNormalizedTime,
                0.5f,
                1f);
            attackBlendOutSeconds = Mathf.Max(0f, configuredAttackBlendOutSeconds);
            ConfigureHitFeedbackDefaults();
            hitFeedbackVersion = CurrentHitFeedbackVersion;
        }

        public bool UpgradeHitFeedbackToV083()
        {
            if (hitFeedbackVersion >= CurrentHitFeedbackVersion)
            {
                return false;
            }

            ConfigureHitFeedbackDefaults();
            hitFeedbackVersion = CurrentHitFeedbackVersion;
            return true;
        }

        private void ConfigureHitFeedbackDefaults()
        {
            hitShakeDuration = 0.09f;
            hitShakePositionAmplitude = 0.018f;
            hitShakeRotationAmplitude = 0.65f;
            hitShakeFrequency = 28f;
        }

        private void OnValidate()
        {
            inputBufferSeconds = Mathf.Max(0f, inputBufferSeconds);
            minimumAttackInterval = Mathf.Max(0.01f, minimumAttackInterval);
            minimumAttackSpeedMultiplier = Mathf.Max(
                0.01f,
                minimumAttackSpeedMultiplier);
            maximumAttackSpeedMultiplier = Mathf.Max(
                minimumAttackSpeedMultiplier,
                maximumAttackSpeedMultiplier);
            indicatorDuration = Mathf.Max(0.02f, indicatorDuration);
            attackAnimationCycleRatio = Mathf.Clamp(
                attackAnimationCycleRatio,
                0.1f,
                1f);
            attackBlendInSeconds = Mathf.Max(0f, attackBlendInSeconds);
            attackExitNormalizedTime = Mathf.Clamp(
                attackExitNormalizedTime,
                0.5f,
                1f);
            attackBlendOutSeconds = Mathf.Max(0f, attackBlendOutSeconds);
            hitShakeDuration = Mathf.Max(0.01f, hitShakeDuration);
            hitShakePositionAmplitude = Mathf.Max(
                0f,
                hitShakePositionAmplitude);
            hitShakeRotationAmplitude = Mathf.Max(
                0f,
                hitShakeRotationAmplitude);
            hitShakeFrequency = Mathf.Max(0.01f, hitShakeFrequency);
        }
    }
}
