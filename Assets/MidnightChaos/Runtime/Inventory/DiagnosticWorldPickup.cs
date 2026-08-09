using System;
using MidnightChaos.Procedural;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Inventory
{
    public enum VerticalSliceWorldItemMode : byte
    {
        Pickup = 0,
        PlacedWorkbench = 1
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DiagnosticWorldPickup : NetworkBehaviour
    {
        public static event Action WorkbenchPlacedServer;
        private static bool fallbackWarningLogged;

        private readonly NetworkVariable<VerticalSliceItemId> item =
            new NetworkVariable<VerticalSliceItemId>(
                VerticalSliceItemId.ChaosShard,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ushort> amount =
            new NetworkVariable<ushort>(
                1,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<VerticalSliceWorldItemMode> mode =
            new NetworkVariable<VerticalSliceWorldItemMode>(
                VerticalSliceWorldItemMode.Pickup,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private Collider pickupCollider;
        private Rigidbody body;
        private Renderer fallbackRenderer;
        private GameObject spawnedRockVisual;
        [SerializeField] private ProceduralWorldSettings worldSettings;
        [SerializeField] private VerticalSliceGameplaySettings gameplaySettings;
        private VerticalSliceItemId pendingItem =
            VerticalSliceItemId.ChaosShard;
        private ushort pendingAmount = 1;
        private VerticalSliceWorldItemMode pendingMode =
            VerticalSliceWorldItemMode.Pickup;
        private bool hasPendingSpawnConfiguration;

        public VerticalSliceItemId Item => item.Value;
        public int Amount => amount.Value;
        public bool IsPickup => mode.Value == VerticalSliceWorldItemMode.Pickup;

        public void Configure(
            ProceduralWorldSettings configuredWorldSettings,
            VerticalSliceGameplaySettings configuredGameplaySettings)
        {
            worldSettings = configuredWorldSettings;
            gameplaySettings = configuredGameplaySettings;
        }

        private void Awake()
        {
            pickupCollider = GetComponent<Collider>();
            body = GetComponent<Rigidbody>();
            fallbackRenderer = GetComponent<Renderer>();
            bool usedFallback = false;
            if (worldSettings == null)
            {
                worldSettings =
                    UnityEngine.Resources.Load<ProceduralWorldSettings>(
                        "Procedural/ProceduralWorldSettings");
                usedFallback = true;
            }
            if (gameplaySettings == null)
            {
                gameplaySettings =
                    UnityEngine.Resources.Load<VerticalSliceGameplaySettings>(
                        VerticalSliceGameplaySettings.ResourcePath);
                usedFallback = true;
            }
            if (usedFallback)
            {
                LogFallbackWarningOnce();
            }
        }

        private void LogFallbackWarningOnce()
        {
            if (fallbackWarningLogged)
            {
                return;
            }
            fallbackWarningLogged = true;
            Debug.LogWarning(
                "[Settings] DiagnosticWorldPickup had missing injected " +
                "settings; using Resources compatibility fallback.",
                this);
        }

        public override void OnNetworkSpawn()
        {
            item.OnValueChanged += HandleItemChanged;
            mode.OnValueChanged += HandleModeChanged;

            if (IsServer && hasPendingSpawnConfiguration)
            {
                item.Value = pendingItem;
                amount.Value = pendingAmount;
                mode.Value = pendingMode;
                hasPendingSpawnConfiguration = false;
            }

            RefreshPresentation();
        }

        public override void OnNetworkDespawn()
        {
            item.OnValueChanged -= HandleItemChanged;
            mode.OnValueChanged -= HandleModeChanged;
        }

        public void ConfigureBeforeSpawnServer(
            VerticalSliceItemId configuredItem,
            int configuredAmount,
            VerticalSliceWorldItemMode configuredMode =
                VerticalSliceWorldItemMode.Pickup)
        {
            if (NetworkObject.IsSpawned)
            {
                Debug.LogError("[Pickup] Configure must run before NetworkObject.Spawn().", this);
                return;
            }
            pendingItem = configuredItem;
            pendingAmount = (ushort)Mathf.Clamp(
                configuredAmount,
                1,
                ushort.MaxValue);
            pendingMode = configuredMode;
            hasPendingSpawnConfiguration = true;
        }

        public void RequestPickup(NetworkObject playerObject)
        {
            if (!IsSpawned || !IsPickup || playerObject == null)
            {
                return;
            }
            TryPickupRpc(new NetworkObjectReference(playerObject));
        }

        [Rpc(
            SendTo.Server,
            InvokePermission = RpcInvokePermission.Everyone)]
        private void TryPickupRpc(
            NetworkObjectReference playerReference,
            RpcParams rpcParams = default)
        {
            if (!IsServer || !IsSpawned || !IsPickup ||
                !playerReference.TryGet(out NetworkObject playerObject) ||
                playerObject == null ||
                playerObject.OwnerClientId != rpcParams.Receive.SenderClientId)
            {
                return;
            }

            DiagnosticNetworkInventory inventory =
                playerObject.GetComponent<DiagnosticNetworkInventory>();
            float interactionDistance = ResolvePickupDistance();
            Vector3 delta = Vector3.ProjectOnPlane(
                playerObject.transform.position - transform.position,
                Vector3.up);
            if (inventory == null || !inventory.IsSpawned ||
                delta.sqrMagnitude > interactionDistance * interactionDistance ||
                !inventory.TryAddItemServer(Item, Amount))
            {
                return;
            }
            Debug.Log(
                $"[Pickup] Player {inventory.OwnerClientId} picked up " +
                $"{Item} x{Amount}.");
            NetworkObject.Despawn(true);
        }

        private float ResolvePickupDistance()
        {
            return gameplaySettings != null
                ? gameplaySettings.PickupRadius
                : 1.4f;
        }

        private void HandleItemChanged(
            VerticalSliceItemId previous,
            VerticalSliceItemId current) => RefreshPresentation();

        private void HandleModeChanged(
            VerticalSliceWorldItemMode previous,
            VerticalSliceWorldItemMode current) => RefreshPresentation();

        private void RefreshPresentation()
        {
            if (pickupCollider != null)
            {
                pickupCollider.isTrigger = IsPickup;
            }
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            if (spawnedRockVisual != null)
            {
                Destroy(spawnedRockVisual);
                spawnedRockVisual = null;
            }

            if (pickupCollider is BoxCollider boxCollider)
            {
                float diameter = gameplaySettings != null
                    ? gameplaySettings.PickupRadius * 2f
                    : 2f;
                boxCollider.size = IsPickup
                    ? Vector3.one * diameter
                    : Vector3.one;
            }
            else if (pickupCollider is SphereCollider sphereCollider)
            {
                sphereCollider.radius = IsPickup && gameplaySettings != null
                    ? gameplaySettings.PickupRadius
                    : 0.5f;
            }
            if (Item == VerticalSliceItemId.Rock &&
                worldSettings != null &&
                worldSettings.SmallRocks.VisualPrefab != null)
            {
                if (fallbackRenderer != null)
                {
                    fallbackRenderer.enabled = false;
                }
                spawnedRockVisual = Instantiate(
                    worldSettings.SmallRocks.VisualPrefab,
                    transform);
                spawnedRockVisual.name = "RockVisual";
                spawnedRockVisual.transform.localPosition = Vector3.zero;
                spawnedRockVisual.transform.localRotation = Quaternion.identity;
                DisableSourceBehaviours(spawnedRockVisual);
                return;
            }

            if (fallbackRenderer == null)
            {
                return;
            }
            fallbackRenderer.enabled = true;
            fallbackRenderer.material.color = IsPickup
                ? Item switch
                {
                    VerticalSliceItemId.Wood => new Color(0.42f, 0.22f, 0.08f),
                    VerticalSliceItemId.Ore => new Color(0.2f, 0.55f, 0.72f),
                    VerticalSliceItemId.Workbench => new Color(0.5f, 0.3f, 0.12f),
                    VerticalSliceItemId.ChaosShard => new Color(0.75f, 0.1f, 1f),
                    _ => Color.gray
                }
                : new Color(0.45f, 0.27f, 0.1f);
            transform.localScale = mode.Value ==
                VerticalSliceWorldItemMode.PlacedWorkbench
                ? new Vector3(1.8f, 1f, 1.2f)
                : Item switch
                {
                    VerticalSliceItemId.Wood => new Vector3(0.25f, 0.25f, 0.8f),
                    VerticalSliceItemId.Ore => Vector3.one * 0.4f,
                    _ => new Vector3(0.35f, 0.7f, 0.35f)
                };
        }

        private static void DisableSourceBehaviours(GameObject root)
        {
            foreach (Collider sourceCollider in root.GetComponentsInChildren<Collider>(true))
            {
                sourceCollider.enabled = false;
            }
            foreach (Rigidbody sourceBody in root.GetComponentsInChildren<Rigidbody>(true))
            {
                sourceBody.isKinematic = true;
                sourceBody.detectCollisions = false;
            }
            foreach (NetworkObject sourceNetworkObject in root.GetComponentsInChildren<NetworkObject>(true))
            {
                sourceNetworkObject.enabled = false;
            }
        }

        public static NetworkObject SpawnServer(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            VerticalSliceItemId item,
            int amount,
            VerticalSliceWorldItemMode mode = VerticalSliceWorldItemMode.Pickup)
        {
            if (prefab == null)
            {
                Debug.LogError("[Pickup] World item network prefab is missing.");
                return null;
            }
            GameObject instance = Instantiate(prefab, position, rotation);
            DiagnosticWorldPickup pickup =
                instance.GetComponent<DiagnosticWorldPickup>();
            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            if (pickup == null || networkObject == null)
            {
                Debug.LogError("[Pickup] Prefab needs DiagnosticWorldPickup + NetworkObject.");
                Destroy(instance);
                return null;
            }
            pickup.ConfigureBeforeSpawnServer(item, amount, mode);
            networkObject.Spawn(true);
            if (mode == VerticalSliceWorldItemMode.PlacedWorkbench)
            {
                WorkbenchPlacedServer?.Invoke();
            }
            return networkObject;
        }
    }
}
