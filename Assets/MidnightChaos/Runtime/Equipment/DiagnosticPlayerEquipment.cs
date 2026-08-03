using System;
using MidnightChaos.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace MidnightChaos.Equipment
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DiagnosticPlayerEquipment : NetworkBehaviour
    {
        private const string SwordVisualName = "SwordVisual";
        private const string ViewmodelRootName = "FirstPersonViewmodelRoot";
        private const string AttackPivotName = "AttackPivot";

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

        // v0.8.2 moves these values to the motion-set ScriptableObject. They
        // stay hidden here only so the migration command can preserve values
        // already serialized by v0.8.1.2.
        [FormerlySerializedAs("firstPersonLocalPosition")]
        [SerializeField, HideInInspector]
        private Vector3 legacyFirstPersonLocalPosition =
            new Vector3(0.42f, -0.38f, 0.72f);
        [FormerlySerializedAs("firstPersonLocalEulerAngles")]
        [SerializeField, HideInInspector]
        private Vector3 legacyFirstPersonLocalEulerAngles =
            new Vector3(8f, 0f, -18f);
        [FormerlySerializedAs("firstPersonLocalScale")]
        [SerializeField, HideInInspector]
        private Vector3 legacyFirstPersonLocalScale = Vector3.one;

        private DiagnosticPlayerAnimation playerAnimation;
        private GameObject firstPersonViewmodelRoot;
        private Transform firstPersonAttackPivot;
        private GameObject firstPersonSwordVisual;
        private bool warnedAboutMissingWorldSword;

        public event Action<bool, bool> SwordStateChanged;
        public event Action<Transform> FirstPersonViewmodelReady;

        public bool HasSword => hasSword.Value;
        public Transform FirstPersonAttackPivot => firstPersonAttackPivot;
        public Vector3 LegacyFirstPersonLocalPosition =>
            legacyFirstPersonLocalPosition;
        public Vector3 LegacyFirstPersonLocalEulerAngles =>
            legacyFirstPersonLocalEulerAngles;
        public Vector3 LegacyFirstPersonLocalScale =>
            legacyFirstPersonLocalScale;

        private void Awake()
        {
            playerAnimation = GetComponent<DiagnosticPlayerAnimation>();
            ResolveWorldSwordVisual();
            RefreshSwordVisual();
        }

        public override void OnNetworkSpawn()
        {
            hasSword.OnValueChanged += HandleSwordStateChanged;

            if (IsOwner)
            {
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

            if (firstPersonViewmodelRoot != null)
            {
                Destroy(firstPersonViewmodelRoot);
            }

            firstPersonViewmodelRoot = null;
            firstPersonAttackPivot = null;
            firstPersonSwordVisual = null;
        }

        public void ConfigureWorldSwordVisual(GameObject visual)
        {
            worldSwordVisual = visual;
            warnedAboutMissingWorldSword = false;
            RefreshSwordVisual();
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

            GameObject pivotObject = new GameObject(AttackPivotName);
            pivotObject.transform.SetParent(
                firstPersonViewmodelRoot.transform,
                false);
            firstPersonAttackPivot = pivotObject.transform;
            FirstPersonViewmodelReady?.Invoke(firstPersonAttackPivot);

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
                RefreshSwordVisual();
                return;
            }

            if (source.GetComponentInChildren<NetworkObject>(true) != null)
            {
                Debug.LogError(
                    "[Gate H2] Sword visual không được chứa NetworkObject. " +
                    "Hãy dùng prefab chỉ chứa mesh/renderer cho kiếm.",
                    source);
                RefreshSwordVisual();
                return;
            }

            firstPersonSwordVisual = Instantiate(
                source,
                firstPersonAttackPivot,
                false);
            firstPersonSwordVisual.name = "FirstPersonSwordVisual";
            firstPersonSwordVisual.transform.localPosition = Vector3.zero;
            firstPersonSwordVisual.transform.localRotation = Quaternion.identity;
            firstPersonSwordVisual.transform.localScale = Vector3.one;
            firstPersonSwordVisual.SetActive(false);
            RefreshSwordVisual();
        }
    }
}
