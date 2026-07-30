using System;
using MidnightChaos.Inventory;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Resources
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DiagnosticResourceNode : NetworkBehaviour
    {
        [Header("Gate C - Wood Node")]
        [SerializeField, Min(1)] private int maximumHits = 3;
        [SerializeField, Min(1)] private int woodPerHit = 1;
        [SerializeField, Min(0.1f)] private float finalValidationReach = 3f;

        private NetworkVariable<int> remainingHits =
            new NetworkVariable<int>(
                3,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private Renderer[] renderers;
        private Color[] activeColors;

        public event Action<int, int> RemainingHitsChanged;

        public int MaximumHits => maximumHits;
        public int RemainingHits => remainingHits.Value;
        public bool IsDepleted => RemainingHits <= 0;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            activeColors = new Color[renderers.Length];

            for (int index = 0; index < renderers.Length; index++)
            {
                activeColors[index] = renderers[index].material.color;
            }
        }

        public override void OnNetworkSpawn()
        {
            remainingHits.OnValueChanged += HandleRemainingHitsChanged;

            if (IsServer)
            {
                remainingHits.Value = maximumHits;
            }

            RefreshVisuals();
        }

        public override void OnNetworkDespawn()
        {
            remainingHits.OnValueChanged -= HandleRemainingHitsChanged;
        }

        public bool TryHarvestServer(
            NetworkObject harvester,
            DiagnosticNetworkInventory inventory)
        {
            if (!IsServer ||
                IsDepleted ||
                harvester == null ||
                inventory == null ||
                inventory.NetworkObject != harvester)
            {
                return false;
            }

            float distanceSquared =
                (harvester.transform.position - transform.position).sqrMagnitude;

            if (distanceSquared > finalValidationReach * finalValidationReach ||
                !inventory.TryAddWoodServer(woodPerHit))
            {
                return false;
            }

            int previousHits = remainingHits.Value;
            remainingHits.Value = Mathf.Max(0, previousHits - 1);

            Debug.Log(
                $"[Gate C.1] Player {harvester.OwnerClientId} harvested " +
                $"{woodPerHit} Wood. Node {NetworkObjectId}: " +
                $"{previousHits} -> {remainingHits.Value} hits.");

            return true;
        }

        private void HandleRemainingHitsChanged(int previous, int current)
        {
            RefreshVisuals();
            RemainingHitsChanged?.Invoke(previous, current);
        }

        private void RefreshVisuals()
        {
            if (renderers == null)
            {
                return;
            }

            float ratio = maximumHits > 0
                ? Mathf.Clamp01((float)remainingHits.Value / maximumHits)
                : 0f;

            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].material.color = IsDepleted
                    ? new Color(0.18f, 0.18f, 0.18f)
                    : Color.Lerp(
                        new Color(0.28f, 0.22f, 0.16f),
                        activeColors[index],
                        0.45f + ratio * 0.55f);
            }
        }
    }
}
