using System;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkHealth : NetworkBehaviour
    {
        [SerializeField] private string displayName = "Player";
        [SerializeField, Min(1)] private int initialMaxHealth = 100;

        private NetworkVariable<int> replicatedMaxHealth =
            new NetworkVariable<int>(
                100,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkVariable<int> currentHealth =
            new NetworkVariable<int>(
                100,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        public event Action<int, int> HealthChanged;
        public event Action<NetworkHealth> DeathCommittedServer;

        public string DisplayName => displayName;
        public int MaxHealth => replicatedMaxHealth.Value;
        public int CurrentHealth => currentHealth.Value;
        public bool IsDead => CurrentHealth <= 0;

        public void ConfigureForDiagnostics(
            int configuredMaxHealth,
            string configuredDisplayName)
        {
            initialMaxHealth = Mathf.Max(1, configuredMaxHealth);
            displayName = string.IsNullOrWhiteSpace(configuredDisplayName)
                ? "Damageable"
                : configuredDisplayName.Trim();
        }

        public override void OnNetworkSpawn()
        {
            currentHealth.OnValueChanged += HandleHealthChanged;

            if (IsServer)
            {
                replicatedMaxHealth.Value = initialMaxHealth;
                currentHealth.Value = initialMaxHealth;
            }
        }

        public override void OnNetworkDespawn()
        {
            currentHealth.OnValueChanged -= HandleHealthChanged;
        }

        public bool TryApplyDamageServer(int damage, NetworkObject attacker)
        {
            if (!IsServer || damage <= 0 || IsDead)
            {
                return false;
            }

            int previousHealth = currentHealth.Value;
            currentHealth.Value = Mathf.Max(0, previousHealth - damage);
            bool committedDeath =
                previousHealth > 0 && currentHealth.Value <= 0;

            ulong attackerId = attacker != null
                ? attacker.OwnerClientId
                : Unity.Netcode.NetworkManager.ServerClientId;
            string attackerName = attacker != null
                ? attacker.name
                : "Server";

            Debug.Log(
                $"[Combat] {attackerName} (owner {attackerId}) dealt " +
                $"{damage} damage to {DisplayName} {NetworkObjectId}. " +
                $"HP {previousHealth} -> {currentHealth.Value}.");

            if (committedDeath)
            {
                DeathCommittedServer?.Invoke(this);
            }

            return true;
        }

        public bool TrySetMaxHealthPreserveRatioServer(int newMaxHealth)
        {
            if (!IsServer || !IsSpawned || IsDead || newMaxHealth <= 0)
            {
                return false;
            }

            int oldMaxHealth = Mathf.Max(1, replicatedMaxHealth.Value);
            float healthRatio =
                Mathf.Clamp01((float)currentHealth.Value / oldMaxHealth);
            int adjustedHealth = Mathf.Clamp(
                Mathf.RoundToInt(newMaxHealth * healthRatio),
                1,
                newMaxHealth);

            replicatedMaxHealth.Value = newMaxHealth;
            currentHealth.Value = adjustedHealth;
            return true;
        }

        private void HandleHealthChanged(int previousHealth, int newHealth)
        {
            HealthChanged?.Invoke(previousHealth, newHealth);
        }
    }
}
