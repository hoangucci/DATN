using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidnightChaos.Resources
{
    [DisallowMultipleComponent]
    public sealed class StylizedNatureMapGenerator : MonoBehaviour
    {
        [Header("Seed Settings")]
        [Tooltip("0 = Sinh seed ngẫu nhiên mới mỗi lần bấm Generate")]
        [SerializeField] private int mapSeed = 0;

        [Header("Map Dimensions")]
        [SerializeField, Min(100f)] private float mapSize = 1350f;
        [SerializeField, Min(0f)] private float safeCenterRadius = 10f;

        [Header("Density Settings")]
        [SerializeField, Range(0, 8000)] private int treeCount = 3000;
        [SerializeField, Range(0, 5000)] private int rockCount = 1500;
        [SerializeField, Range(0, 8000)] private int vegetationCount = 6000;

        [Header("Prefab Collections (StylizedNatureBundle)")]
        [SerializeField] private GameObject[] treePrefabs = new GameObject[0];
        [SerializeField] private GameObject[] rockPrefabs = new GameObject[0];
        [SerializeField] private GameObject[] vegetationPrefabs = new GameObject[0];

        [Header("Container")]
        [SerializeField] private Transform generatedContainer;

        public void ConfigurePrefabs(GameObject[] trees, GameObject[] rocks, GameObject[] vegetation)
        {
            treePrefabs = trees ?? new GameObject[0];
            rockPrefabs = rocks ?? new GameObject[0];
            vegetationPrefabs = vegetation ?? new GameObject[0];
        }

        private void Start()
        {
            // Tự động sinh map khi Play nếu chưa có vật thể nào
            if (generatedContainer == null || generatedContainer.childCount == 0)
            {
                GenerateMap();
            }
        }

        [ContextMenu("Generate Map")]
        public void GenerateMap()
        {
            ClearMap();

            if (generatedContainer == null)
            {
                GameObject containerObj = new GameObject("GeneratedEnvironment");
                containerObj.transform.SetParent(transform, false);
                generatedContainer = containerObj.transform;
            }

            int actualSeed = mapSeed == 0 ? UnityEngine.Random.Range(1, 999999) : mapSeed;
            System.Random rng = new System.Random(actualSeed);
            Debug.Log($"[Stylized Map Generator] Đang rải đều toàn bộ map rộng {mapSize}m với Seed: {actualSeed}");

            List<Vector3> placedPositions = new List<Vector3>();

            // 1. Rải Cây (Trees) đồng đều
            if (treePrefabs != null && treePrefabs.Length > 0)
            {
                SpawnCategoryGrid(rng, treePrefabs, treeCount, 3.5f, 0.85f, 1.25f, placedPositions, true);
            }

            // 2. Rải Đá (Rocks) đồng đều
            if (rockPrefabs != null && rockPrefabs.Length > 0)
            {
                SpawnCategoryGrid(rng, rockPrefabs, rockCount, 2.0f, 0.7f, 1.4f, placedPositions, false);
            }

            // 3. Rải Cỏ / Hoa (Vegetation) đồng đều
            if (vegetationPrefabs != null && vegetationPrefabs.Length > 0)
            {
                SpawnCategoryGrid(rng, vegetationPrefabs, vegetationCount, 0.8f, 0.9f, 1.3f, placedPositions, false);
            }

            Debug.Log($"[Stylized Map Generator] Hoàn thành rải phủ đều map với {generatedContainer.childCount} vật thể.");
        }

        [ContextMenu("Clear Map")]
        public void ClearMap()
        {
            if (generatedContainer == null) return;

            // Xóa sạch các object con trong Container (Hỗ trợ cả Editor và Runtime)
            int childCount = generatedContainer.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                Transform child = generatedContainer.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void SpawnCategoryGrid(
            System.Random rng,
            GameObject[] prefabs,
            int targetCount,
            float minSpacing,
            float minScale,
            float maxScale,
            List<Vector3> placedPositions,
            bool alignUpright)
        {
            int spawned = 0;
            int attempts = 0;
            int maxAttempts = targetCount * 25;

            float halfSize = mapSize * 0.5f;
            int gridSide = Mathf.CeilToInt(Mathf.Sqrt(targetCount));
            float cellSize = mapSize / gridSide;

            int cellIndex = 0;

            while (spawned < targetCount && attempts < maxAttempts)
            {
                attempts++;

                // Phân bổ đều theo các ô lưới (Grid Cell Stratified Sampling)
                int gx = cellIndex % gridSide;
                int gz = cellIndex / gridSide;
                cellIndex = (cellIndex + 1) % (gridSide * gridSide);

                float minX = -halfSize + (gx * cellSize);
                float maxX = minX + cellSize;
                float minZ = -halfSize + (gz * cellSize);
                float maxZ = minZ + cellSize;

                float rx = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());
                float rz = Mathf.Lerp(minZ, maxZ, (float)rng.NextDouble());
                Vector3 pos = new Vector3(rx, 0f, rz);

                if (Vector3.Distance(Vector3.zero, pos) < safeCenterRadius)
                {
                    continue;
                }

                if (IsTooClose(pos, placedPositions, minSpacing))
                {
                    continue;
                }

                pos.y = GetGroundHeight(pos);

                // Bỏ qua nếu vị trí thuộc mặt biển hoặc bãi biển dưới nước (Y < 0.6m)
                if (pos.y < 0.6f)
                {
                    continue;
                }

                GameObject selectedPrefab = prefabs[rng.Next(prefabs.Length)];
                if (selectedPrefab == null) continue;

                Quaternion rotation;
                if (alignUpright)
                {
                    rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                }
                else
                {
                    rotation = Quaternion.Euler(
                        (float)(rng.NextDouble() * 15.0 - 7.5),
                        (float)(rng.NextDouble() * 360.0),
                        (float)(rng.NextDouble() * 15.0 - 7.5));
                }

                float scaleMult = Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());

                GameObject instance = Instantiate(selectedPrefab, pos, rotation, generatedContainer);
                instance.transform.localScale = selectedPrefab.transform.localScale * scaleMult;

                placedPositions.Add(pos);
                spawned++;
            }
        }

        private Vector3 GetRandomPointInCircle(System.Random rng, float radius)
        {
            double r = radius * Math.Sqrt(rng.NextDouble());
            double theta = rng.NextDouble() * 2.0 * Math.PI;

            float x = (float)(r * Math.Cos(theta));
            float z = (float)(r * Math.Sin(theta));

            return new Vector3(x, 0f, z);
        }

        private bool IsTooClose(Vector3 candidate, List<Vector3> existing, float minDistance)
        {
            float sqrMinDist = minDistance * minDistance;
            for (int i = 0; i < existing.Count; i++)
            {
                if ((existing[i] - candidate).sqrMagnitude < sqrMinDist)
                {
                    return true;
                }
            }
            return false;
        }

        private float GetGroundHeight(Vector3 point)
        {
            Vector3 rayStart = new Vector3(point.x, 150f, point.z);
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 300f))
            {
                return hit.point.y;
            }
            return 0f;
        }
    }
}
