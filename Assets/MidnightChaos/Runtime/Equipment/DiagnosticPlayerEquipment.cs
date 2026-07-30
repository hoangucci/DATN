using System;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Equipment
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DiagnosticPlayerEquipment : NetworkBehaviour
    {
        private const string SwordVisualName = "SwordVisual";

        private NetworkVariable<bool> hasSword =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private GameObject swordVisual;

        public event Action<bool, bool> SwordStateChanged;

        public bool HasSword => hasSword.Value;

        private void Awake()
        {
            Transform swordTransform = transform.Find(SwordVisualName);
            swordVisual = swordTransform != null
                ? swordTransform.gameObject
                : null;

            RefreshSwordVisual();
        }

        public override void OnNetworkSpawn()
        {
            hasSword.OnValueChanged += HandleSwordStateChanged;

            if (IsServer)
            {
                hasSword.Value = false;
            }

            RefreshSwordVisual();
        }

        public override void OnNetworkDespawn()
        {
            hasSword.OnValueChanged -= HandleSwordStateChanged;
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

        private void RefreshSwordVisual()
        {
            if (swordVisual != null)
            {
                swordVisual.SetActive(hasSword.Value);
            }
        }
    }
}
