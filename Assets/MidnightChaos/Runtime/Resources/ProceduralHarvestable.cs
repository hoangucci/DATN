using System.Collections.Generic;
using MidnightChaos.Inventory;
using UnityEngine;
using UnityEngine.AI;

namespace MidnightChaos.Resources
{
    [DisallowMultipleComponent]
    public sealed class ProceduralHarvestable : MonoBehaviour
    {
        private static readonly List<ProceduralHarvestable> ActiveEntries =
            new List<ProceduralHarvestable>();
        private static readonly Dictionary<string, List<ProceduralHarvestable>>
            Registry = new Dictionary<string, List<ProceduralHarvestable>>();

        private string stableKey;
        private int maximumHealth;
        private int remainingHealth;
        private VerticalSliceItemId dropItem;
        private int dropAmount;
        private bool depleted;
        private Renderer[] renderers;
        private Collider[] colliders;
        private NavMeshObstacle[] obstacles;

        public string StableKey => stableKey;
        public bool IsDepleted => depleted;
        public VerticalSliceItemId DropItem => dropItem;
        public int DropAmount => dropAmount;
        public Vector3 DropPosition => transform.position + Vector3.up * 0.6f;
        public static IReadOnlyList<ProceduralHarvestable> Active =>
            ActiveEntries;

        public void Initialize(
            string key,
            int health,
            VerticalSliceItemId configuredDrop,
            int configuredDropAmount)
        {
            stableKey = key;
            maximumHealth = Mathf.Max(1, health);
            remainingHealth = maximumHealth;
            dropItem = configuredDrop;
            dropAmount = Mathf.Max(1, configuredDropAmount);
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            obstacles = GetComponentsInChildren<NavMeshObstacle>(true);
            Register();
        }

        private void OnDestroy()
        {
            if (string.IsNullOrEmpty(stableKey) ||
                !Registry.TryGetValue(stableKey, out List<ProceduralHarvestable> entries))
            {
                return;
            }
            entries.Remove(this);
            ActiveEntries.Remove(this);
            if (entries.Count == 0)
            {
                Registry.Remove(stableKey);
            }
        }

        public bool TryDamage(int damage, out bool destroyed)
        {
            destroyed = false;
            if (depleted || damage <= 0)
            {
                return false;
            }
            remainingHealth = Mathf.Max(0, remainingHealth - damage);
            destroyed = remainingHealth == 0;
            Debug.Log(
                $"[Harvest] {stableKey}: {remainingHealth}/{maximumHealth} HP.");
            return true;
        }

        public void SetDepleted()
        {
            if (depleted)
            {
                return;
            }
            depleted = true;
            foreach (Renderer target in renderers)
            {
                if (target != null) target.enabled = false;
            }
            foreach (Collider target in colliders)
            {
                if (target != null) target.enabled = false;
            }
            foreach (NavMeshObstacle target in obstacles)
            {
                if (target != null) target.enabled = false;
            }
        }

        public static void SetDepletedByKey(string key)
        {
            if (string.IsNullOrEmpty(key) ||
                !Registry.TryGetValue(key, out List<ProceduralHarvestable> entries))
            {
                return;
            }
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                if (entries[index] == null)
                {
                    entries.RemoveAt(index);
                }
                else
                {
                    entries[index].SetDepleted();
                }
            }
        }

        private void Register()
        {
            if (string.IsNullOrEmpty(stableKey))
            {
                return;
            }
            if (!Registry.TryGetValue(stableKey, out List<ProceduralHarvestable> entries))
            {
                entries = new List<ProceduralHarvestable>();
                Registry.Add(stableKey, entries);
            }
            if (!entries.Contains(this))
            {
                entries.Add(this);
            }
            if (!ActiveEntries.Contains(this))
            {
                ActiveEntries.Add(this);
            }
        }
    }
}
