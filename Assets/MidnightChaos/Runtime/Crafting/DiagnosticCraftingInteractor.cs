using MidnightChaos.Combat;
using MidnightChaos.Equipment;
using MidnightChaos.Inventory;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidnightChaos.Crafting
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkHealth))]
    [RequireComponent(typeof(DiagnosticNetworkInventory))]
    [RequireComponent(typeof(DiagnosticPlayerEquipment))]
    public sealed class DiagnosticCraftingInteractor : NetworkBehaviour
    {
        public const int DefaultSwordWoodCost = 3;

        [Header("Gate D - Host Validated Sword Craft")]
        [SerializeField, Min(0.1f)] private float interactionReach = 2.8f;
        [SerializeField, Range(1f, 180f)] private float interactionHalfAngle = 75f;
        [SerializeField, Min(1)] private int swordWoodCost = DefaultSwordWoodCost;
        [SerializeField, Min(0.05f)] private float requestCooldownSeconds = 0.25f;

        private NetworkHealth health;
        private DiagnosticNetworkInventory inventory;
        private DiagnosticPlayerEquipment equipment;
        private double nextAllowedServerRequestTime;

        public int SwordWoodCost => swordWoodCost;

        private void Awake()
        {
            health = GetComponent<NetworkHealth>();
            inventory = GetComponent<DiagnosticNetworkInventory>();
            equipment = GetComponent<DiagnosticPlayerEquipment>();
        }

        private void Update()
        {
            if (!IsSpawned ||
                !IsOwner ||
                health == null ||
                health.IsDead ||
                Keyboard.current == null ||
                !Keyboard.current.eKey.wasPressedThisFrame)
            {
                return;
            }

            RequestCraftSwordRpc();
        }

        [Rpc(
            SendTo.Server,
            InvokePermission = RpcInvokePermission.Owner)]
        private void RequestCraftSwordRpc()
        {
            if (!IsServer ||
                !IsSpawned ||
                health == null ||
                health.IsDead ||
                inventory == null ||
                equipment == null ||
                equipment.HasSword)
            {
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextAllowedServerRequestTime)
            {
                return;
            }

            nextAllowedServerRequestTime = now + requestCooldownSeconds;

            DiagnosticCraftingStation station = FindBestStationServer();
            if (station == null || !inventory.TrySpendWoodServer(swordWoodCost))
            {
                return;
            }

            // Every precondition was checked on the Host immediately before
            // this commit. The refund is a defensive guard if an invariant is
            // broken by a later code change.
            if (!equipment.TryGrantSwordServer())
            {
                if (!inventory.TryAddWoodServer(swordWoodCost))
                {
                    Debug.LogError(
                        $"[Gate D] Failed to refund {swordWoodCost} Wood to " +
                        $"Player {OwnerClientId} after sword grant failed.");
                }

                return;
            }

            Debug.Log(
                $"[Gate D] Player {OwnerClientId} crafted a Sword at " +
                $"{station.name}. Wood cost: {swordWoodCost}.");
        }

        private DiagnosticCraftingStation FindBestStationServer()
        {
            if (!IsServer || interactionReach <= 0f)
            {
                return null;
            }

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            float clampedHalfAngle = Mathf.Clamp(interactionHalfAngle, 1f, 180f);
            float minimumDot = Mathf.Cos(clampedHalfAngle * Mathf.Deg2Rad);
            float maximumDistanceSquared = interactionReach * interactionReach;
            float bestDistanceSquared = float.PositiveInfinity;
            DiagnosticCraftingStation bestStation = null;

            DiagnosticCraftingStation[] stations =
                FindObjectsByType<DiagnosticCraftingStation>(
                    FindObjectsSortMode.None);

            foreach (DiagnosticCraftingStation station in stations)
            {
                if (!station.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 toStation = Vector3.ProjectOnPlane(
                    station.transform.position - transform.position,
                    Vector3.up);

                float distanceSquared = toStation.sqrMagnitude;
                if (distanceSquared > maximumDistanceSquared ||
                    distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                if (distanceSquared > 0.0001f &&
                    Vector3.Dot(forward, toStation.normalized) < minimumDot)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                bestStation = station;
            }

            return bestStation;
        }
    }
}
