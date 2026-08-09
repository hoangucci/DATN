using MidnightChaos.Combat;
using MidnightChaos.Inventory;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Resources
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkHealth))]
    [RequireComponent(typeof(DiagnosticNetworkInventory))]
    public sealed class DiagnosticResourceGatherer : NetworkBehaviour
    {
        private static bool fallbackWarningLogged;

        private NetworkList<FixedString64Bytes> depletedKeys;
        private NetworkHealth health;
        private DiagnosticNetworkInventory inventory;
        [SerializeField] private VerticalSliceGameplaySettings gameplaySettings;

        public void Configure(
            VerticalSliceGameplaySettings configuredGameplaySettings)
        {
            gameplaySettings = configuredGameplaySettings;
        }

        private void Awake()
        {
            depletedKeys = new NetworkList<FixedString64Bytes>();
            health = GetComponent<NetworkHealth>();
            inventory = GetComponent<DiagnosticNetworkInventory>();
            if (gameplaySettings == null)
            {
                gameplaySettings =
                    UnityEngine.Resources.Load<VerticalSliceGameplaySettings>(
                        VerticalSliceGameplaySettings.ResourcePath);
                LogFallbackWarningOnce();
            }
        }

        public override void OnNetworkSpawn()
        {
            depletedKeys.OnListChanged += HandleDepletedChanged;
            ApplyAllDepletedKeys();
        }

        public override void OnNetworkDespawn()
        {
            depletedKeys.OnListChanged -= HandleDepletedChanged;
        }

        public ProceduralHarvestable FindBestResourceServer(
            float attackReach,
            float attackHalfAngle,
            out float bestDistanceSquared)
        {
            bestDistanceSquared = float.PositiveInfinity;
            if (!IsServer || !IsSpawned || health == null || health.IsDead ||
                inventory == null ||
                inventory.SelectedItem != VerticalSliceItemId.Rock ||
                attackReach <= 0f)
            {
                return null;
            }

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            forward = forward.sqrMagnitude < 0.0001f
                ? Vector3.forward
                : forward.normalized;
            float minimumDot = Mathf.Cos(
                Mathf.Clamp(attackHalfAngle, 1f, 180f) * Mathf.Deg2Rad);
            float maximumDistanceSquared = attackReach * attackReach;
            ProceduralHarvestable best = null;
            foreach (ProceduralHarvestable target in ProceduralHarvestable.Active)
            {
                if (target == null || target.IsDepleted)
                {
                    continue;
                }
                Vector3 delta = Vector3.ProjectOnPlane(
                    target.transform.position - transform.position,
                    Vector3.up);
                float distanceSquared = delta.sqrMagnitude;
                if (distanceSquared < 0.0001f ||
                    distanceSquared > maximumDistanceSquared ||
                    distanceSquared >= bestDistanceSquared ||
                    Vector3.Dot(forward, delta.normalized) < minimumDot)
                {
                    continue;
                }
                bestDistanceSquared = distanceSquared;
                best = target;
            }
            return best;
        }

        public bool TryHarvestServer(ProceduralHarvestable target)
        {
            if (!IsServer || !IsSpawned || health == null || health.IsDead ||
                inventory == null ||
                inventory.SelectedItem != VerticalSliceItemId.Rock ||
                target == null || gameplaySettings == null ||
                (target.transform.position - transform.position).sqrMagnitude > 16f)
            {
                return false;
            }
            if (!target.TryDamage(
                    gameplaySettings.RockHarvestDamage,
                    out bool destroyed))
            {
                return false;
            }
            if (!destroyed)
            {
                return true;
            }

            FixedString64Bytes key = new FixedString64Bytes(target.StableKey);
            if (!ContainsKey(key))
            {
                depletedKeys.Add(key);
            }
            ProceduralHarvestable.SetDepletedByKey(target.StableKey);
            DiagnosticWorldPickup.SpawnServer(
                gameplaySettings.WorldItemNetworkPrefab,
                target.DropPosition,
                Quaternion.identity,
                target.DropItem,
                target.DropAmount);
            Debug.Log(
                $"[Harvest] Destroyed {target.StableKey}; dropped " +
                $"{target.DropItem} x{target.DropAmount}.");
            return true;
        }

        public void ClearHarvestStateServer()
        {
            if (IsServer)
            {
                depletedKeys.Clear();
            }
        }

        private bool ContainsKey(FixedString64Bytes key)
        {
            for (int index = 0; index < depletedKeys.Count; index++)
            {
                if (depletedKeys[index].Equals(key)) return true;
            }
            return false;
        }

        private void HandleDepletedChanged(
            NetworkListEvent<FixedString64Bytes> change)
        {
            if (change.Type == NetworkListEvent<FixedString64Bytes>.EventType.Add ||
                change.Type == NetworkListEvent<FixedString64Bytes>.EventType.Insert ||
                change.Type == NetworkListEvent<FixedString64Bytes>.EventType.Value)
            {
                ProceduralHarvestable.SetDepletedByKey(change.Value.ToString());
            }
        }

        private void ApplyAllDepletedKeys()
        {
            for (int index = 0; index < depletedKeys.Count; index++)
            {
                ProceduralHarvestable.SetDepletedByKey(
                    depletedKeys[index].ToString());
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
                "[Settings] DiagnosticResourceGatherer had no injected " +
                "Gameplay Settings; using Resources compatibility fallback.",
                this);
        }
    }
}
