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
        [SerializeField]
        private DiagnosticFirstPersonAttackMotionSet firstPersonMotionSet;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? name
            : displayName;
        public int Damage => Mathf.Max(1, damage);
        public float AttackReach => Mathf.Max(0.1f, attackReach);
        public float AttackHalfAngle => Mathf.Clamp(attackHalfAngle, 1f, 180f);
        public float BaseAttackInterval => Mathf.Max(0.01f, baseAttackInterval);
        public DiagnosticFirstPersonAttackMotionSet FirstPersonMotionSet =>
            firstPersonMotionSet;

        public void ConfigureForDiagnostics(
            string configuredDisplayName,
            int configuredDamage,
            float configuredAttackReach,
            float configuredAttackHalfAngle,
            float configuredBaseAttackInterval,
            DiagnosticFirstPersonAttackMotionSet configuredMotionSet)
        {
            displayName = string.IsNullOrWhiteSpace(configuredDisplayName)
                ? "Melee Attack"
                : configuredDisplayName.Trim();
            damage = Mathf.Max(1, configuredDamage);
            attackReach = Mathf.Max(0.1f, configuredAttackReach);
            attackHalfAngle = Mathf.Clamp(configuredAttackHalfAngle, 1f, 180f);
            baseAttackInterval = Mathf.Max(0.01f, configuredBaseAttackInterval);
            firstPersonMotionSet = configuredMotionSet;
        }

        public void SetFirstPersonMotionSetForMigration(
            DiagnosticFirstPersonAttackMotionSet configuredMotionSet)
        {
            firstPersonMotionSet = configuredMotionSet;
        }

        private void OnValidate()
        {
            damage = Mathf.Max(1, damage);
            attackReach = Mathf.Max(0.1f, attackReach);
            attackHalfAngle = Mathf.Clamp(attackHalfAngle, 1f, 180f);
            baseAttackInterval = Mathf.Max(0.01f, baseAttackInterval);
        }
    }
}
