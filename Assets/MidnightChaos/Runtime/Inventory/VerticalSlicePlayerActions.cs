using MidnightChaos.Combat;
using MidnightChaos.Equipment;
using MidnightChaos.Procedural;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidnightChaos.Inventory
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(DiagnosticNetworkInventory))]
    public sealed class VerticalSlicePlayerActions : NetworkBehaviour
    {
        private static bool fallbackWarningLogged;

        private DiagnosticNetworkInventory inventory;
        private NetworkHealth health;
        private DiagnosticPlayerEquipment equipment;
        [SerializeField] private ProceduralWorldSettings worldSettings;
        [SerializeField] private VerticalSliceGameplaySettings gameplaySettings;
        private GameObject heldRock;
        private GameObject placementPreview;
        private bool placementValid;
        private Vector3 placementPosition;
        private DiagnosticWorldPickup nearestPickup;
        private string placementStatus = string.Empty;

        public void Configure(
            ProceduralWorldSettings configuredWorldSettings,
            VerticalSliceGameplaySettings configuredGameplaySettings)
        {
            worldSettings = configuredWorldSettings;
            gameplaySettings = configuredGameplaySettings;
        }

        private void Awake()
        {
            inventory = GetComponent<DiagnosticNetworkInventory>();
            health = GetComponent<NetworkHealth>();
            equipment = GetComponent<DiagnosticPlayerEquipment>();
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
                "[Settings] VerticalSlicePlayerActions had missing injected " +
                "settings; using Resources compatibility fallback.",
                this);
        }

        public override void OnNetworkSpawn()
        {
            inventory.InventoryChanged += RefreshEquippedVisual;
            if (equipment != null)
            {
                equipment.FirstPersonViewmodelReady +=
                    HandleFirstPersonViewmodelReady;
            }
            RefreshEquippedVisual();
        }

        public override void OnNetworkDespawn()
        {
            inventory.InventoryChanged -= RefreshEquippedVisual;
            if (equipment != null)
            {
                equipment.FirstPersonViewmodelReady -=
                    HandleFirstPersonViewmodelReady;
            }
            DestroyLocalVisuals();
        }

        private void Update()
        {
            if (!IsOwner || !IsSpawned || worldSettings == null ||
                gameplaySettings == null ||
                health == null || health.IsDead)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;

            nearestPickup = FindNearestPickup();
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame &&
                nearestPickup != null)
            {
                nearestPickup.RequestPickup(NetworkObject);
            }

            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            {
                CraftWorkbenchRpc();
            }

            bool placing =
                inventory.SelectedItem == VerticalSliceItemId.Workbench;
            if (placing)
            {
                UpdatePlacementPreview();
                bool placementPressed =
                    keyboard != null && keyboard.gKey.wasPressedThisFrame;
                if (placementPressed && placementValid)
                {
                    PlaceWorkbenchRpc(
                        placementPosition,
                        transform.eulerAngles.y);
                }
            }
            else
            {
                SetPreviewVisible(false);
            }
        }

        private DiagnosticWorldPickup FindNearestPickup()
        {
            float maximumDistance = gameplaySettings.PickupRadius;
            float bestDistanceSquared = maximumDistance * maximumDistance;
            DiagnosticWorldPickup best = null;
            foreach (DiagnosticWorldPickup candidate in
                     FindObjectsByType<DiagnosticWorldPickup>(
                         FindObjectsSortMode.None))
            {
                if (candidate == null || !candidate.IsSpawned ||
                    !candidate.IsPickup)
                {
                    continue;
                }

                Vector3 delta = Vector3.ProjectOnPlane(
                    candidate.transform.position - transform.position,
                    Vector3.up);
                float distanceSquared = delta.sqrMagnitude;
                if (distanceSquared > bestDistanceSquared)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                best = candidate;
            }
            return best;
        }

        [Rpc(
            SendTo.Server,
            InvokePermission = RpcInvokePermission.Owner)]
        private void CraftWorkbenchRpc()
        {
            int cost = gameplaySettings.WorkbenchWoodCost;
            if (!inventory.TrySpendItemServer(VerticalSliceItemId.Wood, cost))
            {
                Debug.Log($"[Craft] Need Wood x{cost} for Workbench.");
                return;
            }
            if (!inventory.TryAddItemServer(VerticalSliceItemId.Workbench, 1))
            {
                inventory.TryAddItemServer(VerticalSliceItemId.Wood, cost);
                Debug.LogWarning("[Craft] Hotbar is full; Wood was refunded.");
                return;
            }
            Debug.Log(
                $"[Craft] Player {OwnerClientId} crafted Workbench for " +
                $"Wood x{cost}.");
        }

        [Rpc(
            SendTo.Server,
            InvokePermission = RpcInvokePermission.Owner)]
        private void PlaceWorkbenchRpc(Vector3 requestedPosition, float yaw)
        {
            VerticalSliceGameplaySettings gameplay = gameplaySettings;
            if (inventory.SelectedItem != VerticalSliceItemId.Workbench)
            {
                RejectPlacementServer(
                    $"selected item is {inventory.SelectedItem}, not Workbench");
                return;
            }
            Vector2 planarDelta = new Vector2(
                requestedPosition.x - transform.position.x,
                requestedPosition.z - transform.position.z);
            float allowedDistance = gameplay.PlacementDistance + 0.25f;
            if (planarDelta.sqrMagnitude > allowedDistance * allowedDistance)
            {
                RejectPlacementServer(
                    $"distance {planarDelta.magnitude:0.00}m exceeds " +
                    $"{allowedDistance:0.00}m");
                return;
            }
            Vector3 rayStart = requestedPosition +
                               Vector3.up * gameplay.PlacementGroundProbe;
            if (!Physics.Raycast(
                    rayStart,
                    Vector3.down,
                    out RaycastHit hit,
                    gameplay.PlacementGroundProbe * 2f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                RejectPlacementServer("no valid ground below requested point");
                return;
            }
            Vector3 finalPosition = hit.point + Vector3.up * 0.5f;
            Collider[] overlaps = Physics.OverlapBox(
                finalPosition,
                new Vector3(0.85f, 0.45f, 0.55f),
                Quaternion.Euler(0f, yaw, 0f),
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
            {
                if (overlap != null && overlap != hit.collider &&
                    !overlap.transform.IsChildOf(transform) &&
                    overlap.GetComponentInParent<DiagnosticWorldPickup>() == null)
                {
                    RejectPlacementServer(
                        $"overlaps '{overlap.name}'");
                    return;
                }
            }
            if (!inventory.TrySpendSelectedServer(
                    VerticalSliceItemId.Workbench,
                    1))
            {
                RejectPlacementServer("selected Workbench stack is empty");
                return;
            }
            NetworkObject placed = DiagnosticWorldPickup.SpawnServer(
                gameplay.WorldItemNetworkPrefab,
                finalPosition,
                Quaternion.Euler(0f, yaw, 0f),
                VerticalSliceItemId.Workbench,
                1,
                VerticalSliceWorldItemMode.PlacedWorkbench);
            if (placed == null)
            {
                inventory.TryAddItemServer(VerticalSliceItemId.Workbench, 1);
                RejectPlacementServer("network world item spawn failed; item refunded");
                return;
            }
            Debug.Log(
                $"[Placement] Player {OwnerClientId} placed Workbench " +
                $"{placed.NetworkObjectId}.");
            PlacementResultRpc(true, "Workbench placed");
        }

        private void RejectPlacementServer(string reason)
        {
            Debug.LogWarning($"[Placement] Rejected: {reason}.", this);
            PlacementResultRpc(false, reason);
        }

        [Rpc(SendTo.Owner)]
        private void PlacementResultRpc(bool success, string message)
        {
            placementStatus = success
                ? message
                : $"Cannot place: {message}";
        }

        private void UpdatePlacementPreview()
        {
            EnsurePreview();
            Camera camera = Camera.main;
            Vector3 origin = camera != null
                ? camera.transform.position
                : transform.position + Vector3.up;
            Vector3 direction = camera != null
                ? camera.transform.forward
                : transform.forward;
            Vector3 projected = transform.position +
                                Vector3.ProjectOnPlane(direction, Vector3.up)
                                    .normalized *
                                gameplaySettings.PlacementDistance;
            Vector3 rayStart = new Vector3(
                projected.x,
                origin.y + gameplaySettings.PlacementGroundProbe,
                projected.z);
            placementValid = Physics.Raycast(
                rayStart,
                Vector3.down,
                out RaycastHit hit,
                gameplaySettings.PlacementGroundProbe * 4f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            placementPosition = placementValid
                ? hit.point + Vector3.up * 0.5f
                : projected;
            placementStatus = placementValid
                ? "Valid placement - press G"
                : "Cannot place: no ground";
            if (placementValid)
            {
                Collider[] overlaps = Physics.OverlapBox(
                    placementPosition,
                    new Vector3(0.85f, 0.45f, 0.55f),
                    Quaternion.Euler(0f, transform.eulerAngles.y, 0f),
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                foreach (Collider overlap in overlaps)
                {
                    if (overlap != null && overlap != hit.collider &&
                        !overlap.transform.IsChildOf(transform) &&
                        overlap.GetComponentInParent<DiagnosticWorldPickup>() == null)
                    {
                        placementValid = false;
                        placementStatus =
                            $"Cannot place: blocked by {overlap.name}";
                        break;
                    }
                }
            }
            placementPreview.transform.SetPositionAndRotation(
                placementPosition,
                Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            Renderer renderer = placementPreview.GetComponent<Renderer>();
            renderer.material.color = placementValid
                ? new Color(0.1f, 0.9f, 0.2f, 0.45f)
                : new Color(0.9f, 0.1f, 0.1f, 0.45f);
            SetPreviewVisible(true);
        }

        private void RefreshEquippedVisual()
        {
            if (worldSettings == null)
            {
                return;
            }
            bool showRock = inventory.SelectedItem == VerticalSliceItemId.Rock;
            if (showRock && heldRock == null &&
                worldSettings.SmallRocks.VisualPrefab != null)
            {
                Transform parent = ResolveHeldRockParent();
                heldRock = Instantiate(
                    worldSettings.SmallRocks.VisualPrefab,
                    parent);
                heldRock.name = "HeldRockVisual";
                bool usesAnimatedWeapon =
                    IsOwner && equipment != null &&
                    parent == equipment.FirstPersonAnimatedWeapon;
                heldRock.transform.localPosition = usesAnimatedWeapon
                    ? Vector3.zero
                    : IsOwner
                        ? new Vector3(0.45f, -0.35f, 0.75f)
                    : new Vector3(0.45f, 1.15f, 0.25f);
                heldRock.transform.localRotation = usesAnimatedWeapon
                    ? Quaternion.Euler(0f, 90f, 0f)
                    : IsOwner
                        ? Quaternion.Euler(25f, 35f, 0f)
                    : Quaternion.Euler(25f, 35f, 20f);
                heldRock.transform.localScale *= 0.55f;
                foreach (Collider target in heldRock.GetComponentsInChildren<Collider>(true))
                {
                    target.enabled = false;
                }
            }
            if (heldRock != null)
            {
                heldRock.SetActive(showRock);
            }
        }

        private Transform ResolveHeldRockParent()
        {
            if (IsOwner && equipment != null &&
                equipment.FirstPersonAnimatedWeapon != null)
            {
                return equipment.FirstPersonAnimatedWeapon;
            }
            if (IsOwner && Camera.main != null)
            {
                return Camera.main.transform;
            }
            return transform;
        }

        private void HandleFirstPersonViewmodelReady(Transform _)
        {
            if (heldRock != null)
            {
                Destroy(heldRock);
                heldRock = null;
            }
            RefreshEquippedVisual();
        }

        private void EnsurePreview()
        {
            if (placementPreview != null)
            {
                return;
            }
            placementPreview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placementPreview.name = "WorkbenchPlacementPreview";
            placementPreview.transform.localScale = new Vector3(1.8f, 1f, 1.2f);
            Collider previewCollider = placementPreview.GetComponent<Collider>();
            if (previewCollider != null) Destroy(previewCollider);
        }

        private void SetPreviewVisible(bool visible)
        {
            if (placementPreview != null)
            {
                placementPreview.SetActive(visible);
            }
        }

        private void DestroyLocalVisuals()
        {
            if (heldRock != null) Destroy(heldRock);
            if (placementPreview != null) Destroy(placementPreview);
        }

        private void OnGUI()
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }
            GUI.Label(
                new Rect(Screen.width * 0.5f - 270f, 8f, 540f, 24f),
                "1-0/Scroll: select | E: pickup | C: craft Workbench | G: place");
            if (nearestPickup != null)
            {
                GUI.Box(
                    new Rect(
                        Screen.width * 0.5f - 110f,
                        Screen.height * 0.5f + 55f,
                        220f,
                        30f),
                    $"E - Pick up {nearestPickup.Item} x{nearestPickup.Amount}");
            }
            if (inventory.SelectedItem == VerticalSliceItemId.Workbench &&
                !string.IsNullOrEmpty(placementStatus))
            {
                GUI.Box(
                    new Rect(
                        Screen.width * 0.5f - 140f,
                        Screen.height * 0.5f + 90f,
                        280f,
                        30f),
                    placementStatus);
            }
        }
    }
}
