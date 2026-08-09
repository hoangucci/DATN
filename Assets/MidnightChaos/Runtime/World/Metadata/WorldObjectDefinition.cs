using System;
using UnityEngine;

namespace MidnightChaos.World
{
    [CreateAssetMenu(
        fileName = "WorldObjectDefinition",
        menuName = "Midnight Chaos/World/Object Definition")]
    public sealed class WorldObjectDefinition : ScriptableObject
    {
        [Tooltip("ID ổn định dùng cho deterministic layout, hash, save và network. Không đổi sau khi asset đã được dùng trong dữ liệu phát hành.")]
        [SerializeField] private string stableId = string.Empty;
        [Tooltip("Nhóm gameplay của object. Category trong catalog phải trùng giá trị này.")]
        [SerializeField] private WorldObjectCategory category;
        [Tooltip("Prefab được instantiate hoặc GPU-instance. Metadata không được tự thêm component vào prefab.")]
        [SerializeField] private GameObject prefab;
        [Tooltip("Ý nghĩa tĩnh của object. Trạng thái runtime như máu hoặc đã bị phá không đặt tại đây.")]
        [SerializeField] private WorldObjectFlags flags;

        public string StableId => stableId ?? string.Empty;
        public WorldObjectCategory Category => category;
        public GameObject Prefab => prefab;
        public WorldObjectFlags Flags => flags;

        public bool HasFlag(WorldObjectFlags flag)
        {
            return (flags & flag) == flag;
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(StableId))
            {
                error = $"Definition '{name}' has an empty Stable ID.";
                return false;
            }

            if (!string.Equals(StableId, StableId.Trim(), StringComparison.Ordinal))
            {
                error = $"Definition '{name}' Stable ID has leading or trailing whitespace.";
                return false;
            }

            if (prefab == null)
            {
                error = $"Definition '{name}' ({StableId}) has no prefab.";
                return false;
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void Configure(
            string configuredStableId,
            WorldObjectCategory configuredCategory,
            GameObject configuredPrefab,
            WorldObjectFlags configuredFlags)
        {
            stableId = configuredStableId;
            category = configuredCategory;
            prefab = configuredPrefab;
            flags = configuredFlags;
        }
#endif

        private void OnValidate()
        {
            if (!TryValidate(out string error))
            {
                Debug.LogError($"[World Metadata] {error}", this);
            }
        }
    }
}
