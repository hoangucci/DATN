using System.Collections.Generic;
using System.IO;
using System.Linq;
using MidnightChaos.Resources;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MidnightChaos.Editor
{
    public static class MidnightChaosMapSceneBuilder
    {
        private const string Root = "Assets/MidnightChaos";
        private const string SceneFolder = "Assets/Game/Scenes";
        private const string ScenePath = SceneFolder + "/Map.unity";

        private const string StylizedBundlePath = "Assets/Asset/StylizedNatureBundle/Prefabs";

        [MenuItem("Midnight Chaos/Map Generator/Create or Refresh 'Map' Scene")]
        public static void BuildMapScene()
        {
            EnsureFolder("Assets/Game", "Scenes");

            // 1. Thu thập các Prefab mẫu từ StylizedNatureBundle
            GameObject[] treePrefabs = LoadPrefabsFromPath($"{StylizedBundlePath}/Trees/noLODs");
            GameObject[] rockPrefabs = LoadPrefabsFromPath($"{StylizedBundlePath}/Rocks/noLODs");
            GameObject[] vegetationPrefabs = LoadPrefabsFromPath($"{StylizedBundlePath}/GrassFlower");

            if (treePrefabs.Length == 0)
            {
                treePrefabs = LoadPrefabsFromPath($"{StylizedBundlePath}/Trees");
            }
            if (rockPrefabs.Length == 0)
            {
                rockPrefabs = LoadPrefabsFromPath($"{StylizedBundlePath}/Rocks");
            }

            Debug.Log($"[Map Scene Builder] Đã tìm thấy {treePrefabs.Length} Mẫu cây, {rockPrefabs.Length} Mẫu đá, {vegetationPrefabs.Length} Mẫu cỏ/hoa.");

            // 2. Khởi tạo Scene 'Map' mới
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Map";

            // 3. Tạo Camera & Ánh sáng Directional Light
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 45f, -70f);
            cameraObject.transform.rotation = Quaternion.Euler(35f, 0f, 0f);

            GameObject lightObject = new GameObject("Directional Light", typeof(Light));
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.93f, 0.82f); // Ánh nắng ấm cúng chuẩn DemoScene_01
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(45f, 130f, 0f);

            // Gán Material M_SNB_Skybox & cấu hình Lighting, Fog chuẩn 100% DemoScene_01
            Material skyboxMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Asset/StylizedNatureBundle/Materials/M_SNB_Skybox.mat")
                                   ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Asset/Environments/StylizedNatureBundle/Materials/M_SNB_Skybox.mat");
            if (skyboxMaterial != null)
            {
                RenderSettings.skybox = skyboxMaterial;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.035f, 0.133f, 0.255f);
                RenderSettings.ambientEquatorColor = new Color(0.314f, 0.377f, 0.300f);
                RenderSettings.ambientGroundColor = new Color(0.185f, 0.254f, 0.128f);

                // Cấu hình sương mù Stylized Fog chuẩn DemoScene_01
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(0.485f, 0.855f, 0.943f);
                RenderSettings.fogStartDistance = 50f;
                RenderSettings.fogEndDistance = 700f;

                DynamicGI.UpdateEnvironment();
            }

            // 4. Tạo Địa hình Hòn Đảo (Island Terrain - 1400m x 1400m)
            Material groundMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Asset/StylizedNatureBundle/Materials/M_Terrain_01.mat");
            if (groundMaterial == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:Material Terrain", new[] { "Assets/Asset/StylizedNatureBundle" });
                if (guids.Length > 0)
                {
                    groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            GameObject ground = CreateIslandTerrainGround(1400f, 1400f, 140, 140, groundMaterial);
            ground.name = "IslandGround";

            // Tạo Mặt nước biển (Ocean Water Plane) bao quanh Hòn đảo
            GameObject oceanWater = CreateOceanWaterPlane(2200f);
            oceanWater.name = "OceanWater";

            // 5. Tạo MapGenerator Object & Cấu hình Prefabs
            GameObject generatorObj = new GameObject("MapGenerator");
            StylizedNatureMapGenerator generator = generatorObj.AddComponent<StylizedNatureMapGenerator>();
            generator.ConfigurePrefabs(treePrefabs, rockPrefabs, vegetationPrefabs);

            // 6. Thực hiện rải bản đồ ngẫu nhiên
            generator.GenerateMap();

            // 7. Lưu Scene vào đĩa cứng
            AddSceneToBuildSettings();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);

            EditorUtility.DisplayDialog(
                "Midnight Chaos - Map Generator",
                $"Đã tạo thành công Scene 'Map' tại:\n{ScenePath}\n\n" +
                $"Bản đồ đã được sinh ra ngẫu nhiên với cây cối, đá và thảm thực vật từ StylizedNatureBundle!",
                "OK");
        }

        private static GameObject[] LoadPrefabsFromPath(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                return new GameObject[0];
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { path });
            List<GameObject> prefabs = new List<GameObject>();

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab != null)
                {
                    prefabs.Add(prefab);
                }
            }

            return prefabs.ToArray();
        }

        private static void AddSceneToBuildSettings()
        {
            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            if (current.Any(entry => entry.path == ScenePath))
            {
                return;
            }

            EditorBuildSettings.scenes = current
                .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) })
                .ToArray();
        }

        private static GameObject CreateIslandTerrainGround(
            float width,
            float length,
            int segmentsX,
            int segmentsZ,
            Material material)
        {
            GameObject groundObj = new GameObject("IslandGround");
            MeshFilter meshFilter = groundObj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = groundObj.AddComponent<MeshRenderer>();
            MeshCollider meshCollider = groundObj.AddComponent<MeshCollider>();

            Mesh mesh = new Mesh();
            mesh.name = "IslandTerrainMesh";

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

                    float distFromCenter = Mathf.Sqrt(xPos * xPos + zPos * zPos);

                    // Biến thiên bờ biển hữu cơ (Organic Coastline Noise)
                    float coastlineNoise = (Mathf.PerlinNoise((xPos + 4000f) * 0.003f, (zPos + 4000f) * 0.003f) - 0.5f) * 220f;
                    float maxIslandRadius = 550f + coastlineNoise;

                    // Hệ số dốc hòn đảo (1 ở trung tâm, 0 ở bãi biển, âm dưới lòng biển)
                    float islandFalloff = 1.0f - Mathf.Clamp01((distFromCenter - maxIslandRadius * 0.55f) / (maxIslandRadius * 0.45f));
                    islandFalloff = islandFalloff * islandFalloff * (3f - 2f * islandFalloff);

                    // Thuật toán Perlin Noise tạo các ngọn đồi nhấp nhô vừa phải (cao từ 15m - 32m)
                    float baseHills = (Mathf.PerlinNoise((xPos + 2000f) * 0.0035f, (zPos + 2000f) * 0.0035f) * 28.0f)
                                     + (Mathf.PerlinNoise((xPos + 500f) * 0.012f, (zPos + 500f) * 0.012f) * 8.0f) + 4.0f;

                    float height = (baseHills * islandFalloff) - ((1.0f - islandFalloff) * 15.0f);

                    // Làm phẳng tâm xuất phát an toàn (bán kính 12m) tại độ cao Y=3.0m
                    float flattenFactor = Mathf.Clamp01((distFromCenter - 8f) / 12f);
                    height = Mathf.Lerp(3.0f, height, flattenFactor);

                    vertices[index] = new Vector3(xPos, height, zPos);
                    uvs[index] = new Vector2(xPos / 15f, zPos / 15f);
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
            mesh.RecalculateBounds();

            meshFilter.sharedMesh = mesh;
            meshCollider.sharedMesh = mesh;
            if (material != null)
            {
                meshRenderer.sharedMaterial = material;
            }

            return groundObj;
        }

        private static GameObject CreateOceanWaterPlane(float oceanSize)
        {
            GameObject oceanObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
            oceanObj.transform.position = new Vector3(0f, 0.5f, 0f);
            float scale = oceanSize / 10f;
            oceanObj.transform.localScale = new Vector3(scale, 1f, scale);

            // Tạo Material nước biển Stylized xanh trong trẻo
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            Material waterMat = new Material(shader)
            {
                name = "StylizedOceanWater"
            };

            Color waterColor = new Color(0.12f, 0.55f, 0.75f, 0.85f);
            if (waterMat.HasProperty("_BaseColor"))
            {
                waterMat.SetColor("_BaseColor", waterColor);
            }
            else
            {
                waterMat.color = waterColor;
            }

            oceanObj.GetComponent<Renderer>().sharedMaterial = waterMat;
            return oceanObj;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(parent))
            {
                Directory.CreateDirectory(parent);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
