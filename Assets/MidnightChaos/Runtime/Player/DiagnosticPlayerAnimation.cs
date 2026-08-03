using System;
using MidnightChaos.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace MidnightChaos.Player
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(DiagnosticNetworkPlayer))]
    [RequireComponent(typeof(DiagnosticMeleeCombat))]
    public sealed class DiagnosticPlayerAnimation : NetworkBehaviour
    {
        private const string PlayerVisualName = "PlayerVisual";

        private enum LocomotionState : byte
        {
            Idle = 0,
            Run = 1,
            Sprint = 2,
            Jump = 3,
            Fall = 4,
            Land = 5
        }

        private enum AttackLayerPhase : byte
        {
            Inactive = 0,
            BlendIn = 1,
            Playing = 2,
            BlendOut = 3
        }

        [Header("Player Visual")]
        [SerializeField] private Transform playerVisual;
        [SerializeField] private Animator animator;
        [SerializeField] private bool showLocalVisualForDebug;
        [SerializeField] private Key localVisualDebugKey = Key.F8;

        [Header("Code-Driven Base Layer")]
        [SerializeField] private string baseLayerName = "Base Layer";
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string runStateName = "Run_F";
        [SerializeField] private string sprintStateName = "Sprint_F";
        [SerializeField] private string jumpStateName = "Jump";
        [SerializeField] private string fallStateName = "InAir_Fall";
        [SerializeField] private string landStateName = "Land";
        [SerializeField, Min(0f)] private float baseCrossFadeSeconds = 0.1f;
        [SerializeField, Min(0f)] private float movingSpeedThreshold = 0.05f;
        [SerializeField] private float jumpVelocityThreshold = 0.05f;
        [SerializeField, Range(0.5f, 1f)] private float landExitNormalizedTime = 0.9f;

        [Header("Code-Driven Attack Layer")]
        [SerializeField] private string attackLayerName = "UpperBody";
        [SerializeField] private string attackStateName = "Attack";

        // v0.8.2 reads attack tuning from DiagnosticMeleeCombatSettings. The
        // hidden legacy values only let the migration command preserve values
        // previously serialized on this component.
        [FormerlySerializedAs("attackBlendInSeconds")]
        [SerializeField, HideInInspector]
        private float legacyAttackBlendInSeconds = 0.08f;
        [FormerlySerializedAs("attackExitNormalizedTime")]
        [SerializeField, HideInInspector]
        private float legacyAttackExitNormalizedTime = 0.95f;
        [FormerlySerializedAs("attackBlendOutSeconds")]
        [SerializeField, HideInInspector]
        private float legacyAttackBlendOutSeconds = 0.1f;

        private NetworkVariable<byte> replicatedLocomotionState =
            new NetworkVariable<byte>(
                (byte)LocomotionState.Idle,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private DiagnosticNetworkPlayer player;
        private DiagnosticMeleeCombat combat;
        private Renderer[] visualRenderers = System.Array.Empty<Renderer>();
        private bool[] initialRendererStates = System.Array.Empty<bool>();
        private int baseLayerIndex = -1;
        private int attackLayerIndex = -1;
        private int idleStateHash;
        private int runStateHash;
        private int sprintStateHash;
        private int jumpStateHash;
        private int fallStateHash;
        private int landStateHash;
        private int attackStateHash;
        private LocomotionState activeLocomotionState = (LocomotionState)byte.MaxValue;
        private bool ownerWasGrounded;
        private AttackLayerPhase attackLayerPhase = AttackLayerPhase.Inactive;
        private float attackLayerWeight;
        private float attackElapsed;
        private float attackSampleDuration;
        private float attackBlendInDuration;
        private float attackBlendOutDuration;
        private float activeAttackExitNormalizedTime;
        private bool configurationValid;

        public bool ShowLocalVisualForDebug => showLocalVisualForDebug;
        public float LegacyAttackBlendInSeconds => legacyAttackBlendInSeconds;
        public float LegacyAttackExitNormalizedTime =>
            legacyAttackExitNormalizedTime;
        public float LegacyAttackBlendOutSeconds => legacyAttackBlendOutSeconds;
        public event Action<bool> LocalVisualDebugChanged;

        private void Awake()
        {
            player = GetComponent<DiagnosticNetworkPlayer>();
            combat = GetComponent<DiagnosticMeleeCombat>();
            ResolveVisualReferences();
            CacheAnimatorConfiguration();
            CacheRendererStates();
        }

        public override void OnNetworkSpawn()
        {
            replicatedLocomotionState.OnValueChanged += HandleReplicatedLocomotionChanged;
            combat.AttackAccepted += HandleAttackAccepted;

            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                if (attackLayerIndex >= 0)
                {
                    SetAttackLayerWeight(0f);
                }
            }

            ApplyLocalVisualVisibility();

            // CharacterController.isGrounded is not reliable until its first
            // Move call. Start visually grounded, then let the first Update
            // correct to Jump/Fall if the spawn point is actually airborne.
            ownerWasGrounded = true;
            if (IsOwner)
            {
                activeLocomotionState = (LocomotionState)byte.MaxValue;
                CommitOwnerState(LocomotionState.Idle, true);
            }
            else
            {
                PlayBaseState(
                    DecodeState(replicatedLocomotionState.Value),
                    true);
            }
        }

        public override void OnNetworkDespawn()
        {
            replicatedLocomotionState.OnValueChanged -= HandleReplicatedLocomotionChanged;
            combat.AttackAccepted -= HandleAttackAccepted;
            RestoreRendererStates();

            if (animator != null && attackLayerIndex >= 0)
            {
                animator.SetLayerWeight(attackLayerIndex, 0f);
            }

            attackLayerPhase = AttackLayerPhase.Inactive;
            attackLayerWeight = 0f;
            attackElapsed = 0f;
            attackSampleDuration = 0f;
            attackBlendInDuration = 0f;
            attackBlendOutDuration = 0f;
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                HandleLocalVisualDebugInput();
                CommitOwnerState(ResolveOwnerLocomotionState(), false);
            }

            UpdateAttackLayer();
        }

        public void SetLocalVisualDebug(bool visible)
        {
            if (showLocalVisualForDebug == visible)
            {
                return;
            }

            showLocalVisualForDebug = visible;
            ApplyLocalVisualVisibility();
            LocalVisualDebugChanged?.Invoke(showLocalVisualForDebug);
        }

        private void ResolveVisualReferences()
        {
            if (playerVisual == null)
            {
                playerVisual = transform.Find(PlayerVisualName);
            }

            if (animator == null && playerVisual != null)
            {
                animator = playerVisual.GetComponent<Animator>();
            }
        }

        private void CacheAnimatorConfiguration()
        {
            configurationValid = false;

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            baseLayerIndex = animator.GetLayerIndex(baseLayerName);
            attackLayerIndex = animator.GetLayerIndex(attackLayerName);
            idleStateHash = Animator.StringToHash($"{baseLayerName}.{idleStateName}");
            runStateHash = Animator.StringToHash($"{baseLayerName}.{runStateName}");
            sprintStateHash = Animator.StringToHash($"{baseLayerName}.{sprintStateName}");
            jumpStateHash = Animator.StringToHash($"{baseLayerName}.{jumpStateName}");
            fallStateHash = Animator.StringToHash($"{baseLayerName}.{fallStateName}");
            landStateHash = Animator.StringToHash($"{baseLayerName}.{landStateName}");
            attackStateHash = Animator.StringToHash($"{attackLayerName}.{attackStateName}");

            configurationValid =
                baseLayerIndex >= 0 &&
                animator.HasState(baseLayerIndex, idleStateHash) &&
                animator.HasState(baseLayerIndex, runStateHash) &&
                animator.HasState(baseLayerIndex, sprintStateHash) &&
                animator.HasState(baseLayerIndex, jumpStateHash) &&
                animator.HasState(baseLayerIndex, fallStateHash) &&
                animator.HasState(baseLayerIndex, landStateHash) &&
                attackLayerIndex >= 0 &&
                animator.HasState(attackLayerIndex, attackStateHash);

            if (!configurationValid)
            {
                Debug.LogError(
                    "[Gate H1] Animator Controller không khớp state/layer đã cấu hình. " +
                    "Cần Base Layer: Idle, Run_F, Sprint_F, Jump, InAir_Fall, Land; " +
                    "UpperBody: Attack.",
                    this);
            }
        }

        private void CacheRendererStates()
        {
            if (playerVisual == null)
            {
                return;
            }

            visualRenderers = playerVisual.GetComponentsInChildren<Renderer>(true);
            initialRendererStates = new bool[visualRenderers.Length];

            for (int index = 0; index < visualRenderers.Length; index++)
            {
                initialRendererStates[index] = visualRenderers[index].enabled;
            }
        }

        private void ApplyLocalVisualVisibility()
        {
            bool restoreInitialState = !IsSpawned || !IsOwner || showLocalVisualForDebug;

            for (int index = 0; index < visualRenderers.Length; index++)
            {
                Renderer currentRenderer = visualRenderers[index];
                if (currentRenderer != null)
                {
                    currentRenderer.enabled =
                        restoreInitialState && initialRendererStates[index];
                }
            }
        }

        private void RestoreRendererStates()
        {
            for (int index = 0; index < visualRenderers.Length; index++)
            {
                Renderer currentRenderer = visualRenderers[index];
                if (currentRenderer != null)
                {
                    currentRenderer.enabled = initialRendererStates[index];
                }
            }
        }

        private void HandleLocalVisualDebugInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null ||
                localVisualDebugKey == Key.None ||
                !keyboard[localVisualDebugKey].wasPressedThisFrame)
            {
                return;
            }

            SetLocalVisualDebug(!showLocalVisualForDebug);
            Debug.Log(
                $"[Gate H1] Local PlayerVisual debug: " +
                $"{(showLocalVisualForDebug ? "VISIBLE" : "HIDDEN")}",
                this);
        }

        private LocomotionState ResolveOwnerLocomotionState()
        {
            if (!player.IsAlive)
            {
                ownerWasGrounded = player.IsGrounded;
                return LocomotionState.Idle;
            }

            bool grounded = player.IsGrounded;

            if (!grounded)
            {
                ownerWasGrounded = false;
                return player.VerticalVelocity > jumpVelocityThreshold
                    ? LocomotionState.Jump
                    : LocomotionState.Fall;
            }

            if (!ownerWasGrounded)
            {
                ownerWasGrounded = true;
                return LocomotionState.Land;
            }

            if (activeLocomotionState == LocomotionState.Land &&
                !HasBaseStateReachedExit(landStateHash, landExitNormalizedTime))
            {
                return LocomotionState.Land;
            }

            if (player.PlanarSpeed <= movingSpeedThreshold)
            {
                return LocomotionState.Idle;
            }

            return player.IsSprinting
                ? LocomotionState.Sprint
                : LocomotionState.Run;
        }

        private void CommitOwnerState(LocomotionState state, bool immediate)
        {
            if (state == activeLocomotionState)
            {
                return;
            }

            PlayBaseState(state, immediate);

            if (IsServer)
            {
                replicatedLocomotionState.Value = (byte)state;
            }
            else
            {
                SubmitLocomotionStateRpc((byte)state);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitLocomotionStateRpc(byte rawState)
        {
            if (rawState > (byte)LocomotionState.Land)
            {
                return;
            }

            replicatedLocomotionState.Value = rawState;
        }

        private void HandleReplicatedLocomotionChanged(byte previous, byte current)
        {
            if (IsOwner || previous == current)
            {
                return;
            }

            PlayBaseState(DecodeState(current), false);
        }

        private void PlayBaseState(LocomotionState state, bool immediate)
        {
            activeLocomotionState = state;

            if (!configurationValid)
            {
                return;
            }

            int stateHash = GetBaseStateHash(state);
            if (immediate || baseCrossFadeSeconds <= 0f)
            {
                animator.Play(stateHash, baseLayerIndex, 0f);
            }
            else
            {
                animator.CrossFadeInFixedTime(
                    stateHash,
                    baseCrossFadeSeconds,
                    baseLayerIndex,
                    0f);
            }
        }

        private bool HasBaseStateReachedExit(int expectedStateHash, float exitTime)
        {
            if (!configurationValid || animator.IsInTransition(baseLayerIndex))
            {
                return false;
            }

            AnimatorStateInfo stateInfo =
                animator.GetCurrentAnimatorStateInfo(baseLayerIndex);
            return stateInfo.fullPathHash == expectedStateHash &&
                   stateInfo.normalizedTime >= exitTime;
        }

        private void HandleAttackAccepted(
            DiagnosticAttackPresentation presentation)
        {
            DiagnosticMeleeCombatSettings settings = combat.Settings;
            if (!configurationValid || settings == null)
            {
                return;
            }

            animator.Play(attackStateHash, attackLayerIndex, 0f);
            attackElapsed = 0f;
            attackSampleDuration = Mathf.Max(
                0.01f,
                presentation.AttackInterval *
                settings.AttackAnimationCycleRatio);
            float playbackMultiplier = Mathf.Max(
                0.01f,
                presentation.AttackSpeedMultiplier);
            attackBlendInDuration =
                settings.AttackBlendInSeconds / playbackMultiplier;
            attackBlendOutDuration =
                settings.AttackBlendOutSeconds / playbackMultiplier;
            activeAttackExitNormalizedTime =
                settings.AttackExitNormalizedTime;
            attackLayerWeight = animator.GetLayerWeight(attackLayerIndex);

            if (attackBlendInDuration <= 0f)
            {
                SetAttackLayerWeight(1f);
                attackLayerPhase = AttackLayerPhase.Playing;
            }
            else
            {
                attackLayerPhase = AttackLayerPhase.BlendIn;
            }
        }

        private void UpdateAttackLayer()
        {
            if (attackLayerPhase == AttackLayerPhase.Inactive ||
                !configurationValid)
            {
                return;
            }

            attackElapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(
                attackElapsed / attackSampleDuration);

            // This layer is sampled from a code-owned clock. Reapplying the
            // normalized time each frame prevents an old Animator state time
            // from ending a newly accepted attack and lets attack speed change
            // without accelerating the locomotion layer.
            animator.Play(
                attackStateHash,
                attackLayerIndex,
                normalizedTime);

            if (attackLayerPhase == AttackLayerPhase.BlendIn)
            {
                float blendStep =
                    attackBlendInDuration <= 0f
                        ? 1f
                        : Time.unscaledDeltaTime / attackBlendInDuration;
                SetAttackLayerWeight(
                    Mathf.MoveTowards(attackLayerWeight, 1f, blendStep));

                if (attackLayerWeight >= 1f)
                {
                    attackLayerPhase = AttackLayerPhase.Playing;
                }
            }

            if (attackLayerPhase != AttackLayerPhase.BlendOut &&
                normalizedTime >= activeAttackExitNormalizedTime)
            {
                attackLayerPhase = AttackLayerPhase.BlendOut;
            }

            if (attackLayerPhase != AttackLayerPhase.BlendOut)
            {
                return;
            }

            if (attackBlendOutDuration <= 0f)
            {
                FinishAttackLayer();
                return;
            }

            float fadeStep =
                Time.unscaledDeltaTime / attackBlendOutDuration;
            SetAttackLayerWeight(
                Mathf.MoveTowards(attackLayerWeight, 0f, fadeStep));

            if (attackLayerWeight <= 0f)
            {
                FinishAttackLayer();
            }
        }

        private void SetAttackLayerWeight(float weight)
        {
            attackLayerWeight = Mathf.Clamp01(weight);
            animator.SetLayerWeight(attackLayerIndex, attackLayerWeight);
        }

        private void FinishAttackLayer()
        {
            SetAttackLayerWeight(0f);
            attackLayerPhase = AttackLayerPhase.Inactive;
            attackElapsed = 0f;
            attackSampleDuration = 0f;
            attackBlendInDuration = 0f;
            attackBlendOutDuration = 0f;
        }

        private int GetBaseStateHash(LocomotionState state)
        {
            return state switch
            {
                LocomotionState.Run => runStateHash,
                LocomotionState.Sprint => sprintStateHash,
                LocomotionState.Jump => jumpStateHash,
                LocomotionState.Fall => fallStateHash,
                LocomotionState.Land => landStateHash,
                _ => idleStateHash
            };
        }

        private static LocomotionState DecodeState(byte rawState)
        {
            return rawState <= (byte)LocomotionState.Land
                ? (LocomotionState)rawState
                : LocomotionState.Idle;
        }
    }
}
