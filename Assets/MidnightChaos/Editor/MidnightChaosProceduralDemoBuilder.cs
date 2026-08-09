using System.Collections.Generic;
using System.IO;
using System.Linq;
using MidnightChaos.Procedural;
using MidnightChaos.Inventory;
using MidnightChaos.Enemies;
using MidnightChaos.Player;
using MidnightChaos.Resources;
using MidnightChaos.World;
using Unity.Netcode;
using Unity.Netcode.Components;
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
        private const string RenderingSettingsPath =
            SettingsFolder + "/ProceduralRenderingSettings.asset";
        internal const string NavigationSettingsPath =
            SettingsFolder + "/ProceduralNavigationSettings.asset";
        internal const string GameplaySettingsPath =
            SettingsFolder + "/VerticalSliceGameplaySettings.asset";
        private const string EvolutionProfilePath =
            SettingsFolder + "/ChaosEvolutionProfile.asset";
        private const string ScenePath =
            "Assets/MidnightChaos/Generated/Scenes/ProceduralDemo.unity";
        private const string CombatScenePath =
            "Assets/MidnightChaos/Generated/Scenes/ProceduralCombatDemo.unity";
        private const string PlayerPrefabPath =
            "Assets/MidnightChaos/Generated/Prefabs/DiagnosticNetworkPlayer.prefab";
        private const string ChaosShardPrefabPath =
            "Assets/MidnightChaos/Generated/Prefabs/DiagnosticChaosShard.prefab";
        private const string SmallRockPrefabPath =
            "Assets/Game/Prefab/Item/SmallRock.prefab";
        private const string NatureAssetRoot =
            "Assets/Asset/Environments/StylizedNatureBundle";
        private const string DefinitionRoot =
            "Assets/MidnightChaos/Definitions/World/Procedural";

        [MenuItem(
            "Midnight Chaos/Procedural/Migrate Demo Vertical Slice V1")]
        public static void MigrateDemoVerticalSliceV1()
        {
            EnsureVerticalSlicePlayerPrefab();
            EnsureVerticalSliceWorldItemPrefab();
            CreateOrRefreshProceduralCombatDemo();
            Debug.Log(
                "[VerticalSlice] Migration V1 completed: player, generic " +
                "world item, settings and ProceduralCombatDemo refreshed.");
        }

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
            if (!TryLoadSplitSettings(
                    out ProceduralRenderingSettings renderingSettings,
                    out ProceduralNavigationSettings navigationSettings,
                    out VerticalSliceGameplaySettings gameplaySettings,
                    out ChaosEvolutionProfile evolutionProfile))
            {
                return;
            }
            EnsureVerticalSliceWorldItemPrefab();

            GameObject refreshedEnemyPrefab =
                MidnightChaosBootstrapBuilder.CreateOrRefreshProceduralEnemyPrefab();
            settings.ConfigureDefinitionReferencesIfEmpty(
                LoadDefinitions(
                    "Tree_01_a", "Tree_01_b", "Tree_01_d",
                    "Tree_02_a", "Tree_02_b", "Tree_02_d",
                    "Tree_03_a", "Tree_03_b", "Tree_03_d",
                    "Tree_04_a", "Tree_04_b", "Tree_04_d"),
                LoadDefinitions(
                    "Rock_01", "Rock_02", "Rock_03", "Rock_04", "Rock_05",
                    "Rock_06", "Rock_07", "Rock_08", "Rock_09", "Rock_11"),
                LoadDefinitions("Ore_10", "Ore_12", "Ore_13"),
                LoadDefinitions(
                    "Vegetation_Flower_01", "Vegetation_Flower_02",
                    "Vegetation_Flower_03", "Vegetation_Flower_04",
                    "Vegetation_Flower_05"),
                LoadDefinitions(
                    "Vegetation_Grass_01", "Vegetation_Grass_03"),
                AssetDatabase.LoadAssetAtPath<Material>(
                    NatureAssetRoot + "/Materials/M_SNB_Terrain_01.mat"));
            gameplaySettings.ConfigureReferencesIfEmpty(
                null,
                refreshedEnemyPrefab);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(gameplaySettings);

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            scene.name = "ProceduralDemo";

            GameObject networkRoot = new GameObject("NetworkManager");
            ProceduralDemoBootstrap bootstrap =
                networkRoot.AddComponent<ProceduralDemoBootstrap>();
            GameObject chaosShardPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(ChaosShardPrefabPath);
            SerializedObject serializedBootstrap =
                new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("settings").objectReferenceValue =
                settings;
            serializedBootstrap.FindProperty("renderingSettings")
                .objectReferenceValue = renderingSettings;
            serializedBootstrap.FindProperty("navigationSettings")
                .objectReferenceValue = navigationSettings;
            serializedBootstrap.FindProperty("gameplaySettings")
                .objectReferenceValue = gameplaySettings;
            serializedBootstrap.FindProperty("evolutionProfile")
                .objectReferenceValue = evolutionProfile;
            serializedBootstrap.FindProperty("chaosShardPrefab")
                .objectReferenceValue = chaosShardPrefab;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

            new GameObject(
                "ProceduralWorldGenerator",
                typeof(ProceduralWorldGenerator));
            GameObject navMeshObject = new GameObject(
                "RuntimeNavMeshBuilder",
                typeof(RuntimeNavMeshBuilder));
            navMeshObject.GetComponent<RuntimeNavMeshBuilder>()
                .Initialize(settings, navigationSettings);
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
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log(
                "[Procedural] Created standalone ProceduralDemo scene. " +
                "It contains spawn-point markers but no Player instance.");
        }

        [MenuItem(
            "Midnight Chaos/Procedural/Create or Refresh Procedural Combat Demo")]
        public static void CreateOrRefreshProceduralCombatDemo()
        {
            EnsureFolder(SettingsFolder);
            EnsureFolder(
                Path.GetDirectoryName(CombatScenePath)?.Replace('\\', '/'));

            ProceduralWorldSettings settings =
                AssetDatabase.LoadAssetAtPath<ProceduralWorldSettings>(
                    SettingsPath);
            if (settings == null)
            {
                Debug.LogError(
                    "[Procedural Combat] ProceduralWorldSettings is missing. " +
                    "Run Create or Refresh Procedural Demo first.");
                return;
            }
            if (!TryLoadSplitSettings(
                    out ProceduralRenderingSettings renderingSettings,
                    out ProceduralNavigationSettings navigationSettings,
                    out VerticalSliceGameplaySettings gameplaySettings,
                    out ChaosEvolutionProfile evolutionProfile))
            {
                return;
            }
            EnsureVerticalSlicePlayerPrefab();
            EnsureVerticalSliceWorldItemPrefab();
            GameObject smallRockPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(SmallRockPrefabPath);
            if (smallRockPrefab == null)
            {
                Debug.LogError(
                    "[Procedural Combat] SmallRock prefab is missing at " +
                    SmallRockPrefabPath);
                return;
            }

            GameObject playerPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogError(
                    "[Procedural Combat] DiagnosticNetworkPlayer is missing. " +
                    "Run Create or Refresh LAN Test Scene first.");
                return;
            }
            GameObject chaosShardPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(ChaosShardPrefabPath);
            if (chaosShardPrefab == null)
            {
                Debug.LogError(
                    "[Procedural Combat] DiagnosticChaosShard is missing. " +
                    "Run Create or Refresh LAN Test Scene first.");
                return;
            }

            GameObject enemyPrefab =
                MidnightChaosBootstrapBuilder.CreateOrRefreshProceduralEnemyPrefab();
            settings.ConfigureDefinitionReferencesIfEmpty(
                LoadDefinitions(
                    "Tree_01_a", "Tree_01_b", "Tree_01_d",
                    "Tree_02_a", "Tree_02_b", "Tree_02_d",
                    "Tree_03_a", "Tree_03_b", "Tree_03_d",
                    "Tree_04_a", "Tree_04_b", "Tree_04_d"),
                LoadDefinitions(
                    "Rock_01", "Rock_02", "Rock_03", "Rock_04", "Rock_05",
                    "Rock_06", "Rock_07", "Rock_08", "Rock_09", "Rock_11"),
                LoadDefinitions("Ore_10", "Ore_12", "Ore_13"),
                LoadDefinitions(
                    "Vegetation_Flower_01", "Vegetation_Flower_02",
                    "Vegetation_Flower_03", "Vegetation_Flower_04",
                    "Vegetation_Flower_05"),
                LoadDefinitions(
                    "Vegetation_Grass_01", "Vegetation_Grass_03"),
                AssetDatabase.LoadAssetAtPath<Material>(
                    NatureAssetRoot + "/Materials/M_SNB_Terrain_01.mat"));
            settings.SmallRocks.ConfigureVisualReferenceIfEmpty(smallRockPrefab);
            gameplaySettings.ConfigureReferencesIfEmpty(
                chaosShardPrefab,
                enemyPrefab);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(gameplaySettings);

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            scene.name = "ProceduralCombatDemo";

            GameObject networkRoot = new GameObject("NetworkManager");
            ProceduralDemoBootstrap bootstrap =
                networkRoot.AddComponent<ProceduralDemoBootstrap>();
            SerializedObject serializedBootstrap =
                new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("settings").objectReferenceValue =
                settings;
            serializedBootstrap.FindProperty("renderingSettings")
                .objectReferenceValue = renderingSettings;
            serializedBootstrap.FindProperty("navigationSettings")
                .objectReferenceValue = navigationSettings;
            serializedBootstrap.FindProperty("gameplaySettings")
                .objectReferenceValue = gameplaySettings;
            serializedBootstrap.FindProperty("evolutionProfile")
                .objectReferenceValue = evolutionProfile;
            serializedBootstrap.FindProperty("enablePlayers").boolValue = true;
            serializedBootstrap.FindProperty("playerPrefab").objectReferenceValue =
                playerPrefab;
            serializedBootstrap.FindProperty("chaosShardPrefab")
                .objectReferenceValue = chaosShardPrefab;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

            new GameObject(
                "ProceduralWorldGenerator",
                typeof(ProceduralWorldGenerator));
            GameObject navMeshObject = new GameObject(
                "RuntimeNavMeshBuilder",
                typeof(RuntimeNavMeshBuilder));
            navMeshObject.GetComponent<RuntimeNavMeshBuilder>()
                .Initialize(settings, navigationSettings);
            new GameObject(
                "PlayerSpawnManager",
                typeof(ProceduralSpawnPointRegistry),
                typeof(ProceduralPlayerSpawnManager));
            new GameObject(
                "EnemySpawnManager",
                typeof(ProceduralEnemySpawnManager));
            new GameObject("UI Debug", typeof(ProceduralDemoUI));

            GameObject cameraObject = new GameObject(
                "Camera fallback",
                typeof(Camera),
                typeof(AudioListener),
                typeof(DiagnosticCameraFollow));
            cameraObject.tag = "MainCamera";
            cameraObject.GetComponent<DiagnosticCameraFollow>()
                .Configure(renderingSettings);
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
            EditorSceneManager.SaveScene(scene, CombatScenePath);
            AddSceneToBuildSettings(CombatScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(CombatScenePath);
            Debug.Log(
                "[Procedural Combat] Created scene with players spawned after " +
                "Host world/NavMesh readiness. Enemy spawning remains manual.");
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
                2000,
                0.7f,
                new Vector2(0.8f, 1.25f),
                0f,
                ProceduralSurfaceAlignment.AlignToSurfaceNormal,
                ProceduralNavigationMode.None,
                0.001f);
            ConfigureCategory(
                serialized.FindProperty("grass"),
                8000,
                0.7f,
                new Vector2(0.8f, 1.25f),
                0f,
                ProceduralSurfaceAlignment.AlignToSurfaceNormal,
                ProceduralNavigationMode.None,
                0.001f);
            SerializedProperty clusters =
                serialized.FindProperty("grassClusters");
            clusters.FindPropertyRelative("instancesPerClusterRange")
                .vector2IntValue = new Vector2Int(50, 100);
            clusters.FindPropertyRelative("radiusRange").vector2Value =
                new Vector2(3f, 7f);
            clusters.FindPropertyRelative("minimumSpacingRange").vector2Value =
                new Vector2(0.15f, 0.3f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool TryLoadSplitSettings(
            out ProceduralRenderingSettings renderingSettings,
            out ProceduralNavigationSettings navigationSettings,
            out VerticalSliceGameplaySettings gameplaySettings,
            out ChaosEvolutionProfile evolutionProfile)
        {
            renderingSettings = AssetDatabase.LoadAssetAtPath<
                ProceduralRenderingSettings>(RenderingSettingsPath);
            navigationSettings = AssetDatabase.LoadAssetAtPath<
                ProceduralNavigationSettings>(NavigationSettingsPath);
            gameplaySettings = AssetDatabase.LoadAssetAtPath<
                VerticalSliceGameplaySettings>(GameplaySettingsPath);
            evolutionProfile = AssetDatabase.LoadAssetAtPath<
                ChaosEvolutionProfile>(EvolutionProfilePath);

            if (renderingSettings != null && navigationSettings != null &&
                gameplaySettings != null && evolutionProfile != null)
            {
                return true;
            }

            Debug.LogError(
                "[Procedural] Split config migration is incomplete. Expected " +
                "World, Rendering, Navigation, Vertical Slice Gameplay and " +
                "Chaos Evolution assets in Resources/Procedural.");
            return false;
        }

        private static void ConfigureCategory(
            SerializedProperty category,
            int count,
            float clearance,
            Vector2 scaleRange,
            float tilt,
            ProceduralSurfaceAlignment surfaceAlignment,
            ProceduralNavigationMode navigationMode,
            float layoutHashCompatibilityValue)
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
            category.FindPropertyRelative("layoutHashCompatibilityValue")
                .floatValue = layoutHashCompatibilityValue;
        }

        private static WorldObjectDefinition[] LoadDefinitions(
            params string[] assetNames)
        {
            return assetNames
                .Select(
                    assetName =>
                        AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>(
                            $"{DefinitionRoot}/{assetName}.asset"))
                .ToArray();
        }

        private static void EnsureVerticalSlicePlayerPrefab()
        {
            ProceduralWorldSettings worldSettings =
                AssetDatabase.LoadAssetAtPath<ProceduralWorldSettings>(
                    SettingsPath);
            VerticalSliceGameplaySettings gameplaySettings =
                AssetDatabase.LoadAssetAtPath<VerticalSliceGameplaySettings>(
                    GameplaySettingsPath);
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (root == null)
            {
                Debug.LogError(
                    "[VerticalSlice] Player prefab missing. Run LAN builder first.");
                return;
            }
            VerticalSlicePlayerActions actions =
                root.GetComponent<VerticalSlicePlayerActions>() ??
                root.AddComponent<VerticalSlicePlayerActions>();
            actions.Configure(worldSettings, gameplaySettings);
            DiagnosticResourceGatherer gatherer =
                root.GetComponent<DiagnosticResourceGatherer>();
            gatherer?.Configure(gameplaySettings);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void EnsureVerticalSliceWorldItemPrefab()
        {
            ProceduralWorldSettings worldSettings =
                AssetDatabase.LoadAssetAtPath<ProceduralWorldSettings>(
                    SettingsPath);
            VerticalSliceGameplaySettings gameplaySettings =
                AssetDatabase.LoadAssetAtPath<VerticalSliceGameplaySettings>(
                    GameplaySettingsPath);
            GameObject root = PrefabUtility.LoadPrefabContents(
                ChaosShardPrefabPath);
            if (root == null)
            {
                Debug.LogError(
                    "[VerticalSlice] ChaosShard prefab missing. Run LAN builder first.");
                return;
            }
            if (root.GetComponent<Collider>() == null)
            {
                root.AddComponent<BoxCollider>().isTrigger = true;
            }
            else
            {
                root.GetComponent<Collider>().isTrigger = true;
            }
            if (root.GetComponent<Rigidbody>() == null)
            {
                Rigidbody body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
            }
            if (root.GetComponent<NetworkTransform>() == null)
            {
                NetworkTransform networkTransform =
                    root.AddComponent<NetworkTransform>();
                networkTransform.AuthorityMode =
                    NetworkTransform.AuthorityModes.Server;
            }
            DiagnosticWorldPickup pickup =
                root.GetComponent<DiagnosticWorldPickup>() ??
                root.AddComponent<DiagnosticWorldPickup>();
            pickup.Configure(worldSettings, gameplaySettings);
            PrefabUtility.SaveAsPrefabAsset(root, ChaosShardPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            if (current.Any(scene => scene.path == scenePath))
            {
                return;
            }

            EditorBuildSettings.scenes = current
                .Concat(new[] { new EditorBuildSettingsScene(scenePath, true) })
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
            try
            {
                settings.ValidateDefinitionsOrThrow();
            }
            catch (System.InvalidOperationException exception)
            {
                warnings.Add(exception.Message);
            }
            ValidateCategory(settings.Trees, "Trees", true, warnings);
            ValidateCategory(settings.Rocks, "Rocks", true, warnings);
            ValidateCategory(settings.Ores, "Ores", true, warnings);
            ValidateCategory(settings.Vegetation, "Vegetation", false, warnings);
            ValidateCategory(settings.Grass, "Grass", false, warnings);
            warnings.AddRange(settings.CollectDefinitionWarnings());

            VerticalSliceGameplaySettings gameplaySettings =
                AssetDatabase.LoadAssetAtPath<VerticalSliceGameplaySettings>(
                    MidnightChaosProceduralDemoBuilder.GameplaySettingsPath);
            ProceduralNavigationSettings navigationSettings =
                AssetDatabase.LoadAssetAtPath<ProceduralNavigationSettings>(
                    MidnightChaosProceduralDemoBuilder.NavigationSettingsPath);
            if (gameplaySettings == null || gameplaySettings.EnemyPrefab == null)
            {
                warnings.Add("Enemy Prefab chưa được gán.");
            }
            else
            {
                NavMeshAgent agent =
                    gameplaySettings.EnemyPrefab.GetComponent<NavMeshAgent>();
                if (agent == null)
                {
                    warnings.Add(
                        $"Enemy '{gameplaySettings.EnemyPrefab.name}' thiếu NavMeshAgent.");
                }
                else if (navigationSettings == null ||
                         agent.agentTypeID != navigationSettings.NavMeshAgentTypeId)
                {
                    warnings.Add(
                        $"Enemy '{gameplaySettings.EnemyPrefab.name}' Agent Type ID " +
                        $"{agent.agentTypeID} != settings ID " +
                        $"{navigationSettings?.NavMeshAgentTypeId.ToString() ?? "missing"}.");
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
            for (int index = 0; index < category.Definitions.Length; index++)
            {
                WorldObjectDefinition definition = category.Definitions[index];
                if (definition == null)
                {
                    warnings.Add($"{label}[{index}] là null.");
                    continue;
                }

                GameObject prefab = definition.Prefab;
                if (prefab == null)
                {
                    warnings.Add(
                        $"{label}[{index}] '{definition.StableId}' thiếu prefab.");
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
            createdCount += MigrateCategory(
                settings.Grass,
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
            foreach (WorldObjectDefinition definition in category.Definitions)
            {
                GameObject prefab = definition == null
                    ? null
                    : definition.Prefab;
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
