using Unity.Netcode.Components;
using UnityEngine;

namespace MidnightChaos.Networking
{
    /// <summary>
    /// Owner-authoritative movement with a deliberately small server-side sanity check.
    /// This is a bootstrap guard, not production anti-cheat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ValidatedOwnerNetworkTransform : NetworkTransform
    {
        [SerializeField, Min(1f)] private float maximumAcceptedSpeed = 16f;
        [SerializeField, Min(0f)] private float positionTolerance = 0.35f;

        private Vector3 lastAcceptedPosition;
        private double lastAcceptedTime;
        private bool hasAcceptedState;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer)
            {
                return;
            }

            lastAcceptedPosition = transform.position;
            lastAcceptedTime = Time.realtimeSinceStartupAsDouble;
            hasAcceptedState = true;
            OnClientRequestChange = ValidateOwnerRequest;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                OnClientRequestChange = null;
            }

            base.OnNetworkDespawn();
        }

        private (Vector3 pos, Quaternion rotOut, Vector3 scale) ValidateOwnerRequest(
            Vector3 requestedPosition,
            Quaternion requestedRotation,
            Vector3 requestedScale)
        {
            double now = Time.realtimeSinceStartupAsDouble;

            if (!hasAcceptedState)
            {
                lastAcceptedPosition = requestedPosition;
                lastAcceptedTime = now;
                hasAcceptedState = true;
                return (requestedPosition, requestedRotation, Vector3.one);
            }

            float elapsed = Mathf.Clamp((float)(now - lastAcceptedTime), 1f / 120f, 0.25f);
            float allowedDistance = maximumAcceptedSpeed * elapsed + positionTolerance;
            Vector3 delta = requestedPosition - lastAcceptedPosition;

            if (delta.sqrMagnitude > allowedDistance * allowedDistance)
            {
                requestedPosition = lastAcceptedPosition + delta.normalized * allowedDistance;
            }

            lastAcceptedPosition = requestedPosition;
            lastAcceptedTime = now;

            // Player scale is data, not client-controlled runtime state.
            return (requestedPosition, requestedRotation, Vector3.one);
        }
    }
}
