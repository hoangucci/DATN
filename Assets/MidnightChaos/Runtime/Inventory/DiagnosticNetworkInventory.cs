using System;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Inventory
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DiagnosticNetworkInventory : NetworkBehaviour
    {
        [Header("Gate D - Host Authoritative Inventory")]
        [SerializeField, Min(1)] private int maximumWood = 999;

        private NetworkVariable<int> wood =
            new NetworkVariable<int>(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        public event Action<int, int> WoodChanged;

        public int Wood => wood.Value;

        public override void OnNetworkSpawn()
        {
            wood.OnValueChanged += HandleWoodChanged;

            if (IsServer)
            {
                wood.Value = 0;
            }
        }

        public override void OnNetworkDespawn()
        {
            wood.OnValueChanged -= HandleWoodChanged;
        }

        public bool TryAddWoodServer(int amount)
        {
            if (!IsServer || amount <= 0 || wood.Value > maximumWood - amount)
            {
                return false;
            }

            wood.Value += amount;
            return true;
        }

        public bool TrySpendWoodServer(int amount)
        {
            if (!IsServer || amount <= 0 || wood.Value < amount)
            {
                return false;
            }

            wood.Value -= amount;
            return true;
        }

        private void HandleWoodChanged(int previous, int current)
        {
            WoodChanged?.Invoke(previous, current);
        }
    }
}
