using System.IO;
using System.Linq;
using MidnightChaos.Combat;
using MidnightChaos.Crafting;
using MidnightChaos.Enemies;
using MidnightChaos.Equipment;
using MidnightChaos.Inventory;
using MidnightChaos.Networking;
using MidnightChaos.Player;
using MidnightChaos.Resources;
using MidnightChaos.UI;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MidnightChaos.Editor
{
    public static class MidnightChaosBootstrapBuilder
    {
        private const string Root = "Assets/MidnightChaos";
        private const string GeneratedRoot = Root + "/Generated";
        private const string PrefabFolder = GeneratedRoot + "/Prefabs";
        private const string SceneFolder = GeneratedRoot + "/Scenes";
        private const string MaterialFolder = GeneratedRoot + "/Materials";
        private const string PlayerPrefabPath = PrefabFolder + "/DiagnosticNetworkPlayer.prefab";
        private const string ResourcePrefabPath = PrefabFolder + "/DiagnosticResourceNode.prefab";
        private const string EnemyPrefabPath = PrefabFolder + "/DiagnosticMeleeEnemy.prefab";
        private const string ChaosShardPrefabPath = PrefabFolder + "/DiagnosticChaosShard.prefab";
        private const string AttackMaterialPath = MaterialFolder + "/DiagnosticAttackIndicator.mat";
        private const string TrunkMaterialPath = MaterialFolder + "/DiagnosticTreeTrunk.mat";
        private const string LeavesMaterialPath = MaterialFolder + "/DiagnosticTreeLeaves.mat";
        private const string SwordMaterialPath = MaterialFolder + "/DiagnosticSword.mat";
        private const string WorkbenchMaterialPath = MaterialFolder + "/DiagnosticWorkbench.mat";
        private const string EnemyMaterialPath = MaterialFolder + "/DiagnosticMeleeEnemy.mat";
        private const string ChaosShardMaterialPath = MaterialFolder + "/DiagnosticChaosShard.mat";
        private const string ScenePath = SceneFolder + "/LAN_Bootstrap.unity";

        [MenuItem("Midnight Chaos/Bootstrap/Create or Refresh LAN Test Scene")]
        public static void Build()
        {
            EnsureFolder(Root, "Generated");
            EnsureFolder(GeneratedRoot, "Prefabs");
            EnsureFolder(GeneratedRoot, "Scenes");
            EnsureFolder(GeneratedRoot, "Materials");

            Material attackMaterial = CreateOrRefreshAttackMaterial();
            Material trunkMaterial = CreateOrRefreshUnlitMaterial(
                TrunkMaterialPath,
                "DiagnosticTreeTrunk",
                new Color(0.34f, 0.16f, 0.06f));
            Material leavesMaterial = CreateOrRefreshUnlitMaterial(
                LeavesMaterialPath,
                "DiagnosticTreeLeaves",
                new Color(0.12f, 0.62f, 0.18f));
            Material swordMaterial = CreateOrRefreshUnlitMaterial(
                SwordMaterialPath,
                "DiagnosticSword",
                new Color(0.78f, 0.86f, 0.95f));
            Material workbenchMaterial = CreateOrRefreshUnlitMaterial(
                WorkbenchMaterialPath,
                "DiagnosticWorkbench",
                new Color(0.54f, 0.29f, 0.08f));
            Material enemyMaterial = CreateOrRefreshUnlitMaterial(
                EnemyMaterialPath,
                "DiagnosticMeleeEnemy",
                new Color(0.58f, 0.22f, 0.78f));
            Material chaosShardMaterial = CreateOrRefreshUnlitMaterial(
                ChaosShardMaterialPath,
                "DiagnosticChaosShard",
                new Color(0.92f, 0.22f, 1f));

            GameObject playerPrefab = CreatePlayerPrefab(
                attackMaterial,
                swordMaterial);
            GameObject resourcePrefab = CreateResourcePrefab(
                trunkMaterial,
                leavesMaterial);
            GameObject enemyPrefab = CreateEnemyPrefab(enemyMaterial);
            GameObject chaosShardPrefab =
                CreateChaosShardPrefab(chaosShardMaterial);

            CreateLanScene(
                playerPrefab,
                resourcePrefab,
                enemyPrefab,
                chaosShardPrefab,
                workbenchMaterial);
            AddSceneToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "Midnight Chaos",
                "Đã tạo scene và prefab thử nghiệm cho Gate F.\n" +
                "Giết quái gần nhau để test Small → Mature → Alpha → Chaos Shard.",
                "OK");
        }

        private static GameObject CreatePlayerPrefab(
            Material attackMaterial,
            Material swordMaterial)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "DiagnosticNetworkPlayer";
            root.transform.position = Vector3.up;

            Collider primitiveCollider = root.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                Object.DestroyImmediate(primitiveCollider);
            }

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            controller.stepOffset = 0.35f;

            root.AddComponent<NetworkObject>();
            ValidatedOwnerNetworkTransform networkTransform =
                root.AddComponent<ValidatedOwnerNetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            networkTransform.Interpolate = true;

            CreateAttackIndicator(root.transform, attackMaterial);
            CreateSwordVisual(root.transform, swordMaterial);
            NetworkHealth playerHealth = root.AddComponent<NetworkHealth>();
            playerHealth.ConfigureForDiagnostics(100, "Player");
            root.AddComponent<DiagnosticNetworkInventory>();
            root.AddComponent<DiagnosticPlayerEquipment>();
            root.AddComponent<DiagnosticCraftingInteractor>();
            root.AddComponent<DiagnosticResourceGatherer>();
            root.AddComponent<DiagnosticMeleeCombat>();
            root.AddComponent<DiagnosticWorldHealthLabel>();
            root.AddComponent<DiagnosticNetworkPlayer>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateEnemyPrefab(Material enemyMaterial)
        {
            GameObject root = new GameObject("DiagnosticMeleeEnemy");
            root.name = "DiagnosticMeleeEnemy";
            root.transform.position = Vector3.up;

            CreatePrimitiveChild(
                PrimitiveType.Capsule,
                "BodyVisual",
                root.transform,
                Vector3.zero,
                Vector3.one,
                enemyMaterial);

            root.AddComponent<NetworkObject>();
            NetworkTransform networkTransform =
                root.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode =
                NetworkTransform.AuthorityModes.Server;
            networkTransform.Interpolate = true;

            NetworkHealth enemyHealth = root.AddComponent<NetworkHealth>();
            enemyHealth.ConfigureForDiagnostics(66, "Melee Enemy");
            root.AddComponent<DiagnosticEnemyEvolution>();
            root.AddComponent<DiagnosticMeleeEnemy>();
            root.AddComponent<DiagnosticWorldHealthLabel>();

            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);

            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateChaosShardPrefab(
            Material chaosShardMaterial)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "DiagnosticChaosShard";
            root.transform.localScale = new Vector3(0.35f, 0.7f, 0.35f);
            root.GetComponent<Renderer>().sharedMaterial =
                chaosShardMaterial;

            Collider collider = root.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            root.AddComponent<NetworkObject>();
            root.AddComponent<DiagnosticChaosShard>();
            root.AddComponent<DiagnosticWorldChaosShardLabel>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                root,
                ChaosShardPrefabPath);

            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateResourcePrefab(
            Material trunkMaterial,
            Material leavesMaterial)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "DiagnosticResourceNode";
            root.transform.localScale = new Vector3(0.7f, 1.3f, 0.7f);
            root.GetComponent<Renderer>().sharedMaterial = trunkMaterial;

            GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaves.name = "Leaves";
            leaves.transform.SetParent(root.transform, false);
            leaves.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            leaves.transform.localScale = new Vector3(2.2f, 1.45f, 2.2f);
            leaves.GetComponent<Renderer>().sharedMaterial = leavesMaterial;

            Collider leavesCollider = leaves.GetComponent<Collider>();
            if (leavesCollider != null)
            {
                Object.DestroyImmediate(leavesCollider);
            }

            root.AddComponent<NetworkObject>();
            root.AddComponent<DiagnosticResourceNode>();
            root.AddComponent<DiagnosticWorldResourceLabel>();

            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(root, ResourcePrefabPath);

            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material CreateOrRefreshAttackMaterial()
        {
            return CreateOrRefreshUnlitMaterial(
                AttackMaterialPath,
                "DiagnosticAttackIndicator",
                new Color(1f, 0.25f, 0.08f));
        }

        private static Material CreateOrRefreshUnlitMaterial(
            string path,
            string materialName,
            Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader =
                    Shader.Find("Universal Render Pipeline/Unlit") ??
                    Shader.Find("Unlit/Color");

                if (shader == null)
                {
                    throw new System.InvalidOperationException(
                        "Không tìm thấy URP Unlit hoặc Unlit/Color shader.");
                }

                material = new Material(shader)
                {
                    name = materialName
                };

                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else
            {
                material.color = color;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateAttackIndicator(
            Transform playerRoot,
            Material attackMaterial)
        {
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = "AttackIndicator";
            indicator.transform.SetParent(playerRoot, false);
            indicator.transform.localPosition = new Vector3(0f, 0.15f, 1.15f);
            indicator.transform.localScale = new Vector3(1.35f, 0.65f, 1.35f);

            Collider indicatorCollider = indicator.GetComponent<Collider>();
            if (indicatorCollider != null)
            {
                Object.DestroyImmediate(indicatorCollider);
            }

            Renderer indicatorRenderer = indicator.GetComponent<Renderer>();
            indicatorRenderer.sharedMaterial = attackMaterial;
            indicatorRenderer.enabled = false;
        }

        private static void CreateSwordVisual(
            Transform playerRoot,
            Material swordMaterial)
        {
            GameObject swordRoot = new GameObject("SwordVisual");
            swordRoot.transform.SetParent(playerRoot, false);
            swordRoot.transform.localPosition = new Vector3(0.65f, 0.1f, 0f);
            swordRoot.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);

            GameObject blade = CreatePrimitiveChild(
                PrimitiveType.Cube,
                "Blade",
                swordRoot.transform,
                new Vector3(0f, 0.62f, 0f),
                new Vector3(0.13f, 1.05f, 0.07f),
                swordMaterial);

            GameObject guard = CreatePrimitiveChild(
                PrimitiveType.Cube,
                "Guard",
                swordRoot.transform,
                new Vector3(0f, 0.05f, 0f),
                new Vector3(0.42f, 0.09f, 0.12f),
                swordMaterial);

            GameObject grip = CreatePrimitiveChild(
                PrimitiveType.Cube,
                "Grip",
                swordRoot.transform,
                new Vector3(0f, -0.18f, 0f),
                new Vector3(0.09f, 0.38f, 0.09f),
                swordMaterial);

            RemoveCollider(blade);
            RemoveCollider(guard);
            RemoveCollider(grip);
            swordRoot.SetActive(false);
        }

        private static GameObject CreatePrimitiveChild(
            PrimitiveType primitiveType,
            string objectName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject child = GameObject.CreatePrimitive(primitiveType);
            child.name = objectName;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;
            child.GetComponent<Renderer>().sharedMaterial = material;
            return child;
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void CreateCraftingStation(Material workbenchMaterial)
        {
            GameObject station = new GameObject("DiagnosticCraftingStation");
            station.transform.position = new Vector3(0f, 0f, -4f);

            CreatePrimitiveChild(
                PrimitiveType.Cube,
                "Top",
                station.transform,
                new Vector3(0f, 0.75f, 0f),
                new Vector3(2.4f, 0.3f, 1.4f),
                workbenchMaterial);

            Vector3[] legPositions =
            {
                new Vector3(-0.9f, 0.3f, -0.45f),
                new Vector3(0.9f, 0.3f, -0.45f),
                new Vector3(-0.9f, 0.3f, 0.45f),
                new Vector3(0.9f, 0.3f, 0.45f)
            };

            for (int index = 0; index < legPositions.Length; index++)
            {
                GameObject leg = CreatePrimitiveChild(
                    PrimitiveType.Cube,
                    $"Leg_{index + 1}",
                    station.transform,
                    legPositions[index],
                    new Vector3(0.18f, 0.6f, 0.18f),
                    workbenchMaterial);
                RemoveCollider(leg);
            }

            station.AddComponent<DiagnosticCraftingStation>();
            station.AddComponent<DiagnosticWorldCraftingLabel>();
        }

        private static void CreateLanScene(
            GameObject playerPrefab,
            GameObject resourcePrefab,
            GameObject enemyPrefab,
            GameObject chaosShardPrefab,
            Material workbenchMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "LAN_Bootstrap";

            GameObject cameraObject = new GameObject(
                "Main Camera",
                typeof(Camera),
                typeof(AudioListener),
                typeof(DiagnosticCameraFollow));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 8f, -9f);

            GameObject lightObject = new GameObject("Directional Light", typeof(Light));
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(8f, 1f, 8f);

            CreateCraftingStation(workbenchMaterial);

            GameObject networkRoot = new GameObject("NetworkRoot");
            UnityTransport transport = networkRoot.AddComponent<UnityTransport>();
            NetworkManager manager = networkRoot.AddComponent<NetworkManager>();
            LanSessionController sessionController =
                networkRoot.AddComponent<LanSessionController>();
            sessionController.Configure(playerPrefab);
            DiagnosticResourceSpawner resourceSpawner =
                networkRoot.AddComponent<DiagnosticResourceSpawner>();
            resourceSpawner.Configure(resourcePrefab);
            DiagnosticChaosEvolutionService evolutionService =
                networkRoot.AddComponent<DiagnosticChaosEvolutionService>();
            evolutionService.Configure(chaosShardPrefab);
            DiagnosticEnemySpawner enemySpawner =
                networkRoot.AddComponent<DiagnosticEnemySpawner>();
            enemySpawner.Configure(enemyPrefab);
            networkRoot.AddComponent<DiagnosticLanUI>();

            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = playerPrefab;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ForceSamePrefabs = true;
            manager.NetworkConfig.TickRate = 30;
            // Gate F adds replicated evolution state and a network shard prefab.
            // Reject stale builds instead of risking prefab/config mismatch.
            manager.NetworkConfig.ProtocolVersion = 4;

            // NetworkConfig contains serialized references. Explicitly mark the
            // configured components and scene dirty so Unity preserves every
            // prefab/transport fallback used to repair runtime configuration.
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(sessionController);
            EditorUtility.SetDirty(resourceSpawner);
            EditorUtility.SetDirty(evolutionService);
            EditorUtility.SetDirty(enemySpawner);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
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
