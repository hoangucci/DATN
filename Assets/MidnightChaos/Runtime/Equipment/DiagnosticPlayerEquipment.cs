using System;
using MidnightChaos.Combat;
using MidnightChaos.Player;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Equipment
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DiagnosticPlayerEquipment : NetworkBehaviour
    {
        private const string SwordVisualName = "SwordVisual";
        private const string ViewmodelRootName = "FirstPersonViewmodelRoot";
        private const string WeaponPositionName = "WeaponPos";
        private const string AnimatedWeaponName = "Cube";
        private const string WeaponVisualAdapterName = "GameObject";
        private const string HitboxRootName = "Hitbox";
        private const string HitboxObjectName = "Cube";
        private const string TrailName = "Trail";

        // Rotation written by Idle_3 and by the first frame of Attack1/2/3.
        // The visual adapter cancels it at rest so the user-approved Midnight
        // Chaos rest pose is preserved while the remaining Muck curves stay
        // byte-for-byte unchanged.
        private static readonly Quaternion MuckClipRestRotation =
            new Quaternion(-0.5f, -0.5000001f, -0.5f, 0.4999999f);

        private NetworkVariable<bool> hasSword =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        [Header("World Sword")]
        [Tooltip(
            "Sword shown on the animated character. It may be anywhere below " +
            "the DiagnosticNetworkPlayer prefab root, normally below a " +
            "RightHandSocket.")]
        [SerializeField] private GameObject worldSwordVisual;

        [Header("Local First-Person Sword")]
        [Tooltip(
            "Optional visual-only prefab. If empty, the world sword is cloned " +
            "for the local camera.")]
        [SerializeField] private GameObject firstPersonSwordPrefab;
        [SerializeField]
        private RuntimeAnimatorController firstPersonAnimatorController;

        private DiagnosticPlayerAnimation playerAnimation;
        private DiagnosticMeleeCombat combat;
        private DiagnosticMeleeAttackProfile firstPersonSwordProfile;
        private GameObject firstPersonViewmodelRoot;
        private Animator firstPersonViewmodelAnimator;
        private Transform firstPersonWeaponPosition;
        private Transform firstPersonAnimatedWeapon;
        private GameObject firstPersonSwordVisual;
        private bool warnedAboutMissingWorldSword;

        public event Action<bool, bool> SwordStateChanged;
        public event Action<Transform> FirstPersonViewmodelReady;

        public bool HasSword => hasSword.Value;
        public Animator FirstPersonViewmodelAnimator =>
            firstPersonViewmodelAnimator;
        public Transform FirstPersonWeaponPosition =>
            firstPersonWeaponPosition;
        public Transform FirstPersonAnimatedWeapon =>
            firstPersonAnimatedWeapon;

        private void Awake()
        {
            playerAnimation = GetComponent<DiagnosticPlayerAnimation>();
            combat = GetComponent<DiagnosticMeleeCombat>();
            ResolveWorldSwordVisual();
            RefreshSwordVisual();
        }

        public override void OnNetworkSpawn()
        {
            hasSword.OnValueChanged += HandleSwordStateChanged;

            if (IsOwner)
            {
                firstPersonSwordProfile = ResolveSwordAttackProfile();
                if (firstPersonSwordProfile != null)
                {
                    firstPersonSwordProfile.FirstPersonRestPoseChanged +=
                        HandleFirstPersonRestPoseChanged;
                }

                if (playerAnimation != null)
                {
                    playerAnimation.LocalVisualDebugChanged +=
                        HandleLocalVisualDebugChanged;
                }

                CreateFirstPersonViewmodel();
            }

            if (IsServer)
            {
                hasSword.Value = false;
            }

            RefreshSwordVisual();
        }

        public override void OnNetworkDespawn()
        {
            hasSword.OnValueChanged -= HandleSwordStateChanged;

            if (playerAnimation != null)
            {
                playerAnimation.LocalVisualDebugChanged -=
                    HandleLocalVisualDebugChanged;
            }

            if (firstPersonSwordProfile != null)
            {
                firstPersonSwordProfile.FirstPersonRestPoseChanged -=
                    HandleFirstPersonRestPoseChanged;
            }

            if (firstPersonViewmodelRoot != null)
            {
                Destroy(firstPersonViewmodelRoot);
            }

            firstPersonViewmodelRoot = null;
            firstPersonViewmodelAnimator = null;
            firstPersonWeaponPosition = null;
            firstPersonAnimatedWeapon = null;
            firstPersonSwordVisual = null;
            firstPersonSwordProfile = null;
        }

        public void ConfigureWorldSwordVisual(GameObject visual)
        {
            worldSwordVisual = visual;
            warnedAboutMissingWorldSword = false;
            RefreshSwordVisual();
        }

        public void ConfigureFirstPersonViewmodelForMigration(
            RuntimeAnimatorController animatorController)
        {
            firstPersonAnimatorController = animatorController;
        }

        public bool TryGrantSwordServer()
        {
            if (!IsServer || !IsSpawned || hasSword.Value)
            {
                return false;
            }

            hasSword.Value = true;
            return true;
        }

        private void HandleSwordStateChanged(bool previous, bool current)
        {
            RefreshSwordVisual();
            SwordStateChanged?.Invoke(previous, current);
        }

        private void HandleLocalVisualDebugChanged(bool visible)
        {
            RefreshSwordVisual();
        }

        private void HandleFirstPersonRestPoseChanged()
        {
            ApplyFirstPersonRestPose(firstPersonSwordProfile);
        }

        private void RefreshSwordVisual()
        {
            ResolveWorldSwordVisual();

            bool showFullBodyForDebug =
                playerAnimation != null &&
                playerAnimation.ShowLocalVisualForDebug;
            bool swordIsEquipped = hasSword.Value;

            if (worldSwordVisual != null)
            {
                bool showWorldSword =
                    swordIsEquipped &&
                    (!IsSpawned || !IsOwner || showFullBodyForDebug);
                worldSwordVisual.SetActive(showWorldSword);
            }
            else if (IsSpawned && !warnedAboutMissingWorldSword)
            {
                warnedAboutMissingWorldSword = true;
                Debug.LogWarning(
                    "[Gate H2] Không tìm thấy SwordVisual. Gán World Sword " +
                    "Visual hoặc đặt object tên SwordVisual dưới root " +
                    "DiagnosticNetworkPlayer.",
                    this);
            }

            if (firstPersonViewmodelRoot != null)
            {
                firstPersonViewmodelRoot.SetActive(
                    IsSpawned && IsOwner && !showFullBodyForDebug);
            }

            if (firstPersonSwordVisual != null)
            {
                firstPersonSwordVisual.SetActive(
                    swordIsEquipped &&
                    IsSpawned &&
                    IsOwner &&
                    !showFullBodyForDebug);
            }
        }

        private void ResolveWorldSwordVisual()
        {
            if (worldSwordVisual != null)
            {
                return;
            }

            Transform directSword = transform.Find(SwordVisualName);
            if (directSword != null)
            {
                worldSwordVisual = directSword.gameObject;
                return;
            }

            Transform[] descendants = GetComponentsInChildren<Transform>(true);
            foreach (Transform descendant in descendants)
            {
                if (descendant != transform &&
                    descendant.name == SwordVisualName)
                {
                    worldSwordVisual = descendant.gameObject;
                    return;
                }
            }
        }

        private void CreateFirstPersonViewmodel()
        {
            if (!IsOwner || firstPersonViewmodelRoot != null)
            {
                return;
            }

            DiagnosticMeleeAttackProfile swordProfile =
                firstPersonSwordProfile != null
                    ? firstPersonSwordProfile
                    : ResolveSwordAttackProfile();
            if (swordProfile == null)
            {
                Debug.LogError(
                    "[Gate H4] Không tìm thấy SwordAttackProfile để đọc " +
                    "First-Person Rest Pose. Chạy migration v0.8.7.",
                    this);
                return;
            }

            DiagnosticCameraFollow localCamera =
                FindFirstObjectByType<DiagnosticCameraFollow>();
            if (localCamera == null)
            {
                Debug.LogWarning(
                    "[Gate H2] Không tìm thấy camera local để tạo first-person " +
                    "viewmodel.",
                    this);
                return;
            }

            firstPersonViewmodelRoot = new GameObject(ViewmodelRootName);
            firstPersonViewmodelRoot.transform.SetParent(
                localCamera.transform,
                false);
            firstPersonViewmodelRoot.AddComponent<
                DiagnosticFirstPersonAnimationEventRelay>();

            GameObject weaponPositionObject =
                new GameObject(WeaponPositionName);
            weaponPositionObject.transform.SetParent(
                firstPersonViewmodelRoot.transform,
                false);
            firstPersonWeaponPosition = weaponPositionObject.transform;
            firstPersonWeaponPosition.localRotation = Quaternion.identity;

            GameObject animatedWeaponObject =
                new GameObject(AnimatedWeaponName);
            animatedWeaponObject.transform.SetParent(
                firstPersonWeaponPosition,
                false);
            firstPersonAnimatedWeapon = animatedWeaponObject.transform;

            CreateInertHitboxBinding(firstPersonViewmodelRoot.transform);
            CreateFirstPersonSwordVisual(swordProfile);
            ApplyFirstPersonRestPose(swordProfile);
            CreateAndBindFirstPersonAnimator();

            FirstPersonViewmodelReady?.Invoke(firstPersonWeaponPosition);
            RefreshSwordVisual();
        }

        private void CreateFirstPersonSwordVisual(
            DiagnosticMeleeAttackProfile swordProfile)
        {
            GameObject source = firstPersonSwordPrefab != null
                ? firstPersonSwordPrefab
                : worldSwordVisual;

            if (source == null)
            {
                Debug.LogWarning(
                    "[Gate H2] Viewmodel đã được tạo nhưng không thể tạo kiếm " +
                    "góc nhìn thứ nhất vì thiếu First Person Sword Prefab và " +
                    "World Sword Visual.",
                    this);
                return;
            }

            if (source.GetComponentInChildren<NetworkObject>(true) != null)
            {
                Debug.LogError(
                    "[Gate H2] Sword visual không được chứa NetworkObject. " +
                    "Hãy dùng prefab chỉ chứa mesh/renderer cho kiếm.",
                    source);
                return;
            }

            firstPersonSwordVisual = Instantiate(
                source,
                firstPersonAnimatedWeapon,
                false);
            firstPersonSwordVisual.name = WeaponVisualAdapterName;
            firstPersonSwordVisual.transform.localPosition = Vector3.zero;
            firstPersonSwordVisual.transform.localRotation =
                Quaternion.Inverse(MuckClipRestRotation) *
                swordProfile.FirstPersonRestLocalRotation;
            firstPersonSwordVisual.transform.localScale = Vector3.one;

            CreateTrailBinding(firstPersonSwordVisual.transform);
            firstPersonSwordVisual.SetActive(false);
        }

        private void CreateAndBindFirstPersonAnimator()
        {
            firstPersonViewmodelAnimator =
                firstPersonViewmodelRoot.AddComponent<Animator>();
            firstPersonViewmodelAnimator.applyRootMotion = false;
            firstPersonViewmodelAnimator.cullingMode =
                AnimatorCullingMode.AlwaysAnimate;
            firstPersonViewmodelAnimator.runtimeAnimatorController =
                firstPersonAnimatorController;

            if (firstPersonAnimatorController == null)
            {
                Debug.LogError(
                    "[Gate H4] DiagnosticPlayerEquipment chưa được gán " +
                    "Cube.controller. Chạy migration v0.8.7.",
                    this);
                return;
            }

            // The hierarchy is created at runtime, after Animator construction
            // would normally cache bindings. Rebind only after every exact Muck
            // path exists: WeaponPos/Cube, Hitbox/Cube and .../Trail.
            firstPersonViewmodelAnimator.Rebind();
            firstPersonViewmodelAnimator.Update(0f);
            firstPersonViewmodelAnimator.Play("Idle", 0, 0f);
        }

        private DiagnosticMeleeAttackProfile ResolveSwordAttackProfile()
        {
            if (combat == null)
            {
                combat = GetComponent<DiagnosticMeleeCombat>();
            }

            return combat != null
                ? combat.GetAttackProfile(DiagnosticMeleeCombat.SwordProfileSlot)
                : null;
        }

        private void ApplyFirstPersonRestPose(
            DiagnosticMeleeAttackProfile swordProfile)
        {
            if (swordProfile == null)
            {
                return;
            }

            if (firstPersonWeaponPosition != null)
            {
                firstPersonWeaponPosition.localPosition =
                    swordProfile.FirstPersonRestLocalPosition;
                firstPersonWeaponPosition.localScale = SanitizeScale(
                    swordProfile.FirstPersonRestLocalScale);
            }

            if (firstPersonSwordVisual != null)
            {
                firstPersonSwordVisual.transform.localRotation =
                    Quaternion.Inverse(MuckClipRestRotation) *
                    swordProfile.FirstPersonRestLocalRotation;
            }
        }

        private static void CreateInertHitboxBinding(Transform viewmodelRoot)
        {
            GameObject hitboxRoot = new GameObject(HitboxRootName);
            hitboxRoot.transform.SetParent(viewmodelRoot, false);

            GameObject hitboxObject = new GameObject(HitboxObjectName);
            hitboxObject.transform.SetParent(hitboxRoot.transform, false);
            hitboxObject.SetActive(false);
        }

        private static void CreateTrailBinding(Transform visualRoot)
        {
            GameObject trailObject = new GameObject(TrailName);
            trailObject.transform.SetParent(visualRoot, false);
            trailObject.transform.localPosition = new Vector3(0f, 1.15f, 0f);

            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.minVertexDistance = 0.025f;
            trail.startWidth = 0.055f;
            trail.endWidth = 0f;
            trail.startColor = new Color(1f, 1f, 1f, 0.5f);
            trail.endColor = new Color(1f, 1f, 1f, 0f);
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            Renderer[] sourceRenderers =
                visualRoot.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer sourceRenderer in sourceRenderers)
            {
                if (sourceRenderer == null ||
                    sourceRenderer == trail ||
                    sourceRenderer.sharedMaterial == null)
                {
                    continue;
                }

                trail.sharedMaterial = sourceRenderer.sharedMaterial;
                break;
            }

            trail.emitting = false;
            trail.Clear();
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
