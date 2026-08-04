using UnityEngine;

namespace MidnightChaos.Runtime
{
    [CreateAssetMenu(fileName = "MapGeneratorConfig", menuName = "Midnight Chaos/Map Generator Config", order = 1)]
    public class MapGeneratorConfigSO : ScriptableObject
    {
        [Header("Seed Settings")]
        [Tooltip("0 = Sinh seed ngẫu nhiên mới mỗi lần bấm Generate")]
        public int mapSeed = 0;

        [Header("Map Dimensions")]
        [Min(100f)] public float mapSize = 1350f;
        [Min(0f)] public float safeCenterRadius = 10f;

        [Header("Terrain Height Settings (Độ cao địa hình)")]
        [Range(5f, 60f)] public float mountainMaxHeight = 22.0f;
        [Range(1f, 20f)] public float hillsMaxHeight = 6.0f;
        [Range(0f, 10f)] public float baseHeightOffset = 4.0f;
        [Range(300f, 1000f)] public float islandRadius = 550f;

        [Header("Density Settings (Mật độ vật thể)")]
        [Range(0, 5000)] public int treeCount = 950;
        [Range(0, 3000)] public int rockCount = 500;
        [Range(0, 8000)] public int vegetationCount = 1200;

        [Header("Prefab Collections (StylizedNatureBundle)")]
        public GameObject[] treePrefabs = new GameObject[0];
        public GameObject[] rockPrefabs = new GameObject[0];
        public GameObject[] vegetationPrefabs = new GameObject[0];

        [Header("Lighting & Fog Settings (Sương mù & Ánh sáng)")]
        public bool enableDemoLighting = true;
        public Color fogColor = new Color(0.485f, 0.855f, 0.943f, 1f);
        public float fogStartDistance = 15f;
        public float fogEndDistance = 220f;
        public Color sunColor = new Color(1.0f, 0.93f, 0.82f, 1f);
        public float sunIntensity = 1.3f;

#if UNITY_EDITOR
        public event System.Action OnConfigChanged;

        private void OnValidate()
        {
            OnConfigChanged?.Invoke();
        }
#endif
    }
}
