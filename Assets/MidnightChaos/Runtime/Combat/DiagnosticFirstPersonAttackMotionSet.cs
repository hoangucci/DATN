using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MidnightChaos.Combat
{
    [CreateAssetMenu(
        fileName = "DiagnosticFirstPersonAttackMotionSet",
        menuName = "Midnight Chaos/Combat/First-Person Attack Motion Set")]
    public sealed class DiagnosticFirstPersonAttackMotionSet : ScriptableObject
    {
        private const int CurrentMotionDataVersion = 2;

        [Serializable]
        public struct SwingMotion
        {
            [SerializeField] private string displayName;
            [FormerlySerializedAs("startPositionOffset")]
            [SerializeField] private Vector3 startLocalPosition;
            [SerializeField] private Vector3 startEulerOffset;
            [SerializeField] private Vector3 strikeControlLocalPosition;
            [FormerlySerializedAs("endPositionOffset")]
            [SerializeField] private Vector3 endLocalPosition;
            [SerializeField] private Vector3 endEulerOffset;
            [SerializeField] private Vector3 overshootLocalPosition;
            [SerializeField] private Vector3 overshootEulerOffset;

            public string DisplayName => string.IsNullOrWhiteSpace(displayName)
                ? "Swing"
                : displayName;
            public Vector3 StartLocalPosition => startLocalPosition;
            public Vector3 StartEulerOffset => startEulerOffset;
            public Vector3 StrikeControlLocalPosition =>
                strikeControlLocalPosition;
            public Vector3 EndLocalPosition => endLocalPosition;
            public Vector3 EndEulerOffset => endEulerOffset;
            public Vector3 OvershootLocalPosition => overshootLocalPosition;
            public Vector3 OvershootEulerOffset => overshootEulerOffset;

            public SwingMotion(
                string configuredDisplayName,
                Vector3 configuredStartLocalPosition,
                Vector3 configuredStartEulerOffset,
                Vector3 configuredStrikeControlLocalPosition,
                Vector3 configuredEndLocalPosition,
                Vector3 configuredEndEulerOffset,
                Vector3 configuredOvershootLocalPosition,
                Vector3 configuredOvershootEulerOffset)
            {
                displayName = configuredDisplayName;
                startLocalPosition = configuredStartLocalPosition;
                startEulerOffset = configuredStartEulerOffset;
                strikeControlLocalPosition =
                    configuredStrikeControlLocalPosition;
                endLocalPosition = configuredEndLocalPosition;
                endEulerOffset = configuredEndEulerOffset;
                overshootLocalPosition = configuredOvershootLocalPosition;
                overshootEulerOffset = configuredOvershootEulerOffset;
            }
        }

        [Header("Rest Pose")]
        [SerializeField] private Vector3 restLocalPosition =
            new Vector3(0.42f, -0.38f, 0.72f);
        [SerializeField] private Vector3 restLocalEulerAngles =
            new Vector3(8f, 0f, -18f);
        [SerializeField] private Vector3 restLocalScale = Vector3.one;

        [Header("Phase Durations at Attack Speed x1")]
        [Tooltip("Short preparation before the fast strike.")]
        [SerializeField, Min(0.001f)] private float windUpDuration = 0.045f;
        [Tooltip("Time used to travel from Start through Control to End.")]
        [SerializeField, Min(0.001f)] private float strikeDuration = 0.115f;
        [Tooltip("Small continuation past End so the weapon keeps momentum.")]
        [SerializeField, Min(0.001f)] private float overshootDuration = 0.035f;
        [Tooltip("Fast return from Overshoot to Rest.")]
        [SerializeField, Min(0.001f)] private float recoveryDuration = 0.105f;
        [Tooltip("Authoritative hit moment inside the Strike phase.")]
        [SerializeField, Range(0f, 1f)]
        private float strikeImpactNormalizedTime = 0.55f;

        [Header("Phase Curves")]
        [SerializeField] private AnimationCurve windUpCurve =
            CreateWindUpCurve();
        [SerializeField] private AnimationCurve strikeCurve =
            CreateStrikeCurve();
        [SerializeField] private AnimationCurve overshootCurve =
            CreateOvershootCurve();
        [SerializeField] private AnimationCurve recoveryCurve =
            CreateRecoveryCurve();

        [Header("Four Diagonal Motions")]
        [SerializeField] private SwingMotion[] motions = CreateDefaultMotions();

        // Zero means the asset came from v0.8.2.1 or was manually created and
        // has not passed through the v0.8.3 migration command yet.
        [SerializeField, HideInInspector] private int motionDataVersion;

        public Vector3 RestLocalPosition => restLocalPosition;
        public Quaternion RestLocalRotation =>
            Quaternion.Euler(restLocalEulerAngles);
        public Vector3 RestLocalScale => restLocalScale;
        public float WindUpDuration => Mathf.Max(0.001f, windUpDuration);
        public float StrikeDuration => Mathf.Max(0.001f, strikeDuration);
        public float OvershootDuration => Mathf.Max(0.001f, overshootDuration);
        public float RecoveryDuration => Mathf.Max(0.001f, recoveryDuration);
        public float StrikeImpactNormalizedTime =>
            Mathf.Clamp01(strikeImpactNormalizedTime);
        public float TotalBaseDuration =>
            WindUpDuration + StrikeDuration + OvershootDuration + RecoveryDuration;
        public int MotionCount => motions != null ? motions.Length : 0;

        public bool TryGetMotion(int index, out SwingMotion motion)
        {
            if (motions == null || index < 0 || index >= motions.Length)
            {
                motion = default;
                return false;
            }

            SwingMotion configuredMotion = motions[index];
            motion = motionDataVersion >= CurrentMotionDataVersion
                ? configuredMotion
                : CreatePhasedMotion(
                    configuredMotion.DisplayName,
                    configuredMotion.StartLocalPosition,
                    configuredMotion.StartEulerOffset,
                    configuredMotion.EndLocalPosition,
                    configuredMotion.EndEulerOffset);
            return true;
        }

        public float GetImpactDelay(float attackSpeedMultiplier)
        {
            float clampedSpeed = Mathf.Max(0.01f, attackSpeedMultiplier);
            float baseDelay =
                WindUpDuration + StrikeDuration * StrikeImpactNormalizedTime;
            return baseDelay / clampedSpeed;
        }

        public float EvaluateWindUpCurve(float normalizedTime)
        {
            return EvaluateCurve(windUpCurve, normalizedTime);
        }

        public float EvaluateStrikeCurve(float normalizedTime)
        {
            return EvaluateCurve(strikeCurve, normalizedTime);
        }

        public float EvaluateOvershootCurve(float normalizedTime)
        {
            return EvaluateCurve(overshootCurve, normalizedTime);
        }

        public float EvaluateRecoveryCurve(float normalizedTime)
        {
            return EvaluateCurve(recoveryCurve, normalizedTime);
        }

        public void ConfigureForDiagnostics(
            Vector3 configuredRestLocalPosition,
            Vector3 configuredRestLocalEulerAngles,
            Vector3 configuredRestLocalScale)
        {
            restLocalPosition = configuredRestLocalPosition;
            restLocalEulerAngles = configuredRestLocalEulerAngles;
            restLocalScale = configuredRestLocalScale;
            motions = CreateDefaultMotions();
            ConfigurePhasedMotionDefaults();
            motionDataVersion = CurrentMotionDataVersion;
        }

        public void ConfigureFromExistingWithFixedMotions(
            DiagnosticFirstPersonAttackMotionSet source,
            Vector3 fallbackRestLocalPosition,
            Vector3 fallbackRestLocalEulerAngles,
            Vector3 fallbackRestLocalScale)
        {
            if (source == null)
            {
                ConfigureForDiagnostics(
                    fallbackRestLocalPosition,
                    fallbackRestLocalEulerAngles,
                    fallbackRestLocalScale);
                return;
            }

            restLocalPosition = source.restLocalPosition;
            restLocalEulerAngles = source.restLocalEulerAngles;
            restLocalScale = source.restLocalScale;
            motions = CreatePhasedMotionsFromSource(source.motions);
            ConfigurePhasedMotionDefaults();
            motionDataVersion = CurrentMotionDataVersion;
        }

        public bool UpgradeToPhasedMotionFeel()
        {
            if (motionDataVersion >= CurrentMotionDataVersion)
            {
                return false;
            }

            if (motions == null || motions.Length == 0)
            {
                motions = CreateDefaultMotions();
            }
            else
            {
                SwingMotion[] upgradedMotions =
                    new SwingMotion[motions.Length];

                for (int index = 0; index < motions.Length; index++)
                {
                    SwingMotion legacyMotion = motions[index];
                    upgradedMotions[index] = CreatePhasedMotion(
                        legacyMotion.DisplayName,
                        legacyMotion.StartLocalPosition,
                        legacyMotion.StartEulerOffset,
                        legacyMotion.EndLocalPosition,
                        legacyMotion.EndEulerOffset);
                }

                motions = upgradedMotions;
            }

            ConfigurePhasedMotionDefaults();
            motionDataVersion = CurrentMotionDataVersion;
            return true;
        }

        private void ConfigurePhasedMotionDefaults()
        {
            windUpDuration = 0.045f;
            strikeDuration = 0.115f;
            overshootDuration = 0.035f;
            recoveryDuration = 0.105f;
            strikeImpactNormalizedTime = 0.55f;
            windUpCurve = CreateWindUpCurve();
            strikeCurve = CreateStrikeCurve();
            overshootCurve = CreateOvershootCurve();
            recoveryCurve = CreateRecoveryCurve();
        }

        private void OnValidate()
        {
            windUpDuration = Mathf.Max(0.001f, windUpDuration);
            strikeDuration = Mathf.Max(0.001f, strikeDuration);
            overshootDuration = Mathf.Max(0.001f, overshootDuration);
            recoveryDuration = Mathf.Max(0.001f, recoveryDuration);
            strikeImpactNormalizedTime =
                Mathf.Clamp01(strikeImpactNormalizedTime);

            if (windUpCurve == null)
            {
                windUpCurve = CreateWindUpCurve();
            }

            if (strikeCurve == null)
            {
                strikeCurve = CreateStrikeCurve();
            }

            if (overshootCurve == null)
            {
                overshootCurve = CreateOvershootCurve();
            }

            if (recoveryCurve == null)
            {
                recoveryCurve = CreateRecoveryCurve();
            }

            if (Mathf.Abs(restLocalScale.x) < 0.0001f)
            {
                restLocalScale.x = 1f;
            }

            if (Mathf.Abs(restLocalScale.y) < 0.0001f)
            {
                restLocalScale.y = 1f;
            }

            if (Mathf.Abs(restLocalScale.z) < 0.0001f)
            {
                restLocalScale.z = 1f;
            }
        }

        private static SwingMotion[] CreateDefaultMotions()
        {
            Vector3 topLeftPosition = new Vector3(-0.52f, 0.34f, 0.68f);
            Vector3 topLeftRotation = new Vector3(-25f, -15f, 55f);
            Vector3 bottomRightPosition = new Vector3(0.52f, -0.38f, 0.76f);
            Vector3 bottomRightRotation = new Vector3(30f, 15f, -75f);

            Vector3 bottomLeftPosition = new Vector3(-0.52f, -0.38f, 0.76f);
            Vector3 bottomLeftRotation = new Vector3(28f, -18f, 75f);
            Vector3 topRightPosition = new Vector3(0.52f, 0.34f, 0.68f);
            Vector3 topRightRotation = new Vector3(-28f, 18f, -55f);

            return new[]
            {
                CreatePhasedMotion(
                    "Top Left to Bottom Right",
                    topLeftPosition,
                    topLeftRotation,
                    bottomRightPosition,
                    bottomRightRotation),
                CreatePhasedMotion(
                    "Bottom Right to Top Left",
                    bottomRightPosition,
                    bottomRightRotation,
                    topLeftPosition,
                    topLeftRotation),
                CreatePhasedMotion(
                    "Bottom Left to Top Right",
                    bottomLeftPosition,
                    bottomLeftRotation,
                    topRightPosition,
                    topRightRotation),
                CreatePhasedMotion(
                    "Top Right to Bottom Left",
                    topRightPosition,
                    topRightRotation,
                    bottomLeftPosition,
                    bottomLeftRotation)
            };
        }

        private static SwingMotion[] CreatePhasedMotionsFromSource(
            SwingMotion[] sourceMotions)
        {
            if (sourceMotions == null || sourceMotions.Length == 0)
            {
                return CreateDefaultMotions();
            }

            SwingMotion[] configuredMotions =
                new SwingMotion[sourceMotions.Length];

            for (int index = 0; index < sourceMotions.Length; index++)
            {
                SwingMotion sourceMotion = sourceMotions[index];
                configuredMotions[index] = CreatePhasedMotion(
                    sourceMotion.DisplayName,
                    sourceMotion.StartLocalPosition,
                    sourceMotion.StartEulerOffset,
                    sourceMotion.EndLocalPosition,
                    sourceMotion.EndEulerOffset);
            }

            return configuredMotions;
        }

        private static SwingMotion CreatePhasedMotion(
            string displayName,
            Vector3 startPosition,
            Vector3 startEulerOffset,
            Vector3 endPosition,
            Vector3 endEulerOffset)
        {
            Vector3 travel = endPosition - startPosition;
            Vector3 planarTravel = new Vector3(travel.x, travel.y, 0f);
            Vector3 perpendicular = planarTravel.sqrMagnitude > 0.0001f
                ? new Vector3(-planarTravel.y, planarTravel.x, 0f).normalized
                : Vector3.up;
            Vector3 controlPosition =
                Vector3.Lerp(startPosition, endPosition, 0.5f) +
                perpendicular * 0.12f +
                Vector3.back * 0.06f;
            Vector3 overshootPosition = endPosition + travel * 0.08f;
            Vector3 overshootEulerOffset =
                endEulerOffset + (endEulerOffset - startEulerOffset) * 0.06f;

            return new SwingMotion(
                displayName,
                startPosition,
                startEulerOffset,
                controlPosition,
                endPosition,
                endEulerOffset,
                overshootPosition,
                overshootEulerOffset);
        }

        private static float EvaluateCurve(
            AnimationCurve curve,
            float normalizedTime)
        {
            float clampedTime = Mathf.Clamp01(normalizedTime);
            return curve != null
                ? curve.Evaluate(clampedTime)
                : clampedTime;
        }

        private static AnimationCurve CreateWindUpCurve()
        {
            return AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        private static AnimationCurve CreateStrikeCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0.2f),
                new Keyframe(0.18f, 0.08f, 1.1f, 1.1f),
                new Keyframe(0.55f, 0.5f, 1.8f, 1.8f),
                new Keyframe(0.82f, 0.94f, 0.8f, 0.8f),
                new Keyframe(1f, 1f, 0.2f, 0f));
        }

        private static AnimationCurve CreateOvershootCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 2.2f, 2.2f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        private static AnimationCurve CreateRecoveryCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 2.4f, 2.4f),
                new Keyframe(1f, 1f, 0f, 0f));
        }
    }
}
