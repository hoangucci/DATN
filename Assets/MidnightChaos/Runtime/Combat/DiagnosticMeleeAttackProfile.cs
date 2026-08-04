using System;
using UnityEngine;

namespace MidnightChaos.Combat
{
    [CreateAssetMenu(
        fileName = "DiagnosticMeleeAttackProfile",
        menuName = "Midnight Chaos/Combat/Melee Attack Profile")]
    public sealed class DiagnosticMeleeAttackProfile : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Melee Attack";

        [Header("Authoritative Gameplay")]
        [SerializeField, Min(1)] private int damage = 25;
        [SerializeField, Min(0.1f)] private float attackReach = 2.6f;
        [SerializeField, Range(1f, 180f)] private float attackHalfAngle = 65f;
        [SerializeField, Min(0.01f)] private float baseAttackInterval = 0.65f;

        [Header("First-Person Presentation")]
        [SerializeField, Range(1, 3)]
        private int firstPersonAnimationVariantCount = 3;
        [Tooltip(
            "Time in seconds at Attack Speed x1 when the authoritative hit " +
            "is resolved. Muck's Attack1/2/3 clips use 0.2666667 seconds.")]
        [SerializeField, Min(0f)]
        private float firstPersonImpactTime = 0.2666667f;

        [Header("First-Person Rest Pose")]
        [Tooltip(
            "Camera-local position of the animated weapon root while idle. " +
            "This is used by the Sword profile; Unarmed has no weapon visual.")]
        [SerializeField]
        private Vector3 firstPersonRestLocalPosition =
            new Vector3(0.45f, -0.35f, 0.65f);
        [Tooltip(
            "Camera-local orientation applied after cancelling the rest " +
            "rotation written by the Muck animation clips.")]
        [SerializeField]
        private Vector3 firstPersonRestLocalEulerAngles =
            new Vector3(0f, 100f, 9.5f);
        [SerializeField]
        private Vector3 firstPersonRestLocalScale =
            new Vector3(0.6f, 0.6f, 0.6f);

        // Zero identifies a v0.8.6-or-older profile that still needs its Rest
        // Pose copied from the retired Sword Motion Set by migration v0.8.7.
        [SerializeField, HideInInspector]
        private int firstPersonRestPoseVersion;

        public event Action FirstPersonRestPoseChanged;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? name
            : displayName;
        public int Damage => Mathf.Max(1, damage);
        public float AttackReach => Mathf.Max(0.1f, attackReach);
        public float AttackHalfAngle => Mathf.Clamp(attackHalfAngle, 1f, 180f);
        public float BaseAttackInterval => Mathf.Max(0.01f, baseAttackInterval);
        public int FirstPersonAnimationVariantCount =>
            Mathf.Clamp(firstPersonAnimationVariantCount, 1, 3);
        public float FirstPersonImpactTime =>
            Mathf.Max(0f, firstPersonImpactTime);
        public Vector3 FirstPersonRestLocalPosition =>
            firstPersonRestLocalPosition;
        public Vector3 FirstPersonRestLocalEulerAngles =>
            firstPersonRestLocalEulerAngles;
        public Quaternion FirstPersonRestLocalRotation =>
            Quaternion.Euler(firstPersonRestLocalEulerAngles);
        public Vector3 FirstPersonRestLocalScale =>
            SanitizeScale(firstPersonRestLocalScale);

        public float GetFirstPersonImpactDelay(float attackSpeedMultiplier)
        {
            return FirstPersonImpactTime /
                   Mathf.Max(0.01f, attackSpeedMultiplier);
        }

        public void ConfigureForDiagnostics(
            string configuredDisplayName,
            int configuredDamage,
            float configuredAttackReach,
            float configuredAttackHalfAngle,
            float configuredBaseAttackInterval)
        {
            displayName = string.IsNullOrWhiteSpace(configuredDisplayName)
                ? "Melee Attack"
                : configuredDisplayName.Trim();
            damage = Mathf.Max(1, configuredDamage);
            attackReach = Mathf.Max(0.1f, configuredAttackReach);
            attackHalfAngle = Mathf.Clamp(configuredAttackHalfAngle, 1f, 180f);
            baseAttackInterval = Mathf.Max(0.01f, configuredBaseAttackInterval);
            firstPersonRestLocalScale = SanitizeScale(
                firstPersonRestLocalScale);
            FirstPersonRestPoseChanged?.Invoke();
        }

        public void ConfigureFirstPersonAnimationForMigration(
            int animationVariantCount,
            float impactTime)
        {
            firstPersonAnimationVariantCount = Mathf.Clamp(
                animationVariantCount,
                1,
                3);
            firstPersonImpactTime = Mathf.Max(0f, impactTime);
        }

        public bool UpgradeFirstPersonRestPoseToV087(
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            if (firstPersonRestPoseVersion >= 1)
            {
                return false;
            }

            firstPersonRestLocalPosition = localPosition;
            firstPersonRestLocalEulerAngles = localEulerAngles;
            firstPersonRestLocalScale = SanitizeScale(localScale);
            firstPersonRestPoseVersion = 1;
            return true;
        }

        private void OnValidate()
        {
            damage = Mathf.Max(1, damage);
            attackReach = Mathf.Max(0.1f, attackReach);
            attackHalfAngle = Mathf.Clamp(attackHalfAngle, 1f, 180f);
            baseAttackInterval = Mathf.Max(0.01f, baseAttackInterval);
            firstPersonAnimationVariantCount = Mathf.Clamp(
                firstPersonAnimationVariantCount,
                1,
                3);
            firstPersonImpactTime = Mathf.Max(0f, firstPersonImpactTime);
            firstPersonRestLocalScale = SanitizeScale(
                firstPersonRestLocalScale);
            FirstPersonRestPoseChanged?.Invoke();
        }

        private static Vector3 SanitizeScale(Vector3 scale)
        {
            if (Mathf.Abs(scale.x) < 0.0001f)
            {
                scale.x = 1f;
            }
            if (Mathf.Abs(scale.y) < 0.0001f)
            {
                scale.y = 1f;
            }
            if (Mathf.Abs(scale.z) < 0.0001f)
            {
                scale.z = 1f;
            }

            return scale;
        }
    }
}
