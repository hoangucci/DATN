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
using UnityEngine.AI;

namespace MidnightChaos.Editor
{
    public static class MidnightChaosBootstrapBuilder
    {
        private const string Root = "Assets/MidnightChaos";
        private const string GeneratedRoot = Root + "/Generated";
        private const string PrefabFolder = GeneratedRoot + "/Prefabs";
        private const string SceneFolder = GeneratedRoot + "/Scenes";
        private const string MaterialFolder = GeneratedRoot + "/Materials";
        private const string SettingsFolder = GeneratedRoot + "/Settings";
        private const string EnemyDefinitionFolder = Root + "/Definitions/Enemies";
        private const string MuckFirstPersonControllerPath =
            Root + "/Animation/MuckFirstPerson/Cube.controller";
        private const string PlayerPrefabPath = PrefabFolder + "/DiagnosticNetworkPlayer.prefab";
        private const string ResourcePrefabPath = PrefabFolder + "/DiagnosticResourceNode.prefab";
        private const string EnemyPrefabPath = PrefabFolder + "/DiagnosticMeleeEnemy.prefab";
        private const string FireMageVisualPrefabPath =
            "Assets/Asset/Monster/Fire Mage Cute Series/Prefabs/Fire Mage.prefab";
        private const string FireMageEnemyDefinitionPath =
            EnemyDefinitionFolder + "/FireMageMeleeEnemy.asset";
        private const string ChaosShardPrefabPath = PrefabFolder + "/DiagnosticChaosShard.prefab";
        private const string AttackMaterialPath = MaterialFolder + "/DiagnosticAttackIndicator.mat";
        private const string TrunkMaterialPath = MaterialFolder + "/DiagnosticTreeTrunk.mat";
        private const string LeavesMaterialPath = MaterialFolder + "/DiagnosticTreeLeaves.mat";
        private const string SwordMaterialPath = MaterialFolder + "/DiagnosticSword.mat";
        private const string WorkbenchMaterialPath = MaterialFolder + "/DiagnosticWorkbench.mat";
        private const string EnemyMaterialPath = MaterialFolder + "/DiagnosticMeleeEnemy.mat";
        private const string ChaosShardMaterialPath = MaterialFolder + "/DiagnosticChaosShard.mat";
        private const string CombatSettingsPath =
            SettingsFolder + "/DiagnosticMeleeCombatSettings.asset";
        private const string UnarmedAttackProfilePath =
            SettingsFolder + "/UnarmedAttackProfile.asset";
        private const string SwordAttackProfilePath =
            SettingsFolder + "/SwordAttackProfile.asset";
        private const string ScenePath = SceneFolder + "/LAN_Bootstrap.unity";
        private const float MuckAttackImpactTime = 0.2666667f;
        private const float UnarmedImpactTime = 0.10825f;
        private const int MuckAttackVariantCount = 3;
        private static readonly Vector3 ReferenceSwordRestPosition =
            new Vector3(0.45f, -0.35f, 0.65f);
        private static readonly Vector3 ReferenceSwordRestEulerAngles =
            new Vector3(0f, 100f, 9.5f);
        private static readonly Vector3 ReferenceSwordRestScale =
            new Vector3(0.6f, 0.6f, 0.6f);
        private static readonly string[] RetiredMotionSetAssetPaths =
        {
            SettingsFolder + "/DiagnosticFirstPersonAttackMotionSet.asset",
            SettingsFolder + "/UnarmedFirstPersonAttackMotionSet.asset",
            SettingsFolder + "/SwordFirstPersonAttackMotionSet.asset"
        };
        private static readonly string[] RetiredMotionSetSourcePaths =
        {
            Root + "/Runtime/Combat/DiagnosticFirstPersonAttackMotionSet.cs",
            Root + "/Editor/DiagnosticFirstPersonAttackMotionSetEditor.cs"
        };

        private sealed class CombatAssetBundle
        {
            public DiagnosticMeleeCombatSettings Settings;
            public DiagnosticMeleeAttackProfile UnarmedProfile;
            public DiagnosticMeleeAttackProfile SwordProfile;
        }

        [MenuItem("Midnight Chaos/Bootstrap/Create or Refresh LAN Test Scene")]
        public static void Build()
        {
            EnsureFolder(Root, "Generated");
            EnsureFolder(GeneratedRoot, "Prefabs");
            EnsureFolder(GeneratedRoot, "Scenes");
            EnsureFolder(GeneratedRoot, "Materials");
            EnsureFolder(GeneratedRoot, "Settings");
            EnsureFolder(Root + "/Definitions", "Enemies");

            CombatAssetBundle combatAssets = CreateOrLoadCombatAssets(
                configureMuckAnimation: true);
            RuntimeAnimatorController firstPersonController =
                LoadMuckFirstPersonController();

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
                swordMaterial,
                combatAssets,
                firstPersonController);
            GameObject resourcePrefab = CreateResourcePrefab(
                trunkMaterial,
                leavesMaterial);
            EnemyDefinition enemyDefinition =
                CreateOrRefreshFireMageEnemyDefinition();
            GameObject enemyPrefab = CreateEnemyPrefab(
                enemyMaterial,
                enemyDefinition);
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
                "Đã tạo scene và prefab thử nghiệm cho Gate H3.\n" +
                "First-person viewmodel đã dùng Cube.controller của Muck. " +
                "Gắn model + Animator Controller third-person vào PlayerVisual. " +
                "Nhấn F8 để hiện/ẩn model local khi debug.",
                "OK");
        }

        [MenuItem(
            "Midnight Chaos/Bootstrap/Migrate Rest Pose and Cleanup to v0.8.7")]
        public static void MigrateRestPoseAndCleanupV087()
        {
            EnsureFolder(Root, "Generated");
            EnsureFolder(GeneratedRoot, "Settings");

            GameObject existingPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            RuntimeAnimatorController firstPersonController =
                LoadMuckFirstPersonController(false);

            if (existingPrefab == null || firstPersonController == null)
            {
                EditorUtility.DisplayDialog(
                    "Midnight Chaos",
                    existingPrefab == null
                        ? "Không tìm thấy DiagnosticNetworkPlayer.prefab tại:\n" +
                          PlayerPrefabPath
                        : "Không tìm thấy Cube.controller tại:\n" +
                          MuckFirstPersonControllerPath,
                    "OK");
                return;
            }

            GameObject prefabRoot =
                PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            try
            {
                DiagnosticMeleeCombat combat =
                    prefabRoot.GetComponent<DiagnosticMeleeCombat>();
                DiagnosticPlayerAnimation playerAnimation =
                    prefabRoot.GetComponent<DiagnosticPlayerAnimation>();
                DiagnosticPlayerEquipment equipment =
                    prefabRoot.GetComponent<DiagnosticPlayerEquipment>();

                if (combat == null || playerAnimation == null || equipment == null)
                {
                    EditorUtility.DisplayDialog(
                        "Midnight Chaos",
                        "Prefab thiếu DiagnosticMeleeCombat, " +
                        "DiagnosticPlayerAnimation hoặc " +
                        "DiagnosticPlayerEquipment. Migration đã dừng mà " +
                        "không lưu prefab.",
                        "OK");
                    return;
                }

                Vector3 legacyRestPosition;
                Vector3 legacyRestEulerAngles;
                Vector3 legacyRestScale;
                ReadRetiredSwordRestPose(
                    out legacyRestPosition,
                    out legacyRestEulerAngles,
                    out legacyRestScale);

                CombatAssetBundle combatAssets = CreateOrLoadCombatAssets(
                    combat,
                    playerAnimation,
                    true);
                if (combatAssets.SwordProfile.UpgradeFirstPersonRestPoseToV087(
                        legacyRestPosition,
                        legacyRestEulerAngles,
                        legacyRestScale))
                {
                    EditorUtility.SetDirty(combatAssets.SwordProfile);
                }
                combat.Configure(
                    combatAssets.Settings,
                    combatAssets.UnarmedProfile,
                    combatAssets.SwordProfile);
                equipment.ConfigureFirstPersonViewmodelForMigration(
                    firstPersonController);

                if (prefabRoot.GetComponent<
                        DiagnosticFirstPersonAttackAnimator>() == null)
                {
                    prefabRoot.AddComponent<
                        DiagnosticFirstPersonAttackAnimator>();
                }

                EditorUtility.SetDirty(combat);
                EditorUtility.SetDirty(equipment);
                EditorUtility.SetDirty(playerAnimation);
                EditorUtility.SetDirty(combatAssets.UnarmedProfile);
                EditorUtility.SetDirty(combatAssets.SwordProfile);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            bool cleanupSucceeded = DeleteRetiredMotionSetFiles();

            DiagnosticMeleeAttackProfile savedSwordProfile =
                AssetDatabase.LoadAssetAtPath<DiagnosticMeleeAttackProfile>(
                    SwordAttackProfilePath);
            Selection.activeObject = savedSwordProfile;
            if (savedSwordProfile != null)
            {
                EditorGUIUtility.PingObject(savedSwordProfile);
            }

            EditorUtility.DisplayDialog(
                "Midnight Chaos",
                "Đã nâng cấp lên v0.8.7.\n\n" +
                "First-Person Rest Pose hiện nằm trong " +
                "SwordAttackProfile.asset.\n" +
                (cleanupSucceeded
                    ? "Ba Motion Set và hai source file lỗi thời đã được xóa.\n"
                    : "Có file lỗi thời không thể xóa; kiểm tra Console.\n") +
                "Protocol vẫn là 10; gameplay/network không thay đổi.",
                "OK");

            AssetDatabase.Refresh();
        }

        [MenuItem("Midnight Chaos/Combat/Select Muck First-Person Controller")]
        public static void SelectMuckFirstPersonController()
        {
            RuntimeAnimatorController controller =
                LoadMuckFirstPersonController(false);

            if (controller == null)
            {
                EditorUtility.DisplayDialog(
                    "Midnight Chaos",
                    "Không tìm thấy Cube.controller tại:\n" +
                    MuckFirstPersonControllerPath,
                    "OK");
                return;
            }

            Selection.activeObject = controller;
            EditorGUIUtility.PingObject(controller);
        }

        private static GameObject CreatePlayerPrefab(
            Material attackMaterial,
            Material swordMaterial,
            CombatAssetBundle combatAssets,
            RuntimeAnimatorController firstPersonController)
        {
            GameObject root = new GameObject("DiagnosticNetworkPlayer");
            root.transform.position = Vector3.up;

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

            CreateCameraAnchor(root.transform);
            CreateOrPreservePlayerVisual(root.transform);
            CreateAttackIndicator(root.transform, attackMaterial);
            GameObject swordVisual = FindDescendantByName(
                root.transform.Find("PlayerVisual"),
                "SwordVisual");
            if (swordVisual == null)
            {
                swordVisual = CreateSwordVisual(root.transform, swordMaterial);
            }

            NetworkHealth playerHealth = root.AddComponent<NetworkHealth>();
            playerHealth.ConfigureForDiagnostics(100, "Player");
            root.AddComponent<DiagnosticNetworkInventory>();
            DiagnosticPlayerEquipment playerEquipment =
                root.AddComponent<DiagnosticPlayerEquipment>();
            playerEquipment.ConfigureWorldSwordVisual(swordVisual);
            playerEquipment.ConfigureFirstPersonViewmodelForMigration(
                firstPersonController);
            root.AddComponent<DiagnosticCraftingInteractor>();
            root.AddComponent<DiagnosticResourceGatherer>();
            DiagnosticMeleeCombat meleeCombat =
                root.AddComponent<DiagnosticMeleeCombat>();
            meleeCombat.Configure(
                combatAssets.Settings,
                combatAssets.UnarmedProfile,
                combatAssets.SwordProfile);
            root.AddComponent<DiagnosticWorldHealthLabel>();
            root.AddComponent<DiagnosticNetworkPlayer>();
            root.AddComponent<DiagnosticPlayerAnimation>();
            root.AddComponent<DiagnosticFirstPersonAttackAnimator>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateCameraAnchor(Transform playerRoot)
        {
            GameObject cameraAnchor = new GameObject("CameraAnchor");
            cameraAnchor.transform.SetParent(playerRoot, false);
            cameraAnchor.transform.localPosition =
                new Vector3(0f, 0.75f, 0.08f);
        }

        private static void CreateOrPreservePlayerVisual(Transform playerRoot)
        {
            GameObject existingPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Transform existingVisual = existingPrefab != null
                ? existingPrefab.transform.Find("PlayerVisual")
                : null;

            if (existingVisual != null)
            {
                GameObject preservedVisual =
                    Object.Instantiate(existingVisual.gameObject);
                preservedVisual.name = "PlayerVisual";
                preservedVisual.transform.SetParent(playerRoot, false);
                return;
            }

            GameObject playerVisual = new GameObject("PlayerVisual");
            playerVisual.transform.SetParent(playerRoot, false);

            Animator animator = playerVisual.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            GameObject armature = new GameObject("Armature");
            armature.transform.SetParent(playerVisual.transform, false);

            GameObject characterMesh = GameObject.CreatePrimitive(
                PrimitiveType.Capsule);
            characterMesh.name = "CharacterMesh";
            characterMesh.transform.SetParent(playerVisual.transform, false);
            RemoveCollider(characterMesh);
        }

        private static EnemyDefinition CreateOrRefreshFireMageEnemyDefinition()
        {
            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                FireMageVisualPrefabPath);
            if (visualPrefab == null)
            {
                Debug.LogError(
                    "[Enemy Definition] Fire Mage visual prefab is missing at " +
                    FireMageVisualPrefabPath + ".");
            }

            EnemyDefinition definition =
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                    FireMageEnemyDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                definition.ConfigureForDiagnostics(visualPrefab);
                AssetDatabase.CreateAsset(
                    definition,
                    FireMageEnemyDefinitionPath);
            }
            else
            {
                // Builder refreshes must preserve values tuned by designers.
                definition.ConfigureVisualIfMissing(visualPrefab);
            }
            EditorUtility.SetDirty(definition);
            return definition;
        }

        public static GameObject CreateOrRefreshProceduralEnemyPrefab()
        {
            EnsureFolder(Root, "Generated");
            EnsureFolder(GeneratedRoot, "Prefabs");
            EnsureFolder(GeneratedRoot, "Materials");
            EnsureFolder(Root, "Definitions");
            EnsureFolder(Root + "/Definitions", "Enemies");

            Material enemyMaterial = CreateOrRefreshUnlitMaterial(
                EnemyMaterialPath,
                "DiagnosticMeleeEnemy",
                new Color(0.58f, 0.22f, 0.78f));
            EnemyDefinition definition =
                CreateOrRefreshFireMageEnemyDefinition();
            GameObject prefab = CreateEnemyPrefab(
                enemyMaterial,
                definition);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static GameObject CreateEnemyPrefab(
            Material enemyMaterial,
            EnemyDefinition definition)
        {
            GameObject root = new GameObject("DiagnosticMeleeEnemy");
            root.name = "DiagnosticMeleeEnemy";
            root.transform.position = Vector3.up;

            GameObject bodyVisual = CreatePrimitiveChild(
                PrimitiveType.Capsule,
                "BodyVisual",
                root.transform,
                Vector3.zero,
                Vector3.one,
                enemyMaterial);
            Renderer fallbackRenderer = bodyVisual.GetComponent<Renderer>();
            fallbackRenderer.enabled = definition == null ||
                                       definition.VisualPrefab == null;

            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);
            visualRoot.transform.localPosition = Vector3.down;

            root.AddComponent<NetworkObject>();
            NetworkTransform networkTransform =
                root.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode =
                NetworkTransform.AuthorityModes.Server;
            networkTransform.Interpolate = true;

            NavMeshAgent navMeshAgent = root.AddComponent<NavMeshAgent>();
            navMeshAgent.agentTypeID = 0;
            navMeshAgent.radius = 0.5f;
            navMeshAgent.height = 2f;
            navMeshAgent.baseOffset = 1f;
            navMeshAgent.speed = 3.5f;
            navMeshAgent.acceleration = 8f;
            navMeshAgent.angularSpeed = 120f;
            navMeshAgent.stoppingDistance = 0f;
            navMeshAgent.autoBraking = true;
            navMeshAgent.enabled = false;

            NetworkHealth enemyHealth = root.AddComponent<NetworkHealth>();
            enemyHealth.ConfigureForDiagnostics(66, "Melee Enemy");
            root.AddComponent<DiagnosticEnemyEvolution>();
            DiagnosticEnemyVisual enemyVisual =
                root.AddComponent<DiagnosticEnemyVisual>();
            enemyVisual.ConfigureForDiagnostics(
                definition,
                visualRoot.transform);
            DiagnosticMeleeEnemy enemy =
                root.AddComponent<DiagnosticMeleeEnemy>();
            enemy.ConfigureForDiagnostics(definition);
            root.AddComponent<DiagnosticEnemyDebugGizmos>();
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

        private static CombatAssetBundle CreateOrLoadCombatAssets(
            DiagnosticMeleeCombat legacyCombat = null,
            DiagnosticPlayerAnimation legacyAnimation = null,
            bool configureMuckAnimation = false)
        {
            DiagnosticMeleeAttackProfile unarmedProfile =
                AssetDatabase.LoadAssetAtPath<
                    DiagnosticMeleeAttackProfile>(
                    UnarmedAttackProfilePath);
            DiagnosticMeleeAttackProfile swordProfile =
                AssetDatabase.LoadAssetAtPath<
                    DiagnosticMeleeAttackProfile>(
                    SwordAttackProfilePath);

            DiagnosticMeleeCombatSettings settings =
                AssetDatabase.LoadAssetAtPath<
                    DiagnosticMeleeCombatSettings>(CombatSettingsPath);

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<
                    DiagnosticMeleeCombatSettings>();
                settings.name = "DiagnosticMeleeCombatSettings";
                settings.ConfigureForDiagnostics(
                    legacyCombat != null
                        ? legacyCombat.LegacyInputBufferSeconds
                        : 0.15f,
                    legacyCombat != null
                        ? legacyCombat.LegacyIndicatorDuration
                        : 0.14f,
                    legacyAnimation != null
                        ? legacyAnimation.LegacyAttackBlendInSeconds
                        : 0.08f,
                    legacyAnimation != null
                        ? legacyAnimation.LegacyAttackExitNormalizedTime
                        : 0.95f,
                    legacyAnimation != null
                        ? legacyAnimation.LegacyAttackBlendOutSeconds
                        : 0.1f);
                AssetDatabase.CreateAsset(settings, CombatSettingsPath);
            }

            if (settings.UpgradeHitFeedbackToV083())
            {
                EditorUtility.SetDirty(settings);
            }

            float attackReach = legacyCombat != null
                ? legacyCombat.LegacyAttackReach
                : 2.6f;
            float attackHalfAngle = legacyCombat != null
                ? legacyCombat.LegacyAttackHalfAngle
                : 65f;
            float baseAttackInterval = legacyCombat != null
                ? legacyCombat.LegacyCooldownSeconds
                : 0.65f;

            if (unarmedProfile == null)
            {
                unarmedProfile = ScriptableObject.CreateInstance<
                    DiagnosticMeleeAttackProfile>();
                unarmedProfile.name = "UnarmedAttackProfile";
                unarmedProfile.ConfigureForDiagnostics(
                    "Unarmed",
                    legacyCombat != null
                        ? legacyCombat.LegacyUnarmedDamage
                        : 25,
                    attackReach,
                    attackHalfAngle,
                    baseAttackInterval);
                AssetDatabase.CreateAsset(
                    unarmedProfile,
                    UnarmedAttackProfilePath);
            }

            if (swordProfile == null)
            {
                swordProfile = ScriptableObject.CreateInstance<
                    DiagnosticMeleeAttackProfile>();
                swordProfile.name = "SwordAttackProfile";
                swordProfile.ConfigureForDiagnostics(
                    "Sword",
                    legacyCombat != null
                        ? legacyCombat.LegacySwordDamage
                        : 40,
                    attackReach,
                    attackHalfAngle,
                    baseAttackInterval);
                swordProfile.UpgradeFirstPersonRestPoseToV087(
                    ReferenceSwordRestPosition,
                    ReferenceSwordRestEulerAngles,
                    ReferenceSwordRestScale);
                AssetDatabase.CreateAsset(
                    swordProfile,
                    SwordAttackProfilePath);
            }

            if (configureMuckAnimation)
            {
                unarmedProfile.ConfigureFirstPersonAnimationForMigration(
                    1,
                    UnarmedImpactTime);
                swordProfile.ConfigureFirstPersonAnimationForMigration(
                    MuckAttackVariantCount,
                    MuckAttackImpactTime);
                EditorUtility.SetDirty(unarmedProfile);
                EditorUtility.SetDirty(swordProfile);
            }

            return new CombatAssetBundle
            {
                Settings = settings,
                UnarmedProfile = unarmedProfile,
                SwordProfile = swordProfile
            };
        }

        private static void ReadRetiredSwordRestPose(
            out Vector3 localPosition,
            out Vector3 localEulerAngles,
            out Vector3 localScale)
        {
            localPosition = ReferenceSwordRestPosition;
            localEulerAngles = ReferenceSwordRestEulerAngles;
            localScale = ReferenceSwordRestScale;

            Object retiredSwordMotionSet =
                AssetDatabase.LoadMainAssetAtPath(
                    RetiredMotionSetAssetPaths[2]);
            if (retiredSwordMotionSet == null)
            {
                return;
            }

            SerializedObject serializedMotionSet =
                new SerializedObject(retiredSwordMotionSet);
            SerializedProperty positionProperty =
                serializedMotionSet.FindProperty("restLocalPosition");
            SerializedProperty eulerProperty =
                serializedMotionSet.FindProperty("restLocalEulerAngles");
            SerializedProperty scaleProperty =
                serializedMotionSet.FindProperty("restLocalScale");

            if (positionProperty != null)
            {
                localPosition = positionProperty.vector3Value;
            }
            if (eulerProperty != null)
            {
                localEulerAngles = eulerProperty.vector3Value;
            }
            if (scaleProperty != null)
            {
                localScale = scaleProperty.vector3Value;
            }
        }

        private static bool DeleteRetiredMotionSetFiles()
        {
            bool succeeded = true;
            foreach (string assetPath in RetiredMotionSetAssetPaths)
            {
                succeeded &= DeleteAssetIfPresent(assetPath);
            }

            foreach (string sourcePath in RetiredMotionSetSourcePaths)
            {
                succeeded &= DeleteAssetIfPresent(sourcePath);
            }

            return succeeded;
        }

        private static bool DeleteAssetIfPresent(string assetPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null &&
                !File.Exists(assetPath))
            {
                return true;
            }

            bool deleted = AssetDatabase.DeleteAsset(assetPath);
            if (!deleted)
            {
                Debug.LogError(
                    "[Migration v0.8.7] Không thể xóa file lỗi thời: " +
                    assetPath);
            }

            return deleted;
        }

        private static RuntimeAnimatorController LoadMuckFirstPersonController(
            bool throwWhenMissing = true)
        {
            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    MuckFirstPersonControllerPath);

            if (controller == null && throwWhenMissing)
            {
                throw new System.InvalidOperationException(
                    "Không tìm thấy Muck first-person Animator Controller tại: " +
                    MuckFirstPersonControllerPath);
            }

            return controller;
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

        private static GameObject CreateSwordVisual(
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
            return swordRoot;
        }

        private static GameObject FindDescendantByName(
            Transform root,
            string objectName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform descendant in descendants)
            {
                if (descendant.name == objectName)
                {
                    return descendant.gameObject;
                }
            }

            return null;
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
            Camera gameplayCamera = cameraObject.GetComponent<Camera>();
            gameplayCamera.nearClipPlane = 0.05f;

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
            // v0.8.6 aligns authoritative Sword impact with Muck's Animation
            // Event at 0.2666667 / AttackSpeed. Reject clients with the old
            // v0.8.4 impact-delay contract even though the serialized network
            // layout itself is unchanged.
            manager.NetworkConfig.ProtocolVersion =
                LanSessionController.CurrentProtocolVersion;

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
