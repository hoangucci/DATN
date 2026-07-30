using MidnightChaos.Combat;
using MidnightChaos.Inventory;
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
        private NetworkHealth health;
        private DiagnosticNetworkInventory inventory;

        private void Awake()
        {
            health = GetComponent<NetworkHealth>();
            inventory = GetComponent<DiagnosticNetworkInventory>();
        }

        public DiagnosticResourceNode FindBestResourceServer(
            float attackReach,
            float attackHalfAngle,
            out float bestDistanceSquared)
        {
            bestDistanceSquared = float.PositiveInfinity;

            if (!IsServer ||
                !IsSpawned ||
                health == null ||
                health.IsDead ||
                attackReach <= 0f)
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

            float clampedHalfAngle = Mathf.Clamp(attackHalfAngle, 1f, 180f);
            float minimumDot = Mathf.Cos(clampedHalfAngle * Mathf.Deg2Rad);
            float maximumDistanceSquared = attackReach * attackReach;
            DiagnosticResourceNode bestTarget = null;

            DiagnosticResourceNode[] nodes =
                FindObjectsByType<DiagnosticResourceNode>(FindObjectsSortMode.None);

            foreach (DiagnosticResourceNode node in nodes)
            {
                if (!node.IsSpawned || node.IsDepleted)
                {
                    continue;
                }

                Vector3 toNode = Vector3.ProjectOnPlane(
                    node.transform.position - transform.position,
                    Vector3.up);

                float distanceSquared = toNode.sqrMagnitude;
                if (distanceSquared < 0.0001f ||
                    distanceSquared > maximumDistanceSquared ||
                    distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                if (Vector3.Dot(forward, toNode.normalized) < minimumDot)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                bestTarget = node;
            }

            return bestTarget;
        }

        public bool TryHarvestServer(DiagnosticResourceNode target)
        {
            if (!IsServer ||
                !IsSpawned ||
                health == null ||
                health.IsDead ||
                inventory == null ||
                target == null)
            {
                return false;
            }

            return target.TryHarvestServer(NetworkObject, inventory);
        }
    }
}
