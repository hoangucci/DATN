using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidnightChaos.Resources
{
    [DisallowMultipleComponent]
    public sealed class StylizedNatureMapGenerator : MonoBehaviour
    {
        [Header("ScriptableObject Configuration")]
        [Tooltip("Kéo thả file ScriptableObject cấu hình thông số độ cao địa hình, mật độ vật thể & sương mù vào đây")]
        [SerializeField] private MidnightChaos.Runtime.MapGeneratorConfigSO mapConfig;

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

        // Getters linh hoạt ưu tiên đọc từ ScriptableObject nếu được gán
        public int MapSeedVal => mapConfig != null ? mapConfig.mapSeed : mapSeed;
        public float MapSizeVal => mapConfig != null ? mapConfig.mapSize : mapSize;
        public float SafeCenterRadiusVal => mapConfig != null ? mapConfig.safeCenterRadius : safeCenterRadius;
        public float MountainMaxHeightVal => mapConfig != null ? mapConfig.mountainMaxHeight : 22.0f;
        public float HillsMaxHeightVal => mapConfig != null ? mapConfig.hillsMaxHeight : 6.0f;
        public float BaseHeightOffsetVal => mapConfig != null ? mapConfig.baseHeightOffset : 4.0f;
        public float IslandRadiusVal => mapConfig != null ? mapConfig.islandRadius : 550f;

        public int TreeCountVal => mapConfig != null ? mapConfig.treeCount : treeCount;
        public int RockCountVal => mapConfig != null ? mapConfig.rockCount : rockCount;
        public int VegetationCountVal => mapConfig != null ? mapConfig.vegetationCount : vegetationCount;

        public void ConfigurePrefabs(GameObject[] trees, GameObject[] rocks, GameObject[] vegetation)
        {
            treePrefabs = trees ?? new GameObject[0];
            rockPrefabs = rocks ?? new GameObject[0];
            vegetationPrefabs = vegetation ?? new GameObject[0];
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (mapConfig != null)
            {
                mapConfig.OnConfigChanged -= HandleConfigChanged;
                mapConfig.OnConfigChanged += HandleConfigChanged;
            }
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (mapConfig != null)
            {
                mapConfig.OnConfigChanged -= HandleConfigChanged;
            }
#endif
        }

        private void HandleConfigChanged()
        {
            if (!Application.isPlaying)
            {
                EnsurePrefabsLoaded();
                GenerateMap();
            }
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
            // HashSet thay List để tránh Contains() O(n) khi kiểm tra duplicate
            var set = new HashSet<GameObject>();
            foreach (string g in guids)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                if (assetPath.Contains("noLODs") || (!assetPath.Contains("LOD1") && !assetPath.Contains("LOD2")))
                {
                    GameObject obj = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (obj != null) set.Add(obj);
                }
            }
            var result = new GameObject[set.Count];
            set.CopyTo(result);
            return result;
        }
#endif

        // --- Seed & cached offsets ---
        private int currentActiveSeed;

        // FIX BUG-02: Cache seedX/seedZ một lần theo seed — không tạo System.Random 19,881 lần nữa
        private float _cachedSeedX;
        private float _cachedSeedZ;

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

            currentActiveSeed = MapSeedVal == 0 ? UnityEngine.Random.Range(1, 999999) : MapSeedVal;

            // FIX BUG-02: Tính seedX/seedZ 1 lần duy nhất cho toàn bộ pipeline sinh map
            var seedRng = new System.Random(currentActiveSeed);
            _cachedSeedX = (float)(seedRng.NextDouble() * 10000.0 + 1000.0);
            _cachedSeedZ = (float)(seedRng.NextDouble() * 10000.0 + 1000.0);

            System.Random rng = new System.Random(currentActiveSeed);
            Debug.Log($"[Stylized Map Generator] Đang sinh địa hình Đảo & rải đều vật thể với Seed: {currentActiveSeed}");

            // 0. Biến đổi địa hình đồi núi & bờ biển Hòn đảo ngẫu nhiên mới 100% theo Seed
            GenerateProceduralTerrainMesh(currentActiveSeed);

            // Đồng bộ hiệu ứng Ánh sáng nắng ấm & Sương mù Atmospheric Fog
            ApplyDemoSceneLighting();

            // Cập nhật lại collider vật lý để raycast chính xác
            Physics.SyncTransforms();

            // FIX BUG-03: SpatialHash O(1) amortized thay List<Vector3> O(n²)
            SpatialHash placedHash = new SpatialHash(2.5f);
            List<Vector3> treePositions = new List<Vector3>();
            List<Vector3> rockPositions = new List<Vector3>();

            // 1. Rải Cây (Trees)
            if (treePrefabs != null && treePrefabs.Length > 0)
            {
                SpawnCategoryGrid(rng, treePrefabs, TreeCountVal, 3.5f, 0.85f, 1.25f, placedHash, true, treePositions);
            }

            // 2. Rải Đá (Rocks)
            if (rockPrefabs != null && rockPrefabs.Length > 0)
            {
                SpawnCategoryGrid(rng, rockPrefabs, RockCountVal, 2.0f, 0.7f, 1.4f, placedHash, false, rockPositions);
            }

            // 3. Rải Cụm Hoa, Bụi Cây & Cỏ ôm sát theo Gốc Cây và Chân Đá
            if (vegetationPrefabs != null && vegetationPrefabs.Length > 0)
            {
                SpawnVegetationClusters(rng, vegetationPrefabs, treePositions, rockPositions, placedHash);
                // Rải phần cỏ & hoa còn lại phủ đều toàn bộ thung lũng
                SpawnCategoryGrid(rng, vegetationPrefabs, VegetationCountVal, 0.6f, 0.8f, 1.3f, placedHash, false, null);
            }

            Debug.Log($"[Stylized Map Generator] Hoàn thành rải phủ đều map với {generatedContainer.childCount} vật thể.");
        }

        private void SpawnVegetationClusters(
            System.Random rng,
            GameObject[] prefabs,
            List<Vector3> treePositions,
            List<Vector3> rockPositions,
            SpatialHash placedHash)
        {
            if (prefabs == null || prefabs.Length == 0) return;

            // FIX BUG-05: Giới hạn tổng cluster items — tránh OutOfMemory trên cấu hình yếu
            int maxClusterItems = TreeCountVal * 4 + RockCountVal * 3;
            int clusterSpawned = 0;

            // 1. Sinh 3 - 6 hoa/bụi cây ôm sát gốc mỗi Cây
            foreach (Vector3 treePos in treePositions)
            {
                if (clusterSpawned >= maxClusterItems) break;
                int count = rng.Next(3, 7);
                for (int i = 0; i < count; i++)
                {
                    if (clusterSpawned >= maxClusterItems) break;
                    float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                    float dist = Mathf.Lerp(1.2f, 4.0f, (float)rng.NextDouble());
                    Vector3 pos = treePos + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                    if (Vector3.Distance(Vector3.zero, pos) < SafeCenterRadiusVal) continue;
                    if (placedHash.HasNearby(pos, 0.4f)) continue;

                    pos.y = GetGroundHeight(pos);
                    if (pos.y < 0.6f) continue;

                    GameObject selectedPrefab = prefabs[rng.Next(prefabs.Length)];
                    if (selectedPrefab == null) continue;

                    Quaternion rotation = Quaternion.Euler(
                        (float)(rng.NextDouble() * 10.0 - 5.0),
                        (float)(rng.NextDouble() * 360.0),
                        (float)(rng.NextDouble() * 10.0 - 5.0));
                    float scaleMult = Mathf.Lerp(0.7f, 1.3f, (float)rng.NextDouble());

                    GameObject instance = Instantiate(selectedPrefab, pos, rotation, generatedContainer);
                    instance.transform.localScale = selectedPrefab.transform.localScale * scaleMult;

                    placedHash.Add(pos);
                    clusterSpawned++;
                }
            }

            // 2. Sinh 2 - 4 hoa/bụi cây ôm sát chân mỗi Khối Đá
            foreach (Vector3 rockPos in rockPositions)
            {
                if (clusterSpawned >= maxClusterItems) break;
                int count = rng.Next(2, 5);
                for (int i = 0; i < count; i++)
                {
                    if (clusterSpawned >= maxClusterItems) break;
                    float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                    float dist = Mathf.Lerp(0.8f, 2.5f, (float)rng.NextDouble());
                    Vector3 pos = rockPos + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                    if (Vector3.Distance(Vector3.zero, pos) < SafeCenterRadiusVal) continue;
                    if (placedHash.HasNearby(pos, 0.4f)) continue;

                    pos.y = GetGroundHeight(pos);
                    if (pos.y < 0.6f) continue;

                    GameObject selectedPrefab = prefabs[rng.Next(prefabs.Length)];
                    if (selectedPrefab == null) continue;

                    Quaternion rotation = Quaternion.Euler(
                        (float)(rng.NextDouble() * 10.0 - 5.0),
                        (float)(rng.NextDouble() * 360.0),
                        (float)(rng.NextDouble() * 10.0 - 5.0));
                    float scaleMult = Mathf.Lerp(0.7f, 1.3f, (float)rng.NextDouble());

                    GameObject instance = Instantiate(selectedPrefab, pos, rotation, generatedContainer);
                    instance.transform.localScale = selectedPrefab.transform.localScale * scaleMult;

                    placedHash.Add(pos);
                    clusterSpawned++;
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
            SpatialHash placedHash, // FIX BUG-03: SpatialHash thay List<Vector3>
            bool alignUpright,
            List<Vector3> outPositions = null)
        {
            int spawned = 0;
            int attempts = 0;
            int maxAttempts = targetCount * 25;

            float halfSize = MapSizeVal * 0.5f;
            int gridSide = Mathf.CeilToInt(Mathf.Sqrt(targetCount));
            float cellSize = MapSizeVal / gridSide;

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

                if (Vector3.Distance(Vector3.zero, pos) < SafeCenterRadiusVal)
                {
                    continue;
                }

                if (placedHash.HasNearby(pos, minSpacing)) // FIX BUG-03
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

                placedHash.Add(pos); // FIX BUG-03
                if (outPositions != null) outPositions.Add(pos);
                spawned++;
            }
        }

        /// <summary>
        /// FIX BUG-03: Thay thế IsTooClose() O(n²) bằng Spatial Grid Hash — O(1) amortized per lookup.
        /// cellSize = 2.5f bao phủ hiệu quả minSpacing từ 0.4f đến 3.5f (chỉ cần duyệt 2–3 ô lân cận).
        /// </summary>
        private sealed class SpatialHash
        {
            private readonly float cellSize;
            private readonly Dictionary<long, List<Vector3>> cells = new Dictionary<long, List<Vector3>>();

            public SpatialHash(float cellSize)
            {
                this.cellSize = cellSize;
            }

            private long Key(int cx, int cz) => ((long)(uint)cx << 32) | (uint)cz;

            private void ToCell(Vector3 pos, out int cx, out int cz)
            {
                cx = Mathf.FloorToInt(pos.x / cellSize);
                cz = Mathf.FloorToInt(pos.z / cellSize);
            }

            public void Add(Vector3 pos)
            {
                ToCell(pos, out int cx, out int cz);
                long key = Key(cx, cz);
                if (!cells.TryGetValue(key, out List<Vector3> list))
                {
                    list = new List<Vector3>(4);
                    cells[key] = list;
                }
                list.Add(pos);
            }

            public bool HasNearby(Vector3 pos, float minDist)
            {
                float sqrMin = minDist * minDist;
                ToCell(pos, out int cx, out int cz);
                int range = Mathf.CeilToInt(minDist / cellSize) + 1;
                for (int dx = -range; dx <= range; dx++)
                {
                    for (int dz = -range; dz <= range; dz++)
                    {
                        if (!cells.TryGetValue(Key(cx + dx, cz + dz), out List<Vector3> list)) continue;
                        foreach (Vector3 p in list)
                        {
                            if ((p - pos).sqrMagnitude < sqrMin) return true;
                        }
                    }
                }
                return false;
            }
        }

        private void ApplyDemoSceneLighting()
        {
            if (mapConfig != null && !mapConfig.enableDemoLighting) return;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = mapConfig != null ? mapConfig.fogColor * 0.1f : new Color(0.035f, 0.133f, 0.255f);
            RenderSettings.ambientEquatorColor = new Color(0.314f, 0.377f, 0.300f);
            RenderSettings.ambientGroundColor = new Color(0.185f, 0.254f, 0.128f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = mapConfig != null ? mapConfig.fogColor : new Color(0.485f, 0.855f, 0.943f);
            RenderSettings.fogStartDistance = mapConfig != null ? mapConfig.fogStartDistance : 15f;
            RenderSettings.fogEndDistance = mapConfig != null ? mapConfig.fogEndDistance : 220f;

            Light mainLight = FindFirstObjectByType<Light>();
            if (mainLight != null && mainLight.type == LightType.Directional)
            {
                mainLight.color = mapConfig != null ? mapConfig.sunColor : new Color(1.0f, 0.93f, 0.82f);
                mainLight.intensity = mapConfig != null ? mapConfig.sunIntensity : 1.3f;
                mainLight.shadows = LightShadows.Soft;
                mainLight.transform.rotation = Quaternion.Euler(45f, 130f, 0f);
            }
        }

        // FIX BUG-02: Dùng _cachedSeedX/_cachedSeedZ đã tính 1 lần trong GenerateMap()
        // Tham số seed giữ nguyên để không phá vỡ các lời gọi hiện tại nhưng không dùng nữa
        private float CalculateHeightAt(float xPos, float zPos, int seed)
        {
            float distFromCenter = Mathf.Sqrt(xPos * xPos + zPos * zPos);

            if (_cachedSeedX == 0f && _cachedSeedZ == 0f)
            {
                int activeSeed = seed == 0 ? (MapSeedVal == 0 ? 123456 : MapSeedVal) : seed;
                var seedRng = new System.Random(activeSeed);
                _cachedSeedX = (float)(seedRng.NextDouble() * 10000.0 + 1000.0);
                _cachedSeedZ = (float)(seedRng.NextDouble() * 10000.0 + 1000.0);
            }

            float seedX = _cachedSeedX;
            float seedZ = _cachedSeedZ;

            // Biến thiên đường viền bờ biển hòn đảo
            float coastlineNoise = (Mathf.PerlinNoise((xPos + seedX + 4000f) * 0.003f, (zPos + seedZ + 4000f) * 0.003f) - 0.5f) * 200f;
            float maxIslandRadius = IslandRadiusVal + coastlineNoise;

            float islandFalloff = 1.0f - Mathf.Clamp01((distFromCenter - maxIslandRadius * 0.55f) / (maxIslandRadius * 0.45f));
            islandFalloff = islandFalloff * islandFalloff * (3f - 2f * islandFalloff);

            // Địa hình đồi núi linh hoạt từ ScriptableObject Config
            float mountainPeaks = Mathf.PerlinNoise((xPos + seedX) * 0.0025f, (zPos + seedZ) * 0.0025f) * MountainMaxHeightVal;
            float rollingHills = Mathf.PerlinNoise((xPos + seedX + 500f) * 0.012f, (zPos + seedZ + 500f) * 0.012f) * HillsMaxHeightVal + BaseHeightOffsetVal;

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

            // FIX BUG-04: Dùng MapSizeVal thay hardcode 1400f để đồng bộ với ScriptableObject config
            float width = MapSizeVal;
            float length = MapSizeVal;
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
