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

        [Header("Density Settings (Đa dạng màu sắc & Đồi núi hùng vĩ)")]
        [SerializeField, Range(0, 5000)] private int treeCount = 950;
        [SerializeField, Range(0, 3000)] private int rockCount = 500;
        [SerializeField, Range(0, 5000)] private int vegetationCount = 1200;

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
            // Tự động sinh map ngẫu nhiên khi vào chơi game (Runtime)
            if (Application.isPlaying)
            {
                EnsurePrefabsLoaded();
                GenerateMap();
            }
        }

        private void EnsurePrefabsLoaded()
        {
#if UNITY_EDITOR
            string bundleRoot = "Assets/Asset/StylizedNatureBundle";
            string[] guids = UnityEditor.AssetDatabase.FindAssets("StylizedNatureBundle t:Folder");
            if (guids.Length > 0)
            {
                bundleRoot = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            }

            string prefabsPath = bundleRoot + "/Prefabs";

            // Cưỡng chế nạp đầy đủ 100% tất cả mẫu Cây, Đá và 122+ mẫu Hoa, Bụi Cây
            if (treePrefabs == null || treePrefabs.Length < 10)
            {
                List<GameObject> allTrees = new List<GameObject>();
                allTrees.AddRange(LoadPrefabsInEditor($"{prefabsPath}/Trees"));
                allTrees.AddRange(LoadPrefabsInEditor($"{prefabsPath}/DeadTrees"));
                treePrefabs = allTrees.ToArray();
            }

            if (rockPrefabs == null || rockPrefabs.Length < 5)
            {
                List<GameObject> allRocks = new List<GameObject>();
                allRocks.AddRange(LoadPrefabsInEditor($"{prefabsPath}/Rocks"));
                allRocks.AddRange(LoadPrefabsInEditor($"{prefabsPath}/Rocks/noLODs"));
                rockPrefabs = allRocks.ToArray();
            }

            if (vegetationPrefabs == null || vegetationPrefabs.Length < 30)
            {
                List<GameObject> allVeg = new List<GameObject>();
                allVeg.AddRange(LoadPrefabsInEditor($"{prefabsPath}/GrassFlower"));
                allVeg.AddRange(LoadPrefabsInEditor($"{prefabsPath}/Vegetation"));
                vegetationPrefabs = allVeg.ToArray();
            }
#endif
        }

#if UNITY_EDITOR
        private GameObject[] LoadPrefabsInEditor(string path)
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(path)) return new GameObject[0];
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { path });
            List<GameObject> list = new List<GameObject>();
            foreach (string g in guids)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                // Ưu tiên nạp các mẫu Prefab noLODs để có màu sắc đẹp và không bị lặp lại cấp độ LOD
                if (assetPath.Contains("noLODs") || (!assetPath.Contains("LOD1") && !assetPath.Contains("LOD2")))
                {
                    GameObject obj = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (obj != null && !list.Contains(obj)) list.Add(obj);
                }
            }
            return list.ToArray();
        }
#endif

        private int currentActiveSeed;

        [ContextMenu("Generate Map")]
        public void GenerateMap()
        {
            EnsurePrefabsLoaded();
            ClearMap();

            if (generatedContainer == null)
            {
                GameObject containerObj = new GameObject("GeneratedEnvironment");
                containerObj.transform.SetParent(transform, false);
                generatedContainer = containerObj.transform;
            }

            currentActiveSeed = mapSeed == 0 ? UnityEngine.Random.Range(1, 999999) : mapSeed;
            System.Random rng = new System.Random(currentActiveSeed);
            Debug.Log($"[Stylized Map Generator] Đang sinh địa hình Đảo & rải đều vật thể với Seed: {currentActiveSeed}");

            // 0. Biến đổi địa hình đồi núi & bờ biển Hòn đảo ngẫu nhiên mới 100% theo Seed
            GenerateProceduralTerrainMesh(currentActiveSeed);

            // Đồng bộ hiệu ứng Ánh sáng nắng ấm & Sương mù Atmospheric Fog núi cao chuẩn 100% DemoScene_07
            ApplyDemoSceneLighting();

            // Cập nhật lại collider vật lý để raycast chính xác
            Physics.SyncTransforms();

            List<Vector3> placedPositions = new List<Vector3>();
            List<Vector3> treePositions = new List<Vector3>();
            List<Vector3> rockPositions = new List<Vector3>();

            // 1. Rải Cây (Trees)
            if (treePrefabs != null && treePrefabs.Length > 0)
            {
                SpawnCategoryGrid(rng, treePrefabs, treeCount, 3.5f, 0.85f, 1.25f, placedPositions, true, treePositions);
            }

            // 2. Rải Đá (Rocks)
            if (rockPrefabs != null && rockPrefabs.Length > 0)
            {
                SpawnCategoryGrid(rng, rockPrefabs, rockCount, 2.0f, 0.7f, 1.4f, placedPositions, false, rockPositions);
            }

            // 3. Rải Cụm Hoa, Bụi Cây & Cỏ ôm sát theo Gốc Cây và Chân Đá chuẩn 100% DemoScene_01
            if (vegetationPrefabs != null && vegetationPrefabs.Length > 0)
            {
                SpawnVegetationClusters(rng, vegetationPrefabs, treePositions, rockPositions, placedPositions);
                // Rải phần cỏ & hoa còn lại phủ đều toàn bộ thung lũng
                SpawnCategoryGrid(rng, vegetationPrefabs, vegetationCount, 0.6f, 0.8f, 1.3f, placedPositions, false, null);
            }

            Debug.Log($"[Stylized Map Generator] Hoàn thành rải phủ đều map với {generatedContainer.childCount} vật thể.");
        }

        private void SpawnVegetationClusters(
            System.Random rng,
            GameObject[] prefabs,
            List<Vector3> treePositions,
            List<Vector3> rockPositions,
            List<Vector3> placedPositions)
        {
            if (prefabs == null || prefabs.Length == 0) return;

            // 1. Sinh 3 - 6 hoa/bụi cây ôm sát gốc mỗi Cây (như DemoScene_01)
            foreach (Vector3 treePos in treePositions)
            {
                int count = rng.Next(3, 7);
                for (int i = 0; i < count; i++)
                {
                    float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                    float dist = Mathf.Lerp(1.2f, 4.0f, (float)rng.NextDouble());
                    Vector3 pos = treePos + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                    if (Vector3.Distance(Vector3.zero, pos) < safeCenterRadius) continue;
                    if (IsTooClose(pos, placedPositions, 0.4f)) continue;

                    pos.y = GetGroundHeight(pos);
                    if (pos.y < 0.6f) continue;

                    GameObject selectedPrefab = prefabs[rng.Next(prefabs.Length)];
                    if (selectedPrefab == null) continue;

                    Quaternion rotation = Quaternion.Euler((float)(rng.NextDouble() * 10.0 - 5.0), (float)(rng.NextDouble() * 360.0), (float)(rng.NextDouble() * 10.0 - 5.0));
                    float scaleMult = Mathf.Lerp(0.7f, 1.3f, (float)rng.NextDouble());

                    GameObject instance = Instantiate(selectedPrefab, pos, rotation, generatedContainer);
                    instance.transform.localScale = selectedPrefab.transform.localScale * scaleMult;

                    placedPositions.Add(pos);
                }
            }

            // 2. Sinh 2 - 4 hoa/bụi cây ôm sát chân mỗi Khối Đá
            foreach (Vector3 rockPos in rockPositions)
            {
                int count = rng.Next(2, 5);
                for (int i = 0; i < count; i++)
                {
                    float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                    float dist = Mathf.Lerp(0.8f, 2.5f, (float)rng.NextDouble());
                    Vector3 pos = rockPos + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                    if (Vector3.Distance(Vector3.zero, pos) < safeCenterRadius) continue;
                    if (IsTooClose(pos, placedPositions, 0.4f)) continue;

                    pos.y = GetGroundHeight(pos);
                    if (pos.y < 0.6f) continue;

                    GameObject selectedPrefab = prefabs[rng.Next(prefabs.Length)];
                    if (selectedPrefab == null) continue;

                    Quaternion rotation = Quaternion.Euler((float)(rng.NextDouble() * 10.0 - 5.0), (float)(rng.NextDouble() * 360.0), (float)(rng.NextDouble() * 10.0 - 5.0));
                    float scaleMult = Mathf.Lerp(0.7f, 1.3f, (float)rng.NextDouble());

                    GameObject instance = Instantiate(selectedPrefab, pos, rotation, generatedContainer);
                    instance.transform.localScale = selectedPrefab.transform.localScale * scaleMult;

                    placedPositions.Add(pos);
                }
            }
        }

        [ContextMenu("Clear Map")]
        public void ClearMap()
        {
            if (generatedContainer == null) return;

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
            bool alignUpright,
            List<Vector3> outPositions = null)
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
                if (outPositions != null) outPositions.Add(pos);
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

        private void ApplyDemoSceneLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.035f, 0.133f, 0.255f);
            RenderSettings.ambientEquatorColor = new Color(0.314f, 0.377f, 0.300f);
            RenderSettings.ambientGroundColor = new Color(0.185f, 0.254f, 0.128f);

            // Cấu hình Sương Mù Cyan Atmospheric Fog mộng mơ phủ xa chuẩn 100% DemoScene_01
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.485f, 0.855f, 0.943f);
            RenderSettings.fogStartDistance = 15f;
            RenderSettings.fogEndDistance = 220f;

            Light mainLight = FindFirstObjectByType<Light>();
            if (mainLight != null && mainLight.type == LightType.Directional)
            {
                mainLight.color = new Color(1.0f, 0.93f, 0.82f);
                mainLight.intensity = 1.3f;
                mainLight.shadows = LightShadows.Soft;
                mainLight.transform.rotation = Quaternion.Euler(45f, 130f, 0f);
            }
        }

        private float CalculateHeightAt(float xPos, float zPos, int seed)
        {
            float distFromCenter = Mathf.Sqrt(xPos * xPos + zPos * zPos);

            System.Random rng = new System.Random(seed);
            float seedX = (float)(rng.NextDouble() * 10000.0 + 1000.0);
            float seedZ = (float)(rng.NextDouble() * 10000.0 + 1000.0);

            // Biến thiên đường viền bờ biển hòn đảo
            float coastlineNoise = (Mathf.PerlinNoise((xPos + seedX + 4000f) * 0.003f, (zPos + seedZ + 4000f) * 0.003f) - 0.5f) * 200f;
            float maxIslandRadius = 550f + coastlineNoise;

            float islandFalloff = 1.0f - Mathf.Clamp01((distFromCenter - maxIslandRadius * 0.55f) / (maxIslandRadius * 0.45f));
            islandFalloff = islandFalloff * islandFalloff * (3f - 2f * islandFalloff);

            // Địa hình đồi núi & bình nguyên tự nhiên mượt mà (không bị hố ở giữa)
            float mountainPeaks = Mathf.PerlinNoise((xPos + seedX) * 0.0025f, (zPos + seedZ) * 0.0025f) * 22.0f;
            float rollingHills = Mathf.PerlinNoise((xPos + seedX + 500f) * 0.012f, (zPos + seedZ + 500f) * 0.012f) * 6.0f + 4.0f;

            float baseHills = mountainPeaks + rollingHills;

            float height = (baseHills * islandFalloff) - ((1.0f - islandFalloff) * 15.0f);

            return height;
        }

        private float GetGroundHeight(Vector3 point)
        {
            // Tính toán độ cao chuẩn 100% khớp với bề mặt địa hình đã sinh
            return CalculateHeightAt(point.x, point.z, currentActiveSeed);
        }

        private void GenerateProceduralTerrainMesh(int seed)
        {
            MeshFilter filter = null;
            MeshCollider collider = null;

            GameObject islandGround = GameObject.Find("IslandGround");
            if (islandGround == null) islandGround = GameObject.Find("Ground");

            if (islandGround != null)
            {
                filter = islandGround.GetComponent<MeshFilter>();
                collider = islandGround.GetComponent<MeshCollider>();
            }

            if (filter == null) return;

            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !mesh.isReadable || Application.isPlaying)
            {
                mesh = new Mesh();
                mesh.name = "ProceduralDynamicIslandMesh";
            }

            float width = 1400f;
            float length = 1400f;
            int segmentsX = 140;
            int segmentsZ = 140;

            int vertCount = (segmentsX + 1) * (segmentsZ + 1);
            Vector3[] vertices = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] triangles = new int[segmentsX * segmentsZ * 6];

            for (int z = 0; z <= segmentsZ; z++)
            {
                for (int x = 0; x <= segmentsX; x++)
                {
                    int index = z * (segmentsX + 1) + x;
                    float xPos = ((float)x / segmentsX - 0.5f) * width;
                    float zPos = ((float)z / segmentsZ - 0.5f) * length;

                    float height = CalculateHeightAt(xPos, zPos, seed);

                    vertices[index] = new Vector3(xPos, height, zPos);
                    uvs[index] = new Vector2((float)x / segmentsX, (float)z / segmentsZ);
                }
            }

            int triIndex = 0;
            for (int z = 0; z < segmentsZ; z++)
            {
                for (int x = 0; x < segmentsX; x++)
                {
                    int current = z * (segmentsX + 1) + x;
                    int next = current + segmentsX + 1;

                    triangles[triIndex++] = current;
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = current + 1;

                    triangles[triIndex++] = current + 1;
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = next + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            filter.sharedMesh = mesh;
            if (collider != null)
            {
                collider.sharedMesh = null;
                collider.sharedMesh = mesh;
            }

            // Đảm bảo Material mặt đất hiển thị họa tiết T_SNB_Ground_BaseColor_01 và Normal Map chuẩn đẹp, sắc nét
            MeshRenderer renderer = islandGround.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                Material mat = renderer.sharedMaterial;
                if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", new Vector2(70f, 70f));
                if (mat.HasProperty("_MainTex")) mat.SetTextureScale("_MainTex", new Vector2(70f, 70f));
                if (mat.HasProperty("_BumpMap")) mat.SetTextureScale("_BumpMap", new Vector2(70f, 70f));
            }
        }
    }
}
