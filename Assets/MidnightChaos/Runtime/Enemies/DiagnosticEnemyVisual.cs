using UnityEngine;

namespace MidnightChaos.Enemies
{
    [DisallowMultipleComponent]
    public sealed class DiagnosticEnemyVisual : MonoBehaviour
    {
        private const string VisualRootName = "VisualRoot";

        [SerializeField, Tooltip(
            "Definition that owns this enemy's visual prefab, offsets, and animation names.")]
        private EnemyDefinition definition;

        [SerializeField, Tooltip(
            "Dedicated visual-only child. Gameplay colliders and NavMeshAgent stay on the enemy prefab.")]
        private Transform visualRoot;

        private GameObject visualInstance;
        private Animator animator;
        private DiagnosticMeleeEnemy enemy;
        private Vector3 previousPosition;
        private int idleStateHash;
        private int moveStateHash;
        private int attackStateHash;
        private int spawnStateHash;
        private int hitStateHash;
        private int deathStateHash;
        private int activeStateHash;
        private double attackAnimationEndsAt;
        private double hitAnimationEndsAt;
        private bool animatorStatesValidated;
        private bool spawnAnimationStarted;
        private bool deathAnimationStarted;
        private bool suspensionPresentationApplied;
        private bool lastSuspensionState;

        public EnemyDefinition Definition => definition;
        public Transform VisualRoot => visualRoot;
        public Animator Animator => animator;

        private void Awake()
        {
            ResolveVisualRoot();
            CreateVisualInstance();
            enemy = GetComponent<DiagnosticMeleeEnemy>();
            previousPosition = transform.position;
        }

        private void OnEnable()
        {
            enemy ??= GetComponent<DiagnosticMeleeEnemy>();
            if (enemy == null)
            {
                return;
            }

            enemy.AttackSequenceChanged += HandleAttackSequenceChanged;
            enemy.HitSequenceChanged += HandleHitSequenceChanged;
            enemy.StateChanged += HandleEnemyStateChanged;
            enemy.SuspensionChanged += HandleSuspensionChanged;
        }

        private void OnDisable()
        {
            if (enemy != null)
            {
                enemy.AttackSequenceChanged -= HandleAttackSequenceChanged;
                enemy.HitSequenceChanged -= HandleHitSequenceChanged;
                enemy.StateChanged -= HandleEnemyStateChanged;
                enemy.SuspensionChanged -= HandleSuspensionChanged;
            }
        }

        private void Update()
        {
            ApplySuspensionPresentationIfNeeded();
            if (enemy != null && enemy.IsSuspended)
            {
                previousPosition = transform.position;
                return;
            }

            if (animator == null || definition == null ||
                !animatorStatesValidated)
            {
                previousPosition = transform.position;
                return;
            }

            if (enemy != null &&
                enemy.CurrentState == DiagnosticEnemyState.Dead)
            {
                PlayDeathIfNeeded();
                return;
            }
            if (enemy != null &&
                enemy.SynchronizedNetworkTime < enemy.SpawnEndsAt)
            {
                PlaySpawnIfNeeded();
                return;
            }

            Vector3 delta = Vector3.ProjectOnPlane(
                transform.position - previousPosition,
                Vector3.up);
            previousPosition = transform.position;
            if (Time.realtimeSinceStartupAsDouble < attackAnimationEndsAt)
            {
                return;
            }
            if (Time.realtimeSinceStartupAsDouble < hitAnimationEndsAt)
            {
                return;
            }

            float speed = Time.unscaledDeltaTime > Mathf.Epsilon
                ? delta.magnitude / Time.unscaledDeltaTime
                : 0f;
            PlayState(speed > 0.05f ? moveStateHash : idleStateHash);
        }

        public void ApplyEvolutionScale(Vector3 scale)
        {
            if (visualRoot != null)
            {
                visualRoot.localScale = scale;
            }
        }

#if UNITY_EDITOR
        public void ConfigureForDiagnostics(
            EnemyDefinition configuredDefinition,
            Transform configuredVisualRoot)
        {
            definition = configuredDefinition;
            visualRoot = configuredVisualRoot;
        }
#endif

        private void ResolveVisualRoot()
        {
            if (visualRoot != null)
            {
                return;
            }

            Transform existing = transform.Find(VisualRootName);
            if (existing != null)
            {
                visualRoot = existing;
                return;
            }

            GameObject created = new GameObject(VisualRootName);
            created.transform.SetParent(transform, false);
            visualRoot = created.transform;
        }

        private void CreateVisualInstance()
        {
            if (visualInstance != null ||
                definition == null ||
                definition.VisualPrefab == null ||
                visualRoot == null)
            {
                return;
            }

            visualInstance = Instantiate(definition.VisualPrefab, visualRoot);
            visualInstance.name = definition.VisualPrefab.name;
            Transform instanceTransform = visualInstance.transform;
            instanceTransform.localPosition = definition.VisualLocalPosition;
            instanceTransform.localRotation = Quaternion.Euler(
                definition.VisualLocalEulerAngles);
            instanceTransform.localScale = definition.VisualLocalScale;

            animator = visualInstance.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogWarning(
                    $"[Enemy Visual] '{definition.StableId}' has no Animator.",
                    this);
                return;
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            ValidateAnimatorStates();
        }

        private void ValidateAnimatorStates()
        {
            idleStateHash = Animator.StringToHash(definition.IdleAnimationState);
            moveStateHash = Animator.StringToHash(definition.MoveAnimationState);
            attackStateHash = Animator.StringToHash(
                definition.AttackAnimationState);
            spawnStateHash = Animator.StringToHash(
                definition.SpawnAnimationState);
            hitStateHash = Animator.StringToHash(definition.HitAnimationState);
            deathStateHash = Animator.StringToHash(
                definition.DeathAnimationState);

            bool hasIdle = animator.HasState(0, idleStateHash);
            bool hasMove = animator.HasState(0, moveStateHash);
            bool hasAttack = animator.HasState(0, attackStateHash);
            bool hasSpawn = animator.HasState(0, spawnStateHash);
            bool hasHit = animator.HasState(0, hitStateHash);
            bool hasDeath = animator.HasState(0, deathStateHash);
            animatorStatesValidated = hasIdle && hasMove && hasAttack &&
                                      hasSpawn && hasHit && hasDeath;
            if (!animatorStatesValidated)
            {
                Debug.LogError(
                    $"[Enemy Visual] Animator states invalid for " +
                    $"'{definition.StableId}'. Idle={hasIdle}, " +
                    $"Move={hasMove}, Attack={hasAttack}, Spawn={hasSpawn}, " +
                    $"Hit={hasHit}, " +
                    $"Death={hasDeath}.",
                    this);
                return;
            }

            animator.Play(idleStateHash, 0, 0f);
            activeStateHash = idleStateHash;
        }

        private void HandleAttackSequenceChanged(uint previous, uint current)
        {
            if (current == previous || !animatorStatesValidated ||
                enemy == null ||
                enemy.CurrentState == DiagnosticEnemyState.Dead)
            {
                return;
            }

            animator.CrossFadeInFixedTime(
                attackStateHash,
                definition.AnimationCrossFadeSeconds,
                0);
            activeStateHash = attackStateHash;
            attackAnimationEndsAt = Time.realtimeSinceStartupAsDouble +
                                    definition.AttackAnimationLockSeconds;
        }

        private void HandleHitSequenceChanged(uint previous, uint current)
        {
            if (current == previous || !animatorStatesValidated ||
                enemy == null ||
                enemy.CurrentState == DiagnosticEnemyState.Dead ||
                enemy.SynchronizedNetworkTime < enemy.SpawnEndsAt ||
                Time.realtimeSinceStartupAsDouble < attackAnimationEndsAt)
            {
                return;
            }

            animator.CrossFadeInFixedTime(
                hitStateHash,
                definition.AnimationCrossFadeSeconds,
                0);
            activeStateHash = hitStateHash;
            hitAnimationEndsAt = Time.realtimeSinceStartupAsDouble +
                                 definition.HitReactionSeconds;
        }

        private void HandleEnemyStateChanged(
            DiagnosticEnemyState previous,
            DiagnosticEnemyState current)
        {
            if (current == DiagnosticEnemyState.Dead)
            {
                PlayDeathIfNeeded();
            }
        }

        private void HandleSuspensionChanged(bool previous, bool current)
        {
            ApplySuspensionPresentation(current);
        }

        private void ApplySuspensionPresentationIfNeeded()
        {
            if (enemy == null)
            {
                return;
            }

            bool suspended = enemy.IsSuspended;
            if (!suspensionPresentationApplied)
            {
                suspensionPresentationApplied = true;
                lastSuspensionState = suspended;
                if (suspended)
                {
                    ApplySuspensionPresentation(true);
                }
                return;
            }
            if (lastSuspensionState != suspended)
            {
                ApplySuspensionPresentation(suspended);
            }
        }

        private void ApplySuspensionPresentation(bool suspended)
        {
            suspensionPresentationApplied = true;
            lastSuspensionState = suspended;
            if (suspended)
            {
                attackAnimationEndsAt = 0d;
                hitAnimationEndsAt = 0d;
                if (animator != null)
                {
                    animator.enabled = false;
                }
                if (visualRoot != null)
                {
                    visualRoot.gameObject.SetActive(false);
                }
                return;
            }

            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(true);
            }
            if (animator != null)
            {
                animator.enabled = true;
            }
            previousPosition = transform.position;
            attackAnimationEndsAt = 0d;
            hitAnimationEndsAt = 0d;
            if (!animatorStatesValidated || enemy == null)
            {
                return;
            }

            spawnAnimationStarted =
                enemy.SynchronizedNetworkTime >= enemy.SpawnEndsAt;
            activeStateHash = 0;
            PlayState(
                enemy.CurrentState == DiagnosticEnemyState.Chase
                    ? moveStateHash
                    : idleStateHash);
        }

        private void PlaySpawnIfNeeded()
        {
            if (spawnAnimationStarted || !animatorStatesValidated ||
                enemy == null)
            {
                return;
            }

            spawnAnimationStarted = true;
            double remaining = System.Math.Max(
                0d,
                enemy.SpawnEndsAt - enemy.SynchronizedNetworkTime);
            float elapsed = Mathf.Clamp(
                definition.SpawnPresentationSeconds - (float)remaining,
                0f,
                definition.SpawnPresentationSeconds);
            animator.CrossFadeInFixedTime(
                spawnStateHash,
                definition.AnimationCrossFadeSeconds,
                0,
                elapsed);
            activeStateHash = spawnStateHash;
        }

        private void PlayDeathIfNeeded()
        {
            if (deathAnimationStarted || !animatorStatesValidated ||
                enemy == null || enemy.DeathEndsAt <= 0d)
            {
                return;
            }

            deathAnimationStarted = true;
            double remaining = System.Math.Max(
                0d,
                enemy.DeathEndsAt - enemy.SynchronizedNetworkTime);
            float elapsed = Mathf.Clamp(
                definition.DeathPresentationSeconds - (float)remaining,
                0f,
                definition.DeathPresentationSeconds);
            animator.CrossFadeInFixedTime(
                deathStateHash,
                definition.AnimationCrossFadeSeconds,
                0,
                elapsed);
            activeStateHash = deathStateHash;
        }

        private void PlayState(int stateHash)
        {
            if (stateHash == 0 || activeStateHash == stateHash)
            {
                return;
            }

            animator.CrossFadeInFixedTime(
                stateHash,
                definition.AnimationCrossFadeSeconds,
                0);
            activeStateHash = stateHash;
        }
    }
}
