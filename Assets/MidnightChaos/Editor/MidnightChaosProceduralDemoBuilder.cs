using System.Collections.Generic;
using System.IO;
using System.Linq;
using MidnightChaos.Procedural;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace MidnightChaos.Editor
{
    public static class MidnightChaosProceduralDemoBuilder
    {
        private const string SettingsFolder =
            "Assets/MidnightChaos/Resources/Procedural";
        private const string SettingsPath =
            SettingsFolder + "/ProceduralWorldSettings.asset";
        private const string ScenePath =
            "Assets/MidnightChaos/Generated/Scenes/ProceduralDemo.unity";
        private const string EnvironmentPrefabRoot =
            "Assets/Game/Prefab/Environments";
        private const string NatureAssetRoot =
            "Assets/Asset/Environments/StylizedNatureBundle";

        [MenuItem("Midnight Chaos/Procedural/Create or Refresh Procedural Demo")]
        public static void CreateOrRefreshProceduralDemo()
        {
            EnsureFolder(SettingsFolder);
            EnsureFolder(Path.GetDirectoryName(ScenePath)?.Replace('\\', '/'));

            ProceduralWorldSettings settings =
                AssetDatabase.LoadAssetAtPath<ProceduralWorldSettings>(
                    SettingsPath);
            bool createdSettings = settings == null;
            if (createdSettings)
            {
                settings = ScriptableObject.CreateInstance<ProceduralWorldSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                ConfigureNewSettingsDefaults(settings);
            }

            settings.ConfigureAssetReferencesIfEmpty(
                LoadPrefabs(
                    EnvironmentPrefabRoot + "/Tree",
                    "Tree_01_a_P",
                    "Tree_02_a_P",
                    "Tree_03_a_P"),
                LoadPrefabs(
                    EnvironmentPrefabRoot + "/Rock",
                    "Rock_01",
                    "Rock_04"),
                LoadPrefabs(
                    EnvironmentPrefabRoot + "/Rock",
                    "Rock_10",
                    "Rock_12",
                    "Rock_13"),
                LoadPrefabs(
                    NatureAssetRoot + "/Prefabs/GrassFlower",
                    "GrassFlower_01_LOD",
                    "GrassFlower_03_LOD"),
                AssetDatabase.LoadAssetAtPath<Material>(
                    NatureAssetRoot + "/Materials/M_SNB_Terrain_01.mat"),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/MidnightChaos/Generated/Prefabs/" +
                    "DiagnosticMeleeEnemy.prefab"));
            EditorUtility.SetDirty(settings);

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            scene.name = "ProceduralDemo";

            GameObject networkRoot = new GameObject("NetworkManager");
            ProceduralDemoBootstrap bootstrap =
                networkRoot.AddComponent<ProceduralDemoBootstrap>();
            SerializedObject serializedBootstrap =
                new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("settings").objectReferenceValue =
                settings;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

            new GameObject(
                "ProceduralWorldGenerator",
                typeof(ProceduralWorldGenerator));
            GameObject navMeshObject = new GameObject(
                "RuntimeNavMeshBuilder",
                typeof(RuntimeNavMeshBuilder));
            navMeshObject.GetComponent<RuntimeNavMeshBuilder>()
                .Initialize(settings);
            new GameObject(
                "PlayerSpawnManager",
                typeof(ProceduralSpawnPointRegistry));
            new GameObject(
                "EnemySpawnManager",
                typeof(ProceduralEnemySpawnManager));
            new GameObject("UI Debug", typeof(ProceduralDemoUI));

            GameObject cameraObject = new GameObject(
                "Camera fallback",
                typeof(Camera),
                typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 95f, -105f);
            cameraObject.transform.rotation = Quaternion.Euler(38f, 0f, 0f);

            GameObject lightObject = new GameObject(
                "Directional Light",
                typeof(Light));
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log(
                "[Procedural] Created standalone ProceduralDemo scene. " +
                "It contains spawn-point markers but no Player instance.");
        }

        private static void ConfigureNewSettingsDefaults(
            ProceduralWorldSettings settings)
        {
            SerializedObject serialized = new SerializedObject(settings);
            ConfigureCategory(
                serialized.FindProperty("trees"),
                120,
                3.2f,
                new Vector2(0.85f, 1.2f),
                0f,
                ProceduralSurfaceAlignment.Upright,
                ProceduralNavigationMode.DynamicCarving,
                0f);
            ConfigureCategory(
                serialized.FindProperty("rocks"),
                60,
                2.4f,
                new Vector2(0.75f, 1.25f),
                0f,
                ProceduralSurfaceAlignment.AlignToSurfaceNormal,
                ProceduralNavigationMode.DynamicCarving,
                0f);
            ConfigureCategory(
                serialized.FindProperty("ores"),
                15,
                2.8f,
                new Vector2(0.7f, 1.05f),
                0f,
                ProceduralSurfaceAlignment.AlignToSurfaceNormal,
                ProceduralNavigationMode.DynamicCarving,
                0f);
            ConfigureCategory(
                serialized.FindProperty("vegetation"),
                45,
                0.7f,
                new Vector2(0.8f, 1.25f),
                0f,
                ProceduralSurfaceAlignment.AlignToSurfaceNormal,
                ProceduralNavigationMode.None,
                0.001f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureCategory(
            SerializedProperty category,
            int count,
            float clearance,
            Vector2 scaleRange,
            float tilt,
            ProceduralSurfaceAlignment surfaceAlignment,
            ProceduralNavigationMode navigationMode,
            float lodCullScreenHeightOverride)
        {
            category.FindPropertyRelative("count").intValue = count;
            category.FindPropertyRelative("clearanceRadius").floatValue =
                clearance;
            category.FindPropertyRelative("uniformScaleRange").vector2Value =
                scaleRange;
            category.FindPropertyRelative("randomTiltDegrees").floatValue =
                tilt;
            category.FindPropertyRelative("surfaceAlignment").enumValueIndex =
                (int)surfaceAlignment;
            category.FindPropertyRelative("navigationMode").enumValueIndex =
                (int)navigationMode;
            category.FindPropertyRelative("lodCullScreenHeightOverride")
                .floatValue = lodCullScreenHeightOverride;
        }

        private static GameObject[] LoadPrefabs(
            string folder,
            params string[] names)
        {
            return names
                .Select(
                    name => AssetDatabase.LoadAssetAtPath<GameObject>(
                        $"{folder}/{name}.prefab"))
                .Where(prefab => prefab != null)
                .ToArray();
        }

        private static void AddSceneToBuildSettings()
        {
            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            if (current.Any(scene => scene.path == ScenePath))
            {
                return;
            }

            EditorBuildSettings.scenes = current
                .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) })
                .ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    [CustomEditor(typeof(ProceduralWorldSettings))]
    public sealed class ProceduralWorldSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Tree/Rock/Ore: đặt child BottomPoint tại điểm tiếp xúc mặt đất. " +
                "Rotation của BottomPoint là frame được căn theo surface normal. " +
                "Vegetation có thể không cần BottomPoint.",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "Dynamic Carving bắt buộc có NavMeshObstacle authored trên " +
                "prefab và bật Carving. Runtime không tự tạo obstacle.",
                MessageType.Info);

            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Validate Procedural Prefab Contracts"))
            {
                ValidateAndLog((ProceduralWorldSettings)target);
            }
            if (GUILayout.Button("Migrate Missing Obstacles Into Prefabs"))
            {
                if (EditorUtility.DisplayDialog(
                        "Migrate NavMeshObstacle",
                        "Thêm NavMeshObstacle vào các prefab Dynamic Carving " +
                        "đang thiếu. Component hiện có sẽ không bị sửa. Tiếp tục?",
                        "Migrate",
                        "Cancel"))
                {
                    MigrateMissingObstacles((ProceduralWorldSettings)target);
                }
            }
        }

        private static void ValidateAndLog(ProceduralWorldSettings settings)
        {
            List<string> warnings = new List<string>();
            ValidateCategory(settings.Trees, "Trees", true, warnings);
            ValidateCategory(settings.Rocks, "Rocks", true, warnings);
            ValidateCategory(settings.Ores, "Ores", true, warnings);
            ValidateCategory(settings.Vegetation, "Vegetation", false, warnings);

            if (settings.EnemyPrefab == null)
            {
                warnings.Add("Enemy Prefab chưa được gán.");
            }
            else
            {
                NavMeshAgent agent =
                    settings.EnemyPrefab.GetComponent<NavMeshAgent>();
                if (agent == null)
                {
                    warnings.Add(
                        $"Enemy '{settings.EnemyPrefab.name}' thiếu NavMeshAgent.");
                }
                else if (agent.agentTypeID != settings.NavMeshAgentTypeId)
                {
                    warnings.Add(
                        $"Enemy '{settings.EnemyPrefab.name}' Agent Type ID " +
                        $"{agent.agentTypeID} != settings ID " +
                        $"{settings.NavMeshAgentTypeId}.");
                }
            }

            if (warnings.Count == 0)
            {
                Debug.Log(
                    "[Procedural] Prefab contract validation passed.",
                    settings);
                return;
            }

            Debug.LogWarning(
                "[Procedural] Prefab contract validation:\n- " +
                string.Join("\n- ", warnings),
                settings);
        }

        private static void ValidateCategory(
            ProceduralCategorySettings category,
            string label,
            bool anchorRequired,
            List<string> warnings)
        {
            for (int index = 0; index < category.Prefabs.Length; index++)
            {
                GameObject prefab = category.Prefabs[index];
                if (prefab == null)
                {
                    warnings.Add($"{label}[{index}] là null.");
                    continue;
                }

                if (anchorRequired &&
                    !ProceduralPrefabContract.TryFindPlacementAnchor(
                        prefab.transform,
                        out _))
                {
                    warnings.Add(
                        $"{label}[{index}] '{prefab.name}' thiếu BottomPoint.");
                }

                if (category.NavigationMode ==
                    ProceduralNavigationMode.DynamicCarving)
                {
                    NavMeshObstacle obstacle =
                        prefab.GetComponentInChildren<NavMeshObstacle>(true);
                    if (obstacle == null)
                    {
                        warnings.Add(
                            $"{label}[{index}] '{prefab.name}' thiếu " +
                            "NavMeshObstacle authored trên prefab.");
                    }
                    else if (!obstacle.carving)
                    {
                        warnings.Add(
                            $"{label}[{index}] '{prefab.name}' có " +
                            "NavMeshObstacle nhưng Carving đang tắt.");
                    }
                }

                if (category.NavigationMode != ProceduralNavigationMode.None &&
                    prefab.GetComponentInChildren<Collider>(true) == null)
                {
                    warnings.Add(
                        $"{label}[{index}] '{prefab.name}' không có Collider.");
                }
            }
        }

        private static void MigrateMissingObstacles(
            ProceduralWorldSettings settings)
        {
            HashSet<string> visitedPaths = new HashSet<string>();
            int createdCount = 0;
            createdCount += MigrateCategory(
                settings.Trees,
                visitedPaths);
            createdCount += MigrateCategory(
                settings.Rocks,
                visitedPaths);
            createdCount += MigrateCategory(
                settings.Ores,
                visitedPaths);
            createdCount += MigrateCategory(
                settings.Vegetation,
                visitedPaths);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[Procedural] Added authored NavMeshObstacle to " +
                $"{createdCount} prefab(s). Review Center/Size/Shape on every " +
                "prefab before runtime testing.",
                settings);
            ValidateAndLog(settings);
        }

        private static int MigrateCategory(
            ProceduralCategorySettings category,
            HashSet<string> visitedPaths)
        {
            if (category.NavigationMode !=
                ProceduralNavigationMode.DynamicCarving)
            {
                return 0;
            }

            int createdCount = 0;
            foreach (GameObject prefab in category.Prefabs)
            {
                if (prefab == null)
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(prefab);
                if (string.IsNullOrWhiteSpace(path) ||
                    !visitedPaths.Add(path) ||
                    prefab.GetComponentInChildren<NavMeshObstacle>(true) != null)
                {
                    continue;
                }

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (contents.GetComponentInChildren<NavMeshObstacle>(true) !=
                        null)
                    {
                        continue;
                    }

                    NavMeshObstacle obstacle =
                        contents.AddComponent<NavMeshObstacle>();
                    obstacle.shape = NavMeshObstacleShape.Box;
                    obstacle.carving = true;
                    obstacle.carveOnlyStationary = true;
                    if (TryCalculateLocalColliderBounds(
                            contents.transform,
                            out Bounds bounds))
                    {
                        obstacle.center = bounds.center;
                        obstacle.size = new Vector3(
                            Mathf.Max(0.1f, bounds.size.x),
                            Mathf.Max(0.1f, bounds.size.y),
                            Mathf.Max(0.1f, bounds.size.z));
                    }

                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    createdCount++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            return createdCount;
        }

        private static bool TryCalculateLocalColliderBounds(
            Transform root,
            out Bounds localBounds)
        {
            localBounds = default;
            bool hasBounds = false;
            foreach (Collider collider in
                     root.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                Vector3 minimum = collider.bounds.min;
                Vector3 maximum = collider.bounds.max;
                for (int x = 0; x <= 1; x++)
                {
                    for (int y = 0; y <= 1; y++)
                    {
                        for (int z = 0; z <= 1; z++)
                        {
                            Vector3 point = root.InverseTransformPoint(
                                new Vector3(
                                    x == 0 ? minimum.x : maximum.x,
                                    y == 0 ? minimum.y : maximum.y,
                                    z == 0 ? minimum.z : maximum.z));
                            if (!hasBounds)
                            {
                                localBounds = new Bounds(point, Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                localBounds.Encapsulate(point);
                            }
                        }
                    }
                }
            }

            return hasBounds;
        }
    }
}
