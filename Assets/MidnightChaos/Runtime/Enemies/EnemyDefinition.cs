using UnityEngine;

namespace MidnightChaos.Enemies
{
    [CreateAssetMenu(
        fileName = "EnemyDefinition",
        menuName = "Midnight Chaos/Enemies/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Identity and Visual")]
        [SerializeField, Tooltip(
            "Stable identifier used by saves, diagnostics, and future enemy registries. " +
            "Do not change it after content has shipped.")]
        private string stableId = "fire_mage_melee";

        [SerializeField, Tooltip(
            "Purely visual prefab instantiated below VisualRoot on every peer. " +
            "It must not contain NetworkObject or gameplay colliders.")]
        private GameObject visualPrefab;

        [SerializeField, Tooltip(
            "Local position applied to the instantiated visual below VisualRoot.")]
        private Vector3 visualLocalPosition;

        [SerializeField, Tooltip(
            "Local Euler rotation applied to the instantiated visual.")]
        private Vector3 visualLocalEulerAngles;

        [SerializeField, Tooltip(
            "Local scale applied to the instantiated visual before evolution scaling.")]
        private Vector3 visualLocalScale = Vector3.one;

        [Header("Patrol")]
        [SerializeField, Min(0.1f), Tooltip(
            "Maximum NavMesh radius used when choosing a patrol destination around spawn.")]
        private float patrolRadius = 15f;

        [SerializeField, Min(0.1f), Tooltip(
            "Base NavMeshAgent speed while patrolling. Evolution may multiply this value.")]
        private float patrolSpeed = 2f;

        [SerializeField, Min(0f), Tooltip(
            "Minimum idle duration after reaching a patrol destination.")]
        private float minimumPatrolWait = 1f;

        [SerializeField, Min(0f), Tooltip(
            "Maximum idle duration after reaching a patrol destination.")]
        private float maximumPatrolWait = 3f;

        [Header("Detection and Targeting")]
        [SerializeField, Min(0.1f), Tooltip(
            "Maximum horizontal distance at which a visible living player may be acquired.")]
        private float detectionRange = 15f;

        [SerializeField, Min(0.1f), Tooltip(
            "Distance beyond which the current target is immediately released.")]
        private float loseTargetRange = 22f;

        [SerializeField, Tooltip(
            "Physics layers considered by line-of-sight checks. Self colliders are ignored.")]
        private LayerMask lineOfSightMask = ~0;

        [SerializeField, Tooltip(
            "Local offset from the enemy root used as the line-of-sight ray origin.")]
        private Vector3 eyeOffset = new Vector3(0f, 1.35f, 0f);

        [SerializeField, Tooltip(
            "Local offset from the player root used as the line-of-sight target point.")]
        private Vector3 targetEyeOffset = new Vector3(0f, 0.8f, 0f);

        [SerializeField, Min(0f), Tooltip(
            "How long the enemy remembers its target after line of sight becomes blocked.")]
        private float loseSightGraceSeconds = 1.5f;

        [SerializeField, Min(0.05f), Tooltip(
            "Seconds between nearest-player and line-of-sight target evaluations.")]
        private float retargetInterval = 0.5f;

        [SerializeField, Min(0f), Tooltip(
            "A new target must be this many metres closer before replacing the current one.")]
        private float targetSwitchAdvantage = 1f;

        [Header("Chase and Combat")]
        [SerializeField, Min(0.1f), Tooltip(
            "Base NavMeshAgent speed while chasing. Evolution may multiply this value.")]
        private float chaseSpeed = 3.5f;

        [SerializeField, Min(0.05f), Tooltip(
            "Minimum interval between NavMeshAgent.SetDestination calls while chasing.")]
        private float repathInterval = 0.3f;

        [SerializeField, Min(0.1f), Tooltip(
            "Base melee reach. Evolution may multiply this value.")]
        private float attackReach = 1.8f;

        [SerializeField, Range(0f, 180f), Tooltip(
            "Half-angle of the server-authoritative melee cone in front of the locked attack direction.")]
        private float attackConeHalfAngleDegrees = 50f;

        [SerializeField, Min(1), Tooltip(
            "Base server-authoritative damage dealt when the delayed attack impact hits.")]
        private int attackDamage = 20;

        [SerializeField, Min(0.05f), Tooltip(
            "Minimum time between server-authoritative attacks.")]
        private float attackCooldownSeconds = 1.15f;

        [SerializeField, Min(0.02f), Tooltip(
            "How long AI remains in the Attack pose after starting an attack attempt.")]
        private float attackPoseSeconds = 0.65f;

        [SerializeField, Min(0f), Tooltip(
            "Delay from the start of an attack animation to its server-authoritative impact check.")]
        private float attackImpactDelaySeconds = 0.35f;

        [Header("Animation")]
        [SerializeField, Tooltip("Animator state used while stationary or waiting during patrol.")]
        private string idleAnimationState = "Idle";

        [SerializeField, Tooltip("In-place Animator state used for both patrol and chase movement.")]
        private string moveAnimationState = "Fly Forward In Place";

        [SerializeField, Tooltip("Animator state played when the server attack sequence increments.")]
        private string attackAnimationState = "Slap Attack";

        [SerializeField, Tooltip(
            "Animator state played once when this enemy is first network-spawned.")]
        private string spawnAnimationState = "Spawn";

        [SerializeField, Tooltip(
            "Animator state played for a confirmed non-fatal server-authoritative hit.")]
        private string hitAnimationState = "Take Damage";

        [SerializeField, Tooltip(
            "Animator state played when authoritative health reaches zero.")]
        private string deathAnimationState = "Die";

        [SerializeField, Min(0f), Tooltip(
            "Cross-fade duration used when switching locomotion and attack animation states.")]
        private float animationCrossFadeSeconds = 0.12f;

        [SerializeField, Min(0f), Tooltip(
            "Minimum time the attack animation remains active before locomotion may replace it.")]
        private float attackAnimationLockSeconds = 0.65f;

        [SerializeField, Min(0f), Tooltip(
            "Seconds server AI remains locked while the initial Spawn animation is presented.")]
        private float spawnPresentationSeconds = 0.85f;

        [SerializeField, Min(0f), Tooltip(
            "Seconds a confirmed non-fatal hit pauses server AI and keeps the hit reaction visible.")]
        private float hitReactionSeconds = 0.3f;

        [SerializeField, Min(0f), Tooltip(
            "Minimum interval between replicated hit visuals. Damage is never discarded by this cooldown.")]
        private float hitVisualCooldownSeconds = 0.25f;

        [SerializeField, Min(0f), Tooltip(
            "Seconds the dead visual remains before the server despawns the enemy NetworkObject.")]
        private float deathPresentationSeconds = 1.7f;

        public string StableId => stableId;
        public GameObject VisualPrefab => visualPrefab;
        public Vector3 VisualLocalPosition => visualLocalPosition;
        public Vector3 VisualLocalEulerAngles => visualLocalEulerAngles;
        public Vector3 VisualLocalScale => visualLocalScale;
        public float PatrolRadius => patrolRadius;
        public float PatrolSpeed => patrolSpeed;
        public float MinimumPatrolWait => minimumPatrolWait;
        public float MaximumPatrolWait => maximumPatrolWait;
        public float DetectionRange => detectionRange;
        public float LoseTargetRange => loseTargetRange;
        public LayerMask LineOfSightMask => lineOfSightMask;
        public Vector3 EyeOffset => eyeOffset;
        public Vector3 TargetEyeOffset => targetEyeOffset;
        public float LoseSightGraceSeconds => loseSightGraceSeconds;
        public float RetargetInterval => retargetInterval;
        public float TargetSwitchAdvantage => targetSwitchAdvantage;
        public float ChaseSpeed => chaseSpeed;
        public float RepathInterval => repathInterval;
        public float AttackReach => attackReach;
        public float AttackConeHalfAngleDegrees => attackConeHalfAngleDegrees;
        public int AttackDamage => attackDamage;
        public float AttackCooldownSeconds => attackCooldownSeconds;
        public float AttackPoseSeconds => attackPoseSeconds;
        public float AttackImpactDelaySeconds => attackImpactDelaySeconds;
        public string IdleAnimationState => idleAnimationState;
        public string MoveAnimationState => moveAnimationState;
        public string AttackAnimationState => attackAnimationState;
        public string SpawnAnimationState => spawnAnimationState;
        public string HitAnimationState => hitAnimationState;
        public string DeathAnimationState => deathAnimationState;
        public float AnimationCrossFadeSeconds => animationCrossFadeSeconds;
        public float AttackAnimationLockSeconds => attackAnimationLockSeconds;
        public float SpawnPresentationSeconds => spawnPresentationSeconds;
        public float HitReactionSeconds => hitReactionSeconds;
        public float HitVisualCooldownSeconds => hitVisualCooldownSeconds;
        public float DeathPresentationSeconds => deathPresentationSeconds;

#if UNITY_EDITOR
        public void ConfigureForDiagnostics(GameObject configuredVisualPrefab)
        {
            stableId = "fire_mage_melee";
            visualPrefab = configuredVisualPrefab;
            visualLocalPosition = Vector3.zero;
            visualLocalEulerAngles = Vector3.zero;
            visualLocalScale = Vector3.one;
            patrolRadius = 15f;
            patrolSpeed = 2f;
            minimumPatrolWait = 1f;
            maximumPatrolWait = 3f;
            detectionRange = 15f;
            loseTargetRange = 22f;
            lineOfSightMask = ~0;
            eyeOffset = new Vector3(0f, 1.35f, 0f);
            targetEyeOffset = new Vector3(0f, 0.8f, 0f);
            loseSightGraceSeconds = 1.5f;
            retargetInterval = 0.5f;
            targetSwitchAdvantage = 1f;
            chaseSpeed = 3.5f;
            repathInterval = 0.3f;
            attackReach = 1.8f;
            attackConeHalfAngleDegrees = 50f;
            attackDamage = 20;
            attackCooldownSeconds = 1.15f;
            attackPoseSeconds = 0.65f;
            attackImpactDelaySeconds = 0.35f;
            idleAnimationState = "Idle";
            moveAnimationState = "Fly Forward In Place";
            attackAnimationState = "Slap Attack";
            spawnAnimationState = "Spawn";
            hitAnimationState = "Take Damage";
            deathAnimationState = "Die";
            animationCrossFadeSeconds = 0.12f;
            attackAnimationLockSeconds = 0.65f;
            spawnPresentationSeconds = 0.85f;
            hitReactionSeconds = 0.3f;
            hitVisualCooldownSeconds = 0.25f;
            deathPresentationSeconds = 1.7f;
        }

        public void ConfigureVisualIfMissing(GameObject configuredVisualPrefab)
        {
            if (visualPrefab == null)
            {
                visualPrefab = configuredVisualPrefab;
            }
        }
#endif

        private void OnValidate()
        {
            stableId = string.IsNullOrWhiteSpace(stableId)
                ? name.Trim().ToLowerInvariant().Replace(' ', '_')
                : stableId.Trim();
            visualLocalScale.x = Mathf.Max(0.0001f, visualLocalScale.x);
            visualLocalScale.y = Mathf.Max(0.0001f, visualLocalScale.y);
            visualLocalScale.z = Mathf.Max(0.0001f, visualLocalScale.z);
            maximumPatrolWait = Mathf.Max(minimumPatrolWait, maximumPatrolWait);
            loseTargetRange = Mathf.Max(detectionRange, loseTargetRange);
            attackPoseSeconds = Mathf.Max(
                attackPoseSeconds,
                attackImpactDelaySeconds);
            attackAnimationLockSeconds = Mathf.Max(
                attackAnimationLockSeconds,
                attackImpactDelaySeconds);
        }
    }
}
